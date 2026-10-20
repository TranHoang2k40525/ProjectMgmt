import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { AuthSession, TOKEN_STORE, TokenStore } from '../auth/token-store';
import { authInterceptor } from './auth.interceptor';

class InterceptorTokenStore implements TokenStore {
  session: AuthSession | null = {
    accessToken: 'access-old',
    refreshToken: 'refresh-old',
    userId: 'user-1',
    email: 'member@huce.edu.vn',
    fullName: 'Nguyễn Văn A',
    roles: ['Member']
  };

  getAccessToken(): string | null { return this.session?.accessToken ?? null; }
  getRefreshToken(): string | null { return this.session?.refreshToken ?? null; }
  getSession(): AuthSession | null { return this.session; }
  setSession(session: AuthSession): void { this.session = session; }
  clear(): void { this.session = null; }
}

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let http: HttpTestingController;
  let tokenStore: InterceptorTokenStore;

  beforeEach(() => {
    tokenStore = new InterceptorTokenStore();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: TOKEN_STORE, useValue: tokenStore }
      ]
    });
    httpClient = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
  });

  it('gắn access token cho API backend', () => {
    httpClient.get(`${environment.apiBaseUrl}/v1/users/me`).subscribe();

    const request = http.expectOne(`${environment.apiBaseUrl}/v1/users/me`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-old');
    request.flush({ success: true });
    http.verify();
  });

  it('xoay refresh token rồi thử lại request 401 đúng một lần', () => {
    let response: unknown;
    httpClient.get(`${environment.apiBaseUrl}/v1/users/me`).subscribe(value => response = value);

    const first = http.expectOne(`${environment.apiBaseUrl}/v1/users/me`);
    first.flush({ message: 'Hết hạn token' }, { status: 401, statusText: 'Unauthorized' });

    const refresh = http.expectOne(`${environment.apiBaseUrl}/v1/auth/refresh-token`);
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-old' });
    refresh.flush({
      success: true,
      accessToken: 'access-new',
      refreshToken: 'refresh-new',
      userId: 'user-1',
      email: 'member@huce.edu.vn',
      fullName: 'Nguyễn Văn A',
      roles: ['Member']
    });

    const retried = http.expectOne(`${environment.apiBaseUrl}/v1/users/me`);
    expect(retried.request.headers.get('Authorization')).toBe('Bearer access-new');
    retried.flush({ success: true });

    expect(response).toEqual({ success: true });
    expect(tokenStore.getRefreshToken()).toBe('refresh-new');
    http.verify();
  });
});
