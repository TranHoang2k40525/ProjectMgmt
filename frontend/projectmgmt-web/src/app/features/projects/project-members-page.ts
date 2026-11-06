import { Component, ElementRef, OnDestroy, OnInit, inject, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { gsap } from 'gsap';
import { environment } from '../../../environments/environment';
import { RbacApi } from '../../core/api/rbac.api';
import { ProjectManagementService, ProjectMember, UserDisplayInfo } from '../../core/services/project-management.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { IdentityService } from '../../core/services/identity.service';
import { NoProjectStateComponent } from '../../shared/components/no-project-state/no-project-state.component';
import { AvatarModalComponent } from '../../shared/components/avatar-modal/avatar-modal.component';
import { resolveAvatarUrl } from '../../core/utils/avatar.utils';

interface ProjectRoleOption {
  id: string;
  name: string;
  code: string;
  description?: string;
  isPreset?: boolean;
}

interface CandidateOrgMember {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
}

@Component({
  selector: 'app-project-members-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, NoProjectStateComponent, AvatarModalComponent],
  template: `
    @if (!projectService.hasActiveProject()) {
      <app-no-project-state featureName="Thành viên & Phân quyền Nhóm"></app-no-project-state>
    } @else {
    <div class="project-members-page settings-container">
      
      <!-- Breadcrumb Navigation -->
      <div class="breadcrumb">
        <a routerLink="/projects">Dự án</a> &gt;
        @if (currentProject(); as proj) {
          <a routerLink="/project/summary" class="hover:text-primary hover:underline">{{ proj.name }} ({{ proj.projectKey }})</a> &gt;
        }
        <span>Cài đặt</span>
      </div>

      <div class="settings-header">
        <div>
          <h1>Cài đặt Dự án</h1>
          <p class="subtitle">Quản lý cấu hình thông tin chung, loại công việc động, nhãn dán, quy trình (Workflow) và bảng (Board)</p>
        </div>
      </div>

      <!-- Settings Tabs Navigation -->
      @if (currentProject(); as proj) {
        <div class="settings-tabs">
          <a [routerLink]="['/projects', proj.id, 'settings']" class="tab-item">
            <span class="material-symbols-outlined text-[18px]">tune</span>
            <span>Thông tin chung</span>
          </a>
          <a [routerLink]="['/projects', proj.id, 'settings']" [queryParams]="{ tab: 'issue-types' }" class="tab-item">
            <span class="material-symbols-outlined text-[18px]">category</span>
            <span>Loại công việc (Issue Types)</span>
          </a>
          <a [routerLink]="['/projects', proj.id, 'settings']" [queryParams]="{ tab: 'labels' }" class="tab-item">
            <span class="material-symbols-outlined text-[18px]">label</span>
            <span>Quản lý Nhãn (Labels)</span>
          </a>
          <a [routerLink]="['/projects', proj.id, 'settings', 'workflow']" class="tab-item">
            <span class="material-symbols-outlined text-[18px]">sync_alt</span>
            <span>Cấu hình Workflow</span>
          </a>
          <a [routerLink]="['/projects', proj.id, 'settings', 'board']" class="tab-item">
            <span class="material-symbols-outlined text-[18px]">view_kanban</span>
            <span>Cấu hình Board</span>
          </a>
          <span class="tab-item active">
            <span class="material-symbols-outlined text-[18px]">badge</span>
            <span>Thành viên &amp; Phân quyền</span>
          </span>
        </div>
      }

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

        @if (canManageMembers()) {
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
        }
      </div>

      @if (!canManageMembers()) {
        <div class="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/40 text-amber-800 dark:text-amber-300 border border-amber-200 dark:border-amber-900 text-xs font-medium flex items-center gap-2">
          <span class="material-symbols-outlined text-[18px]">lock</span>
          <span>Chế độ chỉ đọc: Chỉ Lãnh đạo hoặc Scrum Master mới có quyền phân vai trò dự án.</span>
        </div>
      }

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
                  <div
                    (click)="openAvatarModal(member)"
                    class="relative group cursor-pointer shrink-0"
                    [title]="isCurrentUser(member) ? 'Xem hoặc đổi ảnh đại diện của bạn' : 'Xem ảnh đại diện'"
                  >
                    @if (member.avatarUrl) {
                      <img [src]="getAvatarUrl(member.avatarUrl)" [alt]="member.displayName" class="w-10 h-10 rounded-full object-cover border border-slate-200 dark:border-slate-700 shadow-xs" />
                    } @else {
                      <div class="w-10 h-10 rounded-full bg-emerald-800 text-white flex items-center justify-center font-bold text-sm shadow-xs">
                        {{ getInitials(member.displayName) }}
                      </div>
                    }
                    <div class="absolute inset-0 bg-slate-900/40 rounded-full opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center text-white">
                      <span class="material-symbols-outlined text-[16px]">{{ isCurrentUser(member) ? 'photo_camera' : 'zoom_in' }}</span>
                    </div>
                  </div>
                  <div class="flex flex-col min-w-0">
                    <span class="text-sm font-bold text-slate-900 dark:text-white truncate">{{ member.displayName }}</span>
                    <span class="text-xs text-slate-500 dark:text-slate-400 truncate">{{ member.email }}</span>
                  </div>
                </div>

                <!-- Quick Action Menu / Lead Indicator -->
                @if (isLead(member)) {
                  <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-800 flex items-center gap-1 shadow-2xs" title="Giám đốc dự án / Người tạo (Cố định)">
                    <span class="material-symbols-outlined text-[14px] text-amber-500" aria-hidden="true">verified</span>
                    <span>Giám đốc</span>
                  </span>
                } @else if (canManageMembers()) {
                  <button
                    (click)="confirmRemoveMember(member)"
                    class="text-slate-400 hover:text-rose-600 transition-colors p-1 rounded-md hover:bg-rose-50 dark:hover:bg-rose-950/40 cursor-pointer"
                    title="Xóa khỏi dự án"
                  >
                    <span class="material-symbols-outlined text-[18px]">person_remove</span>
                  </button>
                }
              </div>

              <div class="flex items-center justify-between pt-3 border-t border-slate-100 dark:border-slate-800 gap-2">
                <!-- Role Badge with inline change trigger or read-only display -->
                <div class="relative inline-block">
                  @if (canManageMembers() && !isLead(member)) {
                    <select
                      [ngModel]="getRoleOptionId(member.role)"
                      (ngModelChange)="onRoleChange(member, $event)"
                      class="appearance-none pl-2.5 pr-6 py-1 rounded-full text-xs font-semibold cursor-pointer border transition-all focus:outline-none"
                      [ngClass]="getRoleBadgeClass(member.role)"
                      title="Nhấp để thay đổi vai trò"
                    >
                      @for (role of availableRoles(); track role.id) {
                        <option [value]="role.id">{{ role.name }}</option>
                      }
                    </select>
                    <span class="material-symbols-outlined absolute right-1.5 top-1/2 -translate-y-1/2 text-[14px] pointer-events-none text-current opacity-70">
                      expand_more
                    </span>
                  } @else {
                    <span
                      class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold border"
                      [ngClass]="getRoleBadgeClass(member.role)"
                    >
                      @if (isLead(member)) {
                        <span class="material-symbols-outlined text-[14px] text-amber-500" aria-hidden="true">stars</span>
                      }
                      <span>{{ member.role }}</span>
                    </span>
                  }
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

      <!-- Invite Member Modal (Restricted to Project's Organization Members) -->
      @if (isInviteModalOpen) {
        <div class="members-modal-backdrop fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs">
          <div #modalContent class="members-modal w-full max-w-lg bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col p-6 gap-4 max-h-[90vh] overflow-y-auto">
            
            <div class="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <div class="flex items-center gap-2.5">
                <span class="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300 flex items-center justify-center">
                  <span class="material-symbols-outlined text-[20px]">person_add</span>
                </span>
                <div>
                  <h3 class="text-base font-bold text-slate-900 dark:text-white">Thêm Thành Viên Vào Dự Án</h3>
                  <p class="text-[11px] text-slate-500">Chỉ thành viên thuộc Tổ chức của dự án mới có thể được thêm vào.</p>
                </div>
              </div>
              <button (click)="isInviteModalOpen = false" class="text-slate-400 hover:text-slate-600 rounded-lg p-1 cursor-pointer">
                <span class="material-symbols-outlined text-[18px]">close</span>
              </button>
            </div>

            <div class="flex flex-col gap-4">
              <!-- Candidate Members Picker from Organization -->
              @if (orgCandidateMembers().length > 0) {
                <div class="flex flex-col gap-1.5">
                  <label for="org-member-picker" class="text-xs font-bold text-slate-700 dark:text-slate-300">
                    Chọn thành viên từ Tổ chức
                  </label>
                  <select
                    id="org-member-picker"
                    [(ngModel)]="selectedCandidateUserId"
                    class="h-10 px-3 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-800 dark:text-slate-200 cursor-pointer font-medium focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                  >
                    <option value="">-- Hoặc nhập email bên dưới --</option>
                    @for (cand of orgCandidateMembers(); track cand.id) {
                      <option [value]="cand.id">{{ cand.displayName }} ({{ cand.email }})</option>
                    }
                  </select>
                  <span class="text-[11px] text-slate-400">Danh sách thành viên thuộc tổ chức chưa tham gia dự án này.</span>
                </div>
              }

              <!-- Direct Email Invitation Input -->
              <div class="flex flex-col gap-1.5">
                <label for="invite-email-input" class="text-xs font-bold text-slate-700 dark:text-slate-300">
                  Mời qua địa chỉ Email @if (!selectedCandidateUserId) { <span class="text-rose-500">*</span> }
                </label>
                <input
                  id="invite-email-input"
                  type="text"
                  [ngModel]="newMemberEmail"
                  (ngModelChange)="onEmailsInput($event)"
                  placeholder="Nhập email (ví dụ: hoangteam2004@gmail.com, ngăn cách nhiều email bằng dấu ;)"
                  class="h-10 px-3 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-800 dark:text-slate-200 font-medium focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                />
                <span class="text-[11px] text-slate-400">Hệ thống sẽ gửi cả Email và Thông báo thời gian thực (SignalR) tới người nhận.</span>
              </div>

              <!-- Role Selector Loaded From Database -->
              <div class="flex flex-col gap-1.5">
                <label for="member-role-picker" class="text-xs font-bold text-slate-700 dark:text-slate-300">
                  Vai trò phân bổ trong dự án <span class="text-rose-500">*</span>
                </label>
                <select
                  id="member-role-picker"
                  [(ngModel)]="selectedRoleId"
                  class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs font-medium text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary cursor-pointer"
                >
                  @for (role of availableRoles(); track role.id) {
                    <option [value]="role.id">
                      {{ role.name }} {{ role.isPreset ? '• [Mẫu chuẩn]' : '• [Tùy biến]' }}
                    </option>
                  }
                </select>
                <span class="text-[11px] text-slate-400">Vai trò được định nghĩa từ cơ sở dữ liệu hệ thống (RBAC).</span>
              </div>

              <!-- Real-time Notification / Email Notice -->
              <div class="p-3 rounded-xl bg-indigo-50/70 dark:bg-indigo-950/30 border border-indigo-100 dark:border-indigo-900/60 flex items-start gap-2.5 text-xs text-indigo-900 dark:text-indigo-200">
                <span class="material-symbols-outlined text-indigo-600 dark:text-indigo-400 text-[18px] shrink-0 mt-0.5">mark_email_read</span>
                <div class="flex flex-col gap-0.5">
                  <span class="font-bold">Quy trình mời tham gia bảo mật:</span>
                  <span class="text-[11px] text-indigo-800/80 dark:text-indigo-300/80 leading-relaxed">
                    Hệ thống sẽ gửi thư mời qua email và thông báo trực tiếp đến tài khoản thành viên. Người được mời cần nhấn liên kết xác nhận để chính thức gia nhập dự án.
                  </span>
                </div>
              </div>
            </div>

            <div class="flex items-center justify-end gap-2.5 mt-2 pt-3 border-t border-slate-100 dark:border-slate-800">
              <button
                type="button"
                (click)="isInviteModalOpen = false"
                class="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="button"
                (click)="submitInvite()"
                [disabled]="submitting() || saving || (!selectedCandidateUserId && validInviteCount === 0)"
                class="px-5 py-2 rounded-xl bg-primary hover:bg-primary-container text-white text-xs font-bold transition-all disabled:opacity-50 flex items-center gap-1.5 cursor-pointer shadow-xs"
              >
                @if (submitting() || saving) {
                  <span class="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
                  <span>Đang gửi lời mời...</span>
                } @else {
                  <span class="material-symbols-outlined text-[16px]">send</span>
                  <span>Gửi lời mời tham gia</span>
                }
              </button>
            </div>

          </div>
        </div>
      }

      <!-- Avatar Preview & Change Modal -->
      <app-avatar-modal
        [isOpen]="isAvatarModalOpen()"
        [avatarUrl]="avatarModalData().avatarUrl ?? null"
        [displayName]="avatarModalData().displayName"
        [isCurrentUser]="avatarModalData().isCurrentUser"
        (closed)="isAvatarModalOpen.set(false)"
        (avatarChanged)="onAvatarUpdated()"
      ></app-avatar-modal>

    </div>
    }
  `,
  styles: [`
    :host {
      display: block;
    }
    .settings-container {
      padding: 1.75rem 2rem;
      max-width: 1180px;
      width: 100%;
      box-sizing: border-box;
      margin: 0 auto;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
    }
    .breadcrumb {
      font-size: 0.85rem;
      color: #64748b;
      margin-bottom: -0.5rem;
    }
    .breadcrumb a {
      color: #1b4332;
      text-decoration: none;
    }
    .settings-header {
      margin-bottom: 0;
    }
    .settings-header h1 {
      font-size: 1.6rem;
      font-weight: 700;
      color: #1e293b;
      margin: 0;
    }
    .subtitle {
      color: #64748b;
      font-size: 0.9rem;
      margin-top: 0.25rem;
    }
    .settings-tabs {
      display: flex;
      align-items: center;
      gap: 0.25rem;
      border-bottom: 2px solid #e2e8f0;
      margin: 0.25rem 0 0.5rem 0;
      overflow-x: auto;
      scrollbar-width: none;
    }
    .settings-tabs::-webkit-scrollbar {
      display: none;
    }
    .tab-item {
      padding: 0.75rem 1rem;
      font-weight: 600;
      font-size: 0.9rem;
      color: #64748b;
      background: transparent;
      border: none;
      text-decoration: none;
      border-bottom: 2px solid transparent;
      margin-bottom: -2px;
      cursor: pointer;
      transition: color 0.15s, border-color 0.15s;
      white-space: nowrap;
      flex: 0 0 auto;
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
    }
    .tab-item:hover {
      color: #1b4332;
    }
    .tab-item.active {
      color: #1b4332;
      border-bottom-color: #2d6a4f;
    }
  `]
})
export class ProjectMembersPageComponent implements OnInit, OnDestroy {
  readonly projectService = inject(ProjectManagementService);
  readonly identity = inject(IdentityService);
  private readonly route = inject(ActivatedRoute, { optional: true });
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

  loading = signal<boolean>(false);
  submitting = signal<boolean>(false);
  searchQuery = signal<string>('');
  selectedRoleFilter = signal<string | null>(null);

  isInviteModalOpen = false;
  isNameManuallyEdited = false;
  newMemberName = '';
  newMemberEmail = '';
  newMemberRole: ProjectMember['role'] = 'Developer';

  selectedUserId = '';
  selectedRoleId = '';
  readonly orgCandidateMembers = signal<CandidateOrgMember[]>([]);
  selectedCandidateUserId = '';

  readonly isAvatarModalOpen = signal(false);
  readonly avatarModalData = signal<{ avatarUrl?: string | null; displayName: string; isCurrentUser: boolean }>({
    avatarUrl: null,
    displayName: '',
    isCurrentUser: false
  });

  readonly canManageMembers = computed(() => {
    if (this.identity.isSystemAdmin()) return true;
    const currentUser = this.identity.authState().currentUser;
    const currentUserId = currentUser?.id;
    const currentUserName = currentUser?.displayName;
    const proj = this.currentProject();
    if (proj) {
      if (proj.leadUserId && currentUserId && proj.leadUserId.toLowerCase() === currentUserId.toLowerCase()) return true;
      if (proj.leadName && currentUserName && proj.leadName.toLowerCase() === currentUserName.toLowerCase()) return true;
    }
    const myMember = this.members().find(m =>
      (currentUserId && m.userId && m.userId.toLowerCase() === currentUserId.toLowerCase()) ||
      (currentUserName && m.displayName && m.displayName.toLowerCase() === currentUserName.toLowerCase())
    );
    if (myMember) {
      const r = myMember.role.toLowerCase();
      if (r.includes('lead') || r.includes('manager') || r.includes('scrum') || r.includes('owner')) {
        return true;
      }
    }
    return false;
  });

  getAvatarUrl(raw?: string | null): string | null {
    return resolveAvatarUrl(raw);
  }

  isLead(member: ProjectMember): boolean {
    const proj = this.currentProject();
    if (!proj) return false;
    if (proj.leadUserId && member.userId && proj.leadUserId.toLowerCase() === member.userId.toLowerCase()) return true;
    if (proj.leadName && member.displayName && proj.leadName.toLowerCase() === member.displayName.toLowerCase()) return true;
    return member.role === 'Project Lead';
  }

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

  private lastLoadedProjectId = '';

  constructor() {
    effect(() => {
      const proj = this.currentProject?.();
      if (proj?.id && proj.id !== this.lastLoadedProjectId) {
        this.lastLoadedProjectId = proj.id;
        if (proj.members && proj.members.length > 0 && this.members().length === 0) {
          this.members.set(proj.members);
        }
        this.loadProjectMembers(proj.id);
      }
    });
  }

  ngOnInit(): void {
    const routeProjId = this.route?.snapshot?.paramMap?.get('id') || '';
    const proj = this.currentProject?.();
    const targetId = routeProjId || proj?.id || '';
    if (proj?.members && proj.members.length > 0 && this.members().length === 0) {
      this.members.set(proj.members);
    }
    if (targetId && targetId !== this.lastLoadedProjectId) {
      this.lastLoadedProjectId = targetId;
      this.loadProjectMembers(targetId);
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
    if (this.members().length === 0) {
      this.loading.set(true);
    }
    this.projectService.loadProjectMembers(projectId).subscribe({
      next: members => {
        this.members.set(members);
        this.loading.set(false);
      },
      error: () => {
        const fallback = this.currentProject()?.members ?? [];
        if (this.members().length === 0) {
          this.members.set(fallback);
        }
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
              description: r.description,
              isPreset: r.isPreset ?? false
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

  loadCandidateOrgMembers(projectId: string): void {
    this.projectService.getCandidateOrgMembers(projectId).subscribe({
      next: (candidates) => {
        const mapped: CandidateOrgMember[] = (candidates || []).map(c => ({
          id: c.userId,
          email: c.email,
          displayName: c.displayName,
          avatarUrl: c.avatarUrl
        }));
        this.orgCandidateMembers.set(mapped);
        if (mapped.length > 0 && !this.selectedCandidateUserId) {
          this.selectedCandidateUserId = mapped[0].id;
        }
      },
      error: (err: unknown) => {
        const errObj = err as { error?: { message?: string } };
        this.toastService?.error('Lỗi', errObj?.error?.message || 'Không thể tải danh sách thành viên tổ chức.');
      }
    });
  }

  openInviteModal(): void {
    this.newMemberEmail = '';
    this.newMemberName = '';
    this.isNameManuallyEdited = false;
    this.selectedUserId = '';
    this.selectedCandidateUserId = '';
    this.error = '';
    this.selectedRoleId = this.availableRoles()[0]?.id || '';
    this.isInviteModalOpen = true;

    const projId = this.currentProject()?.id;
    if (projId) {
      this.loadCandidateOrgMembers(projId);
    }

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

  openAvatarModal(member: ProjectMember): void {
    const isMe = this.isCurrentUser(member);
    this.avatarModalData.set({
      avatarUrl: member.avatarUrl,
      displayName: member.displayName,
      isCurrentUser: isMe
    });
    this.isAvatarModalOpen.set(true);
  }

  isCurrentUser(member: ProjectMember): boolean {
    const currentUser = this.identity.authState().currentUser;
    if (!currentUser) return false;
    if (currentUser.id && member.userId && currentUser.id.toLowerCase() === member.userId.toLowerCase()) return true;
    if (currentUser.displayName && member.displayName && currentUser.displayName.toLowerCase() === member.displayName.toLowerCase()) return true;
    return false;
  }

  onAvatarUpdated(): void {
    const projId = this.currentProject()?.id;
    if (projId) {
      this.loadProjectMembers(projId);
      this.projectService.loadWorkspace(projId);
    }
    this.identity.getMyProfile().subscribe();
  }

  onEmailsInput(value: string): void {
    this.newMemberEmail = value;
    if (value.trim().length > 0) {
      this.selectedCandidateUserId = '';
    }
    if (!this.isNameManuallyEdited) {
      this.autoGenerateNamesFromEmails(value);
    }
  }

  onNamesInput(value: string): void {
    this.newMemberName = value;
    this.isNameManuallyEdited = value.trim().length > 0;
  }

  autoGenerateNamesFromEmails(emailStr: string): void {
    const tokens = emailStr.split(';')
      .map(t => t.trim())
      .filter(t => t.length > 0);

    const names = tokens.map(token => {
      const atIndex = token.indexOf('@');
      return atIndex > 0 ? token.substring(0, atIndex) : token;
    });

    this.newMemberName = names.join('; ');
  }

  get parsedInviteList(): { email: string; name: string; isValid: boolean }[] {
    const emailTokens = this.newMemberEmail
      .split(';')
      .map(e => e.trim())
      .filter(e => e.length > 0);

    const nameTokens = this.newMemberName
      .split(';')
      .map(n => n.trim());

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    return emailTokens.map((email, idx) => {
      const name = (idx < nameTokens.length && nameTokens[idx])
        ? nameTokens[idx]
        : (email.includes('@') ? email.split('@')[0] : email);
      return {
        email,
        name,
        isValid: emailRegex.test(email)
      };
    });
  }

  get validInviteCount(): number {
    return this.parsedInviteList.filter(item => item.isValid).length;
  }

  removeInviteItem(index: number): void {
    const list = [...this.parsedInviteList];
    if (index < 0 || index >= list.length) return;
    list.splice(index, 1);
    this.newMemberEmail = list.map(item => item.email).join('; ');
    this.newMemberName = list.map(item => item.name).join('; ');
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

    const projId = this.currentProject()?.id;
    if (!projId) return;

    const candidateId = this.selectedCandidateUserId || this.selectedUserId;
    const resolvedRoleId = this.selectedRoleId || this.getRoleOptionId(this.newMemberRole);

    if (candidateId && resolvedRoleId) {
      this.submitting.set(true);
      this.error = '';
      this.projectService.inviteProjectMember(projId, candidateId, resolvedRoleId).subscribe({
        next: () => {
          this.submitting.set(false);
          this.isInviteModalOpen = false;
          this.selectedCandidateUserId = '';
          this.selectedUserId = '';
          this.toastService?.success('Thành công', 'Đã thêm thành viên vào dự án và đồng bộ phân quyền.');
          this.loadProjectMembers(projId);
          this.projectService.loadWorkspace(projId);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          const errObj = err as { title?: string; detail?: string; error?: { message?: string } };
          const msg = errObj?.detail ?? errObj?.title ?? errObj?.error?.message ?? 'Không thể thêm thành viên. Vui lòng thử lại.';
          this.error = msg;
          this.toastService?.error('Lỗi thêm thành viên', msg);
        }
      });
      return;
    }

    const validItems = this.parsedInviteList.filter(i => i.isValid);
    if (validItems.length === 0) {
      this.toastService?.error('Chưa có email hợp lệ', 'Vui lòng nhập ít nhất một địa chỉ email đúng định dạng.');
      return;
    }

    const emailsPayload = validItems.map(i => i.email).join(';');
    const namesPayload = validItems.map(i => i.name).join(';');
    const selectedRoleObj = this.availableRoles().find(r => r.id === this.selectedRoleId);
    const resolvedRoleLabel = (selectedRoleObj?.name as ProjectMember['role']) || this.newMemberRole;

    this.saving = true;
    this.error = '';

    if (this.rbac && typeof this.rbac.batchInviteProjectMembers === 'function') {
      this.rbac.batchInviteProjectMembers(projId, emailsPayload, namesPayload, resolvedRoleLabel).subscribe({
        next: (res) => {
          this.saving = false;
          this.isInviteModalOpen = false;
          this.newMemberEmail = '';
          this.newMemberName = '';
          this.isNameManuallyEdited = false;
          this.toastService?.success('Thành công', res.message || `Đã gửi thư mời tới ${validItems.length} thành viên.`);
          this.loadProjectMembers(projId);
          this.projectService.loadWorkspace(projId);
        },
        error: (err: unknown) => {
          this.saving = false;
          const errObj = err as { error?: { message?: string } };
          const msg = errObj?.error?.message || 'Không thể gửi lời mời. Vui lòng thử lại sau.';
          this.error = msg;
          this.toastService?.error('Lỗi mời thành viên', msg);
        }
      });
    } else {
      this.projectService.addProjectMember({
        displayName: namesPayload,
        email: emailsPayload,
        role: this.newMemberRole
      }).subscribe({
        next: () => {
          this.saving = false;
          this.isInviteModalOpen = false;
          this.newMemberName = '';
          this.newMemberEmail = '';
          this.loadProjectMembers(projId);
        },
        error: (err: unknown) => {
          this.saving = false;
          const errObj = err as { detail?: string; error?: { message?: string } };
          this.error = errObj?.detail || errObj?.error?.message || 'Không thể gửi lời mời.';
          this.toastService?.error('Lỗi', this.error);
        }
      });
    }
  }

  removeMember(userId: string): void {
    const projId = this.currentProject()?.id;
    if (this.rbac && projId) {
      this.rbac.removeProjectMember(projId, userId).subscribe({
        next: () => {
          this.loadProjectMembers(projId);
          this.projectService.loadWorkspace(projId);
        },
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
