import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FocusAction } from '../../core/attention/focus-recommendation.model';
import { AttentionOrchestratorService } from '../../core/attention/attention-orchestrator.service';
import { FocusRecommendationService } from '../../core/attention/focus-recommendation.service';
import { RecentWorkService } from '../../core/attention/recent-work.service';
import { IdentityApi } from '../../core/api/identity.api';
import { ForYouDto } from '../../core/api/identity-api.models';
import { IdentityService } from '../../core/services/identity.service';
import { Project, ProjectManagementService, WorkItem } from '../../core/services/project-management.service';

@Component({
  selector: 'app-for-you-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './for-you-page.html',
  styleUrl: './for-you-page.scss'
})
export class ForYouPageComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly projectService = inject(ProjectManagementService);
  readonly identity = inject(IdentityService);
  private readonly identityApi = inject(IdentityApi);
  private readonly recent = inject(RecentWorkService);
  private readonly attention = inject(AttentionOrchestratorService);
  readonly focus = inject(FocusRecommendationService);

  @ViewChild('focusStage') private focusStage?: ElementRef<HTMLElement>;
  @ViewChild('focusSymbol') private focusSymbol?: ElementRef<HTMLElement>;
  @ViewChild('focusCta') private focusCta?: ElementRef<HTMLElement>;

  readonly user = computed(() => this.identity.authState().currentUser);
  readonly projects = this.projectService.allProjectsList;
  readonly currentProject = this.projectService.currentProject;
  readonly workItems = this.projectService.workItems;
  readonly activeTab = signal<'mine' | 'project'>('mine');
  readonly assignedWork = signal<WorkItem[]>([]);
  readonly recentWork = signal<WorkItem[]>([]);
  readonly attentionFocus = signal<string[]>([]);
  readonly forYouError = signal<string | null>(null);

  readonly dateLabel = new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date());
  readonly greeting = new Date().getHours() < 11 ? 'Chào buổi sáng' : new Date().getHours() < 18 ? 'Chào buổi chiều' : 'Chào buổi tối';

  readonly currentWork = computed(() => this.workItems()
    .filter(item => item.projectId === this.currentProject().id && item.statusName !== 'Done' && !item.parentId)
    .sort((a, b) => {
      const score = (item: WorkItem) => (item.statusName === 'In Progress' ? 10 : 0) +
        (item.priority === 'Urgent' ? 8 : item.priority === 'High' ? 5 : 0);
      return score(b) - score(a) || a.issueKey.localeCompare(b.issueKey);
    }));

  readonly myWork = computed(() => this.assignedWork());

  readonly visibleWork = computed(() => (this.activeTab() === 'mine' ? this.myWork() : this.currentWork()).slice(0, 5));

  readonly recentTask = computed(() => this.recentWork()[0] ?? this.workItems().find(item =>
    item.id === this.recent.lastTaskId() && item.statusName !== 'Done' && item.projectId === this.currentProject().id
  ) ?? null);

  ngOnInit(): void {
    this.identityApi.getForYou().subscribe({
      next: result => {
        this.assignedWork.set((result.assignedIssues ?? []).map(item => this.toWorkItem(item)));
        this.recentWork.set((result.recentIssues ?? []).map(item => this.toWorkItem(item)));
        this.attentionFocus.set(result.attentionFocus ?? []);
        if (this.assignedWork().length === 0) this.activeTab.set('project');
      },
      error: error => this.forYouError.set(this.readError(error))
    });
  }

  ngAfterViewInit(): void {
    if (this.focusStage && this.focusSymbol && this.focusCta) {
      this.attention.nudge(this.focusStage.nativeElement, this.focusSymbol.nativeElement, this.focusCta.nativeElement);
    }
  }

  ngOnDestroy(): void {
    this.attention.stop();
  }

  activate(action: FocusAction): void {
    this.attention.stop();
    if (action.kind === 'task' && action.taskId) {
      const task = this.workItems().find(item => item.id === action.taskId);
      if (task) this.openDrawer(task);
      return;
    }
    if (action.route) void this.router.navigateByUrl(action.route);
  }

  openDrawer(task: WorkItem): void {
    this.attention.stop();
    this.recent.rememberTask(task.id);
    this.projectService.activeDrawerTask.set(task);
  }

  selectProject(project: Project): void {
    this.projectService.currentProject.set(project);
    void this.router.navigate(['/project/summary']);
  }

  private toWorkItem(item: ForYouDto): WorkItem {
    return {
      id: item.issueId ?? '',
      issueKey: item.issueKey ?? 'ISSUE',
      projectId: item.projectId ?? '',
      title: item.title ?? 'Công việc chưa có tiêu đề',
      description: item.dueDate ? `Hạn xử lý: ${item.dueDate}` : undefined,
      statusId: '',
      statusName: item.statusName ?? 'To Do',
      issueType: 'Task',
      priority: 'Medium',
      storyPoints: 0,
      assigneeId: this.user()?.id,
      assigneeName: this.user()?.displayName,
      reporterName: '',
      createdAt: new Date().toISOString()
    };
  }

  private readError(error: unknown): string {
    if (typeof error !== 'object' || error === null) return 'Không thể tải dữ liệu Dành cho bạn.';
    const value = error as { title?: unknown; detail?: unknown; message?: unknown };
    if (typeof value.title === 'string') return value.title;
    if (typeof value.detail === 'string') return value.detail;
    return typeof value.message === 'string' ? value.message : 'Không thể tải dữ liệu Dành cho bạn.';
  }
}
