import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FocusAction } from '../../core/attention/focus-recommendation.model';
import { FocusRecommendationService } from '../../core/attention/focus-recommendation.service';
import { RecentWorkService } from '../../core/attention/recent-work.service';
import { IdentityService } from '../../core/services/identity.service';
import { Project, ProjectManagementService, WorkItem } from '../../core/services/project-management.service';

@Component({
  selector: 'app-for-you-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './for-you-page.html',
  styleUrl: './for-you-page.scss'
})
export class ForYouPageComponent {
  private readonly router = inject(Router);
  private readonly projectService = inject(ProjectManagementService);
  readonly identity = inject(IdentityService);
  private readonly recent = inject(RecentWorkService);
  readonly focus = inject(FocusRecommendationService);

  readonly user = computed(() => this.identity.authState().currentUser);
  readonly projects = this.projectService.allProjectsList;
  readonly currentProject = this.projectService.currentProject;
  readonly workItems = this.projectService.workItems;
  readonly activeTab = signal<'mine' | 'project'>('mine');

  readonly dateLabel = new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date());
  readonly greeting = new Date().getHours() < 11 ? 'Chào buổi sáng' : new Date().getHours() < 18 ? 'Chào buổi chiều' : 'Chào buổi tối';

  readonly currentWork = computed(() => this.workItems()
    .filter(item => item.projectId === this.currentProject().id && item.statusName !== 'Done' && !item.parentId)
    .sort((a, b) => {
      const score = (item: WorkItem) => (item.statusName === 'In Progress' ? 10 : 0) +
        (item.priority === 'Urgent' ? 8 : item.priority === 'High' ? 5 : 0);
      return score(b) - score(a) || a.issueKey.localeCompare(b.issueKey);
    }));

  readonly myWork = computed(() => {
    const user = this.user();
    if (!user) return [];
    const name = this.normalizeName(user.displayName);
    return this.currentWork().filter(item => item.assigneeId === user.id || this.normalizeName(item.assigneeName) === name);
  });

  readonly visibleWork = computed(() => (this.activeTab() === 'mine' ? this.myWork() : this.currentWork()).slice(0, 5));

  readonly recentTask = computed(() => this.workItems().find(item =>
    item.id === this.recent.lastTaskId() && item.statusName !== 'Done' && item.projectId === this.currentProject().id
  ) ?? null);

  constructor() {
    if (this.myWork().length === 0) this.activeTab.set('project');
  }

  activate(action: FocusAction): void {
    if (action.kind === 'task' && action.taskId) {
      const task = this.workItems().find(item => item.id === action.taskId);
      if (task) this.openDrawer(task);
      return;
    }
    if (action.route) void this.router.navigateByUrl(action.route);
  }

  openDrawer(task: WorkItem): void {
    this.recent.rememberTask(task.id);
    this.projectService.activeDrawerTask.set(task);
  }

  selectProject(project: Project): void {
    this.projectService.currentProject.set(project);
    void this.router.navigate(['/project/summary']);
  }

  private normalizeName(value: string | undefined): string {
    return (value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
  }
}
