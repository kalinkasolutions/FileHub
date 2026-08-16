import { HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { finalize } from 'rxjs';
import { inject } from '@angular/core';
import { LoadingService } from '@services/loading.service';

export const loadingInterceptor: HttpInterceptorFn = (req: HttpRequest<any>, next: HttpHandlerFn) => {
    const loadingService = inject(LoadingService);
    loadingService.show();
    return next(req).pipe(
        finalize(() => loadingService.hide())
    );
};