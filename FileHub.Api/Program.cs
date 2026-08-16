using System.Threading.RateLimiting;
using Dal;
using Dal.Repositories.Account;
using Dal.Repositories.Admin;
using Dal.Repositories.BasePaths;
using Dal.Repositories.Email;
using Dal.Repositories.Identity;
using Dal.Repositories.Shares;
using Entities.Account;
using FileHub;
using FileHub.Authorization;
using FileHub.BusinessLogic;
using FileHub.BusinessLogic.Email;
using FileHub.BusinessLogic.Services.Account;
using FileHub.BusinessLogic.Services.Admin;
using FileHub.BusinessLogic.Services.BasePaths;
using FileHub.BusinessLogic.Services.Email;
using FileHub.BusinessLogic.Services.Files;
using FileHub.BusinessLogic.Services.Identity;
using FileHub.BusinessLogic.Services.Shares;
using FileHub.Endpoints;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Westwind.AspNetCore.LiveReload;

// The SPA build output. A checkout that has not run `npm run build` yet has no wwwroot at all, and
// the host resolves (and warns about) the web root while the builder is being created — so this has
// to happen before that, against the same directory the host takes as its content root.
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("FileHub")
                       ?? throw new InvalidOperationException("No FileHub connection string configured.");

// Log to the console (so `docker compose logs -f` shows them) and to a "Logs" table in the same
// SQLite database. The SQLite sink writes through its own ADO.NET connection, so persisting a log
// entry does not go through EF and cannot feed back into the logging pipeline. The app-wide
// minimum level comes from Logging:LogLevel:Default (LOG_LEVEL in docker-compose).
// Absolute on purpose: the SQLite sink resolves a relative path against the *binary's* directory
// while EF resolves the connection string against the working directory, so a relative
// "Data Source=./data/filehub.db" (which is what the Development config uses) would quietly put the
// Logs table in a second database nobody ever looks at.
var logDbPath = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);
Directory.CreateDirectory(Path.GetDirectoryName(logDbPath)!);
var minimumLevel = ParseLogLevel(builder.Configuration["Logging:LogLevel:Default"]);
builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Is(minimumLevel)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.SQLite(logDbPath, tableName: "Logs", storeTimestampInUtc: true));

builder.Services.AddDbContext<FileHubContext>(options => options.UseSqlite(connectionString));
builder.Services.AddHttpContextAccessor();

builder.Services.AddIdentity<FileHubUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        // The username is a display name, not an identifier, so it has to allow what people call
        // themselves — spaces and accented letters included. An empty string turns Identity's
        // character filter off; uniqueness is still enforced by the normalized-name index.
        options.User.AllowedUserNameCharacters = string.Empty;
        // Accounts are created by an admin and activated through the invitation link, so an
        // unconfirmed address means the invitation was never accepted.
        options.SignIn.RequireConfirmedEmail = true;
        // This login is reachable from the internet, so a wrong password has to cost something.
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 10;
    })
    .AddEntityFrameworkStores<FileHubContext>()
    .AddDefaultTokenProviders()
    // Puts the forced-password-change flag on the cookie principal, so the gate below is a claim
    // check rather than a database read on every request. A password change rotates the security
    // stamp and the account endpoints refresh the sign-in, which regenerates the claim.
    .AddClaimsPrincipalFactory<FileHubClaimsPrincipalFactory>();

// AddIdentity has already registered the cookie schemes and pinned Identity.Application as the
// default authenticate/challenge scheme, so the app cookie has to be configured here rather than
// by registering a "Cookies" scheme of our own, which would be dead config.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;

    // The session cookie must never travel over plain http. Outside Development that is
    // unconditional rather than SameAsRequest: TLS terminates at the proxy, so Request.Scheme is
    // only ever "https" because X-Forwarded-Proto said so, and SameAsRequest would quietly drop
    // the flag the moment that header is missing or comes from an untrusted hop — which is
    // exactly the failure this is meant to survive. Development runs on plain http://localhost,
    // where Always would mean the browser stores no cookie at all and nobody can sign in.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    // Everything behind auth is called by the SPA with fetch/XHR, so answer a missing or stale
    // cookie with a status code the client can act on rather than a 302 to a login page.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// How long a cookie may outlive a security-stamp change (a password change, "sign out
// everywhere", turning two-factor on). The default 30 minutes would make signing other devices
// out look like it did nothing.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

// Persist the Data Protection key ring so auth cookies — and the encrypted SMTP password —
// survive container recreation. The default location inside the container is wiped on redeploy.
var keyRingPath = builder.Configuration["DataProtection:KeyPath"] ?? "/var/srv/keys";
Directory.CreateDirectory(keyRingPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
    .SetApplicationName("FileHub");

builder.Services.AddAuthorization();

// The sign-in and forgot-password routes are the two anonymous endpoints that cost real work and
// that a stranger can reach: this login is on the internet, and Identity's lockout only slows an
// attacker down per account, not per caller. The limit is per client address and deliberately
// loose enough that a person fumbling their password never meets it.
//
// Every policy here partitions on Connection.RemoteIpAddress, which is only the caller's address
// because the forwarded-headers trust list further down is set. With the framework default the
// proxy's own address is the partition key for the whole internet, and one attacker holding this
// limit at 429 locks the login screen for everybody.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1)
        }));

    // The share links. They are the whole anonymous surface, and a directory link rebuilds a
    // recursive zip on every hit — with Kestrel's minimum-data-rate floor deliberately switched
    // off for downloads and an hour of read timeout on the proxy behind it, an unlimited leaked
    // link is repeatable CPU and IO amplification. Sized for what opening a link actually costs:
    // one metadata call and one download per visitor, one page per chat client unfurling it.
    // Anything walking a list of ids meets this; a person never does.
    options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1)
        }));
});

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

builder.Services.AddScoped<IEmailSettingRepository, EmailSettingRepository>();
builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IUserAdminRepository, UserAdminRepository>();
builder.Services.AddScoped<IBasePathRepository, BasePathRepository>();
builder.Services.AddScoped<IShareRepository, ShareRepository>();

builder.Services.AddScoped<IEmailSettingsProvider, EmailSettingsProvider>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailSettingService, EmailSettingService>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IBasePathService, BasePathService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IShareService, ShareService>();

var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddLiveReload(config =>
        config.FolderToMonitor = webRootPath);
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseLiveReload();
}

// TLS is terminated by the reverse proxy, so the scheme and the caller's address arrive in
// headers — but ASP.NET only believes them from a peer it has been told is a proxy, and its
// default trust list is loopback alone. The reference deployment proxies from another address, so
// with the default the headers are silently dropped and two things collapse together: every
// request looks like plain http (auth cookies then ship without Secure) and every caller looks
// like the proxy (one shared rate-limit bucket, so one attacker can hold login and password reset
// at 429 for everybody). This is the Go build's TrustedProxies list, back as configuration.
app.UseForwardedHeaders(BuildForwardedHeadersOptions(builder.Configuration));

app.UseSerilogRequestLogging();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

// Reads the forced-password-change claim off the cookie principal, so it has to run after
// authentication; before authorization, so a gated request never reaches an endpoint's own checks.
app.UseMiddleware<MustChangePasswordMiddleware>();

app.UseAuthorization();

// ---- endpoints ----

app.MapAuthEndpoint();
app.MapAccountEndpoint();
app.MapAdminUserEndpoint();
app.MapAdminRoleEndpoint();
app.MapEmailSettingEndpoint();
app.MapBasePathEndpoint();
app.MapFileEndpoint();
app.MapShareEndpoint();
app.MapPublicShareEndpoint();

app.MapFallbackToFile("index.html");

await Seed.InitializeAsync(app);
await app.RunAsync();

// Reads ForwardedHeaders:TrustedProxies — a comma-separated list of IP addresses and/or CIDR
// blocks — into the options the middleware checks the immediate peer against. Anything not on the
// list has its X-Forwarded-* headers ignored, so a client cannot pick its own address or claim the
// request arrived over https.
//
// The default is loopback plus the private ranges a reverse proxy normally sits in, which is what
// the Go build shipped. It is a default, not a policy: on a host where something untrusted can
// reach the container directly from a private address, set this to the proxy's exact address.
// ForwardLimit stays at 1 — exactly one hop is taken off the right-hand end of X-Forwarded-For, so
// whatever a client prepended to the header is carried but never believed. Behind two proxies
// (a CDN in front of nginx) that has to become 2, and both of them have to be trusted.
static ForwardedHeadersOptions BuildForwardedHeadersOptions(IConfiguration configuration)
{
    const string defaultTrustedProxies =
        "127.0.0.0/8, ::1/128, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, fc00::/7";

    var options = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };

    // Both start out holding loopback. Replace rather than extend, so the configured value is the
    // whole answer to "whose X-Forwarded-* may be believed" and an operator can narrow it.
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();

    var configured = configuration["ForwardedHeaders:TrustedProxies"];
    if (string.IsNullOrWhiteSpace(configured))
    {
        configured = defaultTrustedProxies;
    }

    var entries = configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    foreach (var entry in entries)
    {
        if (System.Net.IPNetwork.TryParse(entry, out var network))
        {
            options.KnownIPNetworks.Add(network);
            continue;
        }

        if (System.Net.IPAddress.TryParse(entry, out var address))
        {
            options.KnownProxies.Add(address);
            continue;
        }

        // Refuse to start rather than run with a half-parsed trust list: a dropped entry is a
        // deployment that looks fine and rate-limits the whole internet as one caller.
        throw new InvalidOperationException(
            $"ForwardedHeaders:TrustedProxies contains \"{entry}\", which is neither an IP address " +
            "nor a CIDR block. Note that a CIDR block must have its host bits clear (10.0.0.0/8, not 10.0.0.1/8).");
    }

    return options;
}

// Maps a Microsoft-style log level name (as used in the Logging:LogLevel config section) to the
// Serilog level the pipeline is configured with. Falls back to Information for missing values.
static LogEventLevel ParseLogLevel(string? value) => value?.Trim().ToLowerInvariant() switch
{
    "trace" => LogEventLevel.Verbose,
    "debug" => LogEventLevel.Debug,
    "information" or "info" => LogEventLevel.Information,
    "warning" or "warn" => LogEventLevel.Warning,
    "error" => LogEventLevel.Error,
    "critical" or "fatal" => LogEventLevel.Fatal,
    _ => LogEventLevel.Information
};
