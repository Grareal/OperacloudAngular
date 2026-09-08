import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const authenticatedRequest = request.clone({ withCredentials: true });

  return next(authenticatedRequest).pipe(catchError(error => {
    const isAuthenticationCall = request.url.includes('/api/auth/');
    if (error?.status === 401 && !isAuthenticationCall) {
      auth.clear();
      void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
    }
    return throwError(() => error);
  }));
};
