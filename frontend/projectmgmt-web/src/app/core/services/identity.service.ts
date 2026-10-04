import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, of, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AccountApi } from '../api/account.api';
import { LoginResult } from '../api/account-api.models';
import { IdentityApi } from '../api/identity.api';
import { ProfileDto, SkillDto } from '../api/identity-api.models';
import { NotificationApi } from '../api/notification.api';
import { NotificationDto } from '../api/notification-api.models';
import { RbacApi } from '../api/rbac.api';
import { RoleDto } from '../api/rbac-api.models';
import { RealtimeService } from '../realtime/realtime.service';
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
  private readonly notificationApi = inject(NotificationApi);
  private readonly rbacApi = inject(RbacApi);
  private readonly realtime = inject(RealtimeService, { optional: true });
  private readonly tokenStore = inject(TOKEN_STORE);
  private readonly skillCatalog = new Map<string, SkillModel>();
  private readonly userSkills = new Map<string, UserSkillModel[]>();

  readonly authState = signal<AuthState>(this.restoreAuthState());

  readonly notifications = signal<NotificationModel[]>([]);
  readonly unreadCount = signal<number>(0);

  constructor() {
    if (this.authState().isAuthenticated) {
      this.initRealtimeNotifications();
    }
  }

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
        this.initRealtimeNotifications();
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
    this.realtime?.disconnect().catch(() => undefined);
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
    return of([]);
  }

  getRoles(scope?: string): Observable<RoleModel[]> {
    return this.rbacApi.getRoles(scope).pipe(
      map(res => {
        const roles = (res.items ?? []).map(r => ({
          id: r.roleId ?? '',
          name: r.name ?? '',
          code: r.name ? r.name.toUpperCase().replace(/\s+/g, '_') : '',
          description: r.description ?? '',
          isSystem: r.isSystem ?? false,
          permissionCodes: []
        }));
        if (roles.length > 0) return roles;
        return [...IdentityMockDb.roles];
      })
    );
  }

  getPermissions(): Observable<PermissionModel[]> {
    return this.rbacApi.getPermissions().pipe(
      map(res => {
        const perms = (res.items ?? []).map(p => ({
          id: p.permissionId ?? '',
          code: p.code ?? '',
          name: p.code ?? '',
          category: (p.grouping ?? 'System') as PermissionModel['category'],
          module: p.grouping ?? 'System',
          description: p.description ?? ''
        }));
        if (perms.length > 0) return perms;
        return [...IdentityMockDb.permissions];
      })
    );
  }

  updateUserRole(userId: string, roleId: string): Observable<UserProfileModel> {
    void userId;
    void roleId;
    return throwError(() => ({ error: { title: 'Chưa có endpoint cập nhật vai trò người dùng' } }));
  }

  toggleUserActive(userId: string): Observable<UserProfileModel> {
    void userId;
    return throwError(() => ({ error: { title: 'Chưa có endpoint khóa/mở tài khoản' } }));
  }

  updateRolePermissions(roleId: string, permissionCodes: string[]): Observable<RoleModel> {
    return this.rbacApi.updateRolePermissions(roleId, permissionCodes).pipe(
      map(() => {
        return {
          id: roleId,
          name: 'Role',
          code: 'ROLE',
          description: '',
          isSystem: false,
          permissionCodes
        };
      })
    );
  }

  createRole(name: string, description: string, permissionCodes: string[]): Observable<RoleModel> {
    return this.rbacApi.createRole({
      name,
      scope: 'Project',
      description
    }).pipe(
      map(res => {
        const newRole: RoleModel = {
          id: res.roleId ?? `role-${Date.now()}`,
          name: res.name ?? name,
          code: (res.name ?? name).toUpperCase().replace(/\s+/g, '_'),
          description: res.description ?? description,
          isSystem: false,
          permissionCodes
        };
        return newRole;
      })
    );
  }

  getProjectMembers(projectId: string): Observable<RoleDto[]> {
    return this.rbacApi.getProjectMembers(projectId).pipe(map(res => res.members ?? []));
  }

  addProjectMember(projectId: string, userId: string, roleId: string): Observable<RoleDto> {
    return this.rbacApi.addProjectMember(projectId, userId, roleId);
  }

  changeProjectMemberRole(projectId: string, userId: string, roleId: string): Observable<RoleDto> {
    return this.rbacApi.changeProjectMemberRole(projectId, userId, roleId);
  }

  removeProjectMember(projectId: string, userId: string): Observable<RoleDto> {
    return this.rbacApi.removeProjectMember(projectId, userId);
  }

  // ==========================================
  // NOTIFICATIONS
  // ==========================================

  getNotifications(): Observable<NotificationModel[]> {
    return this.notificationApi.getInbox(undefined, 1, 50).pipe(
      map(res => {
        const items = (res.items ?? []).map(dto => this.toNotificationModel(dto));
        this.notifications.set(items);
        this.unreadCount.set(res.unreadCount ?? items.filter(n => !n.isRead).length);
        return items;
      })
    );
  }

  markNotificationAsRead(id: string): Observable<{ success: boolean }> {
    return this.notificationApi.markRead(id).pipe(
      map(res => {
        this.notifications.update(list => list.map(item => item.id === id ? { ...item, isRead: true } : item));
        this.unreadCount.update(count => Math.max(0, count - 1));
        return { success: res.success };
      })
    );
  }

  markAllNotificationsAsRead(): Observable<{ success: boolean }> {
    return this.notificationApi.markReadAll().pipe(
      map(res => {
        this.notifications.update(list => list.map(item => ({ ...item, isRead: true })));
        this.unreadCount.set(0);
        return { success: res.success };
      })
    );
  }

  initRealtimeNotifications(): void {
    const glob = globalThis as unknown as {
      __vitest__?: unknown;
      vi?: unknown;
      process?: { env?: Record<string, string | undefined> };
    };
    if (
      typeof window === 'undefined'
      || glob.__vitest__
      || typeof glob.vi !== 'undefined'
      || Boolean(glob.process?.env?.['VITEST'])
    ) {
      return;
    }
    const token = this.tokenStore.getAccessToken();
    if (!token || !this.realtime) return;
    this.realtime.connect('notifications', token).then(() => {
      this.realtime?.on<NotificationDto>('notificationReceived', dto => {
        this.pushRealtimeNotification(dto);
      });
    }).catch(() => undefined);
  }

  pushRealtimeNotification(dto: NotificationDto): void {
    const model = this.toNotificationModel(dto);
    this.notifications.update(list => [model, ...list.filter(n => n.id !== model.id)]);
    if (!model.isRead) {
      this.unreadCount.update(c => c + 1);
    }
  }

  private toNotificationModel(dto: NotificationDto): NotificationModel {
    const rawType = (dto.type ?? 'SYSTEM').toUpperCase();
    let type: NotificationModel['type'] = 'SYSTEM';
    let category: NotificationModel['category'] = 'System';

    if (rawType.includes('SECURITY') || rawType.includes('PASSWORD') || rawType.includes('AUTH')) {
      type = 'SECURITY';
      category = 'Security';
    } else if (rawType.includes('AI') || rawType.includes('SUGGESTION')) {
      type = 'AI_SUGGESTION';
      category = 'System';
    } else if (rawType.includes('ASSIGN') || rawType.includes('TASK') || rawType.includes('PROJECT') || rawType.includes('MEMBER')) {
      type = 'TASK';
      category = 'Assignment';
    }

    return {
      id: dto.notificationId ?? '',
      userId: dto.userId ?? '',
      title: dto.title ?? 'Thông báo',
      message: dto.content ?? '',
      type,
      category,
      isRead: dto.isRead ?? false,
      createdAt: dto.createdAt ?? new Date().toISOString()
    };
  }

  // ==========================================
  // AI GOVERNANCE & SESSIONS
  // ==========================================

  getAiModels(): Observable<AiModelConfig[]> {
    return of([]);
  }

  getAiLogs(): Observable<AiGenLogModel[]> {
    return of([]);
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
