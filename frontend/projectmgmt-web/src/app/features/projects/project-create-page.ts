import { forkJoin } from 'rxjs';
import { apiErrorMessage } from '../../core/http/api-error-message';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  CreateProjectRequest,
  Organization,
  ProjectManagementService,
  UserDisplayInfo
} from '../../core/services/project-management.service';
import { IdentityService } from '../../core/services/identity.service';
import { ToastService } from '../../core/services/toast.service';

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
          <!-- Decoupled Organization Row -->
          <div class="form-group">
            <div class="org-label-row">
              <label for="orgId">Tổ chức / Workspace <span class="required">*</span></label>
              <button
                type="button"
                (click)="openCreateOrgModal()"
                class="btn-text-action"
                title="Tạo tổ chức / workspace mới độc lập"
              >
                <span class="material-symbols-outlined text-[16px]">domain_add</span>
                <span>+ Tạo tổ chức mới</span>
              </button>
            </div>

            @if (organizations().length > 0) {
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
              <small class="form-hint">Dự án sẽ thuộc phạm vi quản lý và phân quyền của tổ chức này.</small>
            } @else {
              <div class="empty-org-notice">
                <span class="material-symbols-outlined notice-icon">corporate_fare</span>
                <div class="notice-text">
                  <strong>Chưa có tổ chức nào</strong>
                  <p>Hệ thống cần ít nhất một tổ chức làm không gian chứa dự án.</p>
                </div>
                <button type="button" class="btn btn-sm btn-primary" (click)="openCreateOrgModal()">
                  Tạo Tổ Chức Ngay
                </button>
              </div>
            }
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
              maxlength="200"
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
            <small class="form-hint">Chủ nhiệm dự án chịu trách nhiệm cao nhất về backlog, quy trình Scrum và phân bổ công việc.</small>
          </div>

          <!-- Thành viên ban đầu của dự án -->
          <div class="form-group members-section-group">
            <div class="members-header-row">
              <label>Thành viên ban đầu của dự án</label>
              <span class="member-count-badge">{{ selectedMemberIds().length }} đã chọn</span>
            </div>
            <p class="section-hint">
              Các nhân sự được chọn sẽ tự động được thêm vào dự án với vai trò <strong>Thành viên phát triển (Developer)</strong> ngay khi dự án được khởi tạo.
            </p>
            @if (candidateMembers().length > 0) {
              <div class="members-grid">
                @for (user of candidateMembers(); track user.userId) {
                  <label class="member-card" [class.selected]="isMemberSelected(user.userId)">
                    <input
                      type="checkbox"
                      [checked]="isMemberSelected(user.userId)"
                      (change)="toggleMember(user.userId)"
                      class="member-checkbox"
                    />
                    <div class="member-avatar">
                      {{ user.displayName.slice(0, 1).toUpperCase() }}
                    </div>
                    <div class="member-meta">
                      <span class="member-name">{{ user.displayName }}</span>
                    </div>
                  </label>
                }
              </div>
            } @else {
              <div class="empty-members-hint">Không có thành viên phụ khác để chọn.</div>
            }
          </div>

          <div class="form-actions">
            <a routerLink="/projects" class="btn btn-secondary">Hủy bỏ</a>
            <button
              type="submit"
              [disabled]="loadingInitial() || submitting() || !formData.name.trim() || !formData.projectKey || !formData.orgId"
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

      <!-- Dedicated Separate Modal for Creating Organization -->
      @if (isCreateOrgModalOpen()) {
        <div class="modal-backdrop" (click)="closeCreateOrgModal()">
          <div class="modal-card" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <div class="modal-header-title">
                <span class="material-symbols-outlined text-primary">domain_add</span>
                <h3>Tạo Không Gian Tổ Chức Mới</h3>
              </div>
              <button type="button" class="btn-close flex items-center justify-center" (click)="closeCreateOrgModal()" title="Đóng modal">
                <span class="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <div class="modal-body">
              <p class="modal-desc">
                Khởi tạo không gian tổ chức (Tenant / Workspace) độc lập để lưu trữ và quản lý các dự án phần mềm.
              </p>

              @if (orgError()) {
                <div class="alert alert-error">
                  {{ orgError() }}
                </div>
              }

              <div class="form-group">
                <label for="modalOrgName">Tên tổ chức / Đơn vị <span class="required">*</span></label>
                <input
                  id="modalOrgName"
                  type="text"
                  [(ngModel)]="newOrgName"
                  (ngModelChange)="onOrgNameChange($event)"
                  maxlength="200"
                  class="form-control"
                  placeholder="VD: Tập đoàn Công nghệ Số hoặc Chi nhánh Hà Nội"
                  required
                />
              </div>

              <div class="form-group">
                <label for="modalOrgSlug">Mã định danh (Slug) <span class="required">*</span></label>
                <input
                  id="modalOrgSlug"
                  type="text"
                  [(ngModel)]="newOrgSlug"
                  maxlength="80"
                  class="form-control"
                  placeholder="VD: tap-doan-cong-nghe-so"
                  required
                />
                <small class="form-hint">Dùng ký tự chữ thường, số, dấu gạch ngang (không dấu, không khoảng trắng).</small>
              </div>

              <!-- Thành viên ban đầu của tổ chức -->
              <div class="form-group">
                <div class="members-header-row">
                  <label>Thành viên ban đầu của tổ chức</label>
                  <span class="member-count-badge">{{ selectedOrgMemberIds().length }} đã chọn</span>
                </div>
                <small class="form-hint">Các nhân sự được chọn sẽ có quyền truy cập và cộng tác trong không gian tổ chức này.</small>
                <div class="org-members-grid">
                  @for (user of users(); track user.userId) {
                    <label class="member-card member-card-sm" [class.selected]="isOrgMemberSelected(user.userId)">
                      <input
                        type="checkbox"
                        [checked]="isOrgMemberSelected(user.userId)"
                        (change)="toggleOrgMember(user.userId)"
                        class="member-checkbox"
                      />
                      <div class="member-avatar member-avatar-sm">
                        {{ user.displayName.slice(0, 1).toUpperCase() }}
                      </div>
                      <span class="member-name">{{ user.displayName }}</span>
                    </label>
                  }
                </div>
              </div>
            </div>

            <div class="modal-footer">
              <button type="button" class="btn btn-secondary" (click)="closeCreateOrgModal()">Hủy bỏ</button>
              <button
                type="button"
                class="btn btn-primary"
                [disabled]="creatingOrg() || !newOrgName.trim() || !newOrgSlug.trim()"
                (click)="submitCreateOrg()"
              >
                @if (creatingOrg()) {
                  <span class="btn-spinner"></span> Đang tạo...
                } @else {
                  Tạo Tổ Chức
                }
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .create-project-container {
      padding: 2rem;
      max-width: 700px;
      margin: 0 auto;
    }
    .org-label-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.35rem;
    }
    .btn-text-action {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      font-size: 0.85rem;
      font-weight: 600;
      color: #1b4332;
      background: none;
      border: none;
      cursor: pointer;
      padding: 0.2rem 0.4rem;
      border-radius: 6px;
      transition: background 0.15s;
    }
    .btn-text-action:hover {
      background: #e8f5e9;
      color: #2d6a4f;
    }
    .empty-org-notice {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 1rem 1.25rem;
      border-radius: 10px;
      background: #f8fafc;
      border: 1px dashed #cbd5e1;
      margin-top: 0.25rem;
    }
    .notice-icon {
      font-size: 2rem;
      color: #64748b;
    }
    .notice-text {
      flex: 1;
    }
    .notice-text strong {
      display: block;
      color: #1e293b;
      font-size: 0.9rem;
    }
    .notice-text p {
      margin: 0.2rem 0 0;
      color: #64748b;
      font-size: 0.8rem;
    }
    .breadcrumb {
      font-size: 0.9rem;
      color: #64748b;
      margin-bottom: 1rem;
    }
    .breadcrumb a {
      color: #1b4332;
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
      border-color: #2d6a4f;
      box-shadow: 0 0 0 3px rgba(45, 106, 79, 0.15);
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
      background: #1b4332;
      color: #ffffff;
    }
    .btn-primary:hover:not(:disabled) {
      background: #2d6a4f;
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
    .btn-sm {
      padding: 0.4rem 0.9rem;
      font-size: 0.85rem;
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

    /* Modal Styles */
    .modal-backdrop {
      position: fixed;
      top: 0;
      left: 0;
      width: 100vw;
      height: 100vh;
      background: rgba(15, 23, 42, 0.6);
      backdrop-filter: blur(4px);
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
    }
    .modal-card {
      background: #ffffff;
      width: 100%;
      max-width: 520px;
      border-radius: 16px;
      box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
      border: 1px solid #e2e8f0;
      animation: modalIn 0.2s cubic-bezier(0.16, 1, 0.3, 1);
      overflow: hidden;
    }
    @keyframes modalIn {
      from { opacity: 0; transform: scale(0.96) translateY(8px); }
      to { opacity: 1; transform: scale(1) translateY(0); }
    }
    .modal-header {
      padding: 1.25rem 1.5rem;
      border-bottom: 1px solid #f1f5f9;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .modal-header-title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .modal-header-title h3 {
      font-size: 1.15rem;
      font-weight: 700;
      color: #0f172a;
      margin: 0;
    }
    .btn-close {
      background: none;
      border: none;
      color: #94a3b8;
      font-size: 1.2rem;
      cursor: pointer;
      padding: 0.25rem;
      line-height: 1;
      border-radius: 6px;
    }
    .btn-close:hover {
      color: #1e293b;
      background: #f1f5f9;
    }
    .modal-body {
      padding: 1.5rem;
    }
    .modal-desc {
      font-size: 0.875rem;
      color: #64748b;
      margin: 0 0 1.25rem 0;
      line-height: 1.4;
    }
    .members-section-group {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
    }
    .members-header-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.25rem;
    }
    .members-header-row label {
      margin-bottom: 0;
      font-weight: 600;
      color: #1e293b;
    }
    .member-count-badge {
      font-size: 0.75rem;
      font-weight: 600;
      background: #e8f5e9;
      color: #1b4332;
      padding: 0.2rem 0.6rem;
      border-radius: 9999px;
      border: 1px solid #c8e6c9;
    }
    .section-hint {
      font-size: 0.8rem;
      color: #64748b;
      margin-bottom: 0.75rem;
    }
    .members-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 0.5rem;
    }
    .org-members-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 0.4rem;
      max-height: 160px;
      overflow-y: auto;
      padding: 0.25rem;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
    }
    .member-card {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.6rem 0.8rem;
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      cursor: pointer;
      transition: all 0.15s ease-in-out;
      user-select: none;
    }
    .member-card:hover {
      border-color: #2d6a4f;
      background: #f0fdf4;
    }
    .member-card.selected {
      border-color: #1b4332;
      background: #ecfdf5;
      box-shadow: 0 0 0 1px #1b4332;
    }
    .member-card-sm {
      padding: 0.35rem 0.5rem;
      gap: 0.45rem;
    }
    .member-checkbox {
      accent-color: #1b4332;
      cursor: pointer;
      width: 16px;
      height: 16px;
    }
    .member-avatar {
      width: 28px;
      height: 28px;
      border-radius: 50%;
      background: #1b4332;
      color: #ffffff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.75rem;
      font-weight: 700;
      flex-shrink: 0;
    }
    .member-avatar-sm {
      width: 22px;
      height: 22px;
      font-size: 0.65rem;
    }
    .member-meta {
      flex: 1;
      min-width: 0;
    }
    .member-name {
      display: block;
      font-size: 0.85rem;
      font-weight: 500;
      color: #1e293b;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .empty-members-hint {
      font-size: 0.85rem;
      color: #94a3b8;
      font-style: italic;
    }

    @media (max-width: 1279px) {
      .create-project-container { width: 100%; max-width: 700px; min-width: 0; padding: 1rem; }
      .form-card, form { min-width: 0; }
    }

    @media (max-width: 767px) {
      .create-project-container { padding: 0; }
      .form-card { padding: 1rem; }
      h1 { font-size: 1.35rem; }
      .form-actions { align-items: stretch; flex-direction: column-reverse; }
      .form-actions .btn { justify-content: center; min-height: 44px; width: 100%; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectCreatePage implements OnInit {
  private readonly projectService = inject(ProjectManagementService);
  private readonly identity = inject(IdentityService);
  private readonly toastService = inject(ToastService);
  private readonly router = inject(Router);

  readonly organizations = signal<Organization[]>([]);
  readonly users = signal<UserDisplayInfo[]>([]);
  readonly submitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly loadingInitial = signal(true);

  // Dedicated Organization Modal State
  readonly isCreateOrgModalOpen = signal(false);
  readonly creatingOrg = signal(false);
  readonly orgError = signal<string | null>(null);
  newOrgName = '';
  newOrgSlug = '';

  // Member Selection State for Project & Organization
  readonly selectedMemberIds = signal<string[]>([]);
  readonly selectedOrgMemberIds = signal<string[]>([]);

  readonly candidateMembers = computed(() => {
    const currentLead = this.formData.leadUserId;
    return this.users().filter(u => u.userId !== currentLead);
  });

  isMemberSelected(userId: string): boolean {
    return this.selectedMemberIds().includes(userId);
  }

  toggleMember(userId: string): void {
    this.selectedMemberIds.update(ids =>
      ids.includes(userId) ? ids.filter(id => id !== userId) : [...ids, userId]
    );
  }

  isOrgMemberSelected(userId: string): boolean {
    return this.selectedOrgMemberIds().includes(userId);
  }

  toggleOrgMember(userId: string): void {
    this.selectedOrgMemberIds.update(ids =>
      ids.includes(userId) ? ids.filter(id => id !== userId) : [...ids, userId]
    );
  }

  formData: CreateProjectRequest = {
    orgId: '',
    name: '',
    projectKey: '',
    description: '',
    leadUserId: '',
    memberUserIds: []
  };

  ngOnInit(): void {
    this.loadInitialData();
  }

  loadInitialData(): void {
    this.loadingInitial.set(true);
    forkJoin({
      organizations: this.projectService.getOrganizations(),
      users: this.projectService.getUsers()
    }).subscribe({
      next: data => {
        this.organizations.set(data.organizations);
        this.users.set(data.users);
        const currentUserId = this.identity.authState().currentUser?.id;
        const defaultLead = (currentUserId && data.users.some(u => u.userId === currentUserId))
          ? currentUserId
          : (data.users[0]?.userId ?? currentUserId ?? '');
        this.formData.leadUserId = defaultLead;

        if (data.organizations.length > 0) {
          this.formData.orgId = data.organizations[0].id;
        }
        this.loadingInitial.set(false);
        if (!data.users.length && !currentUserId) {
          this.errorMessage.set('Chưa có tài khoản người dùng hoạt động. Vui lòng đăng nhập lại.');
        }
      },
      error: err => {
        this.loadingInitial.set(false);
        this.errorMessage.set(apiErrorMessage(err, 'Không thể tải tổ chức và người phụ trách.'));
      }
    });
  }

  openCreateOrgModal(): void {
    this.newOrgName = '';
    this.newOrgSlug = '';
    this.orgError.set(null);
    this.selectedOrgMemberIds.set([]);
    this.isCreateOrgModalOpen.set(true);
  }

  closeCreateOrgModal(): void {
    if (this.creatingOrg()) return;
    this.isCreateOrgModalOpen.set(false);
  }

  onOrgNameChange(name: string): void {
    if (!name) return;
    const normalized = name
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/đ/g, 'd')
      .replace(/Đ/g, 'D')
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9\s-]/g, '')
      .replace(/\s+/g, '-');
    this.newOrgSlug = normalized.slice(0, 50);
  }

  submitCreateOrg(): void {
    if (this.creatingOrg() || !this.newOrgName.trim() || !this.newOrgSlug.trim()) return;
    this.creatingOrg.set(true);
    this.orgError.set(null);

    const ownerId = this.identity.authState().currentUser?.id || this.formData.leadUserId;
    this.projectService.createOrganization({
      name: this.newOrgName.trim(),
      slug: this.newOrgSlug.trim().toLowerCase(),
      ownerId: ownerId || undefined,
      memberUserIds: this.selectedOrgMemberIds().length > 0 ? this.selectedOrgMemberIds() : undefined
    }).subscribe({
      next: (createdOrg) => {
        this.organizations.update(orgs => [...orgs, createdOrg]);
        this.formData.orgId = createdOrg.id;
        this.creatingOrg.set(false);
        this.isCreateOrgModalOpen.set(false);
        this.toastService.success(
          'Tạo tổ chức thành công',
          `Không gian "${createdOrg.name}" (${createdOrg.slug}) đã sẵn sàng!`
        );
      },
      error: (err) => {
        this.creatingOrg.set(false);
        this.orgError.set(apiErrorMessage(err, 'Không thể tạo tổ chức mới.'));
      }
    });
  }

  onNameChange(name: string): void {
    if (!this.formData.projectKey && name) {
      const normalized = name.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D');
      const words = normalized.trim().split(/\s+/).filter(w => w.length > 0);
      let autoKey: string;
      if (words.length === 1) {
        autoKey = words[0].slice(0, 4).toUpperCase();
      } else {
        autoKey = words.slice(0, 4).map(w => w[0]).join('').toUpperCase();
      }
      autoKey = autoKey.replace(/[^A-Z0-9]/g, '');
      if (/^[A-Z]/.test(autoKey) && autoKey.length >= 2) {
        this.formData.projectKey = autoKey.slice(0, 6);
      }
    }
  }

  onKeyInput(value: string): void {
    this.formData.projectKey = (value || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
  }

  onSubmit(): void {
    if (this.submitting() || this.loadingInitial()) return;
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (!this.formData.orgId) {
      this.errorMessage.set('Vui lòng chọn hoặc tạo một tổ chức trước khi tạo dự án.');
      return;
    }

    if (!this.formData.leadUserId) {
      this.formData.leadUserId = this.identity.authState().currentUser?.id ?? '';
    }

    if (!this.formData.name.trim()) {
      this.errorMessage.set('Vui lòng nhập tên dự án.');
      return;
    }

    const keyRegex = /^[A-Z][A-Z0-9]{1,9}$/;
    if (!keyRegex.test(this.formData.projectKey)) {
      this.errorMessage.set('Project Key không hợp lệ! Bắt buộc phải bắt đầu bằng chữ cái in hoa và có 2-10 ký tự chữ hoặc số.');
      return;
    }

    this.formData.memberUserIds = this.selectedMemberIds();
    this.submitting.set(true);
    this.executeCreateProject();
  }

  private executeCreateProject(): void {
    this.projectService.createProject(this.formData).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.successMessage.set(`Dự án "${created.name}" đã được tạo thành công kèm cấu hình mặc định!`);
        this.projectService.selectProject(created);
        this.toastService.success('Khởi tạo thành công', `Dự án "${created.name}" (${created.projectKey}) đã sẵn sàng hoạt động!`);
        setTimeout(() => {
          this.router.navigate(['/board']);
        }, 500);
      },
      error: (err) => {
        this.submitting.set(false);
        const problem = { title: apiErrorMessage(err, 'Yêu cầu không thành công.') };
        const msg = problem.title || 'Không thể tạo dự án. Vui lòng kiểm tra lại.';
        this.errorMessage.set(msg);
      }
    });
  }
}
