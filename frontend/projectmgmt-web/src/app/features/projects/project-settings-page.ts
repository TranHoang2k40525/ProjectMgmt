import { apiErrorMessage } from '../../core/http/api-error-message';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  CustomIssueType,
  Project,
  ProjectLabel,
  ProjectManagementService,
  UpdateProjectRequest,
  UserDisplayInfo
} from '../../core/services/project-management.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-project-settings-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="settings-container">
      <div class="breadcrumb">
        <a routerLink="/projects">Dự án</a> &gt;
        @if (project()) {
          <span>{{ project()?.name }} ({{ project()?.projectKey }})</span> &gt;
        }
        <span>Cài đặt</span>
      </div>

      <div class="settings-header">
        <div>
          <h1>Cài đặt Dự án</h1>
          <p class="subtitle">Quản lý cấu hình thông tin chung, loại công việc động, nhãn dán, quy trình (Workflow) và bảng (Board)</p>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="settings-tabs">
        <button
          type="button"
          (click)="activeTab.set('general')"
          [class.active]="activeTab() === 'general'"
          class="tab-item flex items-center gap-1.5"
        >
          <span class="material-symbols-outlined text-[18px]">tune</span>
          <span>Thông tin chung</span>
        </button>
        <button
          type="button"
          (click)="activeTab.set('issue-types')"
          [class.active]="activeTab() === 'issue-types'"
          class="tab-item flex items-center gap-1.5"
        >
          <span class="material-symbols-outlined text-[18px]">category</span>
          <span>Loại công việc (Issue Types)</span>
        </button>
        <button
          type="button"
          (click)="activeTab.set('labels')"
          [class.active]="activeTab() === 'labels'"
          class="tab-item flex items-center gap-1.5"
        >
          <span class="material-symbols-outlined text-[18px]">label</span>
          <span>Quản lý Nhãn (Labels)</span>
        </button>
        <a
          [routerLink]="['/projects', projectId, 'settings', 'workflow']"
          class="tab-item flex items-center gap-1.5"
        >
          <span class="material-symbols-outlined text-[18px]">sync_alt</span>
          <span>Cấu hình Workflow</span>
        </a>
        <a
          [routerLink]="['/projects', projectId, 'settings', 'board']"
          class="tab-item flex items-center gap-1.5"
        >
          <span class="material-symbols-outlined text-[18px]">view_kanban</span>
          <span>Cấu hình Board</span>
        </a>
        <a
          [routerLink]="['/projects', projectId, 'members']"
          class="tab-item flex items-center gap-1.5"
        >
          <span class="material-symbols-outlined text-[18px]">badge</span>
          <span>Thành viên & Phân quyền</span>
        </a>
      </div>

      @if (loading()) {
        <div class="loading-box">
          <div class="spinner"></div>
          <p>Đang tải dữ liệu cài đặt dự án...</p>
        </div>
      } @else if (error()) {
        <div class="alert alert-error">
          {{ error() }}
        </div>
      } @else {
        <!-- TAB 1: THÔNG TIN CHUNG -->
        @if (activeTab() === 'general' && project()) {
          <div class="settings-content">
            <div class="card">
              <h2>Chỉnh sửa thông tin cơ bản</h2>
              <form (ngSubmit)="onSaveGeneral()">
                <div class="form-group">
                  <label for="projectKeyDisabled">Mã dự án (Project Key)</label>
                  <input
                    type="text"
                    id="projectKeyDisabled"
                    [value]="project()?.projectKey"
                    disabled
                    class="form-control readonly-input"
                  />
                  <small class="form-hint">Mã Key là định danh bất biến, không thể thay đổi sau khi tạo.</small>
                </div>

                <div class="form-group">
                  <label for="name">Tên dự án <span class="required">*</span></label>
                  <input
                    type="text"
                    id="name"
                    name="name"
                    [(ngModel)]="editForm.name"
                    required
                    maxlength="200"
                    class="form-control"
                  />
                </div>

                <div class="form-group">
                  <label for="description">Mô tả dự án</label>
                  <textarea
                    id="description"
                    name="description"
                    [(ngModel)]="editForm.description"
                    rows="3"
                    class="form-control"
                  ></textarea>
                </div>

                <div class="form-group">
                  <label for="lead">Người phụ trách (Lead)</label>
                  <select
                    id="lead"
                    name="lead"
                    [(ngModel)]="editForm.leadUserId"
                    class="form-control"
                  >
                    @for (user of users(); track user.userId) {
                      <option [value]="user.userId">{{ user.displayName }}</option>
                    }
                  </select>
                </div>

                <div class="form-actions">
                  <button type="submit" [disabled]="saving()" class="btn btn-primary">
                    {{ saving() ? 'Đang lưu...' : 'Lưu thay đổi' }}
                  </button>
                </div>
              </form>
            </div>

            <!-- Danger Zone -->
            <div class="card danger-card">
              <h3>Khu vực nguy hiểm</h3>
              <p>Xóa mềm dự án sẽ ẩn dự án khỏi danh sách làm việc và giữ lại dữ liệu trong cơ sở dữ liệu.</p>
              <button (click)="onDeleteProject()" class="btn btn-danger">Xóa Dự Án Này</button>
            </div>
          </div>
        }

        <!-- TAB 2: QUẢN LÝ LOẠI CÔNG VIỆC DỘNG (ISSUE TYPES) -->
        @if (activeTab() === 'issue-types') {
          <div class="settings-content">
            <div class="card">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h2>Các loại công việc trong dự án</h2>
                  <p class="text-sm text-slate-500">Được lưu trữ động ở backend và database. Bạn có thể tự do thêm, sửa tên, màu sắc, biểu tượng Material Symbols.</p>
                </div>
                <button type="button" (click)="openAddIssueTypeModal()" class="btn btn-primary flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[18px]">add</span>
                  <span>Thêm loại công việc</span>
                </button>
              </div>

              <div class="divide-y divide-slate-100 dark:divide-slate-800">
                @for (t of issueTypes(); track t.id) {
                  <div class="py-3 flex items-center justify-between gap-4">
                    <div class="flex items-center gap-3">
                      <div class="w-9 h-9 rounded-lg flex items-center justify-center text-white shrink-0 shadow-2xs" [style.backgroundColor]="t.color">
                        <span class="material-symbols-outlined text-[20px]">{{ t.icon }}</span>
                      </div>
                      <div>
                        <div class="flex items-center gap-2">
                          <span class="font-bold text-slate-800 dark:text-slate-200 text-sm">{{ t.name }}</span>
                          @if (t.isSubtask) {
                            <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-amber-50 text-amber-700 border border-amber-200">Subtask</span>
                          }
                          @if (t.isCustom) {
                            <span class="px-2 py-0.5 rounded-full text-[11px] font-medium bg-blue-50 text-blue-700 border border-blue-200">Tùy biến</span>
                          }
                        </div>
                        <p class="text-xs text-slate-400 mt-0.5">Icon: {{ t.icon }} | Màu: {{ t.color }} | Cấp bậc: {{ t.hierarchyLevel ?? 2 }}</p>
                      </div>
                    </div>
                    <div class="flex items-center gap-1">
                      <button type="button" (click)="openEditIssueTypeModal(t)" class="p-1.5 rounded-md text-slate-500 hover:text-slate-800 hover:bg-slate-100 transition-colors" title="Chỉnh sửa">
                        <span class="material-symbols-outlined text-[18px]">edit</span>
                      </button>
                      <button type="button" (click)="deleteIssueType(t)" class="p-1.5 rounded-md text-red-500 hover:text-red-700 hover:bg-red-50 transition-colors" title="Xóa loại công việc">
                        <span class="material-symbols-outlined text-[18px]">delete</span>
                      </button>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>
        }

        <!-- TAB 3: QUẢN LÝ NHÃN DÁN (LABELS) -->
        @if (activeTab() === 'labels') {
          <div class="settings-content">
            <div class="card">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h2>Danh sách nhãn dán (Labels)</h2>
                  <p class="text-sm text-slate-500">Gán vào User Story, Task, Bug để phân loại module (frontend, backend, auth, v.v.). Lưu trữ động tại backend.</p>
                </div>
                <button type="button" (click)="openAddLabelModal()" class="btn btn-primary flex items-center gap-1.5">
                  <span class="material-symbols-outlined text-[18px]">add</span>
                  <span>Thêm nhãn mới</span>
                </button>
              </div>

              @if (labels().length === 0) {
                <div class="py-8 text-center text-slate-400">
                  <span class="material-symbols-outlined text-[40px] opacity-40 mb-1">label_off</span>
                  <p class="text-sm">Chưa có nhãn dán nào. Hãy thêm nhãn để phân loại công việc thuận tiện hơn.</p>
                </div>
              } @else {
                <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">
                  @for (l of labels(); track l.id) {
                    <div class="p-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50 flex items-center justify-between gap-2 shadow-2xs">
                      <div class="flex items-center gap-2 min-w-0">
                        <span class="w-3.5 h-3.5 rounded-full shrink-0" [style.backgroundColor]="l.colorHex"></span>
                        <span class="font-semibold text-sm text-slate-800 dark:text-slate-200 truncate">{{ l.name }}</span>
                      </div>
                      <div class="flex items-center gap-1 shrink-0">
                        <button type="button" (click)="openEditLabelModal(l)" class="p-1 rounded text-slate-400 hover:text-slate-700" title="Sửa nhãn">
                          <span class="material-symbols-outlined text-[16px]">edit</span>
                        </button>
                        <button type="button" (click)="deleteLabel(l)" class="p-1 rounded text-red-400 hover:text-red-700" title="Xóa nhãn">
                          <span class="material-symbols-outlined text-[16px]">delete</span>
                        </button>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          </div>
        }
      }

      <!-- MODAL TẠO / SỬA ISSUE TYPE -->
      @if (showIssueTypeModal()) {
        <div class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div class="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl max-w-md w-full p-6 animate-in fade-in zoom-in-95 duration-150">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <span class="material-symbols-outlined text-primary">{{ editingIssueType() ? 'edit' : 'add_circle' }}</span>
                <span>{{ editingIssueType() ? 'Sửa Loại Công Việc' : 'Thêm Loại Công Việc Mới' }}</span>
              </h3>
              <button type="button" (click)="showIssueTypeModal.set(false)" class="text-slate-400 hover:text-slate-600">
                <span class="material-symbols-outlined">close</span>
              </button>
            </div>

            <div class="space-y-4">
              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Tên loại công việc <span class="text-red-500">*</span></label>
                <input type="text" [(ngModel)]="issueTypeForm.name" placeholder="Ví dụ: Spike, Research, Release..." class="form-control" />
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Biểu tượng Material Symbol</label>
                <div class="flex items-center gap-2 mb-2">
                  <div class="w-9 h-9 rounded-lg flex items-center justify-center text-white shrink-0 shadow-2xs" [style.backgroundColor]="issueTypeForm.color">
                    <span class="material-symbols-outlined text-[20px]">{{ issueTypeForm.icon }}</span>
                  </div>
                  <input type="text" [(ngModel)]="issueTypeForm.icon" placeholder="Tên icon, ví dụ: bolt, task, biotech..." class="form-control font-mono text-xs" />
                </div>
                <div class="flex flex-wrap gap-1.5 pt-1">
                  @for (ic of sampleIcons; track ic) {
                    <button type="button" (click)="issueTypeForm.icon = ic" class="p-1.5 rounded-lg border border-slate-200 hover:border-primary hover:bg-primary/5 transition-colors" [class.border-primary]="issueTypeForm.icon === ic">
                      <span class="material-symbols-outlined text-[18px] text-slate-700 dark:text-slate-300">{{ ic }}</span>
                    </button>
                  }
                </div>
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Mã màu (Hex)</label>
                <div class="flex items-center gap-2 mb-2">
                  <input type="color" [(ngModel)]="issueTypeForm.color" class="w-9 h-9 rounded-lg border border-slate-300 cursor-pointer p-0.5" />
                  <input type="text" [(ngModel)]="issueTypeForm.color" class="form-control font-mono text-xs" />
                </div>
                <div class="flex flex-wrap gap-2 pt-1">
                  @for (col of sampleColors; track col) {
                    <button type="button" (click)="issueTypeForm.color = col" [attr.aria-label]="'Chọn màu ' + col" class="w-6 h-6 rounded-full border-2 border-white shadow-2xs hover:scale-110 transition-transform" [style.backgroundColor]="col">
                      <span class="sr-only">Màu {{ col }}</span>
                    </button>
                  }
                </div>
              </div>

              <div class="pt-2">
                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" [(ngModel)]="issueTypeForm.isSubtask" class="w-4 h-4 rounded text-primary focus:ring-primary" />
                  <span class="text-sm font-medium text-slate-700 dark:text-slate-300">Là công việc con (Subtask / công việc cấp dưới)?</span>
                </label>
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-2">
              <button type="button" (click)="showIssueTypeModal.set(false)" class="btn btn-secondary">Hủy bỏ</button>
              <button type="button" (click)="saveIssueType()" [disabled]="!issueTypeForm.name.trim()" class="btn btn-primary">Lưu loại công việc</button>
            </div>
          </div>
        </div>
      }

      <!-- MODAL TẠO / SỬA NHÃN (LABEL) -->
      @if (showLabelModal()) {
        <div class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div class="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl max-w-sm w-full p-6 animate-in fade-in zoom-in-95 duration-150">
            <div class="flex items-center justify-between mb-4">
              <h3 class="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <span class="material-symbols-outlined text-primary">{{ editingLabel() ? 'edit' : 'new_label' }}</span>
                <span>{{ editingLabel() ? 'Sửa Nhãn Dán' : 'Thêm Nhãn Mới' }}</span>
              </h3>
              <button type="button" (click)="showLabelModal.set(false)" class="text-slate-400 hover:text-slate-600">
                <span class="material-symbols-outlined">close</span>
              </button>
            </div>

            <div class="space-y-4">
              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Tên nhãn <span class="text-red-500">*</span></label>
                <input type="text" [(ngModel)]="labelForm.name" placeholder="frontend, backend, api, security..." class="form-control" />
              </div>

              <div>
                <label class="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase mb-1">Mã màu (Hex)</label>
                <div class="flex items-center gap-2 mb-2">
                  <input type="color" [(ngModel)]="labelForm.colorHex" class="w-9 h-9 rounded-lg border border-slate-300 cursor-pointer p-0.5" />
                  <input type="text" [(ngModel)]="labelForm.colorHex" class="form-control font-mono text-xs" />
                </div>
                <div class="flex flex-wrap gap-2 pt-1">
                  @for (col of sampleColors; track col) {
                    <button type="button" (click)="labelForm.colorHex = col" [attr.aria-label]="'Chọn màu ' + col" class="w-6 h-6 rounded-full border-2 border-white shadow-2xs hover:scale-110 transition-transform" [style.backgroundColor]="col">
                      <span class="sr-only">Màu {{ col }}</span>
                    </button>
                  }
                </div>
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-2">
              <button type="button" (click)="showLabelModal.set(false)" class="btn btn-secondary">Hủy bỏ</button>
              <button type="button" (click)="saveLabel()" [disabled]="!labelForm.name.trim()" class="btn btn-primary">Lưu nhãn</button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .settings-container {
      padding: 2rem;
      max-width: 960px;
      margin: 0 auto;
    }
    .breadcrumb {
      font-size: 0.85rem;
      color: #64748b;
      margin-bottom: 1rem;
    }
    .breadcrumb a {
      color: #1b4332;
      text-decoration: none;
    }
    .settings-header {
      margin-bottom: 1.5rem;
    }
    h1 {
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
      gap: 0.5rem;
      border-bottom: 2px solid #e2e8f0;
      margin-bottom: 2rem;
    }
    .tab-item {
      padding: 0.75rem 1.25rem;
      font-weight: 600;
      font-size: 0.95rem;
      color: #64748b;
      background: transparent;
      border: none;
      text-decoration: none;
      border-bottom: 2px solid transparent;
      margin-bottom: -2px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .tab-item:hover {
      color: #1b4332;
    }
    .tab-item.active {
      color: #1b4332;
      border-bottom-color: #2d6a4f;
    }
    .card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.75rem;
      margin-bottom: 1.5rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    h2 {
      font-size: 1.2rem;
      font-weight: 600;
      color: #0f172a;
      margin: 0 0 0.5rem 0;
    }
    .form-group {
      margin-bottom: 1.25rem;
    }
    label {
      display: block;
      font-weight: 600;
      color: #334155;
      font-size: 0.9rem;
      margin-bottom: 0.35rem;
    }
    .required {
      color: #ef4444;
    }
    .form-control {
      width: 100%;
      padding: 0.65rem 0.85rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      font-size: 0.95rem;
      box-sizing: border-box;
    }
    .form-control:focus {
      outline: none;
      border-color: #2d6a4f;
      box-shadow: 0 0 0 3px rgba(45, 106, 79, 0.15);
    }
    .readonly-input {
      background: #f8fafc;
      color: #64748b;
      cursor: not-allowed;
      font-family: monospace;
      font-weight: bold;
    }
    .form-hint {
      display: block;
      color: #64748b;
      font-size: 0.8rem;
      margin-top: 0.35rem;
    }
    .form-actions {
      margin-top: 1.5rem;
      display: flex;
      justify-content: flex-end;
    }
    .btn {
      padding: 0.65rem 1.4rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.9rem;
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 0.2s;
    }
    .btn-primary {
      background: #1b4332;
      color: #ffffff;
    }
    .btn-primary:hover:not(:disabled) {
      background: #2d6a4f;
    }
    .btn-secondary {
      background: #f1f5f9;
      color: #475569;
      border-color: #cbd5e1;
    }
    .btn-secondary:hover {
      background: #e2e8f0;
      color: #1e293b;
    }
    .btn-danger {
      background: #dc2626;
      color: #ffffff;
    }
    .btn-danger:hover {
      background: #b91c1c;
    }
    .danger-card {
      border-color: #fecaca;
      background: #fffafa;
    }
    .danger-card h3 {
      color: #991b1b;
      margin: 0 0 0.5rem 0;
    }
    .danger-card p {
      color: #7f1d1d;
      font-size: 0.9rem;
      margin-bottom: 1rem;
    }
    .alert {
      padding: 0.85rem 1rem;
      border-radius: 8px;
      margin-bottom: 1.25rem;
      font-size: 0.9rem;
    }
    .alert-error {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #991b1b;
    }
    .loading-box {
      text-align: center;
      padding: 3rem;
    }
    .spinner {
      width: 36px;
      height: 36px;
      border: 3px solid #e2e8f0;
      border-top-color: #2563eb;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 1rem auto;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectSettingsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly projectService = inject(ProjectManagementService);
  private readonly confirmService = inject(ConfirmDialogService);
  private readonly toastService = inject(ToastService);

  projectId = '';
  readonly activeTab = signal<'general' | 'issue-types' | 'labels'>('general');
  readonly project = signal<Project | null>(null);
  readonly users = signal<UserDisplayInfo[]>([]);
  readonly issueTypes = signal<CustomIssueType[]>([]);
  readonly labels = signal<ProjectLabel[]>([]);
  readonly loading = signal<boolean>(true);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  // Modal states
  readonly showIssueTypeModal = signal<boolean>(false);
  readonly editingIssueType = signal<CustomIssueType | null>(null);
  issueTypeForm = {
    name: '',
    icon: 'task_alt',
    color: '#6366F1',
    isSubtask: false
  };

  readonly showLabelModal = signal<boolean>(false);
  readonly editingLabel = signal<ProjectLabel | null>(null);
  labelForm = {
    name: '',
    colorHex: '#3B82F6'
  };

  sampleIcons = ['bookmark', 'task_alt', 'bug_report', 'bolt', 'subdirectory_arrow_right', 'biotech', 'palette', 'security', 'description', 'flag', 'code', 'build'];
  sampleColors = ['#EF4444', '#F97316', '#F59E0B', '#10B981', '#06B6D4', '#3B82F6', '#6366F1', '#8B5CF6', '#EC4899', '#64748B'];

  editForm: UpdateProjectRequest = {
    name: '',
    description: '',
    leadUserId: ''
  };

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id') || '';
    if (this.projectId) {
      this.loadProject();
      this.loadUsers();
      this.loadIssueTypes();
      this.loadLabels();
    }
  }

  loadUsers(): void {
    this.projectService.getUsers().subscribe({
      next: users => this.users.set(users),
      error: err => this.error.set(apiErrorMessage(err, 'Không thể tải người phụ trách dự án.'))
    });
  }

  loadProject(): void {
    this.loading.set(true);
    this.error.set(null);
    this.projectService.getProjectById(this.projectId).subscribe({
      next: (p) => {
        this.project.set(p);
        this.editForm = {
          name: p.name,
          description: p.description || '',
          leadUserId: p.leadUserId
        };
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(apiErrorMessage(err, 'Không tìm thấy dự án.'));
        this.loading.set(false);
      }
    });
  }

  loadIssueTypes(): void {
    this.projectService.getIssueTypes(this.projectId).subscribe({
      next: types => this.issueTypes.set(types),
      error: err => console.error('Không thể tải loại công việc:', err)
    });
  }

  loadLabels(): void {
    this.projectService.getProjectLabels(this.projectId).subscribe({
      next: labels => this.labels.set(labels),
      error: err => console.error('Không thể tải nhãn dán:', err)
    });
  }

  onSaveGeneral(): void {
    if (this.saving() || this.loading() || !this.project()) return;
    this.saving.set(true);

    this.projectService.updateProject(this.projectId, this.editForm).subscribe({
      next: (updated) => {
        this.project.set(updated);
        this.saving.set(false);
        this.toastService.success('Cập Nhật Dự Án', 'Đã lưu thay đổi cấu hình dự án thành công.');
      },
      error: (err) => {
        this.saving.set(false);
        this.toastService.error('Lỗi Cập Nhật', apiErrorMessage(err, 'Không thể lưu thay đổi.'));
      }
    });
  }

  onDeleteProject(): void {
    const projName = this.project()?.name || 'dự án này';
    this.confirmService.confirm({
      title: 'Xóa Dự Án',
      message: `Bạn có chắc chắn muốn xóa dự án "${projName}" không? Hành động này sẽ lưu trữ dự án.`,
      type: 'danger',
      confirmText: 'Xóa dự án ngay',
      cancelText: 'Hủy bỏ',
      onConfirm: () => {
        this.projectService.deleteProject(this.projectId).subscribe({
          next: () => {
            this.toastService.warning('Đã Xóa Dự Án', `Đã chuyển dự án "${projName}" sang trạng thái lưu trữ.`);
            this.router.navigate(['/projects']);
          },
          error: (err) => {
            this.toastService.error('Lỗi Xóa Dự Án', apiErrorMessage(err, 'Không thể xóa dự án.'));
          }
        });
      }
    });
  }

  // Issue Type Actions
  openAddIssueTypeModal(): void {
    this.editingIssueType.set(null);
    this.issueTypeForm = { name: '', icon: 'task_alt', color: '#6366F1', isSubtask: false };
    this.showIssueTypeModal.set(true);
  }

  openEditIssueTypeModal(type: CustomIssueType): void {
    this.editingIssueType.set(type);
    this.issueTypeForm = {
      name: type.name,
      icon: type.icon || 'task_alt',
      color: type.color || '#6366F1',
      isSubtask: !!type.isSubtask
    };
    this.showIssueTypeModal.set(true);
  }

  saveIssueType(): void {
    const name = this.issueTypeForm.name.trim();
    if (!name) return;

    const editing = this.editingIssueType();
    if (editing) {
      this.projectService.updateCustomIssueType(
        this.projectId,
        editing.id,
        name,
        this.issueTypeForm.icon,
        this.issueTypeForm.color,
        this.issueTypeForm.isSubtask
      ).subscribe({
        next: (updated) => {
          this.issueTypes.update(types => types.map(t => t.id === editing.id ? updated : t));
          this.showIssueTypeModal.set(false);
          this.toastService.success('Loại Công Việc', `Đã cập nhật loại công việc "${name}".`);
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể cập nhật loại công việc.'))
      });
    } else {
      this.projectService.addCustomIssueType(
        name,
        this.issueTypeForm.icon,
        this.issueTypeForm.color,
        this.issueTypeForm.isSubtask
      ).subscribe({
        next: (created) => {
          this.issueTypes.update(types => [...types, created]);
          this.showIssueTypeModal.set(false);
          this.toastService.success('Loại Công Việc', `Đã tạo loại công việc "${name}".`);
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể tạo loại công việc.'))
      });
    }
  }

  deleteIssueType(type: CustomIssueType): void {
    this.confirmService.confirm({
      title: 'Xóa Loại Công Việc',
      message: `Bạn có chắc muốn xóa loại "${type.name}" không? Thao tác này sẽ bị từ chối nếu còn công việc thuộc loại này.`,
      type: 'danger',
      confirmText: 'Xóa',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.projectService.deleteCustomIssueType(this.projectId, type.id).subscribe({
          next: () => {
            this.issueTypes.update(types => types.filter(t => t.id !== type.id));
            this.toastService.success('Đã Xóa', `Đã xóa loại công việc "${type.name}".`);
          },
          error: (err) => this.toastService.error('Lỗi Xóa', apiErrorMessage(err, 'Không thể xóa loại công việc.'))
        });
      }
    });
  }

  // Label Actions
  openAddLabelModal(): void {
    this.editingLabel.set(null);
    this.labelForm = { name: '', colorHex: '#3B82F6' };
    this.showLabelModal.set(true);
  }

  openEditLabelModal(label: ProjectLabel): void {
    this.editingLabel.set(label);
    this.labelForm = { name: label.name, colorHex: label.colorHex || '#3B82F6' };
    this.showLabelModal.set(true);
  }

  saveLabel(): void {
    const name = this.labelForm.name.trim();
    if (!name) return;

    const editing = this.editingLabel();
    if (editing) {
      this.projectService.updateProjectLabel(editing.id, name, this.labelForm.colorHex).subscribe({
        next: (updated) => {
          this.labels.update(labels => labels.map(l => l.id === editing.id ? updated : l));
          this.showLabelModal.set(false);
          this.toastService.success('Nhãn Dán', `Đã cập nhật nhãn "${name}".`);
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể cập nhật nhãn.'))
      });
    } else {
      this.projectService.createProjectLabel(this.projectId, name, this.labelForm.colorHex).subscribe({
        next: (created) => {
          this.labels.update(labels => [...labels, created]);
          this.showLabelModal.set(false);
          this.toastService.success('Nhãn Dán', `Đã tạo nhãn "${name}".`);
        },
        error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể tạo nhãn mới.'))
      });
    }
  }

  deleteLabel(label: ProjectLabel): void {
    this.confirmService.confirm({
      title: 'Xóa Nhãn',
      message: `Bạn có chắc muốn xóa nhãn "${label.name}" không?`,
      type: 'danger',
      confirmText: 'Xóa',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.projectService.deleteProjectLabel(label.id).subscribe({
          next: () => {
            this.labels.update(labels => labels.filter(l => l.id !== label.id));
            this.toastService.success('Đã Xóa', `Đã xóa nhãn "${label.name}".`);
          },
          error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể xóa nhãn.'))
        });
      }
    });
  }
}
