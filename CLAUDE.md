# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

FileHub is a read-only cloud for browsing and sharing files from mounted disks: an ASP.NET Core
(.NET 10) minimal-API backend that also serves an Angular 21 SPA out of
`backend/FileHub.Api/wwwroot`, over SQLite, shipped as a single Docker image.
`MapFallbackToFile("index.html")` hands every non-API path to the client router.

**The repository has two halves: `backend/` and `frontend/`.** Every .NET project, the solution
file and `Directory.Build.props` live under `backend/`; nothing .NET is at the root. Paths in this
document are written from the root, so a project is `backend/FileHub.Api`.

## Commands

Backend, from the repository root:

```bash
dotnet build backend/FileHub.slnx
dotnet test backend/FileHub.IntegrationTests
dotnet test backend/FileHub.IntegrationTests --filter PathSandboxTests    # one class
```

EF Core migrations (SQLite provider). The design-time tools live in `FileHub.Api` but the
`DbContext` is in `FileHub.Dal`, so both projects have to be named — from `backend/`:

```bash
dotnet ef migrations add <Name> --project FileHub.Dal --startup-project FileHub.Api
```

Migrations are applied at startup by `Seed.InitializeAsync`; there is no manual `database update`
step, and a migration that is added but never applied locally will be applied by the next `dotnet
run` against whatever database the connection string points at.

Frontend, from `frontend/`:

```bash
npm install
npm start          # ng serve — for a quick look at a component, not for talking to the API
npm run build      # outputs to ../backend/FileHub.Api/wwwroot, which is what the API serves
npm run watch      # rebuild into the API's wwwroot on change; this is the dev loop
npm test           # vitest via @angular/build:unit-test
```

**The SPA lives beside the backend projects, not inside `FileHub.Api`.** The build output still
lands in `backend/FileHub.Api/wwwroot` — that has not changed and cannot, since the API serves it
from `ContentRoot/wwwroot`. What changed is where the *sources* sit, and the reason is `dotnet
watch`: it watches each watched file's containing directory recursively, so an SPA inside the API
project put `node_modules` under the watch. That was ~2500 of the ~2800 directories being watched, and on
Linux it exhausts `fs.inotify.max_user_instances` (128 by default), which fails the watcher
outright with `The configured user limit on the number of inotify instances has been reached`.
Do not move it back under a project the SDK globs.

### The dev loop

`npm run watch` in one terminal, `dotnet run` (from `backend/FileHub.Api`) in another. The API
watches `wwwroot` with `Westwind.AspNetCore.LiveReload`, so a rebuild reloads the browser. **There is no
dev-server proxy and no `environment.apiUrl`** — every request the SPA makes is relative and
same-origin, which is the only reason the session works in development at all: served from
`ng serve` on another port, every API call is cross-origin, and no CORS policy is configured to
let a credentialed one through. If you reach for `ng serve` to work on a screen that talks to the
API, you will find this out the slow way.

The connection string and the key ring default to `./data/` — resolved against the working
directory, so `backend/FileHub.Api/data/` for a local run — and only `docker-compose.yml` moves
them to the `/var/srv` volume. A run outside the container therefore never touches `/var/srv`, in any
environment, and `Development` no longer has to override the two paths to arrange that.

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
are `Services/Files`, `Services/Shares`, `Services/BasePaths`, `Services/Groups` and the entity
folders are `Entities/Paths`, `Entities/Shares`, `Entities/Groups` — `Services/Share` would make
every `Share` parameter in the folder fail to compile, and `Services/Group` every `Group` one.

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
- **Roles are `Admin`, `User` and `CreateShares`** (`Shared/Roles.cs`), seeded at startup. They are
  fixed: there is no role creation, and `api/admin/roles` only lists them with their counts.
  **`Admin` implies every other role**, and the implication lives in exactly one place —
  `Roles.Effective`. `FileHubClaimsPrincipalFactory` expands it onto the sign-in cookie, so
  `IsInRole` and every `RequireRole` policy agree with it, and `GET api/auth/status` answers with
  the expanded set so the SPA never hides a control the API would have honoured. The implied roles
  are **not stored as rows**: a demotion then has one row to remove rather than a set, and cannot
  leave a granted-looking row behind. `api/admin/roles`' counts are who holds a role outright, which
  is why an admin does not appear under `CreateShares`.
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
- **Lockout is on** and every anonymous route under `api/auth` that checks a credential carries
  `.RequireRateLimiting("auth")` — `login`, `forgot-password`, `login-2fa`, and the three link
  routes (`accept-invite`, `reset-password`, `confirm-email-change`). They share one per-address
  budget rather than each getting their own. `logout` and `status` are deliberately outside it:
  neither checks a credential, and `status` is asked on every page load. Limiting the password step
  and not `login-2fa` would only have moved where an attacker spends attempts, since the second
  step guesses six digits. The three anonymous share routes carry
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
Graph previews. They stay anonymous, but they do read `HttpContext.User` when there is one, because
a link aimed at a group only answers a member (see *A share can be aimed at a group*). Everything
else answers 401 (not a 302: `ConfigureApplicationCookie` replaces the
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

### Base-path grants, groups, and the admin wildcard

A `BasePath` is a directory on the host FileHub may read. There are exactly three routes to one:

- `BasePathAccess` — a grant of one base path to one user.
- `BasePathGroupAccess` — a grant of one base path to one `Group`, and so to every member of it
  (`GroupMembership`). **A user's effective access is the union of their own grants and the grants
  of every group they belong to.**
- **The `Admin` role, which is an implicit grant of every base path** — browsing, downloading and
  sharing alike. This is a product decision and it reverses the rule the first version shipped
  ("absence of a grant is a denial, admins included"); for everyone who is not an admin, the two
  grant tables are still the whole story and absence from both is still a denial.

Every listing, navigation, download and share creation starts from
`IBasePathRepository.GetForUserAsync(id, userId, callerIsAdmin)`, which returns null when the user
can reach the base path by none of the three routes and which callers answer as "not found". The
union is **one query**, not a grant lookup per route. This is the invariant most likely to be broken
by a well-meaning change: a repository call that fetches a base path by id alone, used on a request
path, silently hands every user every disk.

`callerIsAdmin` is threaded from the endpoint (`ClaimsPrincipal.IsInRole(Roles.Admin)`) down through
the service to the query as an ordinary argument — the same shape `ShareService.DeleteAsync` uses.
It is deliberately **not** resolved inside the repository from an injected accessor: a reader of the
query has to be able to see what decides it, and a service-level test has to be able to set it.

Both directions of both grant tables are edited from either end — `api/admin/base-path/{id}/users`
and `api/admin/users/{id}/base-paths` for the user grants, `api/admin/base-path/{id}/groups` and
`api/admin/groups/{id}/base-paths` for the group ones — and every PUT **replaces** rather than
merges. All of them drop ids that no longer exist, and all of them answer 404 when the row the
route is *named* for is gone; the foreign key would otherwise take the whole grant change down with
it when the admin screen holds a stale row, as an unhandled `FOREIGN KEY constraint failed` — a 500
that loses the edit.

A group's name is unique and stored trimmed. The column carries the `NOCASE` collation so the unique
index and every `Name ==` comparison agree, and `GroupService` checks for a duplicate itself — a name
that is already taken is a clean 400, not a unique-index 500.

**Revoking access deletes the links that user made under it**, from every direction: the two
base-path screens, the two group screens, removing a member from a group, and deleting a group.
It happens *before* the change is saved, so a failure over-revokes rather than leaving a live
anonymous link into a base path its creator can no longer browse. Deleting a user or a base path
already cascaded; revocation is the one direction the foreign keys do not cover, and the redemption
path deliberately carries no access lookup that could catch it later.

Because access is a union, "revoked" now means *lost every route*: the three
`DeleteShares...LosingAccessAsync` queries on `IShareRepository` take the pending state of the one
relation being edited and check the others as they stand, so losing one of two routes revokes
nothing — and an admin's links are never revoked at all.

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
- **Resolving the root is the expensive half, and a listing does it once.** `TryResolve` is
  `ResolveRoot` plus `TryResolveUnder`, and `ToRelative` is `ResolveRoot` plus `ToRelativeUnder`, so
  there is still exactly one implementation of each rule. A caller with many paths under one base
  path — `FileService.ListDirectory`, `ShareService.MapDirectories` — takes the root once and passes
  it in, instead of paying a stat per segment of it for every entry. Nothing but `ResolveRoot`
  produces a root for those overloads; handing them anything else is what would weaken them.

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

#### Publishing is its own permission

**Reaching a base path settles what a user may read; it does not settle whether they may hand out an
anonymous URL into it.** The second is the `CreateShares` role, and nobody holds it until an admin
grants it — an omission in the roles list is a no, and `ResolveRoles` deliberately does not add it
the way it adds `User`. An admin holds it implicitly.

- `POST api/share` carries `.RequireAuthorization(policy => policy.RequireRole(Roles.CreateShares))`
  **and** `ShareService.CreateAsync` refuses without it, taking the answer as a `callerCanCreateShares`
  argument beside `callerIsAdmin`. The route policy is the control; the service check is what makes
  the rule visible where the logic lives and settable from a service-level test. The refusal comes
  **before the base path and the path are looked at**, so it says nothing about either.
- `GET api/share` and `DELETE api/share/{id}` stay open to any signed-in account: an account that
  still holds a link must always be able to take it down.
- **Losing the permission revokes the links.** `UserAdminService.UpdateUserCoreAsync` compares
  `Roles.CanCreateShares` before and after and calls `IShareRepository.DeleteAllSharesOfUserAsync`
  when the answer went from yes to no — *before* the role write, so a failure over-revokes rather
  than leaving live anonymous URLs behind an account that can no longer make them. Both routes to
  the permission count, so **demoting an admin revokes what they published under the wildcard**,
  which is the one direction the base-path revocation queries cannot reach (they exempt admins, and
  a link created through the wildcard has no grant row to lose). That query has no admin exemption:
  the caller has already decided who lost the right.
- Disabling an account does **not** revoke its links — lockout is not the loss of a permission.
  Deleting the account still does, by the FK cascade.

#### A share can be aimed at a group

`Share.AudienceGroupId` is optional. **Null is the default and means anonymous by URL** — today's
behaviour exactly. Set, the link only answers a signed-in member of that group, or an admin.

- **Every refusal is the same refusal.** `public-api/share/{id}` and its download must not
  distinguish "this link is not for you" from "no such link", so an outsider gets the one
  `PublicFailure` an unknown id gets. `og/share/{id}` renders the generic dead-link page rather than
  the file name, because the chat client unfurling a link is never signed in — leaking it there
  would defeat the audience for every link ever pasted.
- The three public routes stay `AllowAnonymous` and now read `HttpContext.User`, which
  `UseAuthentication()` has already decoded. A link with no audience costs no extra query, so the
  anonymous case is as cheap as it was.
- **The audience is re-checked in `TryRegisterDownloadAsync`'s conditional UPDATE**, next to the
  download limit, rather than trusted from the resolve before it. That statement is the one place a
  redemption is granted, so every rule about who may redeem belongs in its `WHERE` clause.
- **Aiming a link at a group is admin-only.** Everyone else gets a 400, and so does an admin naming
  a group id that does not exist, with the same message — the refusal for a non-admin comes *before*
  the group is looked up, so neither answer can be used to enumerate the groups in the install.
  The rule was once "a member of it, or an admin", and it was wrong: a group-aimed link is redeemed
  on membership alone, with no access lookup anywhere on the redemption path, so it hands the group
  a file none of them may hold a route to. That is a grant, not a narrower way to publish, and
  grants belong to the access model. `CreateShares` still means what it did — publish what you can
  reach, to whoever holds the URL — and now means only that.
- **Deleting a group deletes the links aimed at it**, by `ON DELETE CASCADE` on `AudienceGroupId`
  rather than by a service remembering to. EF's default for an optional relationship is `SET NULL`,
  which would quietly turn every gated link into an anonymous one — a privilege escalation nobody
  performed. The `Cascade` in `FileHubContext` is what stops that, and it must stay.

`GET api/groups` is the only group route an ordinary user reaches, and since the audience went
admin-only the share dialog no longer calls it for one. It stays open to a session — a user learning
which of their own groups they are in gives nothing away — and answers their own groups, or every
group for an admin, which is what the picker reads.

**The receiving end is `GET api/share/received`** (`ShareService.ListForAudienceAsync`): the links
aimed at a group the caller belongs to. Without it the audience was write-only — a member could
redeem a link somebody had sent them, and had no way to find one nobody had, because the target
often sits under a base path they hold no grant on and is therefore in no listing of theirs.
Membership is the *whole* condition: there is no admin wildcard, because this answers "what was
shared with me", which is a fact about the caller's groups rather than a privilege — an admin who
wants every link has `api/admin/shares`. It needs only a session, not `CreateShares`: receiving a
link is not publishing one. `ReceivedShareDto` is deliberately thinner than `ShareDto` — no base
path id, no relative path — because naming the directories above the file would hand a recipient
the shape of a disk they cannot browse.

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
  **`batchSize: 1`** rather than the sink's default 100: the admin log screen tails this table, and
  a batch that only flushes when it is full shows nothing for minutes on a quiet install while the
  screen claims to be following. One `INSERT` per entry is affordable at this volume; the round
  trip from `ILogger` call to visible row is ~20 ms.
- **The `Logs` table is the sink's, not ours.** `LogEntry` maps it read-only and
  `ToTable("Logs", t => t.ExcludeFromMigrations())` is the load-bearing line — without it the next
  `migrations add` emits a `CreateTable` for a table that already exists, and a later model change
  emits a `DropTable` that would take the log with it. `EnsureCreated` honours the exclusion too,
  which is why `LogTestBase` creates the table itself. The two indexes the screen's filters need are
  created by `Seed.EnsureLogIndexesAsync` as raw DDL, never fatally: they are not ours to describe
  in the model, but they are ours to keep.
- **The sink gets an absolute path.** Serilog's SQLite sink resolves a relative path against the
  *binary's* directory while EF resolves the connection string against the working directory, so
  `Data Source=./data/filehub.db` — the Development setting — quietly put the `Logs` table in a
  second database under `bin/`. The container never showed it, because its path is absolute.

### Logging and the audit trail

The application is meant to be **traceable**: every action that changes state, and every refusal a
credential could have caused, leaves one line that names *who* did it and *what to*. The log is the
answer to "what happened on this install", so it is written for a person reading it, not for a
grep of GUIDs.

- **The actor is `IAuditActor`** (`BusinessLogic/Auditing`), resolved per request from the sign-in
  cookie's own claims by `HttpContextAuditActor`, and rendering as `Admin <admin@example.com>` —
  the display name for recognition, the address because that is the account's actual identity.
  `SystemAuditActor` (`"system"`) is the registration for startup seeding and for the tests.
  **It decides nothing.** Authorization is still threaded from the endpoint as an ordinary
  argument (`callerIsAdmin`, `callerCanCreateShares`, `callerId`) for exactly the reasons in the
  access-model section; resolving an actor's *name* ambiently changes no answer, and threading a
  display name through twenty signatures that have no other use for it would bury the ones that do.
- `IdentityService` deliberately takes **no** `IAuditActor`: every route on it is anonymous, so
  there is no principal to name. It names the account the credential or the token resolved to,
  which it is holding anyway.
- **`{Property:l}` — the literal specifier — on every string in an audit template.** Serilog wraps
  a string property in quotes when it renders a message, so a template that supplies its own
  punctuation gets `""Family""` and `<"kim@example.com">` without it. The rule is: write the
  punctuation you want, and mark the property `:l`.
- **Levels carry meaning.** A completed action is `Information`. A wrong password, a refused role,
  a rejected reset, a grant that could not be saved is `Warning` — those are what an operator
  filters for, and at `Information` they were indistinguishable from ordinary traffic. `Error` is
  reserved for something that broke.
- **`UseSerilogRequestLogging` picks a level per request** (`GetRequestLogLevel` in `Program.cs`)
  instead of logging everything at `Information`: `Error` for a 5xx or an escaped exception,
  `Warning` for any 4xx, `Information` for `/api`, `/public-api` and `/og`, and `Debug` for the
  SPA's own files. One page load is ~30 requests for hashed chunks and icons, and at `Information`
  those buried the lines that say what somebody actually did — in a table that has no retention.
- Never hand a secret to `ILogger`. The generated bootstrap password goes to `Console.Out` for this
  reason, and so do the dev-seed credentials.

### The admin log viewer

`api/admin/logs` reads the sink's table back: `GET` with `minLevel`, `search`, `from`, `to`,
`afterId` and `take` as query parameters, admin-only. The admin area's sixth section (**Log**) is
the screen.

- **`minLevel` is a minimum, not an exact match** — "Warning" answers warnings, errors and fatals.
  It is resolved through `Shared.LogLevels.AtOrAbove` into a set of names for a `WHERE Level IN
  (...)`, because the column holds the *name*: `Level >= 'Warning'` in SQL compares alphabetically,
  which puts Debug above Warning and Error below it. An unrecognised name means "do not filter" and
  never "no levels" — an empty log screen reads as a quiet system.
- **The timestamp stays a `string` all the way to the comparison.** The sink writes
  `2026-08-31T14:45:39.027` (ISO, 'T', milliseconds, UTC) while EF's SQLite provider formats a
  `DateTime` parameter with a space separator and seven decimals, so a range written against a
  `DateTime` property compares two different shapes. `LogRepository` formats its bounds the sink's
  way; the format is fixed-width, so lexicographic order is chronological order.
- **`search` goes through `EF.Functions.Like` with an explicit `ESCAPE`**, not `Contains`: EF
  translates `Contains` to `instr()`, which is case-sensitive. The term is escaped, because an
  unescaped `%` matches every row and an unescaped `_` matches any character — and log messages are
  full of paths and identifiers.
- **`afterId` narrows the page but not the count.** It is the tail cursor — an id and not a
  timestamp, because two entries can share a millisecond and a timestamp cursor then repeats one or
  drops one. "12 new lines" and "4,000 lines match" are two different questions the screen asks at
  once.
- **Live is a SignalR push, not a poll.** `LogHub` is mapped at `api/admin/logs/stream` and sends
  one parameterless `logged` message; the screen answers it with an ordinary `GET` carrying
  `afterId`. An idle installation therefore costs **zero** requests. Following switches itself off
  when a date range is set — "the newest lines" and "the lines between these times" are different
  questions — and the screen shows the connection's own state, because a live view that has quietly
  stopped being live is worse than one that says so.
- **The hub carries a signal, not the log**, and that is load-bearing. Pushing the entries would
  mean evaluating every connected admin's filter in memory — a second implementation of the query
  that can drift from the SQL one — and a `LogEvent` has no database id yet, because the id comes
  from the SQLite sink's `INSERT`, so the client would lose the cursor it catches up with.
- **Reading the log must not ring the bell.** `LogRoutes.IsLogScreenTraffic` is the one rule, applied
  in two places: `GetRequestLogLevel` drops the log screen's own requests to `Verbose` so they stay
  out of a table with no retention, and `LogSignalSink` refuses to ring for them. The second is what
  matters — without it the view feeds itself (fetch → request-logged → ring → push → fetch), and an
  *idle* screen made about five requests a second, which is worse than the polling this replaced.
  Both are needed: the level alone fails on an install running at `Debug`.
- **The path from sink to socket is deliberately broken in three.** `LogSignalSink` only rings;
  `LogChangeSignal` is a one-slot channel with `DropWrite`, so a burst of two hundred lines is one
  notification and a ring can neither block nor throw nor recurse; `LogBroadcastService` is the only
  thing that sends, and pauses 200ms afterwards to coalesce. Sending over SignalR itself logs, so
  broadcasting from inside `Emit` would be a straight recursion.
- **Behind nginx this needs the WebSocket upgrade headers** (`Upgrade` / `Connection` and the
  `$connection_upgrade` map) — `nginx.example.conf` carries them. Without them the handshake is
  forwarded with the upgrade stripped and the client silently falls back to long-polling.
- The client buffer is capped (1000 lines) as well as the server page, and the section is
  `@defer`red — which also keeps the SignalR client out of the initial bundle, and means the hub is
  not connected from the moment the admin area is opened on another tab.
- **The screen is the standard admin split**: a Filter panel on the leading edge and the Log beside
  it, like every other section. **Following lives in the filter**, since it decides what the list
  shows exactly as the four controls above it do. The tally rides the Log panel's own heading, the
  way the file browser's listing carries its count.
- It uses `admin-section-split` like every other section, so its Filter panel is exactly as wide as
  "Invite an account", "Create a group" and "Add a base path" (450px). It was briefly pinned
  narrower to buy the message more room, and that was wrong: a filter panel narrower than the form
  panel on the tab next door reads as a different kind of screen. The width the log needed came from
  lifting the view's own cap, not from shaving the column beside it — at 1920 the message is 1149px
  and nothing wraps, against 490px and 79 wrapped rows in 200 before.
- **There is deliberately no loading state — and there are two of them to remove.** The one in the
  component, and the `@defer` `@placeholder` in `admin.component.html`, which is what shows while
  the lazy chunk downloads; the Log case is the only one without a placeholder for that reason. A
  spinner on every filter keystroke is the only thing a reader on a slow connection would ever see,
  and a slow request is not worth announcing. The list keeps the rows it has until the new ones
  land, and the empty message is gated on `!isLoading()` so it can never claim an empty log while
  the first request is still in flight — the opposite mistake, and the one worth guarding.
- **A log row is three stacked lines on a phone** — level, then timestamp, then the message, each
  full width — and one line from 470px up. Three columns at 390px leave the message about fifteen
  glyphs, which is not a log line. The expand-exception button is absolutely positioned on the small
  layout so it does not claim a fourth line.

`LogQueryTests` covers the filters; they are all places where the query can be quietly wrong rather
than loudly broken. `LogSignalTests` covers the coalescing and the loop guard, which live in
`FileHub.Shared` precisely so they are reachable without referencing `FileHub.Api`.

### Testing

`FileHub.IntegrationTests` (xUnit, 478 tests) drives the **services**, not the routes: the real service →
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
authorization on the route groups (`ShareService.DeleteAsync` and `CreateAsync`, `IFileService` and
the public resolve all take `callerIsAdmin` — and `CreateAsync` also `callerCanCreateShares` — as a
*parameter*; nothing below the endpoint proves the caller holds either, so a test pins any answer
but not that the principal was read, and in particular not that the claims factory put the roles an
admin only implies onto the cookie the `RequireRole` policy reads), the caller
identity on the anonymous share routes, `FileDownload`'s zip and
headers, `ShareLinks`, the rate limiter, the forwarded-headers configuration, and the SignalR half
of the live log (`LogHub`, `LogSignalSink`, `LogBroadcastService` — the rule and the coalescing they
rest on are covered, the wiring is not). `AdminSeeder` is
covered, because it is the part of startup that can brick an install; the migration and role half
in `Seed` is not.

The SPA has **132 vitest specs** (`npm test`), over the things worth pinning without a browser:
path building, the size and audience formatters, the services against `HttpTestingController`, and
the guards. They do not cover how anything *looks* — the layout bugs in this codebase have all been
found by screenshotting a running instance at 360px and 1280px, not by a spec, so do that when
changing a screen rather than trusting a green suite.

### Frontend

Angular 21, standalone components, **signals** (`signal`/`computed`) and **zoneless** — state
written from an rxjs callback needs `markForCheck()` or nothing renders. Angular Material + CDK,
`ngx-toastr`, SCSS, Prettier. Mobile-first: the small-screen layout is the one written first, and
the phone case is a `max-width: 470px` step-down rather than a pile of `min-width` overrides.
**Never use the `style` attribute in a template** — always a class.

#### The look is FileHub's own

The design system is `src/_variables.scss`, `src/_mixins.scss` and the globals in `styles.scss`.
It is deliberately **not** a generic dark theme, and an earlier version of this app that borrowed
one wholesale was rejected for exactly that. What it is:

- **One monospace face** for the whole app — `$font-main` *is* `$font-mono`. A file listing is a
  column of names, and a proportional face makes that column ragged; every other screen follows the
  listing rather than the other way round.
- **Square corners everywhere.** Every radius token (`$border-radius`, `$radius-icon`,
  `$radius-pill`, …) is `0`. They still exist so a component asks for "the" radius and gets
  FileHub's answer instead of inventing one. This is the decision most of the rest follows from —
  do not introduce a `border-radius`.
- **The panel is the one structural idea.** `@include panel` is a 5px accent rule around a darker
  fill with a shadow offset down and to the right — a blur wider than twice that offset reaches past
  the top and left edges and turns it into the four-sided glow of material elevation, which is not
  this look. Its `> .title` is ruled off in the lighter accent so the rule reads as part of the
  border. Anything that is a *thing* on a screen — a listing, a form, a settings block, a menu, a
  dialog, a toast — is one of these. Reach for it rather than inventing a card. `@include
  panel-grid($max)` lays several out and collapses to a column under 470px; the `$max` argument
  exists because a fixed px track also caps a `grid-column: 1 / -1` child, so a screen with one wide
  listing wants the default `1fr` and a row of forms passes `$max-form-width`.
- **A sticky element inside a scrolling panel has two resting places, and they have to agree.**
  While the scroll runs it rests against the scrollport's content box; once the scroll runs out it
  is wherever its containing block puts it. An admin panel's `.buttons` is sticky inside `.body`, so
  `.body`'s bottom padding sat between the two and the Save button lifted by exactly that much over
  the last few percent of the scroll. The fix is to leave nothing between them: `:has()` zeroes that
  padding on a body holding such a form, and the buttons carry it themselves, painted in the panel's
  fill. Do not put the padding back.
- **The admin area is the one view exempt from `$max-content-width`.** That cap is a *reading*
  measure and there is no prose in there — six dense data screens, and a log with one long record
  per row. Capped, a 2560px monitor gave the log message exactly the same 490px a laptop did. The
  exemption is written in `styles.scss` as `.shell.chromed > admin`, **not** in the component: the
  rule it overrides is two classes and `:host` is one, so a component-level `max-width` loses the
  cascade wherever it sits. Consequently `admin-section-split` caps its *form* track
  (`minmax($panel-min-width, $max-form-width)`) and gives the listing the rest — proportional tracks
  turned an SMTP Port field into a 1217px input. Email is the exception that proves it: **both** of
  its panels are forms, so it sets two capped columns of its own rather than using the split.
- **`admin-panel` makes a panel's `.body` a shrinkable scroller on a wide screen**, which is right
  when the body *is* the content — a form, a block of prose. When the body is a fixed header above a
  long list in the same panel, it loses the flex fight and collapses into a nested scrollport nobody
  would think to scroll: the log screen's filters did exactly that, showing four labels and none of
  their inputs. Such a body needs `flex-shrink: 0; min-height: auto; overflow: visible`, so the list
  is the only thing that scrolls.
- **Inputs are a ruled line**, not a filled box: a 2px bottom border that turns green on focus, and
  that is the whole focus treatment — no ring, no glow. **Buttons** are square translucent-green
  slabs (`$accent-fill`), with `.secondary` and `.danger` outlined, because only one thing on a
  screen should look like the thing to press.
- **Icons are set in the accent** — the green glyph is how a row says it can be acted on. Anything
  that colours its own contents (a filled button, a tab) overrides that with `color: inherit`, which
  `button-base`, `icon-button-base` and `tab-bar` already do.
- **`@include chip`** is the small outlined tag, with `.invited` / `.disabled` / `.restricted` as
  the only states worth a colour. `.restricted` is `$share-purple`, for a share link that answers
  only a group: neither the accent nor a warning, because it is neither.
- **Tabs sit at the top** of a screen (`@include tab-bar`), under the header. The bar scrolls
  sideways rather than wrapping — five sections do not fit a phone, and a wrapped bar pushes the
  content it belongs to off the screen — and marks the current tab with an accent underline rather
  than a fill, since a filled tab reads as a button to press. Its scrollbar is hidden, so a screen
  that restores a remembered section has to scroll the active tab into view itself.
- **The page is lighter than the panels standing on it** (`$bg-page` `#333` against `$surface`
  `#2b2828`), which is the inversion the app has always had. It is why `.icon-btn.quiet` hovers to
  `$surface-hover` and not to `$surface`: hovering to the panel's own fill is no feedback at all,
  and most quiet buttons live inside a panel.
- **Mobile-first, and not as a slogan.** The root font size steps down to 14px under 470px, so every
  measure written in `em` or in the spacing tokens tightens with it — prefer those to px.

Import with `@use 'variables' as *;` / `@use 'mixins' as *;`. Material is **themed, not removed**:
its panel selectors have to stay element-qualified (`div.mat-mdc-menu-panel`, …) because Material
injects its own structural CSS at runtime, after this stylesheet, and wins an equal-specificity tie;
dialog padding goes through its `--mat-dialog-*` variables for the same reason. `ngx-toastr` is
themed too — left alone it is the one surface on screen still wearing somebody else's look.

The logo is **`file-hub.svg`**, the wordmark, in the header and above every auth screen. It is blue
— the one non-green thing in the app — which was true of the original and is left alone
deliberately. The square "F" mark stays as the favicon and in the mail templates, where a raster is
needed. `thebeaver.png` is on the 404, where it has always been.

**The wordmark is outlines, and it has to stay outlines.** It used to be a live `<text>` element set
in `font-family:'Greater Theory'` with no fallback, which meant the logo was one thing on a machine
that had the typeface and the browser's default *serif* on every machine that did not — so it looked
correct to whoever drew it and wrong to everybody else. The glyphs are now `<path>` data and depend
on no installed font. If you ever regenerate it: the source carries **hand-tuned per-character
kerning** in the tspan's `dx` list, so do not re-derive letter positions from the font's own metrics
— recomputing `dx` and kerning by hand drifts a fraction of a pixel per glyph and came out 17% of
pixels different, visibly wider by the final "b". Measure the live text's positions with
`getStartPositionOfChar` in a browser that has the font, and plant the outlines there; that carries
the manual kerning across for free and matched the original's ink bounding box exactly.

Greater Theory is **personal-use only** (brandsemut). The outlines are in the repository, the font
file is not, and the README says what a commercial fork has to do about it — buy a licence or
replace `frontend/public/file-hub.svg`. Keep that notice in step with the artwork.

#### Two things that cost a day each

**A rule in `app.component.scss` cannot reach the routed view.** The router renders that component
as a *sibling* of `<router-outlet>`, outside `app.component`'s template, so Angular's view
encapsulation scopes the rule to a content attribute the view never carries and it silently matches
nothing. The sizing for `.shell.chromed > :not(app-header)` therefore lives in `styles.scss`, which
has no encapsulation. Written in the component, it did nothing, and every chromed view was one
header taller than the screen — which is what pushed a bottom-of-screen element out of sight and
looked like a layout bug anywhere but where it was.

**An `IntersectionObserver` for incremental rendering must take the scrolling list as its `root`.**
The file browser's sentinel is the last row *inside* the list, which is the scroller: against the
implicit viewport root it sits exactly at that list's own overflow clip, so it has zero intersection
area at the moment it should fire, and `rootMargin` grows the *root's* rect rather than an
ancestor's clip. The symptom is a list that renders its first page and never loads another.

Routes (`src/app/app.routes.ts`): `login` and `''` (the browser) are eager — one of the two is the
first thing every visit needs. The screens reached from a mail link (`accept-invite`,
`reset-password`, `confirm-email-change`, plus `change-password` and `account`) are `loadComponent`
routes and stay out of the initial bundle. `share/:id` and `404` carry **no guards** — the share
landing is the one screen a stranger sees, and it must look the same to a signed-in visitor.
`data.chrome` (`none` / `anonymous` / default) is what a route asks of the app shell.

Three guards: `authGuard` (session, else `/login`), `adminGuard` (the `Admin` role; sends a
signed-in non-admin back to `/` rather than to a sign-in screen that would read as a bug), and
`passwordChangeGuard` (described above).

**The account screen is one column at every width**, capped at `$max-reading-width` and centred. It
was two on a desktop, and that was wrong for what it holds: five blocks read top to bottom, half of
them one field and a button, so a two-track grid pairs a three-line block with a ten-line one and
zig-zags the reading order across the screen.

**The listing panel's heading is the breadcrumb trail**, not a title: the back button, then
`home / media / audiobooks / …`, then the tally. It replaced a title that said "Files" over a
separate crumb row saying where you were — the same thing twice, with a rule spent on the
repetition. The trail keeps the title's own type size, scrolls sideways without a scrollbar, and the
component scrolls the last crumb into view whenever the path *or the tally* changes: the tally
appearing narrows the trail beside it, and a scroll already at the end stops being at the end.

**The file browser has three tabs** — Files, Links, Shared. `AuthService.canCreateShares` is the
`CreateShares` role off the status call, and the browser hides both the per-row share button and the
whole **Links** tab without it: losing the role revokes the links, so there is nothing behind that
tab to reach. **Shared** — `api/share/received`, what the caller's groups were sent — is *not* gated
on it, because being sent a link is not publishing one, and it is drawn unconditionally rather than
after a request asking whether it would be empty; an account in no group finds the panel saying so.
A plain `roles.includes(...)` is enough for either because the server sends the *effective* roles:
an admin's status already carries `CreateShares` beside `Admin`, and the client re-derives nothing.

The share dialog's **audience picker is behind `AuthService.isAdmin`**, matching the API, and the
groups are not even fetched for anyone else. Both lists of links — the caller's own and the ones
aimed at their groups — share `_share-list.scss`, so one list of shares does not look like two
different things depending on which end of it you stand at; the received list is the same rows with
`.restricted` on all of them, since nothing aimed at a group is anonymous.

**The admin area is one component with six sections** — Users, Groups, Paths, Links, Email, Log —
not nested routes, which is what lets the header and the tab bar stay put while the section
changes.
The section is remembered in a module variable, so returning to `/admin` reopens the last one.
Group membership is editable **only from the group side**: there is no `api/admin/users/{id}/groups`
because the members list is replaced as a whole, so a per-user editor would mean reading every
group and issuing one PUT per group to save one checkbox, racing the Groups screen while it did.
A base path's two grant lists (users, groups) are siblings, not a hierarchy, and their counts are
deliberately not summed — a user granted directly *and* through a group would be counted twice.

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

The image defaults to uid **1654**, not root, and compose overrides that with
`user: "${PUID:-1000}:${PGID:-1000}"` so the container runs as whoever owns the bind-mounted
`./data` — a fixed uid meant a `chown` before the first start, which is a step an operator only
finds out about by the first run failing on the migration. It is never root either way. Compose
publishes on **`127.0.0.1:4122:4122`** — same number both sides, so
there is one to get wrong — with `read_only`, `tmpfs: /tmp`, `cap_drop: ALL` and
`no-new-privileges`. A bare `4122:4122` binds `0.0.0.0` and puts the login form on the internet in
cleartext, around nginx and around TLS.

Commit messages follow conventional commits (`feat:`, `fix:`, `docs:`, `chore:`).
