import { apiErrorMessage } from '../../core/http/api-error-message';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, ElementRef, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { gsap } from 'gsap';
import { Organization, Project, ProjectManagementService } from '../../core/services/project-management.service';
import { IdentityService } from '../../core/services/identity.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-projects-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="projects-container">
      <div class="page-header">
        <div>
          <h1>Danh sách Dự án</h1>
          <p class="subtitle">Quản lý không gian làm việc Scrum, Kanban và các tổ chức độc lập</p>
        </div>
        <div class="header-actions">
          <button type="button" (click)="openCreateOrgModal()" class="btn btn-secondary">
            <span class="material-symbols-outlined text-[18px]">domain_add</span>
            + Tạo Tổ chức mới
          </button>
          <a routerLink="/projects/new" class="btn btn-primary">
            <span class="icon">+</span> Tạo Project mới
          </a>
        </div>
      </div>

      <!-- Filter Bar by Organization -->
      @if (organizations().length >= 1) {
        <div class="filter-bar">
          <span class="filter-label">Lọc theo Tổ chức:</span>
          <div class="org-pills">
            <button
              type="button"
              class="pill-btn"
              [class.active]="selectedOrgId() === 'all'"
              (click)="selectedOrgId.set('all')"
            >
              Tất cả tổ chức ({{ projects().length }})
            </button>
            @for (org of organizations(); track org.id) {
              <button
                type="button"
                class="pill-btn"
                [class.active]="selectedOrgId() === org.id"
                (click)="selectedOrgId.set(org.id)"
              >
                {{ org.name }}
              </button>
            }
          </div>
        </div>
      }

      @if (loading()) {
        <div class="loading-state">
          <div class="spinner"></div>
          <p>Đang tải danh sách dự án &amp; tổ chức...</p>
        </div>
      } @else if (error()) {
        <div class="error-alert">
          <p>{{ error() }}</p>
          <button (click)="loadData()" class="btn btn-secondary">Thử lại</button>
        </div>
      } @else if (filteredProjects().length === 0) {
        <div class="empty-state">
          <div class="empty-icon">
            <span class="material-symbols-outlined text-[48px] text-slate-400">folder_open</span>
          </div>
          <h3>Chưa có dự án nào trong phạm vi này</h3>
          <p>Hãy khởi tạo dự án đầu tiên để thiết lập Scrum board, Sprint và phân công task thông minh với AI.</p>
          <div class="empty-actions">
            <button (click)="openCreateOrgModal()" class="btn btn-secondary">+ Tạo Tổ chức</button>
            <a routerLink="/projects/new" class="btn btn-primary">Tạo Project ngay</a>
          </div>
        </div>
      } @else {
        <div class="project-grid">
          @for (project of filteredProjects(); track project.id) {
            <div class="project-card">
              <div class="card-header">
                <span class="project-key">{{ project.projectKey }}</span>
                <span class="org-tag" [title]="'Tổ chức: ' + getOrgName(project.orgId)">
                  {{ getOrgName(project.orgId) }}
                </span>
                <span class="badge" [class.badge-active]="!project.isArchived">
                  {{ project.isArchived ? 'Lưu trữ' : 'Đang hoạt động' }}
                </span>
              </div>
              <h2 class="project-name">{{ project.name }}</h2>
              <p class="project-desc">{{ project.description || 'Chưa có mô tả dự án.' }}</p>
              
              <div class="card-meta">
                <div class="meta-item">
                  <span class="label">Mã key:</span>
                  <strong>{{ project.projectKey }}</strong>
                </div>
                <div class="meta-item">
                  <span class="label">Issues:</span>
                  <span>{{ project.issueCounter }}</span>
                </div>
              </div>

              <div class="card-actions">
                <a [routerLink]="['/board']" (click)="selectProject(project)" class="btn btn-sm btn-outline">Mở Board</a>
                <a [routerLink]="['/backlog']" (click)="selectProject(project)" class="btn btn-sm btn-outline">Backlog</a>
                <a [routerLink]="['/projects', project.id, 'settings']" (click)="selectProject(project)" class="btn btn-sm btn-secondary">Cài đặt</a>
              </div>
            </div>
          }
        </div>
      }

      <!-- Dedicated Organization Modal -->
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
                <label for="pageOrgName">Tên tổ chức / Đơn vị <span class="required">*</span></label>
                <input
                  id="pageOrgName"
                  type="text"
                  [(ngModel)]="newOrgName"
                  (ngModelChange)="onOrgNameChange($event)"
                  maxlength="200"
                  class="form-control"
                  placeholder="VD: AgileTech Group hoặc Chi nhánh Hà Nội"
                  required
                />
              </div>

              <div class="form-group">
                <label for="pageOrgSlug">Mã định danh (Slug) <span class="required">*</span></label>
                <input
                  id="pageOrgSlug"
                  type="text"
                  [(ngModel)]="newOrgSlug"
                  maxlength="80"
                  class="form-control"
                  placeholder="VD: agiletech-group"
                  required
                />
                <small class="form-hint">Dùng ký tự chữ thường, số, dấu gạch ngang (không dấu, không khoảng trắng).</small>
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
    .projects-container {
      padding: 2rem;
      max-width: 1200px;
      margin: 0 auto;
    }
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
      padding-bottom: 1rem;
      border-bottom: 1px solid #e2e8f0;
      gap: 1rem;
    }
    .header-actions {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    h1 {
      font-size: 1.75rem;
      font-weight: 700;
      color: #1e293b;
      margin: 0;
    }
    .subtitle {
      color: #64748b;
      margin: 0.25rem 0 0 0;
      font-size: 0.95rem;
    }
    .filter-bar {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 1.5rem;
      flex-wrap: wrap;
    }
    .filter-label {
      font-size: 0.85rem;
      font-weight: 600;
      color: #64748b;
    }
    .org-pills {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .pill-btn {
      padding: 0.35rem 0.85rem;
      border-radius: 9999px;
      font-size: 0.825rem;
      font-weight: 600;
      border: 1px solid #e2e8f0;
      background: #ffffff;
      color: #475569;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .pill-btn:hover {
      background: #f1f5f9;
      border-color: #cbd5e1;
    }
    .pill-btn.active {
      background: #1b4332;
      color: #ffffff;
      border-color: #1b4332;
    }
    .btn {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.6rem 1.2rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.9rem;
      text-decoration: none;
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 0.2s ease;
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
    .btn-outline {
      background: transparent;
      color: #1b4332;
      border-color: rgba(45, 106, 79, 0.35);
    }
    .btn-outline:hover {
      background: #d8f3dc;
      color: #1b4332;
    }
    .btn-sm {
      padding: 0.4rem 0.8rem;
      font-size: 0.85rem;
    }
    .project-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 1.5rem;
    }
    .project-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.5rem;
      display: flex;
      flex-direction: column;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
      transition: transform 0.2s, box-shadow 0.2s;
    }
    .project-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 8px 16px -4px rgba(0,0,0,0.08);
    }
    .card-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.75rem;
    }
    .project-key {
      background: #eff6ff;
      color: #1d4ed8;
      font-family: monospace;
      font-weight: 700;
      padding: 0.2rem 0.5rem;
      border-radius: 6px;
      font-size: 0.8rem;
    }
    .org-tag {
      background: #f8fafc;
      color: #64748b;
      font-size: 0.75rem;
      font-weight: 600;
      padding: 0.15rem 0.5rem;
      border-radius: 4px;
      border: 1px solid #e2e8f0;
      max-width: 140px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .badge {
      font-size: 0.75rem;
      padding: 0.2rem 0.6rem;
      border-radius: 9999px;
      background: #f1f5f9;
      color: #64748b;
      margin-left: auto;
    }
    .badge-active {
      background: #dcfce7;
      color: #15803d;
    }
    .project-name {
      font-size: 1.15rem;
      font-weight: 700;
      color: #1e293b;
      margin: 0 0 0.5rem 0;
      line-height: 1.35;
    }
    .project-desc {
      color: #64748b;
      font-size: 0.875rem;
      line-height: 1.5;
      margin: 0 0 1.25rem 0;
      flex-grow: 1;
      display: -webkit-box;
      -webkit-line-clamp: 3;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .card-meta {
      display: flex;
      gap: 1.5rem;
      padding: 0.75rem 0;
      border-top: 1px solid #f1f5f9;
      border-bottom: 1px solid #f1f5f9;
      margin-bottom: 1rem;
      font-size: 0.85rem;
    }
    .meta-item {
      display: flex;
      align-items: center;
      gap: 0.35rem;
    }
    .meta-item .label {
      color: #94a3b8;
    }
    .card-actions {
      display: flex;
      gap: 0.5rem;
      margin-top: auto;
    }
    .empty-state {
      text-align: center;
      padding: 4rem 2rem;
      background: #ffffff;
      border: 2px dashed #cbd5e1;
      border-radius: 12px;
    }
    .empty-icon {
      font-size: 3rem;
      margin-bottom: 1rem;
    }
    .empty-state h3 {
      font-size: 1.25rem;
      color: #1e293b;
      margin: 0 0 0.5rem 0;
    }
    .empty-state p {
      color: #64748b;
      max-width: 450px;
      margin: 0 auto 1.5rem auto;
    }
    .empty-actions {
      display: flex;
      gap: 0.75rem;
      justify-content: center;
    }
    .loading-state {
      text-align: center;
      padding: 4rem;
      color: #64748b;
    }
    .spinner {
      width: 36px;
      height: 36px;
      border: 3px solid #e2e8f0;
      border-top-color: #1b4332;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 0 auto 1rem auto;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    .error-alert {
      background: #fee2e2;
      color: #991b1b;
      padding: 1rem;
      border-radius: 8px;
      margin-bottom: 1.5rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
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
      box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1);
      border: 1px solid #e2e8f0;
      overflow: hidden;
      animation: modalIn 0.2s cubic-bezier(0.16, 1, 0.3, 1);
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
    .modal-footer {
      padding: 1rem 1.5rem;
      background: #f8fafc;
      border-top: 1px solid #f1f5f9;
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
    }
    .btn-spinner {
      width: 14px;
      height: 14px;
      border: 2px solid #ffffff;
      border-top-color: transparent;
      border-radius: 50%;
      animation: spin 0.6s linear infinite;
    }

    @media (max-width: 767px) {
      .projects-container { padding: 0; }
      .page-header { align-items: stretch; flex-direction: column; gap: 1rem; margin-bottom: 1.25rem; }
      .header-actions { flex-direction: column; }
      .header-actions .btn { width: 100%; justify-content: center; }
      .project-grid { grid-template-columns: minmax(0, 1fr); gap: 1rem; }
      .project-card { padding: 1rem; }
      .card-actions { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .card-actions .btn:last-child { grid-column: 1 / -1; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectsPage implements OnInit, OnDestroy {
  private el = inject<ElementRef<HTMLElement>>(ElementRef);
  private cardTween: gsap.core.Tween | null = null;
  private readonly projectService = inject(ProjectManagementService);
  private readonly identity = inject(IdentityService);
  private readonly toastService = inject(ToastService);
  
  readonly projects = signal<Project[]>([]);
  readonly organizations = signal<Organization[]>([]);
  readonly selectedOrgId = signal<string>('all');
  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  // Dedicated Organization Modal State
  readonly isCreateOrgModalOpen = signal(false);
  readonly creatingOrg = signal(false);
  readonly orgError = signal<string | null>(null);
  newOrgName = '';
  newOrgSlug = '';

  readonly filteredProjects = computed(() => {
    const orgId = this.selectedOrgId();
    if (orgId === 'all') return this.projects();
    return this.projects().filter(p => p.orgId === orgId);
  });

  ngOnInit(): void {
    this.loadData();
  }

  ngOnDestroy(): void {
    this.cardTween?.kill();
  }

  selectProject(project: Project): void {
    this.projectService.selectProject(project);
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({
      projects: this.projectService.getProjects(),
      organizations: this.projectService.getOrganizations()
    }).subscribe({
      next: ({ projects, organizations }) => {
        this.projects.set(projects);
        this.organizations.set(organizations);
        this.loading.set(false);
        setTimeout(() => this.animateCards());
      },
      error: (err) => {
        this.error.set(apiErrorMessage(err, 'Không thể tải danh sách dự án và tổ chức.'));
        this.loading.set(false);
      }
    });
  }

  getOrgName(orgId: string): string {
    const org = this.organizations().find(o => o.id === orgId);
    return org ? org.name : 'Tổ chức chung';
  }

  openCreateOrgModal(): void {
    this.newOrgName = '';
    this.newOrgSlug = '';
    this.orgError.set(null);
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

    const ownerId = this.identity.authState().currentUser?.id;
    this.projectService.createOrganization({
      name: this.newOrgName.trim(),
      slug: this.newOrgSlug.trim().toLowerCase(),
      ownerId: ownerId || undefined
    }).subscribe({
      next: (createdOrg) => {
        this.organizations.update(orgs => [...orgs, createdOrg]);
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

  private animateCards(): void {
    if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      const cards = this.el.nativeElement.querySelectorAll<HTMLElement>('.project-card');
      if (cards.length) {
        this.cardTween?.kill();
        this.cardTween = gsap.fromTo(
          cards,
          { opacity: 0, y: 16 },
          {
            opacity: 1,
            y: 0,
            duration: 0.4,
            stagger: 0.06,
            ease: 'power2.out',
            clearProps: 'transform,opacity'
          }
        );
      }
    }
  }
}
