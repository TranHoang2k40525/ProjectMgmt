import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { firstValueFrom, of } from 'rxjs';
import { AccountApi } from '../api/account.api';
import { LoginResult } from '../api/account-api.models';
import { AuthSession, TOKEN_STORE, TokenStore } from '../auth/token-store';
import { IdentityMockDb } from '../mocks/identity-mock-db';
import { IdentityService } from './identity.service';

class MemoryTokenStore implements TokenStore {
  session: AuthSession | null = null;

  getAccessToken(): string | null { return this.session?.accessToken ?? null; }
  getRefreshToken(): string | null { return this.session?.refreshToken ?? null; }
  getSession(): AuthSession | null { return this.session; }
  setSession(session: AuthSession): void { this.session = session; }
  clear(): void { this.session = null; }
}

describe('IdentityService', () => {
  let service: IdentityService;
  let tokenStore: MemoryTokenStore;
  const loginResult: LoginResult = {
    success: true,
    accessToken: 'access-token',
    refreshToken: 'refresh-token',
    userId: '8ea9699e-2cf2-45b2-956e-90e2cdfdb221',
    email: 'hoang@huce.edu.vn',
    fullName: 'Trần Văn Hoàng',
    roles: ['SystemAdmin']
  };

  const accountApi = {
    login: vi.fn(() => of(loginResult)),
    register: vi.fn(() => of({
      success: true,
      email: 'new@huce.edu.vn',
      message: 'Vui lòng kiểm tra email.',
      resendAfterSeconds: 60
    })),
    sendOtp: vi.fn(() => of({ success: true, message: 'Đã gửi OTP.' })),
    verifyOtp: vi.fn(() => of({ success: true, status: 'Verified' })),
    forgotPassword: vi.fn(() => of({ success: true, message: 'Nếu tài khoản hợp lệ, OTP sẽ được gửi.' })),
    resetPassword: vi.fn(() => of({ success: true, status: 'PasswordChanged' })),
    logout: vi.fn(() => of({ success: true }))
  };

  beforeEach(() => {
    tokenStore = new MemoryTokenStore();
    Object.values(accountApi).forEach(mock => mock.mockClear());
    TestBed.configureTestingModule({
      providers: [
        IdentityService,
        { provide: AccountApi, useValue: accountApi },
        { provide: TOKEN_STORE, useValue: tokenStore }
      ]
    });
    service = TestBed.inject(IdentityService);
  });

  it('khởi tạo ở trạng thái chưa đăng nhập khi không có session', () => {
    expect(service.authState().isAuthenticated).toBe(false);
    expect(service.authState().currentUser).toBeNull();
  });

  it('đăng nhập qua API thật và lưu trọn bộ session', async () => {
    const result = await firstValueFrom(service.login('hoang@huce.edu.vn', 'StrongPassword@123'));

    expect(accountApi.login).toHaveBeenCalledWith({
      email: 'hoang@huce.edu.vn',
      password: 'StrongPassword@123'
    });
    expect(result.user.displayName).toBe('Trần Văn Hoàng');
    expect(tokenStore.getRefreshToken()).toBe('refresh-token');
    expect(service.authState().isAuthenticated).toBe(true);
  });

  it('đăng ký rồi xác minh email với purpose đúng contract backend', async () => {
    await firstValueFrom(service.signup(
      'Nguyễn Văn A',
      'new@huce.edu.vn',
      'StrongPassword@123',
      '+84901234567'
    ));
    await firstValueFrom(service.verifyOtp('new@huce.edu.vn', '123456', 'REGISTER'));

    expect(accountApi.register).toHaveBeenCalledWith({
      fullName: 'Nguyễn Văn A',
      email: 'new@huce.edu.vn',
      password: 'StrongPassword@123',
      phoneNumber: '+84901234567'
    });
    expect(accountApi.verifyOtp).toHaveBeenCalledWith({
      email: 'new@huce.edu.vn',
      code: '123456',
      purpose: 'VerifyEmail'
    });
  });

  it('quên mật khẩu dùng endpoint chống dò tài khoản', async () => {
    await firstValueFrom(service.sendOtp('member@huce.edu.vn', 'FORGOT_PASSWORD'));
    expect(accountApi.forgotPassword).toHaveBeenCalledWith('member@huce.edu.vn');
    expect(accountApi.sendOtp).not.toHaveBeenCalled();
  });

  it('đăng xuất thu hồi refresh token và xóa session cục bộ', () => {
    tokenStore.session = {
      accessToken: 'access-token', refreshToken: 'refresh-token', userId: 'u1',
      email: 'member@huce.edu.vn', fullName: 'Member', roles: []
    };
    service.logout();

    expect(accountApi.logout).toHaveBeenCalledWith('refresh-token');
    expect(tokenStore.getSession()).toBeNull();
    expect(service.authState().isAuthenticated).toBe(false);
  });

  it('vẫn hỗ trợ cập nhật hồ sơ mock trong lát cắt chưa tích hợp', async () => {
    const userId = IdentityMockDb.users[0].id;
    const updated = await firstValueFrom(service.updateProfile(userId, { jobTitle: 'Principal Lead Architect' }));
    expect(updated.jobTitle).toBe('Principal Lead Architect');
  });

  it('vẫn hỗ trợ kỹ năng mock trong lát cắt chưa tích hợp', async () => {
    const userId = IdentityMockDb.users[0].id;
    const added = await firstValueFrom(service.addUserSkill(userId, 'sk-1', 95));
    expect(added.proficiencyLevel).toBe(95);
    expect((await firstValueFrom(service.removeUserSkill(added.id))).success).toBe(true);
  });
});
