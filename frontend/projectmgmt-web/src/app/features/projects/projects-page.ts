import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Project, ProjectManagementService } from '../../core/services/project-management.service';

@Component({
  selector: 'app-projects-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="projects-container">
      <div class="page-header">
        <div>
          <h1>Danh sách Dự án</h1>
          <p class="subtitle">Quản lý không gian làm việc Scrum, Kanban và quy trình phân phối</p>
        </div>
        <a routerLink="/projects/new" class="btn btn-primary">
          <span class="icon">+</span> Tạo Project mới
        </a>
      </div>

      @if (loading()) {
        <div class="loading-state">
          <div class="spinner"></div>
          <p>Đang tải danh sách dự án...</p>
        </div>
      } @else if (error()) {
        <div class="error-alert">
          <p>{{ error() }}</p>
          <button (click)="loadProjects()" class="btn btn-secondary">Thử lại</button>
        </div>
      } @else if (projects().length === 0) {
        <div class="empty-state">
          <div class="empty-icon">📁</div>
          <h3>Chưa có dự án nào</h3>
          <p>Hãy khởi tạo dự án đầu tiên để thiết lập Scrum board, Sprint và phân công task thông minh với AI.</p>
          <a routerLink="/projects/new" class="btn btn-primary">Tạo Project ngay</a>
        </div>
      } @else {
        <div class="project-grid">
          @for (project of projects(); track project.id) {
            <div class="project-card">
              <div class="card-header">
                <span class="project-key">{{ project.projectKey }}</span>
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
                <a [routerLink]="['/board']" class="btn btn-sm btn-outline">Bảng Board</a>
                <a [routerLink]="['/backlog']" class="btn btn-sm btn-outline">Backlog</a>
                <a [routerLink]="['/projects', project.id, 'settings']" class="btn btn-sm btn-secondary">Cài đặt</a>
              </div>
            </div>
          }
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
      margin-bottom: 2rem;
      padding-bottom: 1rem;
      border-bottom: 1px solid #e2e8f0;
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
      background: #2563eb;
      color: #ffffff;
    }
    .btn-primary:hover {
      background: #1d4ed8;
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
      color: #2563eb;
      border-color: #bfdbfe;
    }
    .btn-outline:hover {
      background: #eff6ff;
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
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
      display: flex;
      flex-direction: column;
      transition: transform 0.2s, box-shadow 0.2s;
    }
    .project-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 8px 16px rgba(0,0,0,0.06);
    }
    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.75rem;
    }
    .project-key {
      font-family: monospace;
      font-size: 0.85rem;
      font-weight: 700;
      background: #dbeafe;
      color: #1d4ed8;
      padding: 0.2rem 0.6rem;
      border-radius: 4px;
    }
    .badge {
      font-size: 0.75rem;
      padding: 0.2rem 0.5rem;
      border-radius: 12px;
      background: #f1f5f9;
      color: #64748b;
    }
    .badge-active {
      background: #dcfce7;
      color: #15803d;
    }
    .project-name {
      font-size: 1.2rem;
      font-weight: 600;
      color: #0f172a;
      margin: 0 0 0.5rem 0;
    }
    .project-desc {
      color: #64748b;
      font-size: 0.9rem;
      line-height: 1.5;
      flex-grow: 1;
      margin: 0 0 1.25rem 0;
      display: -webkit-box;
      -webkit-line-clamp: 2;
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
      color: #64748b;
    }
    .meta-item .label {
      margin-right: 0.35rem;
    }
    .card-actions {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .empty-state, .loading-state {
      text-align: center;
      padding: 4rem 2rem;
      background: #ffffff;
      border: 1px dashed #cbd5e1;
      border-radius: 12px;
    }
    .empty-icon {
      font-size: 3rem;
      margin-bottom: 1rem;
    }
    .empty-state h3 {
      font-size: 1.3rem;
      color: #1e293b;
      margin-bottom: 0.5rem;
    }
    .empty-state p {
      color: #64748b;
      max-width: 500px;
      margin: 0 auto 1.5rem auto;
    }
    .spinner {
      width: 40px;
      height: 40px;
      border: 4px solid #e2e8f0;
      border-top-color: #2563eb;
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
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectsPage implements OnInit {
  private readonly projectService = inject(ProjectManagementService);
  
  readonly projects = signal<Project[]>([]);
  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadProjects();
  }

  loadProjects(): void {
    this.loading.set(true);
    this.error.set(null);
    this.projectService.getProjects().subscribe({
      next: (data) => {
        this.projects.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.title || 'Không thể tải danh sách dự án.');
        this.loading.set(false);
      }
    });
  }
}
