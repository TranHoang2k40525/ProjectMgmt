import { Injectable, inject, signal } from '@angular/core';
import { Observable, delay, map, of, throwError } from 'rxjs';
import { AccountApi } from '../api/account.api';
import { LoginResult } from '../api/account-api.models';
import { AuthSession, TOKEN_STORE } from '../auth/token-store';
import {
  ActiveSessionModel,
  AiGenLogModel,
  AiModelConfig,
  IdentityMockDb,
  NotificationModel,
  PermissionModel,
  RoleModel,
  SkillModel,
  UserProfileModel,
  UserSkillModel
} from '../mocks/identity-mock-db';

export interface AuthState {
  currentUser: UserProfileModel | null;
  isAuthenticated: boolean;
  token: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class IdentityService {
  private readonly accountApi = inject(AccountApi);
  private readonly tokenStore = inject(TOKEN_STORE);

  readonly authState = signal<AuthState>(this.restoreAuthState());

  readonly notifications = signal<NotificationModel[]>(IdentityMockDb.notifications);
  readonly unreadCount = signal<number>(IdentityMockDb.notifications.filter(n => !n.isRead).length);

  // ==========================================
  // AUTHENTICATION & OTP
  // ==========================================

  login(email: string, pass: string): Observable<{ user: UserProfileModel; token: string }> {
    return this.accountApi.login({ email, password: pass }).pipe(
      map(result => {
        const session = this.toSession(result);
        const user = this.toUserProfile(session);
        this.tokenStore.setSession(session);
        this.authState.set({ currentUser: user, isAuthenticated: true, token: session.accessToken });
        return { user, token: session.accessToken };
      })
    );
  }

  signup(name: string, email: string, pass: string, phoneNumber?: string): Observable<{
    email: string;
    requiresOtp: boolean;
    message?: string | null;
    resendAfterSeconds?: number | null;
  }> {
    return this.accountApi.register({
      fullName: name,
      email,
      password: pass,
      phoneNumber: phoneNumber?.trim() || null
    }).pipe(
      map(result => ({
        email: result.email ?? email,
        requiresOtp: true,
        message: result.message,
        resendAfterSeconds: result.resendAfterSeconds
      }))
    );
  }

  sendOtp(email: string, purpose: 'REGISTER' | 'FORGOT_PASSWORD'): Observable<{ success: boolean; message: string }> {
    const request = purpose === 'REGISTER'
      ? this.accountApi.sendOtp({ email, purpose: 'VerifyEmail' })
      : this.accountApi.forgotPassword(email);

    return request.pipe(map(result => ({
      success: result.success,
      message: result.message ?? 'Nếu tài khoản hợp lệ, mã OTP sẽ được gửi qua email.'
    })));
  }

  verifyOtp(email: string, code: string, purpose: 'REGISTER' | 'FORGOT_PASSWORD'): Observable<{ success: boolean }> {
    if (purpose !== 'REGISTER') {
      return throwError(() => ({
        status: 400,
        code: 'AUTH_OTP_PURPOSE_INVALID',
        title: 'OTP đặt lại mật khẩu được xác minh cùng lúc khi đặt mật khẩu mới.'
      }));
    }

    return this.accountApi.verifyOtp({ email, code, purpose: 'VerifyEmail' }).pipe(
      map(result => ({ success: result.success }))
    );
  }

  resetPassword(email: string, otpCode: string, newPass: string): Observable<{ success: boolean }> {
    return this.accountApi.resetPassword({ email, code: otpCode, newPassword: newPass }).pipe(
      map(result => ({ success: result.success }))
    );
  }

  logout(): void {
    const refreshToken = this.tokenStore.getRefreshToken();
    this.tokenStore.clear();
    this.authState.set({ currentUser: null, isAuthenticated: false, token: null });
    if (refreshToken) {
      this.accountApi.logout(refreshToken).subscribe({ error: () => undefined });
    }
  }

  private restoreAuthState(): AuthState {
    const session = this.tokenStore.getSession();
    if (!session) {
      return { currentUser: null, isAuthenticated: false, token: null };
    }

    return {
      currentUser: this.toUserProfile(session),
      isAuthenticated: true,
      token: session.accessToken
    };
  }

  private toSession(result: LoginResult): AuthSession {
    if (!result.success
      || !result.accessToken
      || !result.refreshToken
      || !result.userId
      || !result.email) {
      throw new Error('Phản hồi đăng nhập không đầy đủ.');
    }

    return {
      accessToken: result.accessToken,
      refreshToken: result.refreshToken,
      accessTokenExpiresAt: result.accessTokenExpiresAt,
      refreshTokenExpiresAt: result.refreshTokenExpiresAt,
      userId: result.userId,
      email: result.email,
      fullName: result.fullName ?? result.email,
      roles: result.roles ?? []
    };
  }

  private toUserProfile(session: AuthSession): UserProfileModel {
    const now = new Date().toISOString();
    return {
      id: session.userId,
      email: session.email,
      displayName: session.fullName,
      avatarUrl: null,
      jobTitle: '',
      roleId: '',
      roleName: session.roles[0] ?? 'Thành viên',
      isActive: true,
      twoFactorEnabled: false,
      createdAt: now,
      lastLoginAt: now
    };
  }

  // ==========================================
  // PROFILE & CHANGE PASSWORD
  // ==========================================

  changePassword(userId: string, currentPass: string, newPass: string): Observable<{ success: boolean }> {
    if (currentPass.length < 6) {
      return throwError(() => ({ error: { title: 'Mật khẩu hiện tại không chính xác.' } }));
    }
    if (newPass.length < 6) {
      return throwError(() => ({ error: { title: 'Mật khẩu mới phải có ít nhất 6 ký tự.' } }));
    }
    return of({ success: true }).pipe(delay(500));
  }

  updateProfile(userId: string, data: Partial<UserProfileModel>): Observable<UserProfileModel> {
    const user = IdentityMockDb.users.find(u => u.id === userId);
    if (!user) {
      return throwError(() => ({ error: { title: 'User not found' } }));
    }
    Object.assign(user, data);
    this.authState.update(state => ({ ...state, currentUser: { ...user } }));
    return of(user).pipe(delay(400));
  }

  // ==========================================
  // SKILLS & COMPETENCIES
  // ==========================================

  getSkillCatalog(): Observable<SkillModel[]> {
    return of([...IdentityMockDb.skills]).pipe(delay(200));
  }

  getUserSkills(userId: string): Observable<UserSkillModel[]> {
    const list = IdentityMockDb.userSkills.filter(s => s.userId === userId);
    return of(list).pipe(delay(200));
  }

  addUserSkill(userId: string, skillId: string, level: number): Observable<UserSkillModel> {
    const skill = IdentityMockDb.skills.find(s => s.id === skillId);
    if (!skill) {
      return throwError(() => ({ error: { title: 'Kỹ năng không tồn tại trong Catalog.' } }));
    }

    const existing = IdentityMockDb.userSkills.find(s => s.userId === userId && s.skillId === skillId);
    if (existing) {
      existing.proficiencyLevel = level;
      return of(existing).pipe(delay(300));
    }

    const newSkill: UserSkillModel = {
      id: `usk-${Date.now()}`,
      userId,
      skillId,
      skillName: skill.name,
      proficiencyLevel: level
    };
    IdentityMockDb.userSkills.push(newSkill);
    return of(newSkill).pipe(delay(300));
  }

  removeUserSkill(id: string): Observable<{ success: boolean }> {
    const idx = IdentityMockDb.userSkills.findIndex(s => s.id === id);
    if (idx !== -1) {
      IdentityMockDb.userSkills.splice(idx, 1);
    }
    return of({ success: true }).pipe(delay(200));
  }

  // ==========================================
  // USER MANAGEMENT & RBAC ADMIN
  // ==========================================

  getUsers(): Observable<UserProfileModel[]> {
    return of([...IdentityMockDb.users]).pipe(delay(300));
  }

  getRoles(): Observable<RoleModel[]> {
    return of([...IdentityMockDb.roles]).pipe(delay(200));
  }

  getPermissions(): Observable<PermissionModel[]> {
    return of([...IdentityMockDb.permissions]).pipe(delay(200));
  }

  updateUserRole(userId: string, roleId: string): Observable<UserProfileModel> {
    const user = IdentityMockDb.users.find(u => u.id === userId);
    const role = IdentityMockDb.roles.find(r => r.id === roleId);
    if (!user || !role) {
      return throwError(() => ({ error: { title: 'User or Role not found' } }));
    }

    user.roleId = role.id;
    user.roleName = role.name;
    return of(user).pipe(delay(400));
  }

  toggleUserActive(userId: string): Observable<UserProfileModel> {
    const user = IdentityMockDb.users.find(u => u.id === userId);
    if (!user) {
      return throwError(() => ({ error: { title: 'User not found' } }));
    }
    user.isActive = !user.isActive;
    return of(user).pipe(delay(300));
  }

  updateRolePermissions(roleId: string, permissionCodes: string[]): Observable<RoleModel> {
    const role = IdentityMockDb.roles.find(r => r.id === roleId);
    if (!role) {
      return throwError(() => ({ error: { title: 'Role not found' } }));
    }
    role.permissionCodes = [...permissionCodes];
    return of(role).pipe(delay(400));
  }

  createRole(name: string, description: string, permissionCodes: string[]): Observable<RoleModel> {
    const newRole: RoleModel = {
      id: `role-${Date.now()}`,
      name,
      code: name.toUpperCase().replace(/\s+/g, '_'),
      description,
      isSystem: false,
      permissionCodes
    };
    IdentityMockDb.roles.push(newRole);
    return of(newRole).pipe(delay(400));
  }

  // ==========================================
  // NOTIFICATIONS
  // ==========================================

  getNotifications(): Observable<NotificationModel[]> {
    return of([...IdentityMockDb.notifications]).pipe(delay(200));
  }

  markNotificationAsRead(id: string): Observable<{ success: boolean }> {
    const notif = IdentityMockDb.notifications.find(n => n.id === id);
    if (notif) {
      notif.isRead = true;
      this.notifications.set([...IdentityMockDb.notifications]);
      this.unreadCount.set(IdentityMockDb.notifications.filter(n => !n.isRead).length);
    }
    return of({ success: true }).pipe(delay(150));
  }

  markAllNotificationsAsRead(): Observable<{ success: boolean }> {
    IdentityMockDb.notifications.forEach(n => n.isRead = true);
    this.notifications.set([...IdentityMockDb.notifications]);
    this.unreadCount.set(0);
    return of({ success: true }).pipe(delay(200));
  }

  // ==========================================
  // AI GOVERNANCE & SESSIONS
  // ==========================================

  getAiModels(): Observable<AiModelConfig[]> {
    return of([...IdentityMockDb.aiModels]).pipe(delay(200));
  }

  getAiLogs(): Observable<AiGenLogModel[]> {
    return of([...IdentityMockDb.aiLogs]).pipe(delay(200));
  }

  getActiveSessions(): Observable<ActiveSessionModel[]> {
    return of([...IdentityMockDb.activeSessions]).pipe(delay(200));
  }

  revokeSession(sessionId: string): Observable<{ success: boolean }> {
    const idx = IdentityMockDb.activeSessions.findIndex(s => s.id === sessionId);
    if (idx !== -1) {
      IdentityMockDb.activeSessions.splice(idx, 1);
    }
    return of({ success: true }).pipe(delay(250));
  }
}
