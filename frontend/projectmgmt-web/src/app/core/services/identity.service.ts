import { Injectable, inject, signal } from '@angular/core';
import { Observable, delay, map, of, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AccountApi } from '../api/account.api';
import { LoginResult } from '../api/account-api.models';
import { IdentityApi } from '../api/identity.api';
import { ProfileDto, SkillDto } from '../api/identity-api.models';
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
  private readonly identityApi = inject(IdentityApi);
  private readonly tokenStore = inject(TOKEN_STORE);
  private readonly skillCatalog = new Map<string, SkillModel>();
  private readonly userSkills = new Map<string, UserSkillModel[]>();

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

  getMyProfile(): Observable<UserProfileModel> {
    return this.identityApi.getMyProfile().pipe(map(profile => this.applyProfile(profile)));
  }

  changePassword(_userId: string, currentPass: string, newPass: string): Observable<{ success: boolean }> {
    return this.identityApi.changePassword({
      currentPassword: currentPass,
      newPassword: newPass
    }).pipe(map(result => {
      this.tokenStore.clear();
      this.authState.set({ currentUser: null, isAuthenticated: false, token: null });
      return { success: result.success };
    }));
  }

  updateProfile(_userId: string, data: Partial<UserProfileModel>): Observable<UserProfileModel> {
    return this.identityApi.updateMyProfile({
      fullName: data.displayName,
      phoneNumber: data.phoneNumber,
      timezone: data.timezone,
      jobTitle: data.jobTitle,
      seniorityLevel: data.seniorityLevel,
      yearsOfExperience: data.yearsOfExperience,
      bio: data.bio
    }).pipe(map(profile => this.applyProfile(profile)));
  }

  uploadAvatar(file: File): Observable<string> {
    return this.identityApi.uploadAvatar(file).pipe(map(result => {
      const avatarUrl = this.resolveAssetUrl(result.avatarUrl);
      this.authState.update(state => state.currentUser
        ? { ...state, currentUser: { ...state.currentUser, avatarUrl } }
        : state);
      return avatarUrl ?? '';
    }));
  }

  // ==========================================
  // SKILLS & COMPETENCIES
  // ==========================================

  getSkillCatalog(): Observable<SkillModel[]> {
    return this.identityApi.getSkillCatalog().pipe(map(result => {
      const skills = (result.items ?? [])
        .filter(skill => Boolean(skill.skillId && skill.name))
        .map(skill => ({
          id: skill.skillId!,
          name: skill.name!,
          category: skill.category ?? 'Khác',
          description: skill.code ?? ''
        }));
      this.skillCatalog.clear();
      skills.forEach(skill => this.skillCatalog.set(skill.id, skill));
      return skills;
    }));
  }

  getUserSkills(userId: string): Observable<UserSkillModel[]> {
    return this.identityApi.getUserSkills(userId).pipe(map(result => {
      const skills = (result.skills ?? [])
        .filter(skill => Boolean(skill.skillId && skill.name))
        .map(skill => this.toUserSkill(userId, skill));
      this.userSkills.set(userId, skills);
      return skills;
    }));
  }

  addUserSkill(userId: string, skillId: string, level: number): Observable<UserSkillModel> {
    const current = this.userSkills.get(userId);
    const source = current ? of(current) : this.getUserSkills(userId);
    return source.pipe(switchMap(existingSkills => {
      const normalizedLevel = this.toBackendSkillLevel(level);
      const updatedSkills = existingSkills.filter(skill => skill.skillId !== skillId);
      const catalogSkill = this.skillCatalog.get(skillId);
      const savedSkill: UserSkillModel = {
        id: skillId,
        userId,
        skillId,
        skillName: catalogSkill?.name ?? 'Kỹ năng',
        proficiencyLevel: normalizedLevel * 20
      };
      updatedSkills.push(savedSkill);

      return this.identityApi.updateMySkills(this.toSkillDtos(updatedSkills)).pipe(map(() => {
        this.userSkills.set(userId, updatedSkills);
        return savedSkill;
      }));
    }));
  }

  removeUserSkill(id: string): Observable<{ success: boolean }> {
    const userId = this.authState().currentUser?.id;
    if (!userId) {
      return throwError(() => ({ status: 401, code: 'AUTH_UNAUTHENTICATED', title: 'Phiên đăng nhập không hợp lệ.' }));
    }

    const current = this.userSkills.get(userId);
    const source = current ? of(current) : this.getUserSkills(userId);
    return source.pipe(switchMap(existingSkills => {
      const updatedSkills = existingSkills.filter(skill => skill.skillId !== id);
      return this.identityApi.updateMySkills(this.toSkillDtos(updatedSkills)).pipe(map(result => {
        this.userSkills.set(userId, updatedSkills);
        return { success: result.success };
      }));
    }));
  }

  private applyProfile(profile: ProfileDto): UserProfileModel {
    if (!profile.success || !profile.userId || !profile.email) {
      throw new Error('Phản hồi hồ sơ không đầy đủ.');
    }

    const current = this.authState().currentUser;
    const user: UserProfileModel = {
      id: profile.userId,
      email: profile.email,
      displayName: profile.fullName ?? profile.email,
      avatarUrl: this.resolveAssetUrl(profile.avatarUrl),
      jobTitle: profile.jobTitle ?? '',
      roleId: current?.roleId ?? '',
      roleName: current?.roleName ?? 'Thành viên',
      isActive: current?.isActive ?? true,
      twoFactorEnabled: current?.twoFactorEnabled ?? false,
      createdAt: current?.createdAt ?? new Date().toISOString(),
      lastLoginAt: current?.lastLoginAt ?? new Date().toISOString(),
      phoneNumber: profile.phoneNumber,
      bio: profile.bio,
      timezone: profile.timezone,
      seniorityLevel: profile.seniorityLevel,
      yearsOfExperience: profile.yearsOfExperience
    };

    const session = this.tokenStore.getSession();
    if (session) {
      this.tokenStore.setSession({
        ...session,
        email: user.email,
        fullName: user.displayName
      });
    }
    this.authState.update(state => ({ ...state, currentUser: user }));
    return user;
  }

  private toUserSkill(userId: string, skill: SkillDto): UserSkillModel {
    return {
      id: skill.skillId!,
      userId,
      skillId: skill.skillId!,
      skillName: skill.name!,
      proficiencyLevel: (skill.proficiencyLevel ?? skill.level ?? 1) * 20
    };
  }

  private toSkillDtos(skills: UserSkillModel[]): Array<Partial<SkillDto>> {
    return skills.map(skill => ({
      skillId: skill.skillId,
      proficiencyLevel: this.toBackendSkillLevel(skill.proficiencyLevel)
    }));
  }

  private toBackendSkillLevel(level: number): number {
    return Math.min(5, Math.max(1, Math.ceil(level / 20)));
  }

  private resolveAssetUrl(url?: string | null): string | null {
    if (!url || /^https?:\/\//i.test(url)) return url ?? null;
    const backendHost = environment.apiBaseUrl.replace(/\/api\/?$/, '');
    return `${backendHost}${url.startsWith('/') ? '' : '/'}${url}`;
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
    return of([]);
  }

  revokeSession(sessionId: string): Observable<{ success: boolean }> {
    void sessionId;
    return throwError(() => ({
      status: 501,
      code: 'AUTH_SESSION_MANAGEMENT_NOT_IMPLEMENTED',
      title: 'Backend chưa cung cấp API liệt kê và thu hồi từng phiên.'
    }));
  }
}
