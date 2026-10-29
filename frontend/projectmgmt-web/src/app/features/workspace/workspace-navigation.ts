import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { RecentItem, WorkItemSummary, WorkspaceProject } from './data/workspace.models';
import { WorkspaceContextService } from './workspace-context.service';

/** Điều hướng từ các danh sách của không gian làm việc, đồng thời ghi vào "Gần đây". */
@Injectable({ providedIn: 'root' })
export class WorkspaceNavigation {
  private readonly router = inject(Router);
  private readonly context = inject(WorkspaceContextService);

  openOrganization(orgId: string): void {
    void this.router.navigate(['/orgs', orgId, 'overview']);
  }

  openProject(project: Pick<WorkspaceProject, 'id'>): void {
    this.context.recordVisit('project', project.id);
    void this.router.navigate(['/projects', project.id, 'summary']);
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
}
