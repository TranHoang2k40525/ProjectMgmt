import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, finalize, map, shareReplay, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AccountApi } from '../api/account.api';
import { LoginResult } from '../api/account-api.models';
import { AuthSession, TOKEN_STORE, TokenStore } from '../auth/token-store';

let refreshRequest$: Observable<string> | null = null;

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const tokenStore = inject(TOKEN_STORE);
  const accountApi = inject(AccountApi);
  const token = tokenStore.getAccessToken();
  const targetsBackend = request.url.startsWith(environment.apiBaseUrl);

  if (!token || !targetsBackend) {
    return next(request);
  }

  const authorizedRequest = request.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });

  return next(authorizedRequest).pipe(
    catchError(error => {
      if (readStatus(error) !== 401 || isAuthEndpoint(request.url)) {
        return throwError(() => error);
      }

      const refreshToken = tokenStore.getRefreshToken();
      if (!refreshToken) {
        tokenStore.clear();
        return throwError(() => error);
      }

      refreshRequest$ ??= refreshAccessToken(accountApi, tokenStore, refreshToken);
      return refreshRequest$.pipe(
        switchMap(accessToken => next(request.clone({
          setHeaders: { Authorization: `Bearer ${accessToken}` }
        })))
      );
    })
  );
};

function refreshAccessToken(
  accountApi: AccountApi,
  tokenStore: TokenStore,
  refreshToken: string
): Observable<string> {
  return accountApi.refreshToken(refreshToken).pipe(
    map(result => updateSession(result, tokenStore)),
    catchError(error => {
      tokenStore.clear();
      return throwError(() => error);
    }),
    finalize(() => refreshRequest$ = null),
    shareReplay({ bufferSize: 1, refCount: false })
  );
}

function updateSession(result: LoginResult, tokenStore: TokenStore): string {
  const current = tokenStore.getSession();
  if (!result.success || !result.accessToken || !result.refreshToken || !current) {
    tokenStore.clear();
    throw new Error('Phản hồi refresh token không hợp lệ.');
  }

  const session: AuthSession = {
    accessToken: result.accessToken,
    refreshToken: result.refreshToken,
    accessTokenExpiresAt: result.accessTokenExpiresAt,
    refreshTokenExpiresAt: result.refreshTokenExpiresAt,
    userId: result.userId ?? current.userId,
    email: result.email ?? current.email,
    fullName: result.fullName ?? current.fullName,
    roles: result.roles ?? current.roles
  };
  tokenStore.setSession(session);
  return session.accessToken;
}

function isAuthEndpoint(url: string): boolean {
  return url.startsWith(`${environment.apiBaseUrl}/v1/auth/`);
}

function readStatus(error: unknown): number | undefined {
  if (typeof error !== 'object' || error === null || !('status' in error)) return undefined;
  return typeof error.status === 'number' ? error.status : undefined;
}
