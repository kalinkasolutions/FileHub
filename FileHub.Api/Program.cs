using System.Threading.RateLimiting;
using Dal;
using Entities.Account;
using FileHub;
using FileHub.BusinessLogic;
using FileHub.BusinessLogic.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Westwind.AspNetCore.LiveReload;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("FileHub")
                       ?? throw new InvalidOperationException("No FileHub connection string configured.");

// Log to the console (so `docker compose logs -f` shows them) and to a "Logs" table in the same
// SQLite database. The SQLite sink writes through its own ADO.NET connection, so persisting a log
// entry does not go through EF and cannot feed back into the logging pipeline. The app-wide
// minimum level comes from Logging:LogLevel:Default (LOG_LEVEL in docker-compose).
var logDbPath = new SqliteConnectionStringBuilder(connectionString).DataSource;
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logDbPath))!);
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
    .AddDefaultTokenProviders();

// AddIdentity has already registered the cookie schemes and pinned Identity.Application as the
// default authenticate/challenge scheme, so the app cookie has to be configured here rather than
// by registering a "Cookies" scheme of our own, which would be dead config.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;

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
});

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddLiveReload(config =>
        config.FolderToMonitor = Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseLiveReload();
}

// TLS is terminated by the reverse proxy, so the scheme and the caller's address arrive in
// headers. Without this the app would see every request as http from the proxy's own address,
// which is what the Go build's TrustedProxies list was for.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseSerilogRequestLogging();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// ---- endpoints ----

app.MapFallbackToFile("index.html");

await Seed.InitializeAsync(app);
await app.RunAsync();

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
