import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { AccountApi } from './account.api';

describe('AccountApi', () => {
  let api: AccountApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/v1/auth`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    api = TestBed.inject(AccountApi);
    http = TestBed.inject(HttpTestingController);
  });

  it('gửi đăng ký đúng endpoint và DTO dùng chung', () => {
    api.register({
      email: 'member@huce.edu.vn',
      password: 'StrongPassword@123',
      fullName: 'Nguyễn Văn A'
    }).subscribe();

    const request = http.expectOne(`${baseUrl}/register`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'member@huce.edu.vn',
      password: 'StrongPassword@123',
      fullName: 'Nguyễn Văn A'
    });
    request.flush({ success: true });
    http.verify();
  });

  it('gửi đăng nhập và refresh token đúng contract', () => {
    api.login({ email: 'member@huce.edu.vn', password: 'Password@123' }).subscribe();
    const login = http.expectOne(`${baseUrl}/login`);
    expect(login.request.body).toEqual({ email: 'member@huce.edu.vn', password: 'Password@123' });
    login.flush({ success: true, accessToken: 'a', refreshToken: 'r' });

    api.refreshToken('r').subscribe();
    const refresh = http.expectOne(`${baseUrl}/refresh-token`);
    expect(refresh.request.body).toEqual({ refreshToken: 'r' });
    refresh.flush({ success: true, accessToken: 'a2', refreshToken: 'r2' });
    http.verify();
  });

  it('gửi reset password với OTP trong cùng transaction nghiệp vụ', () => {
    api.resetPassword({
      email: 'member@huce.edu.vn',
      code: '123456',
      newPassword: 'NewPassword@123'
    }).subscribe();

    const request = http.expectOne(`${baseUrl}/reset-password`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.code).toBe('123456');
    request.flush({ success: true, status: 'PasswordChanged' });
    http.verify();
  });
});
