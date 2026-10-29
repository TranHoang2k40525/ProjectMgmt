import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Project, ProjectManagementService } from '../../core/services/project-management.service';
import { ToastService } from '../../core/services/toast.service';
import { RecentItem, WorkItemSummary, WorkspaceProject } from './data/workspace.models';
import { WorkspaceContextService } from './workspace-context.service';

/** Điều hướng từ các danh sách của không gian làm việc, đồng thời ghi vào "Gần đây". */
@Injectable({ providedIn: 'root' })
export class WorkspaceNavigation {
  private readonly router = inject(Router);
  private readonly context = inject(WorkspaceContextService);
  private readonly projects = inject(ProjectManagementService);
  private readonly toast = inject(ToastService);

  openOrganization(orgId: string): void {
    void this.router.navigate(['/orgs', orgId, 'overview']);
  }

  /**
   * Các màn trong dự án (tổng quan, board, backlog…) làm việc với dự án đang chọn của ProjectManagementService,
   * nên phải chọn dự án thật trước rồi mới chuyển trang.
   */
  openProject(project: Pick<WorkspaceProject, 'id'>): void {
    this.context.recordVisit('project', project.id);
    const known = this.projects.allProjectsList().find(item => item.id === project.id);
    if (known) {
      this.enterProject(known);
      return;
    }
    this.projects.getProjectById(project.id).subscribe({
      next: found => this.enterProject(found),
      error: () => this.toast.error('Không mở được dự án', 'Dự án không tồn tại hoặc bạn không có quyền truy cập.')
    });
  }

  openWorkItem(item: Pick<WorkItemSummary, 'id'>): void {
    this.context.recordVisit('issue', item.id);
    void this.router.navigate(['/issues', item.id]);
  }

  openRecent(item: RecentItem): void {
    if (item.kind === 'organization') this.openOrganization(item.id);
    else if (item.kind === 'project') this.openProject(item);
    else this.openWorkItem(item);
  }

  private enterProject(project: Project): void {
    this.projects.selectProject(project);
    void this.router.navigate(['/project/summary']);
  }
}
