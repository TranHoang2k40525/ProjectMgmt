export interface ApiResult {
  success: boolean;
  message?: string | null;
  errorCode?: string | null;
}

/**
 * DTO dùng chung với AccountDto của backend. Mỗi endpoint chỉ gửi các trường nó cần.
 */
export interface AccountDto {
  email?: string | null;
  password?: string | null;
  fullName?: string | null;
  phoneNumber?: string | null;
  code?: string | null;
  otpCode?: string | null;
  purpose?: string | null;
  refreshToken?: string | null;
  currentPassword?: string | null;
  newPassword?: string | null;
}

export interface RegisterResult extends ApiResult {
  userId?: string | null;
  email?: string | null;
  status?: string | null;
  otpExpiresAt?: string | null;
  resendAfterSeconds?: number | null;
}

export interface OtpResult extends ApiResult {
  email?: string | null;
  status?: string | null;
  otpExpiresAt?: string | null;
  resendAfterSeconds?: number | null;
  attemptsRemaining?: number | null;
}

export interface LoginResult extends ApiResult {
  accessToken?: string | null;
  refreshToken?: string | null;
  accessTokenExpiresAt?: string | null;
  refreshTokenExpiresAt?: string | null;
  expiresInSeconds?: number | null;
  userId?: string | null;
  email?: string | null;
  fullName?: string | null;
  roles?: string[] | null;
}
