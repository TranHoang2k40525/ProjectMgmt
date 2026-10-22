import { Component, ElementRef, OnDestroy, OnInit, inject, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { gsap } from 'gsap';
import { RbacApi } from '../../core/api/rbac.api';
import { ProjectManagementService, ProjectMember, UserDisplayInfo } from '../../core/services/project-management.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { NoProjectStateComponent } from '../../shared/components/no-project-state/no-project-state.component';

interface ProjectRoleOption {
  id: string;
  name: string;
  code: string;
  description?: string;
}

@Component({
  selector: 'app-project-members-page',
  standalone: true,
  imports: [CommonModule, FormsModule, NoProjectStateComponent],
  template: `
    @if (!projectService.hasActiveProject()) {
      <app-no-project-state featureName="Thành viên & Phân quyền Nhóm"></app-no-project-state>
    } @else {
    <div class="project-members-page flex flex-col gap-5 max-w-7xl mx-auto px-4 sm:px-6 py-6">
      
      @if (error) { <p role="alert" class="p-3 rounded bg-red-50 text-red-700 text-xs font-medium">{{ error }}</p> }

      <!-- Top Action Bar -->
      <div class="members-header flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-slate-200 dark:border-slate-800">
        <div class="flex flex-col gap-1">
          <div class="flex items-center gap-2.5">
            <span class="inline-flex items-center justify-center w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800">
              <span class="material-symbols-outlined text-[20px]">group</span>
            </span>
            <h1 class="text-xl font-bold tracking-tight text-slate-900 dark:text-white">
              Thành viên & Phân quyền Dự án
            </h1>
            @if (currentProject(); as proj) {
              <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-200 dark:border-slate-700">
                {{ proj.projectKey }}
              </span>
            }
          </div>
          <p class="text-xs text-slate-500 dark:text-slate-400">
            Quản lý đội ngũ phát triển Scrum, phân vai trò trách nhiệm và quyền hạn thực thi trong dự án
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="openInviteModal()"
            [disabled]="loading() || !currentProject().id"
            class="px-4 py-2 rounded-lg bg-primary hover:bg-primary-container text-white font-semibold text-xs shadow-sm transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
          >
            <span class="material-symbols-outlined text-[18px]">person_add</span>
            Thêm thành viên
          </button>
        </div>
      </div>

      <!-- Filters & Stats Bar -->
      <div class="members-toolbar flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 bg-white dark:bg-slate-900 p-3 rounded-xl border border-slate-200/80 dark:border-slate-800 shadow-xs">
        <div class="flex items-center gap-3 flex-1">
          <div class="relative flex items-center max-w-xs w-full">
            <span class="material-symbols-outlined absolute left-3 text-[18px] text-slate-400 pointer-events-none">search</span>
            <input
              type="text"
              [ngModel]="searchQuery()"
              (ngModelChange)="searchQuery.set($event)"
              placeholder="Tìm theo tên, email..."
              class="w-full h-9 pl-9 pr-3 rounded-lg bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
            />
          </div>

          <select
            [ngModel]="selectedRoleFilter()"
            (ngModelChange)="selectedRoleFilter.set($event)"
            class="h-9 px-3 rounded-lg bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-xs font-medium text-slate-700 dark:text-slate-300 focus:outline-none cursor-pointer"
          >
            <option [ngValue]="null">Tất cả vai trò</option>
            <option value="Project Lead">Project Lead</option>
            <option value="Scrum Master">Scrum Master</option>
            <option value="Developer">Developer</option>
            <option value="QA Engineer">QA Engineer</option>
            <option value="Viewer">Viewer</option>
          </select>
        </div>

        <div class="text-xs text-slate-500 font-medium">
          Hiển thị <strong>{{ filteredMembers().length }}</strong> / {{ members().length }} thành viên
        </div>
      </div>

      <!-- Members Grid -->
      @if (loading()) {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (i of [1,2,3,4,5,6]; track i) {
            <div class="p-5 rounded-xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 animate-pulse flex flex-col gap-3">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-full bg-slate-200 dark:bg-slate-800"></div>
                <div class="flex flex-col gap-1.5 flex-1">
                  <div class="h-3.5 bg-slate-200 dark:bg-slate-800 rounded-md w-2/3"></div>
                  <div class="h-2.5 bg-slate-100 dark:bg-slate-800/60 rounded-md w-1/2"></div>
                </div>
              </div>
              <div class="h-4 bg-slate-100 dark:bg-slate-800/40 rounded-md w-1/3 mt-2"></div>
            </div>
          }
        </div>
      } @else {
        <div class="members-grid grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          @for (member of filteredMembers(); track member.id) {
            <div class="member-card p-5 rounded-xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs hover:shadow-md transition-all flex flex-col justify-between gap-4">
              <div class="flex items-start justify-between gap-3">
                <div class="flex items-center gap-3 min-w-0">
                  @if (member.avatarUrl) {
                    <img [src]="member.avatarUrl" [alt]="member.displayName" class="w-10 h-10 rounded-full object-cover border border-slate-200 dark:border-slate-700 shrink-0" />
                  } @else {
                    <div class="w-10 h-10 rounded-full bg-emerald-800 text-white flex items-center justify-center font-bold text-sm shrink-0 shadow-xs">
                      {{ getInitials(member.displayName) }}
                    </div>
                  }
                  <div class="flex flex-col min-w-0">
                    <span class="text-sm font-bold text-slate-900 dark:text-white truncate">{{ member.displayName }}</span>
                    <span class="text-xs text-slate-500 dark:text-slate-400 truncate">{{ member.email }}</span>
                  </div>
                </div>

                <!-- Quick Action Menu -->
                <button
                  (click)="confirmRemoveMember(member)"
                  class="text-slate-400 hover:text-rose-600 transition-colors p-1 rounded-md hover:bg-rose-50 dark:hover:bg-rose-950/40 cursor-pointer"
                  title="Xóa khỏi dự án"
                >
                  <span class="material-symbols-outlined text-[18px]">person_remove</span>
                </button>
              </div>

              <div class="flex items-center justify-between pt-3 border-t border-slate-100 dark:border-slate-800 gap-2">
                <!-- Role Badge with inline change trigger -->
                <div class="relative inline-block">
                  <select
                    [ngModel]="getRoleOptionId(member.role)"
                    (ngModelChange)="onRoleChange(member, $event)"
                    class="appearance-none pl-2.5 pr-6 py-1 rounded-full text-xs font-semibold cursor-pointer border transition-all focus:outline-none"
                    [ngClass]="getRoleBadgeClass(member.role)"
                  >
                    @for (role of availableRoles(); track role.id) {
                      <option [value]="role.id">{{ role.name }}</option>
                    }
                  </select>
                  <span class="material-symbols-outlined absolute right-1.5 top-1/2 -translate-y-1/2 text-[14px] pointer-events-none text-current opacity-70">
                    expand_more
                  </span>
                </div>

                <span class="text-[11px] text-slate-400">Tham gia: {{ member.joinedAt }}</span>
              </div>
            </div>
          } @empty {
            <div class="col-span-full py-16 flex flex-col items-center justify-center text-center text-slate-500 bg-white dark:bg-slate-900 rounded-2xl border border-dashed border-slate-300 dark:border-slate-800 p-8">
              <span class="material-symbols-outlined text-5xl text-slate-400 mb-3">group_off</span>
              <h3 class="font-bold text-base text-slate-800 dark:text-slate-200">Chưa có thành viên nào phù hợp</h3>
              <p class="text-xs text-slate-400 max-w-sm mt-1">
                @if (searchQuery() || selectedRoleFilter()) {
                  Không tìm thấy thành viên theo tiêu chí lọc hiện tại. Thử xóa bộ lọc tìm kiếm.
                } @else {
                  Dự án này chưa có thành viên. Bấm nút "Thêm thành viên" ở góc trên để mời nhân sự vào dự án.
                }
              </p>
              @if (!searchQuery() && !selectedRoleFilter()) {
                <button
                  (click)="openInviteModal()"
                  class="mt-4 px-4 py-2 rounded-lg bg-primary hover:bg-primary-container text-white text-xs font-bold transition-all"
                >
                  Thêm thành viên ngay
                </button>
              }
            </div>
          }
        </div>
      }

      <!-- Invite Member Modal -->
      @if (isInviteModalOpen) {
        <div class="members-modal-backdrop fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs">
          <div #modalContent class="members-modal w-full max-w-md bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col p-6 gap-4">
            
            <div class="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <div class="flex items-center gap-2">
                <span class="w-7 h-7 rounded-lg bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300 flex items-center justify-center">
                  <span class="material-symbols-outlined text-[18px]">person_add</span>
                </span>
                <h3 class="text-base font-bold text-slate-900 dark:text-white">Thêm Thành Viên Dự Án</h3>
              </div>
              <button (click)="isInviteModalOpen = false" class="text-slate-400 hover:text-slate-600 rounded-lg p-1">
                <span class="material-symbols-outlined text-[18px]">close</span>
              </button>
            </div>

            <div class="flex flex-col gap-3">
              <div class="flex flex-col gap-1.5">
                <label for="member-email" class="text-xs font-bold text-slate-700 dark:text-slate-300">
                  Email thành viên <span class="text-rose-500">*</span>
                </label>
                <input
                  id="member-email"
                  type="email"
                  [(ngModel)]="newMemberEmail"
                  placeholder="nguyenvana@huce.edu.vn"
                  class="h-10 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                />
              </div>

              <div class="flex flex-col gap-1.5">
                <label for="member-name" class="text-xs font-bold text-slate-700 dark:text-slate-300">
                  Họ và tên
                </label>
                <input
                  id="member-name"
                  type="text"
                  [(ngModel)]="newMemberName"
                  placeholder="Nguyễn Văn A"
                  class="h-10 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                />
              </div>

              <div class="flex flex-col gap-1.5">
                <label for="member-role" class="text-xs font-bold text-slate-700 dark:text-slate-300">
                  Vai trò trong dự án
                </label>
                <select
                  id="member-role"
                  [(ngModel)]="newMemberRole"
                  class="h-10 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                >
                  <option value="Developer">Developer (Phát triển)</option>
                  <option value="Scrum Master">Scrum Master (Quản trị Sprint)</option>
                  <option value="Product Owner">Product Owner</option>
                  <option value="Project Lead">Project Lead (Trưởng dự án)</option>
                  <option value="QA Engineer">QA Engineer</option>
                  <option value="Viewer">Viewer (Xem báo cáo)</option>
                </select>
              </div>

              @if (availableUsersToAdd().length > 0) {
                <div class="pt-2 border-t border-slate-100 dark:border-slate-800 flex flex-col gap-1.5">
                  <label for="member-user-select" class="text-xs font-medium text-slate-500">
                    Hoặc chọn nhanh tài khoản hệ thống đã có:
                  </label>
                  <select
                    id="member-user-select"
                    [(ngModel)]="selectedUserId"
                    (ngModelChange)="onUserSelected($event)"
                    class="h-9 px-2 text-xs rounded border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-800 dark:text-slate-200"
                  >
                    <option value="">-- Chọn tài khoản có sẵn --</option>
                    @for (u of availableUsersToAdd(); track u.userId) {
                      <option [value]="u.userId">{{ u.displayName }} ({{ u.email }})</option>
                    }
                  </select>
                </div>
              }
            </div>

            <div class="flex items-center justify-end gap-3 mt-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <button
                (click)="isInviteModalOpen = false"
                class="px-4 py-2 rounded-lg text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
              >
                Hủy
              </button>
              <button
                (click)="submitInvite()"
                [disabled]="saving || submitting() || (!newMemberEmail.trim() && !selectedUserId)"
                class="px-5 py-2 rounded-lg bg-primary hover:bg-primary-container text-white text-xs font-bold transition-all disabled:opacity-50 flex items-center gap-1.5 cursor-pointer"
              >
                @if (saving || submitting()) {
                  <span class="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
                  Đang thêm...
                } @else {
                  Thêm thành viên
                }
              </button>
            </div>

          </div>
        </div>
      }

    </div>
    }
  `,
  styles: [`
    :host {
      display: block;
    }
  `]
})
export class ProjectMembersPageComponent implements OnInit, OnDestroy {
  readonly projectService = inject(ProjectManagementService);
  private readonly rbac = inject(RbacApi, { optional: true });
  private readonly toastService = inject(ToastService, { optional: true });
  private readonly confirmService = inject(ConfirmDialogService, { optional: true });
  private readonly el = inject(ElementRef, { optional: true });

  error = '';
  saving = false;
  readonly currentProject = this.projectService.currentProject;
  readonly members = signal<ProjectMember[]>([]);
  readonly allUsers = signal<UserDisplayInfo[]>([]);
  readonly availableRoles = signal<ProjectRoleOption[]>([]);

  loading = signal<boolean>(true);
  submitting = signal<boolean>(false);
  searchQuery = signal<string>('');
  selectedRoleFilter = signal<string | null>(null);

  isInviteModalOpen = false;
  newMemberName = '';
  newMemberEmail = '';
  newMemberRole: ProjectMember['role'] = 'Developer';

  selectedUserId = '';
  selectedRoleId = '';

  readonly filteredMembers = computed(() => {
    let list = this.members();
    const query = this.searchQuery().trim().toLowerCase();
    const roleFilter = this.selectedRoleFilter();

    if (query) {
      list = list.filter(m =>
        m.displayName.toLowerCase().includes(query) ||
        m.email.toLowerCase().includes(query)
      );
    }

    if (roleFilter) {
      list = list.filter(m => m.role === roleFilter);
    }

    return list;
  });

  readonly availableUsersToAdd = computed(() => {
    const existingUserIds = new Set(this.members().map(m => m.userId));
    return this.allUsers().filter(u => !existingUserIds.has(u.userId));
  });

  constructor() {
    effect(() => {
      const proj = this.currentProject?.();
      if (proj && proj.id) {
        this.loadProjectMembers(proj.id);
      }
    });
  }

  ngOnInit(): void {
    const proj = this.currentProject?.();
    if (proj && proj.id) {
      this.loadProjectMembers(proj.id);
    }
    this.loadUsersAndRoles();
  }

  ngOnDestroy(): void {
    if (this.el?.nativeElement) {
      gsap.killTweensOf(this.el.nativeElement.querySelectorAll('.member-card, .members-modal'));
    }
  }

  loadProjectMembers(projectId: string): void {
    if (!this.projectService?.loadProjectMembers) return;
    this.loading.set(true);
    this.projectService.loadProjectMembers(projectId).subscribe({
      next: members => {
        this.members.set(members);
        this.loading.set(false);
        this.animateCards();
      },
      error: () => {
        // Fallback to cached project members if any
        const fallback = this.currentProject()?.members ?? [];
        this.members.set(fallback);
        this.loading.set(false);
      }
    });
  }

  loadUsersAndRoles(): void {
    if (this.projectService?.getUsers) {
      this.projectService.getUsers().subscribe({
        next: users => this.allUsers.set(users),
        error: () => undefined
      });
    }

    if (this.projectService?.getRoles) {
      this.projectService.getRoles('Project').subscribe({
        next: roles => {
          if (roles && roles.length > 0) {
            const mapped = roles.map(r => ({
              id: r.id,
              name: this.normalizeRoleLabel(r.name),
              code: r.code,
              description: r.description
            }));
            this.availableRoles.set(mapped);
            if (!this.selectedRoleId && mapped.length > 0) {
              this.selectedRoleId = mapped[0].id;
            }
          }
        },
        error: () => undefined
      });
    }
  }

  openInviteModal(): void {
    this.selectedUserId = '';
    this.newMemberName = '';
    this.newMemberEmail = '';
    this.error = '';
    this.selectedRoleId = this.availableRoles()[0]?.id || '';
    this.isInviteModalOpen = true;

    requestAnimationFrame(() => {
      const modal = this.el?.nativeElement?.querySelector('.members-modal');
      if (modal && typeof window !== 'undefined' && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        gsap.fromTo(modal,
          { autoAlpha: 0, scale: 0.94, y: 12 },
          { autoAlpha: 1, scale: 1, y: 0, duration: 0.3, ease: 'back.out(1.4)' }
        );
      }
    });
  }

  onUserSelected(userId: string): void {
    this.selectedUserId = userId;
    const user = this.allUsers().find(u => u.userId === userId);
    if (user) {
      this.newMemberEmail = user.email || '';
      this.newMemberName = user.displayName || '';
    }
  }

  submitInvite(): void {
    if (this.saving || this.submitting()) return;

    if (this.newMemberEmail.trim()) {
      this.saving = true;
      this.error = '';
      this.projectService.addProjectMember({
        displayName: this.newMemberName || this.newMemberEmail,
        email: this.newMemberEmail,
        role: this.newMemberRole
      }).subscribe({
        next: () => {
          this.saving = false;
          this.isInviteModalOpen = false;
          this.newMemberName = '';
          this.newMemberEmail = '';
          const projId = this.currentProject()?.id;
          if (projId) this.loadProjectMembers(projId);
        },
        error: (e: { title?: string; detail?: string; error?: { message?: string } }) => {
          this.saving = false;
          const msg = e.detail ?? e.title ?? (e as { error?: { message?: string } })?.error?.message ?? 'Không thể thêm thành viên.';
          this.error = msg;
          this.toastService?.error('Lỗi', msg);
        }
      });
      return;
    }

    const projId = this.currentProject()?.id;
    if (!projId || !this.selectedUserId || !this.selectedRoleId) return;

    this.submitting.set(true);
    this.error = '';
    this.projectService.inviteProjectMember(projId, this.selectedUserId, this.selectedRoleId).subscribe({
      next: () => {
        this.submitting.set(false);
        this.isInviteModalOpen = false;
        this.toastService?.success('Thành công', 'Đã thêm thành viên vào dự án');
        this.loadProjectMembers(projId);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        const errObj = err as { title?: string; detail?: string; error?: { message?: string } };
        const msg = errObj?.detail ?? errObj?.title ?? errObj?.error?.message ?? 'Không thể thêm thành viên. Vui lòng thử lại.';
        this.error = msg;
        this.toastService?.error('Lỗi', msg);
      }
    });
  }

  removeMember(userId: string): void {
    const projId = this.currentProject()?.id;
    if (this.rbac && projId) {
      this.rbac.removeProjectMember(projId, userId).subscribe({
        next: () => this.projectService.loadWorkspace(projId),
        error: (e: { title?: string }) => this.error = e.title ?? 'Không thể gỡ thành viên.'
      });
    } else if (projId) {
      this.projectService.removeProjectMember(projId, userId).subscribe({
        next: () => this.loadProjectMembers(projId),
        error: () => this.error = 'Không thể gỡ thành viên.'
      });
    }
  }

  onRoleChange(member: ProjectMember, newRoleId: string): void {
    const projId = this.currentProject()?.id;
    if (!projId || !member.userId || !newRoleId) return;

    this.projectService.changeProjectMemberRole(projId, member.userId, newRoleId).subscribe({
      next: () => {
        this.toastService?.success('Cập nhật vai trò', `Đã đổi vai trò cho ${member.displayName}`);
        this.loadProjectMembers(projId);
      },
      error: (err: unknown) => {
        const errObj = err as { error?: { message?: string } };
        const msg = errObj?.error?.message || 'Không thể thay đổi vai trò.';
        this.toastService?.error('Lỗi', msg);
        this.loadProjectMembers(projId); // revert
      }
    });
  }

  confirmRemoveMember(member: ProjectMember): void {
    const projId = this.currentProject()?.id;
    if (!projId || !member.userId) return;

    if (this.confirmService) {
      this.confirmService.confirm({
        title: 'Xác nhận xóa thành viên',
        message: `Bạn có chắc muốn xóa "${member.displayName}" (${member.email}) khỏi dự án này không?`,
        confirmText: 'Xóa thành viên',
        cancelText: 'Hủy bỏ',
        type: 'danger',
        onConfirm: () => {
          this.removeMember(member.userId);
        }
      });
    } else {
      this.removeMember(member.userId);
    }
  }

  getRoleOptionId(roleName: ProjectMember['role']): string {
    const match = this.availableRoles().find(r => r.name.toLowerCase() === roleName.toLowerCase() || r.code.toLowerCase().includes(roleName.toLowerCase().replace(/\s+/g, '')));
    return match ? match.id : this.availableRoles()[0]?.id || '';
  }

  getRoleBadgeClass(role: ProjectMember['role']): string {
    switch (role) {
      case 'Project Lead':
        return 'bg-amber-50 text-amber-800 border-amber-200 dark:bg-amber-950/60 dark:text-amber-300 dark:border-amber-800';
      case 'Scrum Master':
        return 'bg-purple-50 text-purple-800 border-purple-200 dark:bg-purple-950/60 dark:text-purple-300 dark:border-purple-800';
      case 'QA Engineer':
        return 'bg-cyan-50 text-cyan-800 border-cyan-200 dark:bg-cyan-950/60 dark:text-cyan-300 dark:border-cyan-800';
      case 'Viewer':
        return 'bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800 dark:text-slate-300 dark:border-slate-700';
      case 'Developer':
      default:
        return 'bg-blue-50 text-blue-800 border-blue-200 dark:bg-blue-950/60 dark:text-blue-300 dark:border-blue-800';
    }
  }

  getInitials(name?: string): string {
    if (!name) return 'U';
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  private normalizeRoleLabel(name: string): string {
    if (/manager/i.test(name)) return 'Project Lead';
    if (/scrum/i.test(name)) return 'Scrum Master';
    if (/owner/i.test(name)) return 'Product Owner';
    if (/qa|test/i.test(name)) return 'QA Engineer';
    if (/view/i.test(name)) return 'Viewer';
    return 'Developer';
  }

  private animateCards(): void {
    if (typeof window === 'undefined' || window.matchMedia('(prefers-reduced-motion: reduce)').matches || !this.el?.nativeElement) return;

    requestAnimationFrame(() => {
      const cards = this.el?.nativeElement?.querySelectorAll('.member-card');
      if (cards && cards.length) {
        gsap.fromTo(
          cards,
          { autoAlpha: 0, y: 16 },
          {
            autoAlpha: 1,
            y: 0,
            duration: 0.42,
            stagger: 0.04,
            ease: 'power2.out',
            clearProps: 'transform,opacity,visibility'
          }
        );
      }
    });
  }
}
