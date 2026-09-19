import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  CreateProjectRequest,
  Organization,
  ProjectManagementService,
  UserDisplayInfo
} from '../../core/services/project-management.service';

@Component({
  selector: 'app-project-create-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="create-project-container">
      <div class="breadcrumb">
        <a routerLink="/projects">Dự án</a> &gt; <span>Tạo dự án mới</span>
      </div>

      <div class="form-card">
        <div class="form-header">
          <h1>Tạo Project mới</h1>
          <p>Thiết lập dự án Scrum/Kanban mới kèm cấu hình Issue Type, Workflow và Board mặc định.</p>
        </div>

        @if (errorMessage()) {
          <div class="alert alert-error">
            <strong>Lỗi:</strong> {{ errorMessage() }}
          </div>
        }

        @if (successMessage()) {
          <div class="alert alert-success">
            {{ successMessage() }}
          </div>
        }

        <form (ngSubmit)="onSubmit()" #projectForm="ngForm">
          <div class="form-group">
            <label for="orgId">Organization / Tổ chức <span class="required">*</span></label>
            <select
              id="orgId"
              name="orgId"
              [(ngModel)]="formData.orgId"
              required
              class="form-control"
            >
              @for (org of organizations(); track org.id) {
                <option [value]="org.id">{{ org.name }} ({{ org.slug }})</option>
              }
            </select>
            <small class="form-hint">Dự án sẽ thuộc phạm vi của tổ chức này.</small>
          </div>

          <div class="form-group">
            <label for="name">Tên dự án <span class="required">*</span></label>
            <input
              type="text"
              id="name"
              name="name"
              [(ngModel)]="formData.name"
              (ngModelChange)="onNameChange($event)"
              placeholder="VD: E-Commerce Mobile App"
              required
              class="form-control"
            />
          </div>

          <div class="form-group">
            <label for="projectKey">Mã dự án (Project Key) <span class="required">*</span></label>
            <input
              type="text"
              id="projectKey"
              name="projectKey"
              [ngModel]="formData.projectKey"
              (ngModelChange)="onKeyInput($event)"
              placeholder="VD: ECOM, PROJ, APP1"
              required
              maxlength="10"
              class="form-control uppercase-input"
            />
            <small class="form-hint">
              2-10 ký tự chữ hoa/số, bắt đầu bằng chữ cái. Dùng làm tiền tố mã task (VD: {{ formData.projectKey || 'PROJ' }}-1).
            </small>
          </div>

          <div class="form-group">
            <label for="description">Mô tả dự án</label>
            <textarea
              id="description"
              name="description"
              [(ngModel)]="formData.description"
              rows="3"
              placeholder="Mục tiêu, phạm vi hoặc thông tin tóm tắt về dự án..."
              class="form-control"
            ></textarea>
          </div>

          <div class="form-group">
            <label for="leadUserId">Người phụ trách (Project Lead) <span class="required">*</span></label>
            <select
              id="leadUserId"
              name="leadUserId"
              [(ngModel)]="formData.leadUserId"
              required
              class="form-control"
            >
              @for (user of users(); track user.userId) {
                <option [value]="user.userId">{{ user.displayName }}</option>
              }
            </select>
          </div>

          <div class="form-actions">
            <a routerLink="/projects" class="btn btn-secondary">Hủy bỏ</a>
            <button
              type="submit"
              [disabled]="submitting() || !formData.name || !formData.projectKey"
              class="btn btn-primary"
            >
              @if (submitting()) {
                <span class="btn-spinner"></span> Đang tạo và cấu hình...
              } @else {
                Tạo Dự Án
              }
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .create-project-container {
      padding: 2rem;
      max-width: 700px;
      margin: 0 auto;
    }
    .breadcrumb {
      font-size: 0.9rem;
      color: #64748b;
      margin-bottom: 1rem;
    }
    .breadcrumb a {
      color: #2563eb;
      text-decoration: none;
    }
    .form-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 2rem;
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05);
    }
    .form-header {
      margin-bottom: 1.5rem;
      border-bottom: 1px solid #f1f5f9;
      padding-bottom: 1rem;
    }
    h1 {
      font-size: 1.5rem;
      font-weight: 700;
      color: #1e293b;
      margin: 0 0 0.25rem 0;
    }
    .form-header p {
      color: #64748b;
      margin: 0;
      font-size: 0.9rem;
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
      transition: border-color 0.2s, box-shadow 0.2s;
    }
    .form-control:focus {
      outline: none;
      border-color: #2563eb;
      box-shadow: 0 0 0 3px rgba(37,99,235,0.15);
    }
    .uppercase-input {
      text-transform: uppercase;
      font-family: monospace;
      font-weight: 600;
      letter-spacing: 0.05em;
    }
    .form-hint {
      display: block;
      color: #64748b;
      font-size: 0.8rem;
      margin-top: 0.35rem;
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
    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      margin-top: 2rem;
      padding-top: 1rem;
      border-top: 1px solid #f1f5f9;
    }
    .btn {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.65rem 1.4rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.95rem;
      cursor: pointer;
      text-decoration: none;
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
    .btn-secondary {
      background: #f1f5f9;
      color: #334155;
      border-color: #cbd5e1;
    }
    .btn-secondary:hover {
      background: #e2e8f0;
    }
    .btn-spinner {
      width: 14px;
      height: 14px;
      border: 2px solid #ffffff;
      border-top-color: transparent;
      border-radius: 50%;
      animation: spin 0.6s linear infinite;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectCreatePage implements OnInit {
  private readonly projectService = inject(ProjectManagementService);
  private readonly router = inject(Router);

  readonly organizations = signal<Organization[]>([]);
  readonly users = signal<UserDisplayInfo[]>([]);
  readonly submitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  formData: CreateProjectRequest = {
    orgId: '',
    name: '',
    projectKey: '',
    description: '',
    leadUserId: ''
  };

  ngOnInit(): void {
    this.loadInitialData();
  }

  loadInitialData(): void {
    this.projectService.getOrganizations().subscribe({
      next: (orgs) => {
        this.organizations.set(orgs);
        if (orgs.length > 0 && !this.formData.orgId) {
          this.formData.orgId = orgs[0].id;
        }
      }
    });

    this.projectService.getUsers().subscribe({
      next: (userList) => {
        this.users.set(userList);
        if (userList.length > 0 && !this.formData.leadUserId) {
          this.formData.leadUserId = userList[0].userId;
        }
      }
    });
  }

  onNameChange(name: string): void {
    // Tự động gợi ý Project Key từ Name nếu user chưa nhập key thủ công
    if (!this.formData.projectKey && name) {
      const words = name.trim().split(/\s+/);
      let autoKey: string;
      if (words.length === 1) {
        autoKey = words[0].slice(0, 4).toUpperCase();
      } else {
        autoKey = words.slice(0, 4).map(w => w[0]).join('').toUpperCase();
      }
      autoKey = autoKey.replace(/[^A-Z0-9]/g, '');
      if (autoKey.length >= 2) {
        this.formData.projectKey = autoKey;
      }
    }
  }

  onKeyInput(value: string): void {
    this.formData.projectKey = (value || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
  }

  onSubmit(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);

    // Client-side regex check
    const keyRegex = /^[A-Z][A-Z0-9]{1,9}$/;
    if (!keyRegex.test(this.formData.projectKey)) {
      this.errorMessage.set('Project Key không hợp lệ! Bắt buộc phải bắt đầu bằng chữ cái in hoa và có 2-10 ký tự chữ hoặc số.');
      return;
    }

    this.submitting.set(true);

    this.projectService.createProject(this.formData).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.successMessage.set(`Dự án "${created.name}" đã được tạo thành công kèm cấu hình mặc định!`);
        setTimeout(() => {
          this.router.navigate(['/projects', created.id, 'settings']);
        }, 800);
      },
      error: (err) => {
        this.submitting.set(false);
        const problem = err?.error;
        const msg = problem?.title || problem?.message || 'Không thể tạo dự án. Vui lòng kiểm tra lại.';
        this.errorMessage.set(msg);
      }
    });
  }
}
