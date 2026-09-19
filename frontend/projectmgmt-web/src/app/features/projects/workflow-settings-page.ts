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

@Component({
  selector: 'app-workflow-settings-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="workflow-container">
      <div class="breadcrumb">
        <a routerLink="/projects">Dự án</a> &gt;
        @if (project()) {
          <a [routerLink]="['/projects', projectId, 'settings']">{{ project()?.name }}</a> &gt;
        }
        <span>Cấu hình Workflow</span>
      </div>

      <div class="page-header">
        <div>
          <h1>Cấu hình Workflow & Chuyển trạng thái</h1>
          <p class="subtitle">Thiết lập luật chuyển đổi trạng thái (From ➔ To) và quyền bắt buộc cho dự án {{ project()?.name }}</p>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="settings-tabs">
        <a [routerLink]="['/projects', projectId, 'settings']" class="tab-item">⚙️ Thông tin chung</a>
        <span class="tab-item active">🔄 Cấu hình Workflow</span>
        <a [routerLink]="['/projects', projectId, 'settings', 'board']" class="tab-item">📋 Cấu hình Board</a>
      </div>

      @if (alertMessage()) {
        <div class="alert" [class.alert-success]="alertType() === 'success'" [class.alert-error]="alertType() === 'error'">
          {{ alertMessage() }}
        </div>
      }

      <!-- Statuses overview -->
      <div class="card">
        <h3>Các trạng thái có sẵn trong dự án (Workflow Statuses)</h3>
        <div class="status-tags">
          @for (s of statuses(); track s.id) {
            <span class="status-badge" [class.badge-initial]="s.isInitial">
              {{ s.name }}
              @if (s.isInitial) {
                <small>(Khởi đầu)</small>
              }
            </span>
          }
        </div>
      </div>

      <div class="workflow-grid">
        <!-- Add Transition Form -->
        <div class="card form-box">
          <h3>Thêm Transition mới</h3>
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
            <h3>Danh sách Transition hiện hành ({{ transitions().length }})</h3>
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
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkflowSettingsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly projectService = inject(ProjectManagementService);

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

  onCreateTransition(): void {
    this.alertMessage.set(null);

    // 1. Chặn Self-transition ngay trên Client
    if (this.newTransition.fromStatusId === this.newTransition.toStatusId) {
      this.alertType.set('error');
      this.alertMessage.set('Lỗi: Trạng thái không thể tự chuyển sang chính nó (Self-transition bị chặn)!');
      return;
    }

    // 2. Chặn Duplicate Transition ngay trên Client
    const exists = this.transitions().some(
      t => t.fromStatusId === this.newTransition.fromStatusId && t.toStatusId === this.newTransition.toStatusId
    );
    if (exists) {
      this.alertType.set('error');
      this.alertMessage.set('Lỗi: Luật chuyển đổi giữa 2 trạng thái này đã tồn tại (Trùng lặp bị chặn)!');
      return;
    }

    this.submitting.set(true);

    this.projectService.createWorkflowTransition(this.projectId, this.newTransition).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.alertType.set('success');
        this.alertMessage.set(`Đã thêm thành công luật chuyển "${created.fromStatusName}" ➔ "${created.toStatusName}"!`);
        this.newTransition = { fromStatusId: '', toStatusId: '', name: '', requiredPermissionCode: '' };
        this.loadWorkflowData();
      },
      error: (err) => {
        this.submitting.set(false);
        this.alertType.set('error');
        const problem = err?.error;
        this.alertMessage.set(problem?.title || problem?.message || 'Không thể tạo transition. Vui lòng kiểm tra lại.');
      }
    });
  }

  onDeleteTransition(t: WorkflowTransition): void {
    if (confirm(`Bạn có chắc muốn xóa luật chuyển từ "${t.fromStatusName}" sang "${t.toStatusName}"?`)) {
      this.projectService.deleteWorkflowTransition(t.id).subscribe({
        next: () => {
          this.alertType.set('success');
          this.alertMessage.set('Đã xóa luật chuyển trạng thái thành công.');
          this.loadWorkflowData();
        },
        error: (err) => {
          this.alertType.set('error');
          this.alertMessage.set(err?.error?.title || 'Không thể xóa transition.');
        }
      });
    }
  }
}
