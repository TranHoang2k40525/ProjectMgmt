import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive } from '@angular/router';
import {
  Project,
  ProjectManagementService,
  UpdateProjectRequest,
  UserDisplayInfo
} from '../../core/services/project-management.service';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-project-settings-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, RouterLinkActive],
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
          <p class="subtitle">Quản lý thông tin chung, quy trình trạng thái (Workflow) và thiết lập Bảng (Board)</p>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="settings-tabs">
        <a
          [routerLink]="['/projects', projectId, 'settings']"
          routerLinkActive="active"
          [routerLinkActiveOptions]="{ exact: true }"
          class="tab-item"
        >
          ⚙️ Thông tin chung
        </a>
        <a
          [routerLink]="['/projects', projectId, 'settings', 'workflow']"
          routerLinkActive="active"
          class="tab-item"
        >
          🔄 Cấu hình Workflow (Nhiệm vụ 2)
        </a>
        <a
          [routerLink]="['/projects', projectId, 'settings', 'board']"
          routerLinkActive="active"
          class="tab-item"
        >
          📋 Cấu hình Board (Nhiệm vụ 3)
        </a>
      </div>

      @if (loading()) {
        <div class="loading-box">
          <div class="spinner"></div>
          <p>Đang tải thông tin dự án...</p>
        </div>
      } @else if (error()) {
        <div class="alert alert-error">
          {{ error() }}
        </div>
      } @else if (project()) {
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
            <p>Xóa dự án này sẽ đánh dấu lưu trữ và ẩn khỏi danh sách làm việc.</p>
            <button (click)="onDeleteProject()" class="btn btn-danger">Xóa Dự Án Này</button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .settings-container {
      padding: 2rem;
      max-width: 900px;
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
      text-decoration: none;
      border-bottom: 2px solid transparent;
      margin-bottom: -2px;
      transition: all 0.2s;
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
      padding: 1.75rem;
      margin-bottom: 1.5rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    h2 {
      font-size: 1.2rem;
      font-weight: 600;
      color: #0f172a;
      margin: 0 0 1.25rem 0;
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
      border-color: #2563eb;
      box-shadow: 0 0 0 3px rgba(37,99,235,0.15);
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
      background: #2563eb;
      color: #ffffff;
    }
    .btn-primary:hover:not(:disabled) {
      background: #1d4ed8;
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
    .alert-success {
      background: #f0fdf4;
      border: 1px solid #bbf7d0;
      color: #166534;
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
  readonly project = signal<Project | null>(null);
  readonly users = signal<UserDisplayInfo[]>([]);
  readonly loading = signal<boolean>(true);
  readonly saving = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly feedbackMsg = signal<string | null>(null);
  readonly feedbackType = signal<'success' | 'error'>('success');

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
    }
  }

  loadUsers(): void {
    this.projectService.getUsers().subscribe(u => this.users.set(u));
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
        this.error.set(err?.error?.title || 'Không tìm thấy dự án.');
        this.loading.set(false);
      }
    });
  }

  onSaveGeneral(): void {
    this.saving.set(true);
    this.feedbackMsg.set(null);

    this.projectService.updateProject(this.projectId, this.editForm).subscribe({
      next: (updated) => {
        this.project.set(updated);
        this.saving.set(false);
        this.feedbackType.set('success');
        this.feedbackMsg.set('Đã lưu thay đổi thông tin dự án thành công!');
        this.toastService.success('Cập Nhật Dự Án', 'Đã lưu thay đổi cấu hình dự án thành công.');
      },
      error: (err) => {
        this.saving.set(false);
        this.feedbackType.set('error');
        this.feedbackMsg.set(err?.error?.title || 'Không thể lưu thay đổi.');
        this.toastService.error('Lỗi Cập Nhật', err?.error?.title || 'Không thể lưu thay đổi.');
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
            this.toastService.error('Lỗi Xóa Dự Án', err?.error?.title || 'Không thể xóa dự án.');
          }
        });
      }
    });
  }
}
