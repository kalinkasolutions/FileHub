# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

FileHub is a read-only cloud for browsing and sharing files from mounted disks: an ASP.NET Core
(.NET 10) minimal-API backend that also serves an Angular 21 SPA out of `FileHub.Api/wwwroot`,
over SQLite, shipped as a single Docker image. `MapFallbackToFile("index.html")` hands every
non-API path to the client router.

## Commands

Backend, from the repository root:

```bash
dotnet build FileHub.slnx
dotnet test FileHub.IntegrationTests
dotnet test FileHub.IntegrationTests --filter PathSandboxTests    # one class
```

EF Core migrations (SQLite provider). The design-time tools live in `FileHub.Api` but the
`DbContext` is in `FileHub.Dal`, so both projects have to be named:

```bash
dotnet ef migrations add <Name> --project FileHub.Dal --startup-project FileHub.Api
```

Migrations are applied at startup by `Seed.InitializeAsync`; there is no manual `database update`
step, and a migration that is added but never applied locally will be applied by the next `dotnet
run` against whatever database the connection string points at.

Frontend, from `FileHub.Api/ClientApp`:

```bash
npm install
npm start          # ng serve — for a quick look at a component, not for talking to the API
npm run build      # outputs to ../wwwroot, which is what the API serves
npm run watch      # rebuild into wwwroot on change; this is the dev loop
npm test           # vitest via @angular/build:unit-test
```

### The dev loop

`npm run watch` in one terminal, `dotnet run` (from `FileHub.Api`) in another. The API watches
`wwwroot` with `Westwind.AspNetCore.LiveReload`, so a rebuild reloads the browser. **There is no
dev-server proxy and no `environment.apiUrl`** — every request the SPA makes is relative and
same-origin, which is the only reason the session works in development at all: served from
`ng serve` on another port, every API call is cross-origin, and no CORS policy is configured to
let a credentialed one through. If you reach for `ng serve` to work on a screen that talks to the
API, you will find this out the slow way.

`Development` also points the connection string and the key ring at `FileHub.Api/data/`, so a
local run never touches `/var/srv`.

## Architecture

### Layering and namespaces

Projects reference each other in a strict line: `Api → BusinessLogic → Dal → Entities`, with
`Dtos` and `Shared` used across layers.

**The namespaces do not match the project names**: `FileHub.Dal` → `Dal`, `FileHub.Entities` →
`Entities`, `FileHub.Dtos` → `Dtos`, `FileHub.Shared` → `Shared`. `FileHub.Api` uses `FileHub`
(its `RootNamespace`) and `FileHub.BusinessLogic` uses `FileHub.BusinessLogic`.

**One file, one thing.** Every class, interface, enum or record lives in its own file named after
it — an interface never shares a file with its implementation.

A folder named after a type shadows that type inside it (CS0118), which is why the service folders
are `Services/Files`, `Services/Shares`, `Services/BasePaths` and the entity folders are
`Entities/Paths`, `Entities/Shares` — `Services/Share` would make every `Share` parameter in the
folder fail to compile.

`FileHub.Dal` carries a `FrameworkReference` to `Microsoft.AspNetCore.App`: `SignInManager` writes
the auth cookie and therefore ships in the shared framework rather than in the Identity packages.
It adds nothing the host does not already have.

### Request flow and the OperationResult pattern

Business and data operations return `OperationResult<T>` (`FileHub.Shared/OperationResult.cs`)
rather than throwing:

`Endpoint (Api/Endpoints) → I<X>Service (BusinessLogic) → I<X>Repository (Dal) → EF / Identity`

- Services build results with `OperationResult<T>.Success/Validation/BadRequest/Forbidden/NotFound/BadGateway/Error`.
- Endpoints convert with `.ToHttpResult()` (`Api/Extensions/OperationResultExtension.cs`), which
  maps each `ResultCode` to a status code — `Validation` becomes a 400 `ValidationProblemDetails`
  carrying the field-keyed errors.
- `OperationResult<Empty>` is a success with no payload; `MapError<TNew>()` re-types a failure.

Layer responsibilities are strict and worth keeping that way: **endpoints are thin** (bind, call
the service, `.ToHttpResult()`), **services hold all the logic** — authorization, business rules,
entity↔DTO mapping — and **repositories are dumb data access**: EF queries plus
`SaveChangesAsync`, no `OperationResult`, no auth checks, no validation.

Validation is DTO-declared but service-invoked: DataAnnotations live on the DTOs and
`DtoValidator.Validate(dto)` runs at the top of each service method. This keeps one error channel
and makes validation testable without HTTP — an endpoint that binds a DTO and never reaches a
service that validates it is silently unvalidated.

Endpoints are extension methods on `IEndpointRouteBuilder` (`MapAuthEndpoint`, `MapFileEndpoint`, …)
wired up in `Program.cs`. **Each endpoint file declares its own group and its own authorization**;
it is not inherited, so a new file that forgets `.RequireAuthorization()` is anonymous. The
`app.Map*Endpoint()` block in `Program.cs` is the index of the whole surface.

### Access model — the application authenticates now

This is the largest departure from the Go build, where nginx was the entire access-control system.
Sign-in and roles are the gate; `nginx.example.conf` is now one location whose only jobs are TLS
and forwarding the request untouched.

- **There is no registration.** An admin creates an account (`POST api/admin/users`), which sends
  an invitation mail carrying Identity's **email-confirmation token**. `POST api/auth/accept-invite`
  redeems it: it confirms the address and sets the first password in one call, so an admin never
  learns a user's password. Both halves have to keep using the *email-confirmation* token — a reset
  token will not redeem here.
- **Roles are `Admin` and `User`** (`Shared/Roles.cs`), seeded at startup. They are fixed: there is
  no role creation, and `api/admin/roles` only lists them with their counts.
- **The seeded admin** is `AdminSeeder` (in BusinessLogic, so it is reachable from the tests;
  `Seed` still owns migrations and roles). On a database with no admin at all it uses
  `Admin__Email` / `Admin__Password`, and an unset password is generated — deliberately, because a
  shipped default would be a published credential on an installation that is reachable before
  anyone has signed in. The check is "is there **any** admin", not "is there this admin", so a
  renamed or replaced admin does not get a second one seeded behind it; that would be a back door,
  not a repair.
  - The generated password is written to **`Console.Out`, never through `ILogger`**. Serilog's
    SQLite sink persists everything at `Information` and above into the `Logs` table, which has no
    retention — so logging it would leave the bootstrap credential in the database for the life of
    the install. It still reaches `docker compose logs`, which is the documented way to read it.
  - If the address is already taken, the account holding it is **promoted** — roles restored,
    lockout cleared, address confirmed, password untouched — rather than created a second time.
    An install that lost its last admin used to crash-loop on `Username 'Admin' is already taken`,
    which made a recoverable state unrecoverable.
- **`SignIn.RequireConfirmedEmail` is on**, so an unconfirmed address means the invitation was
  never accepted. The admin screen's "invited" state, and `UserAdminService`'s definition of an
  *active* admin, both depend on it.
- **Lockout is on** and the anonymous `login` / `forgot-password` routes carry
  `.RequireRateLimiting("auth")`; the three anonymous share routes carry
  `.RequireRateLimiting("public")` (both fixed windows per client address, registered in
  `Program.cs`). Identity's lockout is per account; the limiter is what makes a spray across
  accounts, or a scraper on a leaked link, cost something. `app.UseRateLimiter()` has to stay in
  the pipeline — the metadata on its own does nothing, and removing the middleware makes those
  endpoints fail rather than run unlimited.

**Sign-in lives in `IdentityService`, not in the endpoint.** It was moved there so the flow is
reachable from the service-level tests and so its DTO goes through `DtoValidator` like every other
— an endpoint that binds a DTO and talks to `SignInManager` itself is silently unvalidated.

**Every failure a stranger can provoke gives one message and costs one password verification.**
`PasswordSignInAsync` decides `LockedOut` and `NotAllowed` *before* looking at the password, so
naming either of them answers "does this account exist, and what state is it in" for any password
at all — and the early return for an unknown address skipped the hash entirely, which said the same
thing in the timing (~370× in the fixture). So: unknown addresses are verified against a throwaway
hash, the honest "not activated" / "locked out" messages are only produced once the caller has
proved they hold the password, and a refusal that never reached the password still counts towards
the lockout. `reset-password`, `accept-invite` and `confirm-email-change` are unified the same way,
and `forgot-password` sends on a background task so the answer does not wait for SMTP — its
deliberate always-succeed contract was defeated by its own success path taking 45 ms longer.

If you add an anonymous endpoint that looks an account up, this is the standard it has to meet.

**The only anonymous routes are `public-api/*` and `og/share/*`** — the share links and their Open
Graph previews. Everything else answers 401 (not a 302: `ConfigureApplicationCookie` replaces the
redirect, because every authenticated call comes from the SPA's `fetch` and needs a status code it
can act on).

#### The forced first password change

An account whose password was set by someone else carries `FileHubUser.MustChangePassword`.

`FileHubClaimsPrincipalFactory` puts that flag on the sign-in cookie as the `must_change_password`
claim, so the gate costs no database read per request. It needs no invalidation of its own:
changing a password rotates the security stamp, the account endpoints refresh the sign-in, and the
cookie — claim included — is rebuilt from the flag as it then stands.

`MustChangePasswordMiddleware` answers 403 with `type: must-change-password` for everything except
a flat allow-list: `POST api/account/password`, `GET api/account`, `GET api/auth/status`,
`POST api/auth/logout`, anything under `public-api`/`og`, and anything that is not `/api` at all
(the SPA's own files). **It sits between `UseAuthentication` and `UseAuthorization`**: earlier and
it sees an anonymous principal and never fires; later and a gated request has already reached the
endpoint's own checks.

The SPA mirrors it with `passwordChangeGuard` on every signed-in route *except* `/change-password`.
The guard is convenience; the middleware is the control.

`ChangePasswordAsync` rejects a new password equal to the current one. Without that the whole gate
was satisfiable by re-entering the bootstrap credential printed at startup, which cleared the flag
and left the account on the password everyone with log access had seen.

### Admin mutations are serialised

The last-admin rules (no self-delete, no self-disable, no removing the last account that could
still sign in) read in one context and wrote in another, so two concurrent calls — a self-demotion
and a delete — could both pass and leave zero admins. `UserAdminService` holds a process-wide
`SemaphoreSlim` across the check *and* the write in `UpdateUserAsync`, `SetLockoutAsync` and
`DeleteUserAsync`. It covers any interleaving inside this process, which is what a single-container
SQLite deployment has; it does **not** cover a second instance against the same database, which
would need the check and the write in one serialisable transaction. Validation and the self-target
guards stay outside the lock — they touch no database. `InviteUserAsync` is deliberately not
serialised: it can only add an admin, and an unconfirmed one at that.

**Disabling an account, and taking the `Admin` role away, rotate the security stamp.** Lockout is
only consulted at sign-in, so without the rotation a disabled user kept a fully working session for
up to the cookie's 30 days, and a demoted admin kept the `Admin` claim. The stamp is what
`SecurityStampValidator` compares, so rotating it ends the session within its one-minute interval.

### Per-user base-path grants

A `BasePath` is a directory on the host FileHub may read. `BasePathAccess` is a grant of one base
path to one user, and **absence of a grant row is a denial — for admins too.** There is no wildcard
and no implicit access; a fresh admin sees nothing until they grant themselves something.

Every listing, navigation, download and share creation starts from
`IBasePathRepository.GetForUserAsync(id, userId)`, which returns null when there is no grant and
which callers answer as "not found". This is the invariant most likely to be broken by a
well-meaning change: a repository call that fetches a base path by id alone, used on a request
path, silently hands every user every disk.

Both grant directions are the same table, edited from either end
(`api/admin/base-path/{id}/users` and `api/admin/users/{id}/base-paths`), and both PUTs **replace**
rather than merge. Both also drop ids that no longer exist — the foreign key would otherwise take
the whole grant change down with it when the admin screen holds a stale row.

**Revoking a grant deletes the links that user made under it**, in both directions, *before* the
grant change is saved — so a failure over-revokes rather than leaving a live anonymous link into a
base path its creator can no longer browse. Deleting a user or a base path already cascaded; only
revocation was silently missed, and a revoked user's public link went on serving files.

### The path sandbox

`PathSandbox.TryResolve` (`BusinessLogic/Authorization`) is the only place a caller-supplied path
becomes a path on disk. Nothing builds one by concatenation.

- **A climb out fails; it is not clamped.** The Go build's `path.Join(base, clean("/.."))` quietly
  returned the base path — a request for the wrong file answered with a different file. Here it is
  a 404.
- A rooted or drive-qualified relative path is rejected outright rather than trimmed, because
  `Path.Combine("/srv/media", "/etc")` is `/etc`.
- **Containment is decided on a fully resolved path**, produced by the hand-built `RealPath`. The
  lexical check that runs first is only a cheap early out; `RealPath` is what actually answers.
- **`FileSystemInfo.ResolveLinkTarget` is not a `realpath`, and treating it as one is a remote file
  read.** With `returnFinalTarget: true` it follows a chain of *links*, but it does not
  canonicalise the *directories* in the target it hands back. Given `sub → /etc` and
  `escape → sub/passwd` inside a base path, it answers `<base>/sub/passwd` — lexically inside, so
  a containment check on it passes while the open lands in `/etc`. A per-segment check on the
  *requested* path never looks at `sub`, because the request never named it. This was demonstrated
  end to end: browse, download, zip, and publish as an anonymous share link. `RealPath` therefore
  resolves **one hop at a time and pushes each target's own segments back onto the work list**, so
  a target's components get exactly the same treatment as the original path's, with a hop budget
  for cycles and a relative target resolved against the directory holding the link.
- **The root is resolved the same way.** `/data → /mnt/disk1` is the ordinary way a mount is
  exposed; against an unresolved root, everything under it resolves to the mount's real path and
  reads as an escape. Resolving the root is safe in a way that resolving a caller's path is not —
  the root is what an admin typed, not what a request asked for.
- The accepted path is the link, not its target: opening it is what follows the link, and
  rewriting it would change what a share stores and what a listing shows. Resolution decides
  *whether* to answer, not *what* to answer.

The zip walk skips reparse points for the same reason (`AttributesToSkip`) — it has no sandbox of
its own, so following a link there would be the escape hatch the sandbox closed.

### Shares are (base path, relative path)

`Share` stores `BasePathId` + `RelativePath`, not a resolved absolute path. That is what buys:

- A share cannot outlive its base path — the FK cascades, so deleting a base path revokes every
  link into it. The Go build had to hunt them down with a `LIKE` prefix match and manual escaping,
  and any code path that forgot to left working links into a directory nobody could browse.
- A share cannot point outside its base path, because it is **re-resolved through the sandbox on
  every hit** rather than trusting a path stored months ago.
- Deleting the user who made a link revokes it, by the same cascade.

`MaxDownloadCount` of `0` means unlimited (`Share.DownloadLimitReached` on the entity). **The
limit is enforced by `ShareRepository.TryRegisterDownloadAsync`** — one
`ExecuteUpdateAsync ... WHERE DownloadCount < MaxDownloadCount` whose affected-row count is the
answer — not by the read in `ResolvePublicAsync`, which is only a fast path that spares the
obviously-dead link. Read-then-increment let eight concurrent anonymous callers all be served a
link with one download left. If you add another route that serves a share, it has to go through
the same call, not through the flag.

**Size is measured once, on the authenticated create route, and stored.** The public routes must
never walk a tree: they are unauthenticated, so a `Directory.EnumerateFileSystemEntries` there is
free IO amplification for anyone holding a link.

`ShareDto.Link` is empty out of the service — the endpoint stamps it with `ShareLinks`, which is
the only place `App:BaseUrl` turns into a URL.

The Open Graph page (`og/share/{id}`) is served to the public internet and interpolates both the
share id from the URL and a filename from disk, so it escapes per context (`HtmlEncoder` /
`JavaScriptEncoder`). Never assemble that page with string interpolation of raw values.

### Downloads

`Api/Downloads/FileDownload.cs` is the one way bytes leave the app, shared by the authenticated
and the public route. Both hand it a path the sandbox already validated; it does no authorization.

- A file goes out through `Results.File(..., enableRangeProcessing: true)` so a paused download
  resumes and video seeking works, with the filename encoded by `ContentDispositionHeaderValue`
  (quoted ASCII plus RFC 2231 `filename*` — a quote or an accent truncates the saved name if the
  header is assembled by hand).
- A directory is streamed as a zip built on the fly, entries stored uncompressed
  (`CompressionLevel.NoCompression`, matching the Go build's `zip.Store`) because the payload is
  usually already-compressed media.
- **`MinDataRate = null` for the request.** This is the counterpart of the Go server deliberately
  having no `WriteTimeout`: Kestrel's default 240 bytes/s floor aborts a slow client — or a slow
  disk — in the middle of a large file. Removing that line reintroduces a bug that only shows up
  in production, on the biggest files.
- The status line is on the wire before the first byte of an archive, so a mid-stream failure can
  only be signalled by leaving the archive unfinished. Log it; do not try to write an error body.

### Email

SMTP settings live in a single `EmailSettings` **row**, seeded from the `Email` configuration
section the first time it is read, and editable by an admin afterwards. Config is the seed, the
row is the truth — an install configured purely by environment keeps working without anyone
opening the admin screen.

The password is encrypted with Data Protection (`FileHub.EmailSettings.Password` purpose) and is
never returned by the API (`HasPassword` says only whether one is stored). An empty password on
update means "keep the stored one" — but **only while the destination is unchanged**. Change the
host, the port or the transport, or clear the username, and the stored secret is dropped instead,
because otherwise "keep it" quietly means "send it somewhere new": repointing the host at a
listener and pressing *Send test* read the password back in cleartext. The response says it
happened through `EmailSettingDto.PasswordCleared`, which the admin screen renders at the field —
the admin left it empty meaning "keep", and has to be told it was not kept.

Addresses are parsed defensively: `[EmailAddress]` accepts things MimeKit rejects, and an
unhandled `ParseException` used to be a 500 that left an invited account behind with no way to
resend to it — and, once such an address existed, a permanent 500 on the **anonymous**
`forgot-password` route. The invite path checks the address is sendable *before* creating the
account.

Templates are HTML files under `Api/EmailTemplates` with `@Placeholder` tokens, copied next to the
binary by the csproj and loaded from `AppContext.BaseDirectory`. Adding one means adding the
`Content Include` glob keeps matching it — a template that is not copied fails at send time, not at
build time.

**Bootstrap order matters and is deliberate:** the seeded admin signs in with the generated
password → is forced to change it → configures SMTP → invites people. The first login can never be
a mail link, because the mail settings live behind the admin area that account exists to open.

### Data Protection

The key ring is persisted to `DataProtection__KeyPath` (`/var/srv/keys`, on the mounted volume),
and the application name is pinned. Losing it invalidates every auth cookie **and** makes the
stored SMTP password unreadable (the provider logs that and treats it as empty rather than
failing the admin screen). Keeping the key ring inside the container's default location is what
would make every redeploy sign everybody out.

### Persistence

- ASP.NET Core Identity with **`Guid` keys**; `FileHubContext : IdentityDbContext<FileHubUser,
  IdentityRole<Guid>, Guid>`.
- **An account is identified by its email address.** `UserName` is a display name only —
  `AllowedUserNameCharacters` is cleared so it can hold spaces and accents. Sign-in resolves with
  `FindByEmailAsync`; never `FindByNameAsync`.
- Entities implementing `IBaseEntity` get `CreatedAt`/`LastUpdatedAt` set in
  `SaveChanges[Async]` — do not set them by hand.
- Serilog writes to the console and to a `Logs` table in the same SQLite file, through its own
  ADO.NET connection, so persisting a log line cannot feed back through EF into the logging
  pipeline. The table has no retention — which is why a secret must never be handed to `ILogger`,
  as an argument any more than in the message (Serilog persists the arguments as their own column).
- **The sink gets an absolute path.** Serilog's SQLite sink resolves a relative path against the
  *binary's* directory while EF resolves the connection string against the working directory, so
  `Data Source=./data/filehub.db` — the Development setting — quietly put the `Logs` table in a
  second database under `bin/`. The container never showed it, because its path is absolute.

### Testing

`FileHub.IntegrationTests` (xUnit, 370 tests) drives the **services**, not the routes: the real service →
repository → EF/Identity stack over a per-test SQLite **in-memory** database (`TestHostBase` takes
a delegate that registers the slice under test). SQLite rather than the EF in-memory provider,
because the unique indexes and the `ON DELETE CASCADE` behaviour that share revocation and user
deletion rely on only exist in a real database. `FakeEmailService` captures tokens so an
invitation or reset is replayed through the real redemption path, and `TotpCode` computes a real
authenticator code so two-factor can actually be switched on.

The fixtures expose `NewRequest()` (`ChangeTracker.Clear()`) to stand in for the per-request
scope; without it a test asserting on a counter EF has already tracked passes vacuously, and
`NewScope()` for the two-requests-at-once tests that pin the admin serialisation. A test that
exists to prove a lock works should be checked by widening the lock and watching it fail — both
of those were.

**What this level cannot see**, and what therefore needs an HTTP-level test if it is ever to be
covered: `MustChangePasswordMiddleware`, the claims factory and cookie refresh, the role
authorization on the route groups (`ShareService.DeleteAsync` takes `callerIsAdmin` as a
*parameter* — nothing below the endpoint proves the caller is one), `FileDownload`'s zip and
headers, `ShareLinks`, the rate limiter, and the forwarded-headers configuration. `AdminSeeder` is
covered, because it is the part of startup that can brick an install; the migration and role half
in `Seed` is not.

### Frontend

Angular 21, standalone components, **signals** (`signal`/`computed`) and **zoneless** — state
written from an rxjs callback needs `markForCheck()` or nothing renders. Angular Material + CDK,
`ngx-toastr`, SCSS, Prettier. Mobile-first: design for small screens, then layer on `min-width`
media queries. **Never use the `style` attribute in a template** — always a class.

The design system is `src/_variables.scss` and `src/_mixins.scss`: CoreList's structure (the token
names, the type/space/radius scales, the `flex`/`hover`/`icon-size`/`button-base`/`control-input`/
`screen-header`/`account-section` mixins, the global `button` and `.icon-btn` families in
`styles.scss`, the element-qualified Material overlay selectors) wearing **FileHub's colours** —
the terminal green `#2fc812` and the warm near-black its panels were filled with, which the whole
grey ramp is mixed from. Import with `@use 'variables' as *;` / `@use 'mixins' as *;` and prefer
the tokens to hardcoded values. Material's panel selectors have to stay element-qualified
(`div.mat-mdc-menu-panel`, …) because Material injects its own structural CSS at runtime, after
this stylesheet, and wins an equal-specificity tie.

Routes (`src/app/app.routes.ts`): `login` and `''` (the browser) are eager — one of the two is the
first thing every visit needs. The screens reached from a mail link (`accept-invite`,
`reset-password`, `confirm-email-change`, plus `change-password` and `account`) are `loadComponent`
routes and stay out of the initial bundle. `share/:id` and `404` carry **no guards** — the share
landing is the one screen a stranger sees, and it must look the same to a signed-in visitor.
`data.chrome` (`none` / `anonymous` / default) is what a route asks of the app shell.

Three guards: `authGuard` (session, else `/login`), `adminGuard` (the `Admin` role; sends a
signed-in non-admin back to `/` rather than to a sign-in screen that would read as a bug), and
`passwordChangeGuard` (described above).

**The wire is camelCase** — ASP.NET's default JSON policy — and ids are `Guid` strings. A listing
entry's `size` is a byte count for a file and an **entry count** for a directory; `itemId` is a
fresh GUID per listing, valid only for identity comparisons inside one response, never to be
persisted or compared across requests. `nextSegment` carries **no leading separator**: the sandbox
rejects a rooted relative path, so prefixing one turns a working path into a 404.

Mail links land on the query parameters those screens read, and the backend builds exactly those:
`accept-invite?userId&token`, `reset-password?email&token`, `confirm-email-change?userId&email&token`.

The account screen's two-factor block carries the current password through the whole enrolment:
`2fa/setup` (a **POST**, because it takes a body), `2fa/enable` and `2fa/recovery-codes` all
require it, as `2fa/disable` always did. Enrolling with a session cookie alone let anyone holding a
borrowed cookie pair their own authenticator and keep recovery codes that survive both a password
change and *sign out everywhere*.

API failures are turned into a message with `apiErrorMessage(error, fallback)`, which reads
ProblemDetails `detail` and ValidationProblemDetails `errors` before falling back. A 401 clears the
cached auth state and routes to `/login`; a 403 passes through, because it is a real answer.

### Configuration

`appsettings.json` plus environment variables (`__` for nesting), no config file of our own:
`ConnectionStrings__FileHub`, `DataProtection__KeyPath`, `App__BaseUrl`,
`ForwardedHeaders__TrustedProxies`, `Admin__Email`, `Admin__Password`, `Email__*`,
`Logging__LogLevel__Default`. `.env.example` documents them and `docker-compose.yml` wires them.

**`App:BaseUrl` is the one origin.** Share links and the links in invitation, reset and
email-change mails are all built from it, so a wrong value produces links that resolve nowhere
while everything else keeps working — which is why it has no useful default.

**`ForwardedHeaders:TrustedProxies` decides whose `X-Forwarded-*` may be believed** — IP addresses
and/or CIDR blocks, comma separated, defaulting to loopback plus the private ranges (this is the Go
build's `TrustedProxies` list, back as configuration). An entry that parses as neither stops the
app rather than being skipped. Leaving `KnownProxies`/`KnownIPNetworks` at their defaults, as the
first version did, means *loopback only*, so a proxy on another host has its headers dropped
entirely and two things collapse at once: every caller on the internet shares one rate-limit
partition (one attacker can hold login at 429 for everybody), and `Request.Scheme` stays `http` so
the auth cookie never gets `Secure`. The cookie's `SecurePolicy` is `Always` outside Development
regardless — Development runs on plain http and would otherwise have no session at all.

The image runs as uid **1654**, not root, so the bind-mounted `/var/srv` has to be writable by it
before the first start. Compose publishes on **`127.0.0.1:4122:4122`** — same number both sides, so
there is one to get wrong — with `read_only`, `tmpfs: /tmp`, `cap_drop: ALL` and
`no-new-privileges`. A bare `4122:4122` binds `0.0.0.0` and puts the login form on the internet in
cleartext, around nginx and around TLS.

Commit messages follow conventional commits (`feat:`, `fix:`, `docs:`, `chore:`).
