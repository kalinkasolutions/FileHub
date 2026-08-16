# FileHub

A read-only cloud for sharing files. Point it at a disk, browse it in the browser, and hand
out links to individual files or folders.

- Browse mounted drives from any device, with search and infinite scroll
- Download a file directly, or a whole folder as a streamed ZIP
- Create public share links that work without an account
- Link previews (Open Graph) so shares unfurl in chat apps
- Single Docker image, SQLite storage, no external services

## Running it

```yaml
# docker-compose.yml
services:
  app:
    container_name: file-hub
    image: kalinkasolutions/filehub:latest
    ports:
      - "8080:4122"
    volumes:
      - ./conf.json:/app/conf.json
      - ./data:/app/data
      - ./mnt/storage:/srv/storage:ro # mount your drive
    restart: always
```

```bash
mkdir -p data && chown -R 1000:1000 data   # the container runs as uid 1000
docker compose up -d
```

Then open the admin page at `/admin` and add `/srv/storage` as a base path. Anything below a
base path becomes browsable; nothing above it is reachable.

### Configuration

`conf.json`, mounted at `/app/conf.json`:

| Key | Meaning |
| --- | --- |
| `DatabasePath` | Directory for the SQLite database. Must be writable. |
| `DatabaseName` | Database filename, e.g. `db.sqlite3`. |
| `Domain` | Public hostname, used to build share links. |
| `Port` | Port the server listens on inside the container. |
| `Ssl` | `true` if the public URL is `https://`. Only affects generated links. |
| `TrustedProxies` | Hosts allowed to set `X-Forwarded-For`. Set this to your reverse proxy. |
| `Debug` | Development only — see below. |

## Security model

**FileHub has no login. It expects a reverse proxy in front of it.** Access control is the
proxy's job, and the routes are split into two groups so it can do that:

| Group | Routes | Exposure |
| --- | --- | --- |
| Public | `public-api/*`, `og/share/*`, `/share`, `/404`, static assets | Safe to expose to the internet — these are the share links |
| Private | everything else, including all of `api/*` | Must be restricted |

The private group includes `api/admin/*` (add/remove base paths, list/delete shares) and
`api/files/*` (browse and download). **Anyone who can reach `api/admin/base-path` can add
`/` as a base path and read the container's entire filesystem.** Do not publish the
container port directly.

`nginx.example.conf` is a working reference: it restricts `location /` to the local network
and exposes only the public routes. Adapt it, and if you add a route, decide which group it
belongs to.

Two further limits worth knowing:

- **Symlinks are followed.** A symlink inside a shared directory resolves to its target even
  if that target is outside the base path. Don't share directories that contain symlinks you
  don't control. (`..` traversal *is* blocked.)
- **Share links do not expire.** They are unguessable UUIDs and stay valid until the share or
  its base path is deleted. `MaxDownloadCount` is not enforced.

## Development

Backend — Go 1.24, Gin, SQLite. **Requires CGO** (`mattn/go-sqlite3` needs a C compiler):

```bash
cd backend
go build -o main .
go vet ./...
go test ./...
./main -configPath ./conf.json
```

Run it from `backend/`: the migrations directory and the built frontend are both resolved
relative to the working directory.

Frontend — Angular 19:

```bash
cd frontend
npm ci
npm start          # ng serve on :4200
npm run build
```

`backend/conf.json` sets `Debug: true`, which enables permissive CORS and turns off static
file serving so `ng serve` handles the frontend. Leave it `false` in production.

Database migrations live in `backend/migrations/` as `<unix-timestamp>_Name.sql` and are
applied in filename order at startup. A new migration must sort after every existing one.

## License

MIT — see [LICENSE](LICENSE).
