import { Injectable, signal } from '@angular/core';
import { Observable, delay, of, throwError } from 'rxjs';
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
  readonly authState = signal<AuthState>({
    currentUser: IdentityMockDb.users[0], // Default logged in as Admin for easy testing
    isAuthenticated: true,
    token: 'mock-jwt-token-scrumai-2026'
  });

  readonly notifications = signal<NotificationModel[]>(IdentityMockDb.notifications);
  readonly unreadCount = signal<number>(IdentityMockDb.notifications.filter(n => !n.isRead).length);

  // ==========================================
  // AUTHENTICATION & OTP
  // ==========================================

  login(email: string, pass: string): Observable<{ user: UserProfileModel; token: string }> {
    if (pass) { /* no-op reference for lint */ }
    const user = IdentityMockDb.users.find(u => u.email.toLowerCase() === email.toLowerCase());
    if (!user) {
      return throwError(() => ({ error: { title: 'Tài khoản không tồn tại trong hệ thống.' } }));
    }
    if (!user.isActive) {
      return throwError(() => ({ error: { title: 'Tài khoản đã bị tạm khóa. Vui lòng liên hệ Admin.' } }));
    }

    user.lastLoginAt = new Date().toISOString();
    const token = `token-${user.id}-${Date.now()}`;
    this.authState.set({ currentUser: user, isAuthenticated: true, token });
    return of({ user, token }).pipe(delay(600));
  }

  signup(name: string, email: string, pass: string): Observable<{ email: string; requiresOtp: boolean }> {
    if (name || pass) { /* no-op reference for lint */ }
    const existing = IdentityMockDb.users.find(u => u.email.toLowerCase() === email.toLowerCase());
    if (existing) {
      return throwError(() => ({ error: { title: 'Email này đã được sử dụng bởi tài khoản khác.' } }));
    }

    // Generate mock OTP code for registration
    IdentityMockDb.otpStorage.set(email.toLowerCase(), {
      code: '123456',
      expiresAt: Date.now() + 5 * 60 * 1000,
      purpose: 'REGISTER'
    });

    return of({ email, requiresOtp: true }).pipe(delay(500));
  }

  sendOtp(email: string, purpose: 'REGISTER' | 'FORGOT_PASSWORD'): Observable<{ success: boolean; message: string }> {
    const mockCode = '123456';
    IdentityMockDb.otpStorage.set(email.toLowerCase(), {
      code: mockCode,
      expiresAt: Date.now() + 5 * 60 * 1000,
      purpose
    });
    return of({
      success: true,
      message: `Mã OTP xác thực (${mockCode}) đã được gửi tới ${email}. Có hiệu lực trong 5 phút.`
    }).pipe(delay(400));
  }

  verifyOtp(email: string, code: string, purpose: 'REGISTER' | 'FORGOT_PASSWORD'): Observable<{ success: boolean }> {
    const stored = IdentityMockDb.otpStorage.get(email.toLowerCase());
    if (!stored || stored.purpose !== purpose) {
      return throwError(() => ({ error: { title: 'Mã OTP không tồn tại hoặc đã hết hạn. Vui lòng yêu cầu mã mới.' } }));
    }
    if (stored.code !== code && code !== '123456') { // Allow 123456 for easy demo testing
      return throwError(() => ({ error: { title: 'Mã OTP không chính xác. Vui lòng thử lại.' } }));
    }

    if (purpose === 'REGISTER') {
      const newUser: UserProfileModel = {
        id: `user-${Date.now()}`,
        email,
        displayName: email.split('@')[0],
        avatarUrl: null,
        jobTitle: 'Software Engineer',
        roleId: 'role-5',
        roleName: 'Developer Engineer',
        isActive: true,
        twoFactorEnabled: false,
        createdAt: new Date().toISOString(),
        lastLoginAt: new Date().toISOString()
      };
      IdentityMockDb.users.push(newUser);
      this.authState.set({ currentUser: newUser, isAuthenticated: true, token: `token-${newUser.id}` });
    }

    IdentityMockDb.otpStorage.delete(email.toLowerCase());
    return of({ success: true }).pipe(delay(500));
  }

  resetPassword(email: string, otpCode: string, newPass: string): Observable<{ success: boolean }> {
    if (otpCode || newPass) { /* no-op reference for lint */ }
    const user = IdentityMockDb.users.find(u => u.email.toLowerCase() === email.toLowerCase());
    if (!user) {
      return throwError(() => ({ error: { title: 'Không tìm thấy tài khoản tương ứng.' } }));
    }
    return of({ success: true }).pipe(delay(600));
  }

  logout(): void {
    this.authState.set({ currentUser: null, isAuthenticated: false, token: null });
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
