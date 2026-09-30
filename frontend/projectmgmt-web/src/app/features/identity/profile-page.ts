import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { IdentityService } from '../../core/services/identity.service';
import { UserProfileModel, SkillModel, UserSkillModel, ActiveSessionModel } from '../../core/mocks/identity-mock-db';

export type ProfileTab = 'GENERAL' | 'SECURITY' | 'SKILLS' | 'SESSIONS';

@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './profile-page.html',
  styleUrls: ['./profile-page.scss']
})
export class ProfilePageComponent implements OnInit {
  private static readonly strongPasswordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,128}$/;
  private identityService = inject(IdentityService);
  private fb = inject(FormBuilder);
  private router = inject(Router);

  readonly activeTab = signal<ProfileTab>('GENERAL');
  readonly loading = signal<boolean>(false);
  readonly successMsg = signal<string | null>(null);
  readonly errorMsg = signal<string | null>(null);

  user = signal<UserProfileModel | null>(null);
  skillsCatalog = signal<SkillModel[]>([]);
  userSkills = signal<UserSkillModel[]>([]);
  activeSessions = signal<ActiveSessionModel[]>([]);

  // Forms
  profileForm!: FormGroup;
  passwordForm!: FormGroup;

  // Selected Skill for adding
  selectedSkillId: string = '';
  selectedSkillLevel: number = 80;

  ngOnInit(): void {
    this.loading.set(true);
    this.identityService.getMyProfile().subscribe({
      next: profile => {
        this.loading.set(false);
        this.user.set(profile);
        this.initForms(profile);
        this.loadData(profile.id);
      },
      error: error => {
        this.loading.set(false);
        this.errorMsg.set(this.readError(error, 'Không thể tải hồ sơ cá nhân.'));
      }
    });
  }

  private initForms(u: UserProfileModel): void {
    this.profileForm = this.fb.group({
      displayName: [u.displayName, [Validators.required, Validators.minLength(2)]],
      jobTitle: [u.jobTitle],
      email: [{ value: u.email, disabled: true }],
      phoneNumber: [u.phoneNumber ?? ''],
      timezone: [u.timezone ?? 'Asia/Ho_Chi_Minh', [Validators.required]],
      seniorityLevel: [u.seniorityLevel ?? ''],
      yearsOfExperience: [u.yearsOfExperience ?? null, [Validators.min(0), Validators.max(80)]],
      bio: [u.bio ?? '', [Validators.maxLength(5000)]]
    });

    this.passwordForm = this.fb.group({
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.pattern(ProfilePageComponent.strongPasswordPattern)]],
      confirmPassword: ['', [Validators.required]]
    });
  }

  private loadData(userId: string): void {
    this.identityService.getSkillCatalog().subscribe(res => this.skillsCatalog.set(res));
    this.identityService.getUserSkills(userId).subscribe(res => this.userSkills.set(res));
    this.identityService.getActiveSessions().subscribe(res => this.activeSessions.set(res));
  }

  setTab(tab: ProfileTab): void {
    this.activeTab.set(tab);
    this.successMsg.set(null);
    this.errorMsg.set(null);
  }

  onUpdateProfile(): void {
    if (this.profileForm.invalid || !this.user()) return;
    this.loading.set(true);
    this.errorMsg.set(null);

    const data = this.profileForm.value;
    this.identityService.updateProfile(this.user()!.id, data).subscribe({
      next: (updated) => {
        this.loading.set(false);
        this.user.set(updated);
        this.successMsg.set('Cập nhật thông tin cá nhân thành công!');
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMsg.set(this.readError(err, 'Cập nhật thất bại.'));
      }
    });
  }

  onChangePassword(): void {
    if (this.passwordForm.invalid || !this.user()) return;

    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.value;
    if (newPassword !== confirmPassword) {
      this.errorMsg.set('Mật khẩu mới và xác nhận mật khẩu không trùng khớp.');
      return;
    }

    this.loading.set(true);
    this.errorMsg.set(null);

    this.identityService.changePassword(this.user()!.id, currentPassword, newPassword).subscribe({
      next: () => {
        this.loading.set(false);
        this.passwordForm.reset();
        if (typeof sessionStorage !== 'undefined') {
          sessionStorage.setItem(
            'projectmgmt.auth.notice',
            'Đổi mật khẩu thành công. Tất cả phiên cũ đã bị thu hồi, vui lòng đăng nhập lại.'
          );
        }
        void this.router.navigate(['/auth']);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMsg.set(this.readError(err, 'Đổi mật khẩu không thành công.'));
      }
    });
  }

  onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.loading.set(true);
    this.errorMsg.set(null);
    this.identityService.uploadAvatar(file).subscribe({
      next: avatarUrl => {
        this.loading.set(false);
        this.user.update(user => user ? { ...user, avatarUrl } : user);
        this.successMsg.set('Cập nhật ảnh đại diện thành công.');
        input.value = '';
      },
      error: error => {
        this.loading.set(false);
        this.errorMsg.set(this.readError(error, 'Không thể cập nhật ảnh đại diện.'));
        input.value = '';
      }
    });
  }

  onAddSkill(): void {
    if (!this.selectedSkillId || !this.user()) return;

    this.identityService.addUserSkill(this.user()!.id, this.selectedSkillId, this.selectedSkillLevel).subscribe({
      next: () => {
        this.loadData(this.user()!.id);
        this.successMsg.set('Đã cập nhật kỹ năng cá nhân!');
        this.selectedSkillId = '';
      }
    });
  }

  onRemoveSkill(skillId: string): void {
    this.identityService.removeUserSkill(skillId).subscribe({
      next: () => {
        if (this.user()) this.loadData(this.user()!.id);
      }
    });
  }

  onRevokeSession(sessionId: string): void {
    this.identityService.revokeSession(sessionId).subscribe({
      next: () => {
        this.activeSessions.update(list => list.filter(s => s.id !== sessionId));
        this.successMsg.set('Đã đăng xuất phiên làm việc thành công.');
      }
    });
  }

  getProficiencyBadge(level: number): { label: string; colorClass: string } {
    if (level < 40) return { label: 'Cơ bản', colorClass: 'badge-gray' };
    if (level < 70) return { label: 'Khá', colorClass: 'badge-blue' };
    if (level < 90) return { label: 'Thành thạo', colorClass: 'badge-indigo' };
    return { label: 'Chuyên gia', colorClass: 'badge-purple' };
  }

  private readError(error: unknown, fallback: string): string {
    if (typeof error !== 'object' || error === null) return fallback;
    const value = error as { title?: unknown; detail?: unknown; message?: unknown; error?: unknown };
    if (typeof value.title === 'string') return value.title;
    if (typeof value.detail === 'string') return value.detail;
    if (typeof value.error === 'object' && value.error !== null) {
      const nested = value.error as { message?: unknown; title?: unknown };
      if (typeof nested.message === 'string') return nested.message;
      if (typeof nested.title === 'string') return nested.title;
    }
    return typeof value.message === 'string' ? value.message : fallback;
  }
}
