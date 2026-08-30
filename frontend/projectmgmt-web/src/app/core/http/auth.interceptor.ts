import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { TOKEN_STORE } from '../auth/token-store';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(TOKEN_STORE).getAccessToken();
  const targetsBackend = request.url.startsWith(environment.apiBaseUrl);

  if (!token || !targetsBackend) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
