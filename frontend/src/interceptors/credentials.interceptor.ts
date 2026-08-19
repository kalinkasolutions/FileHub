import { HttpInterceptorFn } from '@angular/common/http';

/**
 * The session is a cookie, so every call has to carry it. Requests are same-origin and relative
 * (`/api/...`) — the SPA is served by the API itself, and there is no dev-server proxy: the dev
 * loop is `npm run watch` writing into `wwwroot` while `dotnet run` serves it.
 */
export const credentialsInterceptor: HttpInterceptorFn = (req, next) =>
  next(req.clone({ withCredentials: true }));
