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
cp .env.example .env        # set APP_BASE_URL at minimum
$EDITOR docker-compose.yml  # mount the directories you want to serve, read-only
docker compose up -d
```

The compose file publishes the container's port 4122 on host port 8080 and mounts `./data` at
`/var/srv`, where the database and the encryption keys live.

### First run

There is no registration page. On a fresh database the only account is the one FileHub seeds.

1. **Read the admin password.** If you left `ADMIN_PASSWORD` empty, FileHub generates one and
   writes it to the log **once**, on the run that creates the account:

   ```bash
   docker compose logs app | grep "generated password"
   ```

2. **Sign in** at `APP_BASE_URL` with `ADMIN_EMAIL` (default `admin@filehub.local`).
3. **Change the password.** The bootstrap credential is good for exactly that; until it is
   replaced, every other page answers with a request to change it.
4. **Admin → Paths → add a base path.** This is a path *inside the container* — `/srv/storage`
   for the mount in the example compose file, not the host directory it comes from. Anything
   below a base path is browsable; nothing above it is reachable.
5. **Admin → Users → your own row → Base paths → grant yourself the path.** Access is per
   account and per path, and being an admin grants nothing implicitly, so until you do this your
   own file list is empty.
6. **Admin → Email → enter your SMTP settings and send a test.** Do this before step 7:
   invitations are the only way an account comes into existence, and they arrive by mail.
7. **Admin → Users → invite.** The invited person gets a link that sets their first password and
   confirms their address in one step. Grant them their base paths from the same row menu.

## Configuration

Everything is environment variables; see `.env.example` for the full comments.

| Variable | What it does | Default |
| --- | --- | --- |
| `APP_BASE_URL` | **Public origin, no trailing slash.** Share links and the links in invitation and reset mails are built from it, so a wrong value hands out links nobody can open. | `http://localhost:8080` |
| `ADMIN_EMAIL` | Address of the seeded admin. Only used on a database with no admin. | `admin@filehub.local` |
| `ADMIN_PASSWORD` | Bootstrap password. Leave empty to have one generated and logged once — safer than a value sitting in a file before anyone has signed in. | *(generated)* |
| `CONNECTION_STRING` | SQLite file. Keep it under `/var/srv` or it dies with the container. | `Data Source=/var/srv/filehub.db` |
| `DATA_PROTECTION_KEY_PATH` | Where the encryption key ring is written. Same volume, same reason. | `/var/srv/keys` |
| `LOG_LEVEL` | Minimum level for the console and the `Logs` table. | `Information` |
| `EMAIL_SMTP_HOST`, `EMAIL_PORT`, `EMAIL_USERNAME`, `EMAIL_PASSWORD`, `EMAIL_FROM_ADDRESS`, `EMAIL_FROM_NAME`, `EMAIL_SECURE_SOCKET_OPTIONS` | SMTP. These only *seed* the settings row — once an admin saves the mail settings in the UI, that row is what gets used. | port `587`, `StartTls` |

Only `APP_BASE_URL`, `ADMIN_EMAIL` and the SMTP block need real values; the rest have defaults
that work.

## Access model

- **Signing in is required for everything except share links.** Unauthenticated API calls get a
  401, not a redirect.
- **An account sees exactly the base paths an admin granted it.** There is no wildcard, and an
  admin has no implicit access to anything either.
- **A share link is the one anonymous surface.** It is an unguessable id, it may carry a download
  limit, and it stops working when the share or its base path is deleted.
- `..` traversal is refused, and so is a symlink whose target leaves the base path.

## Behind a reverse proxy

`nginx.example.conf` is a working reference. Two settings in it are not optional:

- **`proxy_buffering off`** — otherwise nginx spools a multi-gigabyte download or a generated ZIP
  to its own disk before sending a byte, and raise `proxy_read_timeout`: building a large ZIP
  produces no output for a while, which the default 60s kills.
- **`X-Forwarded-Proto` and `X-Forwarded-For`** — without the first, FileHub thinks every request
  is plain HTTP and issues auth cookies without the `Secure` flag; without the second, every log
  line and lockout counter shows the proxy's address.

## Backups

Back up the `./data` volume. It holds the SQLite database *and* the Data Protection key ring —
restoring the database without the keys signs everyone out and makes the stored SMTP password
unreadable (re-enter it under Admin → Email).

## Development

Needs the .NET 10 SDK and Node 22+.

```bash
dotnet run --project FileHub.Api   # https://localhost:5000
dotnet test
```

```bash
cd FileHub.Api/ClientApp
npm ci
npm run watch     # rebuilds into ../wwwroot; the API live-reloads on it
npm test
```

The API serves the SPA out of `FileHub.Api/wwwroot`, so `npm run watch` alongside `dotnet run` is
the whole loop — there is no separate dev server and no proxy to configure. In Development the
database and key ring land in `FileHub.Api/data`, and SMTP points at `localhost:1025`, where a
local mail catcher such as Mailpit will show you the invitation and reset mails.

Database migrations are EF Core migrations in `FileHub.Dal/Migrations`, applied automatically at
startup.

## License

MIT — see [LICENSE](LICENSE).
