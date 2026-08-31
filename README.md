# FileHub

A read-only cloud for browsing and sharing files from mounted disks. Point it at a disk, give
people accounts, and hand out links to individual files or folders.

- Accounts and roles — an admin decides which mounted paths each account may see
- Browse from any device; download a file directly, or a whole folder as a streamed ZIP
- Public share links that work without an account, with an optional download limit
- Link previews (Open Graph) so shares unfurl in chat apps
- Two-factor sign-in (TOTP), password reset and email change by mail
- Single Docker image, SQLite storage, no external services

## Running it

The repo ships a working `docker-compose.yml` and a documented `.env.example`.

```bash
cp .env.example .env                        # set APP_BASE_URL at minimum
$EDITOR docker-compose.yml                  # mount the directories you want to serve, read-only
mkdir -p ./data
docker compose up -d
```

The container never runs as root. Compose runs it as `PUID:PGID` from your `.env` (default
`1000:1000`), so `./data` needs no `chown` as long as those match whoever owns it — `id -u` and
`id -g`. If they do not, the first run cannot create the database and exits on the migration.

The compose file publishes port **4122 on `127.0.0.1` only** and mounts `./data` at `/var/srv`,
where the database and the encryption keys live. FileHub speaks plain HTTP — TLS belongs at the
reverse proxy — so publishing it on all interfaces would put the login form and every download on
the internet in cleartext, around nginx. If your proxy runs on another host, bind the port to the
interface that host reaches you on and add that proxy to `TRUSTED_PROXIES`.

### First run

There is no registration page. On a fresh database the only account is the one FileHub seeds.

1. **Read the admin password.** If you left `ADMIN_PASSWORD` empty, FileHub generates one and
   prints it to the container's console **once**, on the run that creates the account. It is
   printed rather than logged on purpose — the log is also a table in the database, and a
   bootstrap credential should not outlive its first use:

   ```bash
   docker compose logs app | grep -A2 "initial administrator"
   ```

2. **Sign in** at `APP_BASE_URL` with `ADMIN_EMAIL` (default `admin@filehub.local`).
3. **Change the password.** The bootstrap credential is good for exactly that; until it is
   replaced, every other page answers with a request to change it.
4. **Admin → Paths → add a base path.** This is a path *inside the container* — `/srv/storage`
   for the mount in the example compose file, not the host directory it comes from. Anything
   below a base path is browsable; nothing above it is reachable.
5. **Browse it.** An admin sees every base path, so there is nothing to grant yourself — the disk
   is already there under *directories*. Everybody else sees only what they are given in step 8.
6. **Admin → Email → enter your SMTP settings and send a test.** Do this before step 7:
   invitations are the only way an account comes into existence, and they arrive by mail. The
   password is write-only — later edits can leave it blank to keep it, *except* when you change the
   host, port, transport or username, which clears it rather than sending your secret to a server
   it was never given to. The screen says so when it happens.
7. **Admin → Users → invite.** The invited person gets a link that sets their first password and
   confirms their address in one step.
8. **Give them access to something.** Either per account — Admin → Users → the row's menu → *Base
   paths* — or, when several people need the same disks, **Admin → Groups**: create a group, put
   accounts in it, and grant the group its paths. An account's access is the union of its own
   grants and its groups', so a group only ever adds. An account with neither sees an empty list.

## Configuration

Everything is environment variables; see `.env.example` for the full comments.

| Variable | What it does | Default |
| --- | --- | --- |
| `APP_BASE_URL` | **Public origin, no trailing slash.** Share links and the links in invitation and reset mails are built from it, so a wrong value hands out links nobody can open. | `http://localhost:4122` |
| `TRUSTED_PROXIES` | **Whose `X-Forwarded-For`/`X-Forwarded-Proto` FileHub believes** — IPs and/or CIDR blocks (host bits clear), comma separated. Get this wrong and the login rate limit counts the whole internet as one caller, and auth cookies lose their `Secure` flag. Narrow it to your proxy. A malformed entry stops the app rather than being skipped. | loopback + the private ranges |
| `ADMIN_EMAIL` | Address of the seeded admin. Only used on a database with no admin. | `admin@filehub.local` |
| `ADMIN_PASSWORD` | Bootstrap password. Leave empty to have one generated and printed once — safer than a value sitting in a file before anyone has signed in. | *(generated)* |
| `PUID` / `PGID` | The uid:gid the container runs as. Match them to the owner of `./data` and the bind mount needs no `chown`. | `1000` / `1000` |
| `CONNECTION_STRING` | SQLite file. Keep it under `/var/srv` or it dies with the container. | `Data Source=/var/srv/filehub.db` |
| `DATA_PROTECTION_KEY_PATH` | Where the encryption key ring is written. Same volume, same reason. | `/var/srv/keys` |
| `LOG_LEVEL` | Minimum level for the console and the `Logs` table. | `Information` |
| `EMAIL_SMTP_HOST`, `EMAIL_PORT`, `EMAIL_USERNAME`, `EMAIL_PASSWORD`, `EMAIL_FROM_ADDRESS`, `EMAIL_FROM_NAME`, `EMAIL_SECURE_SOCKET_OPTIONS` | SMTP. These only *seed* the settings row — once an admin saves the mail settings in the UI, that row is what gets used. | port `587`, `StartTls` |

Only `APP_BASE_URL`, `ADMIN_EMAIL` and the SMTP block need real values; the rest have defaults that
work, though `TRUSTED_PROXIES` is worth narrowing from the private ranges to your proxy's own
address.

## Access model

- **Signing in is required for everything except share links.** Unauthenticated API calls get a
  401, not a redirect.
- **An account sees exactly the base paths it was granted**, directly or through a group it
  belongs to. There is no other way in — except the Admin role, which sees every base path.
- **Groups are named sets of accounts.** Grant a path to a group and every member gets it; a
  member's access is the union of their own grants and their groups'.
- **A share link is normally the one anonymous surface.** It is an unguessable id, it may carry a
  download limit, and it stops working when the share or its base path is deleted — or when its
  creator loses every route to that base path. A link can instead be aimed at a **group**, chosen
  when the link is created, in which case it answers only signed-in members of that group and looks
  like a dead link to everyone else, link previews included — so it cannot usefully be forwarded
  outside the group. Deleting a group deletes the links aimed at it, rather than quietly making
  them public.
- `..` traversal is refused, and so is any path that resolves out of the base path through a
  symlink, however many links deep.
- **Two-factor is per account**, enabled from the account screen; enrolling and regenerating
  recovery codes both ask for the current password, so a stolen session cannot pair a second
  factor of its own.

## Behind a reverse proxy

`nginx.example.conf` is a working reference. Three things in it are not optional:

- **`proxy_buffering off`** — otherwise nginx spools a multi-gigabyte download or a generated ZIP
  to its own disk before sending a byte, and raise `proxy_read_timeout`: building a large ZIP
  produces no output for a while, which the default 60s kills.
- **`X-Forwarded-Proto` and `X-Forwarded-For`**, *and* the proxy's address in `TRUSTED_PROXIES`.
  Sending the headers is half of it: FileHub ignores them from anywhere it was not told to trust,
  and then thinks every request is plain HTTP (auth cookies lose `Secure`) and that the whole
  internet is one caller (the login rate limit becomes a single bucket).
- **The two rate-limit zones.** `limit_req`/`limit_conn` are used in the site file, but
  `limit_req_zone` and `limit_conn_zone` are only valid in `http { }` — copy those two lines from
  the comment at the top of `nginx.example.conf` into `nginx.conf`, or nginx will not start.

It also sets HSTS, `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy` and a CSP. HSTS with
`includeSubDomains` is a commitment — turn it on once every subdomain is on TLS.

## Backups

Back up the `./data` volume. It holds the SQLite database *and* the Data Protection key ring —
restoring the database without the keys signs everyone out and makes the stored SMTP password
unreadable (re-enter it under Admin → Email). Restore it owned by the `PUID:PGID` the container
runs as, the same as a fresh install.

## Development

Needs the .NET 10 SDK and Node 22+.

The repository has two halves: `backend/` holds every .NET project and the solution file,
`frontend/` holds the SPA.

```bash
dotnet run --project backend/FileHub.Api   # http://localhost:5000
dotnet test backend/FileHub.slnx
```

```bash
cd frontend
npm ci
npm run watch     # rebuilds into ../backend/FileHub.Api/wwwroot; the API live-reloads on it
npm test
```

The API serves the SPA out of `backend/FileHub.Api/wwwroot`, so `npm run watch` alongside
`dotnet run` is the whole loop — there is no separate dev server and no proxy to configure. In
Development the database and key ring land in `backend/FileHub.Api/data`, and SMTP points at
`localhost:1025`, where a local mail catcher such as Mailpit will show you the invitation and reset
mails.

Database migrations are EF Core migrations in `backend/FileHub.Dal/Migrations`, applied
automatically at startup.

## License

MIT — see [LICENSE](LICENSE).
