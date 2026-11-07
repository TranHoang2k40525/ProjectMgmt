import {
  Component,
  EventEmitter,
  Output,
  computed,
  effect,
  inject,
  model,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { IdentityService } from '../../../core/services/identity.service';
import { ProjectManagementService, WorkItem } from '../../../core/services/project-management.service';
import { ToastService } from '../../../core/services/toast.service';
import { resolveAvatarUrl } from '../../../core/utils/avatar.utils';

export type QuickProfileTab = 'profile' | 'security' | 'tasks';

@Component({
  selector: 'app-quick-profile-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (isOpen()) {
      <div
        class="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-5 bg-slate-950/60 backdrop-blur-xs animate-fade-in"
        (click)="onBackdropClick($event)"
      >
        <div
          class="relative w-full max-w-2xl max-h-[90vh] bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 flex flex-col overflow-hidden animate-scale-up"
          (click)="$event.stopPropagation()"
        >
          <!-- Modal Top Header & User Summary Banner -->
          <div class="relative bg-gradient-to-r from-slate-900 via-slate-800 to-indigo-950 text-white p-5 sm:p-6 pb-4 shrink-0">
            <!-- Close Button -->
            <button
              type="button"
              (click)="close()"
              class="absolute top-4 right-4 text-slate-300 hover:text-white p-1.5 rounded-lg hover:bg-white/10 transition-colors cursor-pointer"
              title="Đóng cửa sổ hồ sơ"
            >
              <span class="material-symbols-outlined text-[20px]" aria-hidden="true">close</span>
            </button>

            <!-- User Info Header -->
            <div class="flex items-center gap-4">
              <!-- Avatar with edit trigger -->
              <div class="relative group cursor-pointer shrink-0" (click)="triggerAvatarModal()" title="Bấm để đổi ảnh đại diện">
                @if (avatarUrl()) {
                  <img
                    [src]="avatarUrl()"
                    [alt]="currentUser()?.displayName"
                    class="w-16 h-16 rounded-full object-cover ring-2 ring-white/50 shadow-md transition-transform group-hover:scale-105"
                  />
                } @else {
                  <div class="w-16 h-16 rounded-full bg-primary text-white text-2xl font-bold flex items-center justify-center ring-2 ring-white/50 shadow-md transition-transform group-hover:scale-105">
                    {{ currentUser()?.displayName?.charAt(0)?.toUpperCase() || 'U' }}
                  </div>
                }
                <div class="absolute inset-0 rounded-full bg-black/40 flex items-center justify-center text-white opacity-0 group-hover:opacity-100 transition-opacity">
                  <span class="material-symbols-outlined text-[18px]">photo_camera</span>
                </div>
              </div>

              <!-- Name & Context Role Badges -->
              <div class="flex flex-col min-w-0">
                <div class="flex items-center gap-2 flex-wrap">
                  <h2 class="text-lg sm:text-xl font-bold text-white truncate">
                    {{ currentUser()?.displayName || 'Người dùng' }}
                  </h2>
                  <span class="px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-400/30 flex items-center gap-1">
                    <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                    Trực tuyến
                  </span>
                </div>

                <p class="text-xs text-slate-300 truncate mt-0.5">
                  {{ currentUser()?.email }}
                </p>

                <!-- Project context role -->
                <div class="flex items-center gap-2 flex-wrap mt-2">
                  @if (hasProject()) {
                    <span class="px-2.5 py-0.5 rounded-md text-xs font-semibold bg-primary/25 text-primary-200 border border-primary/30 flex items-center gap-1">
                      <span class="material-symbols-outlined text-[14px]">stars</span>
                      {{ projectRoleName() }}
                    </span>
                    <span class="text-[11px] text-slate-300 flex items-center gap-1 truncate">
                      <span class="material-symbols-outlined text-[13px]">folder</span>
                      {{ currentProject().name }}
                    </span>
                  } @else {
                    <span class="px-2.5 py-0.5 rounded-md text-xs font-semibold bg-white/10 text-slate-200 flex items-center gap-1">
                      <span class="material-symbols-outlined text-[14px]">person</span>
                      Tài khoản cá nhân
                    </span>
                  }
                </div>
              </div>
            </div>
          </div>

          <!-- Navigation Tabs -->
          <div class="flex items-center gap-1 px-5 pt-3 border-b border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/50 shrink-0">
            <button
              type="button"
              (click)="activeTab.set('profile')"
              [class.border-primary]="activeTab() === 'profile'"
              [class.text-primary]="activeTab() === 'profile'"
              [class.font-bold]="activeTab() === 'profile'"
              [class.border-transparent]="activeTab() !== 'profile'"
              [class.text-slate-600]="activeTab() !== 'profile'"
              class="flex items-center gap-1.5 py-2.5 px-3 border-b-2 text-xs font-medium hover:text-primary transition-colors cursor-pointer"
            >
              <span class="material-symbols-outlined text-[16px]">badge</span>
              <span>Thông tin cá nhân</span>
            </button>

            <button
              type="button"
              (click)="activeTab.set('security')"
              [class.border-primary]="activeTab() === 'security'"
              [class.text-primary]="activeTab() === 'security'"
              [class.font-bold]="activeTab() === 'security'"
              [class.border-transparent]="activeTab() !== 'security'"
              [class.text-slate-600]="activeTab() !== 'security'"
              class="flex items-center gap-1.5 py-2.5 px-3 border-b-2 text-xs font-medium hover:text-primary transition-colors cursor-pointer"
            >
              <span class="material-symbols-outlined text-[16px]">lock_reset</span>
              <span>Đổi mật khẩu</span>
            </button>

            @if (hasProject()) {
              <button
                type="button"
                (click)="activeTab.set('tasks')"
                [class.border-primary]="activeTab() === 'tasks'"
                [class.text-primary]="activeTab() === 'tasks'"
                [class.font-bold]="activeTab() === 'tasks'"
                [class.border-transparent]="activeTab() !== 'tasks'"
                [class.text-slate-600]="activeTab() !== 'tasks'"
                class="flex items-center gap-1.5 py-2.5 px-3 border-b-2 text-xs font-medium hover:text-primary transition-colors cursor-pointer"
              >
                <span class="material-symbols-outlined text-[16px]">assignment</span>
                <span>Công việc của tôi</span>
                <span class="ml-1 px-1.5 py-0.2 rounded-full text-[10px] font-bold bg-slate-200 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
                  {{ myTasks().length }}
                </span>
              </button>
            }
          </div>

          <!-- Body Content Scrollable Area -->
          <div class="flex-1 overflow-y-auto p-5 sm:p-6 space-y-4">
            @if (loading()) {
              <div class="flex items-center justify-center py-10 text-slate-400 gap-2">
                <span class="material-symbols-outlined text-[24px] animate-spin">progress_activity</span>
                <span class="text-xs">Đang tải thông tin hồ sơ...</span>
              </div>
            } @else {
              <!-- TAB 1: Profile Information -->
              @if (activeTab() === 'profile') {
                <div class="space-y-4">
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div class="flex flex-col gap-1.5">
                      <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
                        Họ và Tên <span class="text-red-500">*</span>
                      </label>
                      <input
                        type="text"
                        [ngModel]="displayName()"
                        (ngModelChange)="displayName.set($event)"
                        placeholder="Nhập họ và tên"
                        class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs text-slate-900 dark:text-white focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary"
                      />
                    </div>

                    <div class="flex flex-col gap-1.5">
                      <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
                        Chức danh / Vị trí
                      </label>
                      <input
                        type="text"
                        [ngModel]="jobTitle()"
                        (ngModelChange)="jobTitle.set($event)"
                        placeholder="VD: Senior Agile Developer, QA Lead..."
                        class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs text-slate-900 dark:text-white focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary"
                      />
                    </div>
                  </div>

                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div class="flex flex-col gap-1.5">
                      <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
                        Email tài khoản
                      </label>
                      <input
                        type="text"
                        [value]="email()"
                        disabled
                        class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-100 dark:bg-slate-800/50 text-xs text-slate-500 cursor-not-allowed"
                      />
                    </div>

                    <div class="flex flex-col gap-1.5">
                      <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
                        Số điện thoại
                      </label>
                      <input
                        type="text"
                        [ngModel]="phoneNumber()"
                        (ngModelChange)="phoneNumber.set($event)"
                        placeholder="VD: 0912345678"
                        class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs text-slate-900 dark:text-white focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary"
                      />
                    </div>
                  </div>

                  <div class="flex flex-col gap-1.5">
                    <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
                      Giới thiệu ngắn (Bio)
                    </label>
                    <textarea
                      rows="3"
                      [ngModel]="bio()"
                      (ngModelChange)="bio.set($event)"
                      placeholder="Mô tả kỹ năng chuyên môn, kinh nghiệm hoặc vai trò của bạn..."
                      class="p-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs text-slate-900 dark:text-white focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary resize-none"
                    ></textarea>
                  </div>

                  <div class="flex items-center justify-between pt-2">
                    <button
                      type="button"
                      (click)="triggerAvatarModal()"
                      class="inline-flex items-center gap-1.5 text-xs text-primary hover:underline font-semibold cursor-pointer"
                    >
                      <span class="material-symbols-outlined text-[16px]">photo_camera</span>
                      <span>Đổi ảnh đại diện</span>
                    </button>

                    <button
                      type="button"
                      (click)="saveProfile()"
                      [disabled]="saving() || !displayName().trim()"
                      class="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-bold bg-slate-900 dark:bg-primary text-white hover:opacity-90 disabled:opacity-50 transition-opacity cursor-pointer shadow-xs"
                    >
                      @if (saving()) {
                        <span class="material-symbols-outlined text-[16px] animate-spin">progress_activity</span>
                        <span>Đang lưu...</span>
                      } @else {
                        <span class="material-symbols-outlined text-[16px]">save</span>
                        <span>Lưu thông tin</span>
                      }
                    </button>
                  </div>
                </div>
              }

              <!-- TAB 2: Change Password -->
              @if (activeTab() === 'security') {
                <div class="space-y-4 max-w-md">
                  <div class="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800/40 text-xs text-amber-800 dark:text-amber-200 flex items-start gap-2">
                    <span class="material-symbols-outlined text-[18px] text-amber-600 shrink-0 mt-0.5">info</span>
                    <span>Mật khẩu mới phải có tối thiểu 8 ký tự, bao gồm ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số và 1 ký tự đặc biệt.</span>
                  </div>

                  @if (passwordError()) {
                    <div class="p-2.5 rounded-lg bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-800 text-xs text-rose-700 dark:text-rose-300 flex items-center gap-2">
                      <span class="material-symbols-outlined text-[16px] text-rose-500">error</span>
                      <span>{{ passwordError() }}</span>
                    </div>
                  }

                  <div class="flex flex-col gap-1.5">
                    <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
                      Mật khẩu hiện tại <span class="text-red-500">*</span>
                    </label>
                    <input
                      type="password"
                      [ngModel]="currentPassword()"
                      (ngModelChange)="currentPassword.set($event)"
                      placeholder="Nhập mật khẩu hiện tại"
                      class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs text-slate-900 dark:text-white focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary"
                    />
                  </div>

                  <div class="flex flex-col gap-1.5">
                    <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
                      Mật khẩu mới <span class="text-red-500">*</span>
                    </label>
                    <input
                      type="password"
                      [ngModel]="newPassword()"
                      (ngModelChange)="newPassword.set($event)"
                      placeholder="Nhập mật khẩu mới"
                      class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs text-slate-900 dark:text-white focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary"
                    />
                  </div>

                  <div class="flex flex-col gap-1.5">
                    <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
                      Xác nhận mật khẩu mới <span class="text-red-500">*</span>
                    </label>
                    <input
                      type="password"
                      [ngModel]="confirmPassword()"
                      (ngModelChange)="confirmPassword.set($event)"
                      placeholder="Nhập lại mật khẩu mới"
                      class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs text-slate-900 dark:text-white focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary"
                    />
                  </div>

                  <div class="flex items-center justify-end pt-2">
                    <button
                      type="button"
                      (click)="changePassword()"
                      [disabled]="changingPass() || !currentPassword() || !newPassword() || !confirmPassword()"
                      class="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-bold bg-slate-900 dark:bg-primary text-white hover:opacity-90 disabled:opacity-50 transition-opacity cursor-pointer shadow-xs"
                    >
                      @if (changingPass()) {
                        <span class="material-symbols-outlined text-[16px] animate-spin">progress_activity</span>
                        <span>Đang cập nhật...</span>
                      } @else {
                        <span class="material-symbols-outlined text-[16px]">lock</span>
                        <span>Cập nhật mật khẩu</span>
                      }
                    </button>
                  </div>
                </div>
              }

              <!-- TAB 3: Tasks assigned to this user in current project -->
              @if (activeTab() === 'tasks') {
                <div class="space-y-3">
                  <div class="flex items-center justify-between text-xs text-slate-500">
                    <span>Tổng số công việc được gán: <strong class="text-slate-800 dark:text-white">{{ myTasks().length }}</strong></span>
                    <span>Dự án: <strong class="text-primary">{{ currentProject().name }}</strong></span>
                  </div>

                  @if (myTasks().length === 0) {
                    <div class="p-8 text-center rounded-xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/80 dark:border-slate-800 text-slate-400">
                      <span class="material-symbols-outlined text-[32px] text-slate-300 dark:text-slate-600 block mb-1">task</span>
                      <span class="text-xs">Bạn chưa có công việc nào được phân công trong dự án này.</span>
                    </div>
                  } @else {
                    <div class="space-y-2 max-h-72 overflow-y-auto pr-1">
                      @for (task of myTasks(); track task.id) {
                        <div
                          (click)="openTaskDetails(task)"
                          class="p-3 rounded-xl border border-slate-200/80 dark:border-slate-800 bg-white dark:bg-slate-800/80 hover:border-primary/50 transition-all flex items-center justify-between gap-3 cursor-pointer group"
                        >
                          <div class="flex items-center gap-2.5 min-w-0">
                            <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-primary/10 text-primary border border-primary/20 shrink-0">
                              {{ task.issueKey }}
                            </span>
                            <span class="text-xs font-semibold text-slate-800 dark:text-slate-200 truncate group-hover:text-primary transition-colors">
                              {{ task.title }}
                            </span>
                          </div>

                          <div class="flex items-center gap-2 shrink-0">
                            <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300">
                              {{ task.statusName || 'To Do' }}
                            </span>
                            @if (task.storyPoints) {
                              <span class="text-[11px] font-bold text-slate-400">
                                {{ task.storyPoints }} SP
                              </span>
                            }
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              }
            }
          </div>

          <!-- Modal Footer -->
          <div class="p-4 px-5 border-t border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/80 flex items-center justify-between gap-3 shrink-0">
            <button
              type="button"
              (click)="goToFullProfile()"
              class="inline-flex items-center gap-1.5 text-xs text-slate-500 hover:text-primary transition-colors cursor-pointer"
              title="Đến trang quản lý tài khoản và phiên đăng nhập chuyên sâu"
            >
              <span class="material-symbols-outlined text-[16px]">open_in_new</span>
              <span>Hồ sơ nâng cao (/profile)</span>
            </button>

            <button
              type="button"
              (click)="close()"
              class="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors cursor-pointer"
            >
              Đóng
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class QuickProfileModalComponent {
  readonly isOpen = model<boolean>(false);
  @Output() readonly openAvatarModal = new EventEmitter<void>();

  private readonly identity = inject(IdentityService);
  private readonly projectService = inject(ProjectManagementService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly activeTab = signal<QuickProfileTab>('profile');
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly changingPass = signal<boolean>(false);
  readonly passwordError = signal<string | null>(null);

  readonly displayName = signal<string>('');
  readonly jobTitle = signal<string>('');
  readonly phoneNumber = signal<string>('');
  readonly bio = signal<string>('');
  readonly email = signal<string>('');

  readonly currentPassword = signal<string>('');
  readonly newPassword = signal<string>('');
  readonly confirmPassword = signal<string>('');

  readonly currentUser = computed(() => this.identity.authState().currentUser);
  readonly avatarUrl = computed(() => resolveAvatarUrl(this.currentUser()?.avatarUrl));
  readonly currentProject = computed(() => this.projectService.currentProject());
  readonly hasProject = computed(() => this.projectService.hasActiveProject());

  readonly projectRoleName = computed(() => {
    if (!this.hasProject()) return 'Tài khoản cá nhân';
    const proj = this.currentProject();
    const myId = this.currentUser()?.id;
    if (proj.leadUserId && myId && proj.leadUserId.toLowerCase() === myId.toLowerCase()) {
      return 'Giám đốc dự án';
    }
    const member = (proj.members || []).find(m => m.userId && myId && m.userId.toLowerCase() === myId.toLowerCase());
    if (member) {
      switch (member.role) {
        case 'Project Lead': return 'Quản lý dự án';
        case 'Scrum Master': return 'Scrum Master';
        case 'Developer': return 'Kỹ sư phát triển';
        case 'QA Engineer': return 'QA Engineer';
        case 'Viewer': return 'Người quan sát';
      }
    }
    return 'Thành viên dự án';
  });

  readonly myTasks = computed(() => {
    const user = this.currentUser();
    if (!user) return [];
    return this.projectService.workItems().filter(item =>
      item.assigneeId === user.id ||
      (item.assigneeName && user.displayName && item.assigneeName.trim().toLowerCase() === user.displayName.trim().toLowerCase())
    );
  });

  constructor() {
    effect(() => {
      if (this.isOpen()) {
        this.loadProfile();
      }
    });
  }

  loadProfile(): void {
    const user = this.currentUser();
    if (user) {
      this.displayName.set(user.displayName || '');
      this.email.set(user.email || '');
    }
    this.loading.set(true);
    this.identity.getMyProfile().subscribe({
      next: p => {
        this.loading.set(false);
        this.displayName.set(p.displayName || '');
        this.jobTitle.set(p.jobTitle || '');
        this.phoneNumber.set(p.phoneNumber || '');
        this.bio.set(p.bio || '');
        this.email.set(p.email || '');
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  saveProfile(): void {
    const user = this.currentUser();
    const name = this.displayName().trim();
    if (!name || name.length < 2) {
      this.toast.error('Hồ sơ', 'Họ và tên phải có tối thiểu 2 ký tự.');
      return;
    }
    if (!user?.id) return;

    this.saving.set(true);
    this.identity.updateProfile(user.id, {
      displayName: name,
      jobTitle: this.jobTitle().trim(),
      phoneNumber: this.phoneNumber().trim(),
      bio: this.bio().trim()
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Hồ sơ cá nhân', 'Đã lưu thay đổi hồ sơ thành công.');
      },
      error: () => {
        this.saving.set(false);
        this.toast.error('Hồ sơ', 'Không thể lưu hồ sơ, vui lòng thử lại.');
      }
    });
  }

  changePassword(): void {
    const user = this.currentUser();
    if (!user?.id) return;
    this.passwordError.set(null);

    const cur = this.currentPassword();
    const next = this.newPassword();
    const confirm = this.confirmPassword();

    if (!cur || !next || !confirm) {
      this.passwordError.set('Vui lòng nhập đầy đủ các thông tin mật khẩu.');
      return;
    }
    if (next !== confirm) {
      this.passwordError.set('Mật khẩu mới và mật khẩu xác nhận không khớp nhau.');
      return;
    }
    const strongPasswordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,128}$/;
    if (!strongPasswordPattern.test(next)) {
      this.passwordError.set('Mật khẩu mới phải có tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.');
      return;
    }

    this.changingPass.set(true);
    this.identity.changePassword(user.id, cur, next).subscribe({
      next: () => {
        this.changingPass.set(false);
        this.currentPassword.set('');
        this.newPassword.set('');
        this.confirmPassword.set('');
        this.toast.success('Bảo mật tài khoản', 'Đã đổi mật khẩu thành công.');
      },
      error: err => {
        this.changingPass.set(false);
        const msg = err?.error?.message || err?.message || 'Không thể đổi mật khẩu, vui lòng kiểm tra mật khẩu hiện tại.';
        this.passwordError.set(msg);
      }
    });
  }

  openTaskDetails(task: WorkItem): void {
    this.close();
    this.projectService.activeDrawerTask.set(task);
  }

  triggerAvatarModal(): void {
    this.openAvatarModal.emit();
  }

  goToFullProfile(): void {
    this.close();
    this.router.navigate(['/profile']);
  }

  close(): void {
    this.isOpen.set(false);
    this.passwordError.set(null);
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.close();
    }
  }
}

