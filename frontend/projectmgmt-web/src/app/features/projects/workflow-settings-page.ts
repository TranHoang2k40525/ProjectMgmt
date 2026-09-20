import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  CreateWorkflowTransitionRequest,
  Project,
  ProjectManagementService,
  ProjectStatus,
  WorkflowTransition
} from '../../core/services/project-management.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-workflow-settings-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="workflow-container font-sans text-slate-900 dark:text-slate-100">
      <div class="breadcrumb">
        <a routerLink="/projects">Dự án</a> &gt;
        @if (project()) {
          <a [routerLink]="['/projects', projectId, 'settings']">{{ project()?.name }}</a> &gt;
        }
        <span>Cấu hình Workflow</span>
      </div>

      <div class="page-header">
        <div>
          <h1 style="font-family: Arial, sans-serif;">Cấu hình Quy trình Workflow (Workflow Settings)</h1>
          <p class="subtitle">Quản lý các bước trạng thái, áp dụng quy trình mẫu và định nghĩa luật chuyển đổi cho dự án {{ project()?.name }}</p>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="settings-tabs">
        <a [routerLink]="['/projects', projectId, 'settings']" class="tab-item">⚙️ Thông tin chung</a>
        <span class="tab-item active">🔄 Cấu hình Workflow</span>
        <a [routerLink]="['/projects', projectId, 'settings', 'board']" class="tab-item">📋 Cấu hình Board</a>
      </div>



      <!-- 1. Apply Workflow Template Section -->
      <div class="card bg-gradient-to-r from-blue-50/50 to-indigo-50/50 border-blue-200">
        <div class="workflow-template-layout flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h3 style="font-family: Arial, sans-serif;" class="text-blue-900 flex items-center gap-2">
              <span>🚀</span> Áp dụng Quy trình Mẫu vào Dự án (Workflow Schemes)
            </h3>
            <p class="hint text-blue-700">Tự động cấu hình danh sách trạng thái và bảng Kanban theo chuẩn phương pháp Agile/Scrum.</p>
          </div>

          <div class="workflow-template-actions flex items-center gap-2.5 shrink-0">
            <select
              [ngModel]="selectedTemplate()"
              (ngModelChange)="selectedTemplate.set($event)"
              class="form-control text-sm font-semibold bg-white border-blue-300"
            >
              <option value="scrum-std">Standard Scrum (To Do ➔ In Progress ➔ Code Review ➔ Done)</option>
              <option value="agile-qa">Agile Software Dev + QA (To Do ➔ In Progress ➔ Review ➔ QA ➔ Done)</option>
              <option value="kanban-simple">Kanban Minimal (To Do ➔ In Progress ➔ Done)</option>
              <option value="enterprise">Enterprise Compliance (Backlog ➔ Spec ➔ Dev ➔ Review ➔ QA ➔ Done)</option>
            </select>

            <button (click)="onApplyWorkflowTemplate()" class="btn btn-primary whitespace-nowrap">
              Áp dụng ngay
            </button>
          </div>
        </div>
      </div>

      <!-- 2. Manage Statuses in Project -->
      <div class="card">
        <div class="status-section-header flex items-center justify-between mb-3">
          <h3 style="font-family: Arial, sans-serif;">Các trạng thái hiện tại ({{ statuses().length }} bước quy trình)</h3>
          <span class="text-xs font-normal text-slate-500">Tự động đồng bộ với Backlog và Scrum Board</span>
        </div>

        <div class="status-tags mb-4">
          @for (s of statuses(); track s.id) {
            <div class="status-badge-item flex items-center gap-2 px-3 py-1.5 rounded-xl border bg-slate-50 border-slate-200 shadow-2xs">
              <span class="w-2.5 h-2.5 rounded-full"
                [class.bg-slate-400]="s.category === 'To Do'"
                [class.bg-blue-500]="s.category === 'In Progress'"
                [class.bg-emerald-500]="s.category === 'Done'"
              ></span>
              <span class="font-semibold text-sm text-slate-800">{{ s.name }}</span>
              <span class="text-xs text-slate-400">({{ s.category }})</span>
              
              @if (!s.isInitial) {
                <button (click)="onDeleteStatus(s)" class="text-red-500 hover:text-red-700 ml-1 text-xs font-bold" title="Xóa trạng thái này">✕</button>
              }
            </div>
          }
        </div>

        <!-- Add New Status Form -->
        <div class="status-create-row flex items-center gap-2.5 pt-3 border-t border-slate-100">
          <input
            type="text"
            [(ngModel)]="newStatusName"
            placeholder="Tên trạng thái mới (VD: QA Testing, Ready for Deploy...)"
            class="form-control text-sm flex-1"
          />
          <select [(ngModel)]="newStatusCategory" class="form-control text-sm w-44">
            <option value="To Do">Phân loại: To Do</option>
            <option value="In Progress">Phân loại: In Progress</option>
            <option value="Done">Phân loại: Done</option>
          </select>
          <button (click)="onAddStatus()" [disabled]="!newStatusName.trim()" class="btn btn-secondary text-sm">
            + Thêm Trạng Thái
          </button>
        </div>
      </div>

      <div class="workflow-grid">
        <!-- Add Transition Form -->
        <div class="card form-box">
          <h3 style="font-family: Arial, sans-serif;">Thêm Transition mới</h3>
          <p class="hint">Định nghĩa một đường đi cho phép Issue di chuyển từ trạng thái nguồn sang trạng thái đích.</p>

          <form (ngSubmit)="onCreateTransition()">
            <div class="form-group">
              <label for="fromStatus">Từ trạng thái (From Status) <span class="required">*</span></label>
              <select
                id="fromStatus"
                name="fromStatus"
                [(ngModel)]="newTransition.fromStatusId"
                required
                class="form-control"
              >
                <option value="" disabled>-- Chọn trạng thái nguồn --</option>
                @for (s of statuses(); track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              </select>
            </div>

            <div class="form-group">
              <label for="toStatus">Đến trạng thái (To Status) <span class="required">*</span></label>
              <select
                id="toStatus"
                name="toStatus"
                [(ngModel)]="newTransition.toStatusId"
                required
                class="form-control"
              >
                <option value="" disabled>-- Chọn trạng thái đích --</option>
                @for (s of statuses(); track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              </select>
            </div>

            <div class="form-group">
              <label for="transName">Tên hành động / Tên nút chuyển</label>
              <input
                type="text"
                id="transName"
                name="transName"
                [(ngModel)]="newTransition.name"
                placeholder="VD: Start Work, Submit Review, Resolve..."
                class="form-control"
              />
            </div>

            <div class="form-group">
              <label for="permission">Quyền bắt buộc (Permission Code)</label>
              <select
                id="permission"
                name="permission"
                [(ngModel)]="newTransition.requiredPermissionCode"
                class="form-control"
              >
                <option value="">(Không yêu cầu - mọi thành viên đều chuyển được)</option>
                <option value="issue:transition">issue:transition (Quyền chuyển trạng thái chuẩn)</option>
                <option value="issue:resolve">issue:resolve (Chỉ Lead/Senior được duyệt/Done)</option>
                <option value="issue:close">issue:close (Quyền đóng Issue)</option>
              </select>
              <small class="form-hint">Nếu chọn, chỉ user có quyền này mới thực hiện được transition.</small>
            </div>

            <button
              type="submit"
              [disabled]="submitting() || !newTransition.fromStatusId || !newTransition.toStatusId"
              class="btn btn-primary btn-block"
            >
              {{ submitting() ? 'Đang thêm...' : '+ Tạo Transition' }}
            </button>
          </form>
        </div>

        <!-- Transitions List -->
        <div class="card list-box">
          <div class="list-header">
            <h3 style="font-family: Arial, sans-serif;">Danh sách Transition hiện hành ({{ transitions().length }})</h3>
            <button (click)="loadWorkflowData()" class="btn btn-sm btn-secondary">Làm mới</button>
          </div>

          @if (loading()) {
            <div class="loading-state">
              <div class="spinner"></div>
              <p>Đang tải luật chuyển trạng thái...</p>
            </div>
          } @else if (transitions().length === 0) {
            <div class="empty-transitions">
              <p>Chưa có luật chuyển đổi trạng thái nào.</p>
            </div>
          } @else {
            <div class="transitions-table-wrap">
              <table class="transitions-table">
                <thead>
                  <tr>
                    <th>Nguồn (From)</th>
                    <th>➔</th>
                    <th>Đích (To)</th>
                    <th>Tên hành động</th>
                    <th>Quyền yêu cầu</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  @for (t of transitions(); track t.id) {
                    <tr>
                      <td>
                        <span class="status-pill pill-from">{{ t.fromStatusName }}</span>
                      </td>
                      <td class="arrow-cell">➔</td>
                      <td>
                        <span class="status-pill pill-to">{{ t.toStatusName }}</span>
                      </td>
                      <td>
                        <strong>{{ t.name || 'Chuyển trạng thái' }}</strong>
                      </td>
                      <td>
                        @if (t.requiredPermissionCode) {
                          <code class="perm-code">{{ t.requiredPermissionCode }}</code>
                        } @else {
                          <span class="perm-none">Mọi người</span>
                        }
                      </td>
                      <td>
                        <button
                          (click)="onDeleteTransition(t)"
                          class="btn-icon-delete"
                          title="Xóa luật này"
                        >
                          ✕
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .workflow-container {
      padding: 2rem;
      max-width: 1100px;
      margin: 0 auto;
    }
    .breadcrumb {
      font-size: 0.85rem;
      color: #64748b;
      margin-bottom: 1rem;
    }
    .breadcrumb a {
      color: #2563eb;
      text-decoration: none;
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
      margin: 1.5rem 0;
    }
    .tab-item {
      padding: 0.75rem 1.25rem;
      font-weight: 600;
      font-size: 0.95rem;
      color: #64748b;
      text-decoration: none;
      border-bottom: 2px solid transparent;
      margin-bottom: -2px;
      cursor: pointer;
    }
    .tab-item:hover {
      color: #2563eb;
    }
    .tab-item.active {
      color: #2563eb;
      border-bottom-color: #2563eb;
    }
    .card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.5rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      margin-bottom: 1.5rem;
    }
    h3 {
      font-size: 1.1rem;
      font-weight: 600;
      color: #0f172a;
      margin: 0 0 0.5rem 0;
    }
    .hint {
      color: #64748b;
      font-size: 0.85rem;
      margin: 0 0 1.25rem 0;
    }
    .status-tags {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      margin-top: 0.75rem;
    }
    .status-badge {
      background: #f1f5f9;
      border: 1px solid #cbd5e1;
      padding: 0.35rem 0.75rem;
      border-radius: 20px;
      font-size: 0.85rem;
      font-weight: 600;
      color: #334155;
    }
    .badge-initial {
      background: #e0f2fe;
      border-color: #7dd3fc;
      color: #0369a1;
    }
    .workflow-grid {
      display: grid;
      grid-template-columns: 360px 1fr;
      gap: 1.5rem;
    }
    .form-group {
      margin-bottom: 1rem;
    }
    label {
      display: block;
      font-weight: 600;
      color: #334155;
      font-size: 0.85rem;
      margin-bottom: 0.3rem;
    }
    .required {
      color: #ef4444;
    }
    .form-control {
      width: 100%;
      padding: 0.6rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      font-size: 0.9rem;
      box-sizing: border-box;
    }
    .form-control:focus {
      outline: none;
      border-color: #2563eb;
      box-shadow: 0 0 0 3px rgba(37,99,235,0.15);
    }
    .form-hint {
      display: block;
      color: #64748b;
      font-size: 0.75rem;
      margin-top: 0.25rem;
    }
    .btn {
      padding: 0.6rem 1.2rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.9rem;
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 0.2s;
    }
    .btn-primary {
      background: #2563eb;
      color: #ffffff;
    }
    .btn-primary:hover:not(:disabled) {
      background: #1d4ed8;
    }
    .btn-primary:disabled {
      background: #94a3b8;
      cursor: not-allowed;
    }
    .btn-block {
      width: 100%;
      margin-top: 0.5rem;
    }
    .btn-secondary {
      background: #f1f5f9;
      color: #334155;
      border-color: #cbd5e1;
    }
    .btn-sm {
      padding: 0.35rem 0.75rem;
      font-size: 0.8rem;
    }
    .list-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
    }
    .transitions-table-wrap {
      overflow-x: auto;
    }
    .transitions-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.9rem;
    }
    .transitions-table th, .transitions-table td {
      padding: 0.75rem;
      border-bottom: 1px solid #f1f5f9;
      text-align: left;
    }
    .transitions-table th {
      color: #64748b;
      font-weight: 600;
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    .status-pill {
      display: inline-block;
      padding: 0.25rem 0.65rem;
      border-radius: 12px;
      font-weight: 600;
      font-size: 0.8rem;
    }
    .pill-from {
      background: #f1f5f9;
      color: #334155;
    }
    .pill-to {
      background: #dbeafe;
      color: #1d4ed8;
    }
    .arrow-cell {
      color: #94a3b8;
      font-size: 1.1rem;
      text-align: center;
    }
    .perm-code {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      padding: 0.2rem 0.4rem;
      border-radius: 4px;
      font-size: 0.8rem;
      color: #d97706;
    }
    .perm-none {
      color: #94a3b8;
      font-style: italic;
      font-size: 0.85rem;
    }
    .btn-icon-delete {
      background: transparent;
      border: none;
      color: #ef4444;
      font-size: 1rem;
      cursor: pointer;
      padding: 0.3rem 0.6rem;
      border-radius: 4px;
    }
    .btn-icon-delete:hover {
      background: #fee2e2;
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
    .alert-success {
      background: #f0fdf4;
      border: 1px solid #bbf7d0;
      color: #166534;
    }
    .spinner {
      width: 32px;
      height: 32px;
      border: 3px solid #e2e8f0;
      border-top-color: #2563eb;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 0.75rem auto;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    .loading-state, .empty-transitions {
      text-align: center;
      padding: 2rem;
      color: #64748b;
    }

    @media (max-width: 1279px) {
      .workflow-container {
        width: 100%;
        max-width: 100%;
        min-width: 0;
        padding: 1rem;
      }
      .workflow-container *,
      .card,
      .list-box {
        min-width: 0;
      }
      .settings-tabs {
        overflow-x: auto;
        overscroll-behavior-inline: contain;
        scroll-snap-type: x proximity;
        scrollbar-width: thin;
      }
      .tab-item {
        flex: 0 0 auto;
        scroll-snap-align: start;
      }
      .workflow-template-layout {
        display: grid;
        grid-template-columns: minmax(0, 1fr);
      }
      .workflow-template-actions {
        width: 100%;
        flex-wrap: wrap;
      }
      .workflow-template-actions .form-control {
        flex: 1 1 420px;
      }
      .status-section-header,
      .status-create-row {
        flex-wrap: wrap;
      }
      .status-create-row > input {
        flex: 1 1 320px;
      }
      .workflow-grid {
        grid-template-columns: minmax(0, 1fr);
      }
      .transitions-table-wrap {
        width: 100%;
        max-width: 100%;
        overflow-x: auto;
        overscroll-behavior-inline: contain;
      }
      .transitions-table {
        min-width: 720px;
      }
      .workflow-container button {
        min-height: 40px;
      }
    }

    @media (max-width: 767px) {
      .workflow-container { padding: 0; }
      h1 { font-size: 1.35rem; line-height: 1.08; }
      .subtitle { line-height: 1.45; }
      .settings-tabs { gap: 0; margin: 1rem 0; }
      .tab-item { padding: .75rem .9rem; font-size: .875rem; }
      .card { padding: 1rem; margin-bottom: 1rem; }
      .workflow-template-actions {
        align-items: stretch;
        flex-direction: column;
      }
      .workflow-template-actions .form-control,
      .workflow-template-actions .btn {
        flex-basis: auto;
        width: 100%;
      }
      .status-section-header {
        align-items: flex-start;
        flex-direction: column;
        gap: .35rem;
      }
      .status-create-row {
        align-items: stretch;
        flex-direction: column;
      }
      .status-create-row > * { width: 100% !important; flex-basis: auto !important; }
      .list-header { align-items: flex-start; flex-wrap: wrap; gap: .75rem; }
      .btn { min-height: 42px; }
    }

    @media (max-width: 932px) and (orientation: landscape) and (max-height: 520px) {
      .workflow-container { padding-block: .5rem; }
      .card { padding: 1rem; }
      .workflow-template-actions { align-items: stretch; }
      .workflow-template-actions .btn { min-height: 42px; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkflowSettingsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly projectService = inject(ProjectManagementService);
  private readonly confirmService = inject(ConfirmDialogService);
  private readonly toastService = inject(ToastService);

  projectId = '';
  readonly project = signal<Project | null>(null);
  readonly statuses = signal<ProjectStatus[]>([]);
  readonly transitions = signal<WorkflowTransition[]>([]);
  readonly loading = signal<boolean>(true);
  readonly submitting = signal<boolean>(false);
  readonly alertMessage = signal<string | null>(null);
  readonly alertType = signal<'success' | 'error'>('success');

  newTransition: CreateWorkflowTransitionRequest = {
    fromStatusId: '',
    toStatusId: '',
    name: '',
    requiredPermissionCode: ''
  };

  selectedTemplate = signal<string>('scrum-std');
  newStatusName = '';
  newStatusCategory = 'In Progress';

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id') || '';
    if (this.projectId) {
      this.loadProject();
      this.loadWorkflowData();
    }
  }

  loadProject(): void {
    this.projectService.getProjectById(this.projectId).subscribe(p => this.project.set(p));
  }

  loadWorkflowData(): void {
    this.loading.set(true);
    this.alertMessage.set(null);

    this.projectService.getProjectStatuses(this.projectId).subscribe({
      next: (statusList) => this.statuses.set(statusList)
    });

    this.projectService.getWorkflowTransitions(this.projectId).subscribe({
      next: (list) => {
        this.transitions.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onApplyWorkflowTemplate(): void {
    const updatedStatuses = this.projectService.applyWorkflowTemplateToProject(this.selectedTemplate());
    this.statuses.set(updatedStatuses);
    this.alertType.set('success');
    this.alertMessage.set(`Đã áp dụng quy trình mẫu thành công! Dự án hiện có ${updatedStatuses.length} trạng thái quy trình.`);
    this.toastService.success('Quy Trình Mới', `Đã cập nhật quy trình dự án với ${updatedStatuses.length} bước.`);
  }

  onAddStatus(): void {
    if (!this.newStatusName.trim()) return;
    const added = this.projectService.addProjectStatus(this.newStatusName.trim(), this.newStatusCategory);
    this.statuses.set(this.projectService.projectStatuses());
    this.toastService.success('Thêm Trạng Thái', `Đã thêm bước quy trình "${added.name}"`);
    this.newStatusName = '';
  }

  onDeleteStatus(status: ProjectStatus): void {
    this.confirmService.confirm({
      title: 'Xóa Bước Quy Trình',
      message: `Bạn có chắc muốn xóa trạng thái "${status.name}" khỏi quy trình dự án không?`,
      type: 'warning',
      confirmText: 'Xóa trạng thái',
      cancelText: 'Hủy bỏ',
      onConfirm: () => {
        this.projectService.deleteProjectStatus(status.id);
        this.statuses.set(this.projectService.projectStatuses());
        this.toastService.warning('Đã Xóa Trạng Thái', `Đã xóa "${status.name}" khỏi quy trình.`);
      }
    });
  }

  onCreateTransition(): void {
    this.alertMessage.set(null);

    // 1. Chặn Self-transition ngay trên Client
    if (this.newTransition.fromStatusId === this.newTransition.toStatusId) {
      this.alertType.set('error');
      this.alertMessage.set('Lỗi: Trạng thái không thể tự chuyển sang chính nó (Self-transition bị chặn)!');
      this.toastService.error('Lỗi Workflow', 'Trạng thái không thể tự chuyển sang chính nó');
      return;
    }

    // 2. Chặn Duplicate Transition ngay trên Client
    const exists = this.transitions().some(
      t => t.fromStatusId === this.newTransition.fromStatusId && t.toStatusId === this.newTransition.toStatusId
    );
    if (exists) {
      this.alertType.set('error');
      this.alertMessage.set('Lỗi: Luật chuyển đổi giữa 2 trạng thái này đã tồn tại (Trùng lặp bị chặn)!');
      this.toastService.error('Lỗi Workflow', 'Luật chuyển đổi giữa 2 trạng thái này đã tồn tại');
      return;
    }

    this.submitting.set(true);

    this.projectService.createWorkflowTransition(this.projectId, this.newTransition).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.alertType.set('success');
        this.alertMessage.set(`Đã thêm thành công luật chuyển "${created.fromStatusName}" ➔ "${created.toStatusName}"!`);
        this.toastService.success('Thêm Rule Workflow', `Đã tạo luật chuyển "${created.fromStatusName}" ➔ "${created.toStatusName}"`);
        this.newTransition = { fromStatusId: '', toStatusId: '', name: '', requiredPermissionCode: '' };
        this.loadWorkflowData();
      },
      error: (err) => {
        this.submitting.set(false);
        this.alertType.set('error');
        const problem = err?.error;
        this.alertMessage.set(problem?.title || problem?.message || 'Không thể tạo transition. Vui lòng kiểm tra lại.');
        this.toastService.error('Lỗi', problem?.title || problem?.message || 'Không thể tạo transition.');
      }
    });
  }

  onDeleteTransition(t: WorkflowTransition): void {
    this.confirmService.confirm({
      title: 'Xóa Luật Workflow',
      message: `Bạn có chắc muốn xóa luật chuyển trạng thái từ "${t.fromStatusName}" sang "${t.toStatusName}" không?`,
      type: 'warning',
      confirmText: 'Xóa luật ngay',
      cancelText: 'Hủy bỏ',
      onConfirm: () => {
        this.projectService.deleteWorkflowTransition(t.id).subscribe({
          next: () => {
            this.alertType.set('success');
            this.alertMessage.set('Đã xóa luật chuyển trạng thái thành công.');
            this.toastService.warning('Đã Xóa Rule', `Đã xóa luật chuyển "${t.fromStatusName}" ➔ "${t.toStatusName}"`);
            this.loadWorkflowData();
          },
          error: (err) => {
            this.alertType.set('error');
            this.alertMessage.set(err?.error?.title || 'Không thể xóa transition.');
            this.toastService.error('Lỗi', err?.error?.title || 'Không thể xóa transition.');
          }
        });
      }
    });
  }
}
