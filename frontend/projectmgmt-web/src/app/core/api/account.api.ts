import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AccountDto, ApiResult, LoginResult, OtpResult, RegisterResult } from './account-api.models';

@Injectable({ providedIn: 'root' })
export class AccountApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1/auth`;

  register(request: AccountDto): Observable<RegisterResult> {
    return this.http.post<RegisterResult>(`${this.baseUrl}/register`, request);
  }

  sendOtp(request: AccountDto): Observable<OtpResult> {
    return this.http.post<OtpResult>(`${this.baseUrl}/otp/send`, request);
  }

  verifyOtp(request: AccountDto): Observable<OtpResult> {
    return this.http.post<OtpResult>(`${this.baseUrl}/otp/verify`, request);
  }

  forgotPassword(email: string): Observable<ApiResult> {
    return this.http.post<ApiResult>(`${this.baseUrl}/forgot-password`, { email });
  }

  resetPassword(request: AccountDto): Observable<OtpResult> {
    return this.http.post<OtpResult>(`${this.baseUrl}/reset-password`, request);
  }

  externalLogin(): Observable<ApiResult> {
    return this.http.post<ApiResult>(`${this.baseUrl}/external-login`, {});
  }

  login(request: AccountDto): Observable<LoginResult> {
    return this.http.post<LoginResult>(`${this.baseUrl}/login`, request);
  }

  refreshToken(refreshToken: string): Observable<LoginResult> {
    return this.http.post<LoginResult>(`${this.baseUrl}/refresh-token`, { refreshToken });
  }

  logout(refreshToken: string): Observable<ApiResult> {
    return this.http.post<ApiResult>(`${this.baseUrl}/logout`, { refreshToken });
  }
}
