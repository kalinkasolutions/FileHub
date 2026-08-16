# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

FileHub is a read-only cloud for browsing and sharing files from mounted disks: a Go/Gin +
SQLite backend that also serves a built Angular SPA, shipped as a single Docker image.

## Commands

Backend (`backend/`) — **requires CGO**, `mattn/go-sqlite3` needs a C compiler:

```bash
go build -o main .          # build
go vet ./...
go test ./...
go test ./services/publicpathservice/ -run TestEscapePath -v   # single test
```

Frontend (`frontend/`):

```bash
npm start                   # ng serve on :4200
npm run build
npm test                    # karma/jasmine is configured but no *.spec.ts exist yet
```

Whole stack: `docker compose up` (see `Dockerfile`, `docker-compose.yml`).

### Running the backend locally

`./main -configPath ./conf.json` — the flag defaults to `/app/conf.json` (the container path),
so it must be passed during development. **Run from `backend/`**: the migration directory
(`./migrations/`) and the SPA directory (`./frontend`) are both resolved relative to the
working directory, not to the binary.

`backend/conf.json` is the dev config (`Debug: true`); the repo-root `conf.json` is the
deployment one. `Debug: true` swaps two behaviours in `api.go`: it enables permissive CORS
and it disables SPA static serving (so `ng serve` handles the frontend instead).

## Architecture

### Access model — there is no authentication in the application

Nothing in Go checks a credential. Access control is entirely nginx's job, and
`nginx.example.conf` is the reference. It splits the routes in two:

- **Public** — `public-api/*`, `og/share/*`, `/share`, `/404`, static assets. Reachable from
  the internet. These are the share-link routes.
- **Restricted** — everything else, including all of `api/*`. `api/admin/*` (base-path CRUD,
  share list/delete) and `api/files/*` (browse, download) are in here.

When adding a route, decide which half it belongs to and check the nginx location regex
covers it. A new admin endpoint that accidentally matches the public pattern is fully
unauthenticated remote access to the host filesystem.

### Path sandboxing

Every filesystem path reaching an API handler must be resolved through
`publicpathservice.GetValidFilePath` / `GetNavigationPaths`, normally via the
`api/utils.TryGetValidatedPath*` helpers. Those look up a base path by id from the `Paths`
table and join the caller-supplied navigation onto it with `cleanPath`, which absolutises
before `path.Clean` so `..` cannot escape.

Known gap: `cleanPath` does not resolve symlinks, so a symlink inside a shared directory
still escapes the base path. Do not add new code that builds a path from user input by
string concatenation.

### Shares store resolved absolute paths

`Shares.Path` is the fully-resolved path, not a (base path id, relative path) pair. That
means a share outlives its base path unless something deletes it explicitly — the base-path
delete handler calls `shareService.DeleteSharesUnderPath` for exactly this reason. Any new
code that removes or repoints a base path has to do the same.

`Shares.MaxDownloadCount` exists in the schema, the struct, and the frontend model but is
never enforced; `InsertShare` hardcodes it to 0.

### Layering

`main.go` → `config.LoadConfig` → `datalayer.NewDb` → `api.Load()`, which constructs the
services and hands them to the three route groups:

| Route group | File | Service |
|---|---|---|
| `api/files/*`, `public-api/files/*` | `api/fileapi/` | publicpathservice, shareservice |
| `api/share/*`, `api/admin/share*`, `og/share/*`, `public-api/share/*` | `api/shareapi/` | publicpathservice, shareservice |
| `api/admin/base-path` | `api/basepath/` | basepathservice, shareservice |

Go interfaces are named `IXxx` with a `NewXxx` constructor. Services take
`(logger.ILogger, *sql.DB)` and own their table.

### datalayer gotchas

- `GetItems[T]` binds columns to struct fields **positionally by declaration order** via
  reflection. The `SELECT` column list must match the struct field order exactly; a mismatch
  mis-binds silently instead of erroring. Prefer explicit column lists over `SELECT *`.
- Migrations are `migrations/<10-digit-unix-timestamp>_Name.sql`, applied in filename sort
  order. The runner records only the newest applied name and skips anything sorting at or
  below it, so a new migration must sort *after* every existing one — backdating a timestamp
  means it never runs. Files are split naively on `;`, so statements cannot contain a
  semicolon (no trigger bodies).

### Logging

`logger.Logger` fans out to sinks. `consolelogsink` writes ANSI-coloured lines to stdout;
`dblogsink` INSERTs every line into the `Logs` table and is attached in `main.go` only after
the DB exists, which is why startup config logging never lands in the database. The `Logs`
table has no retention.

Logger methods are `(message string, args ...any)` and `Sprintf` internally, but they are not
recognised as printf wrappers by `go vet` — verb/argument mismatches are not caught by
tooling. Several existing calls have them.

### Frontend

Angular 19, standalone components, RxJS only (no state library), SCSS, no UI framework.
Path aliases `@components/*`, `@services/*`, `@models/*`, `@env/*` map into `src/`.

Navigation state lives in `PathService`, which mirrors the breadcrumb array into
`history.pushState` and listens for `popstate`. Actual directory requests are keyed on
`(Id, NextSegment)`; `ItemId` is a server-generated UUID regenerated on every listing, so it
is only valid for identity comparisons *within* a single in-memory path array — never persist
it or compare it across requests.

Commit messages follow conventional commits (`feat:`, `fix:`).
