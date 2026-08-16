# FileHub — ClientApp

The Angular 21 SPA. It is built into `../wwwroot`, which is what `FileHub.Api` serves, so there is
no dev-server proxy and no `environment.apiUrl`: every request is same-origin and relative
(`/api/...`), and the session is a cookie.

```bash
npm install
npm start          # ng serve, for working on the SPA alone
npm run build      # production build into ../wwwroot
npm run watch      # rebuild into ../wwwroot on change — pair with `dotnet run`
npm test           # vitest, through @angular/build:unit-test
npx prettier --write .
```

The dev loop is `npm run watch` in one terminal and `dotnet run` in another: the API watches
`wwwroot` and live-reloads the browser.

## Layout

- `src/_variables.scss`, `src/_mixins.scss`, `src/styles.scss` — the design system. Tokens and
  mixins first, global `button` / `input` / `.icon-btn` families and the Material overlay
  surfaces in `styles.scss`. Import with `@use 'variables' as *;` / `@use 'mixins' as *;`.
- `src/_legacy.scss` — the globals the not-yet-ported screens were written against, scoped to
  their elements. Temporary; delete it with them.
- `src/components/*` — screens. `@components/*`, `@services/*`, `@models/*`, `@guards/*` and
  `@interceptors/*` are the path aliases.
- `src/guards/*` — `authGuard` (session), `adminGuard` (role) and `passwordChangeGuard`, which
  keeps an account that must choose a password on `/change-password`.

Mobile-first, and never a `style` attribute in a template — always a class.
