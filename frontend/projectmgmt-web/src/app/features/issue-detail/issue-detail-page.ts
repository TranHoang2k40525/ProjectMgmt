import { Component, DestroyRef, OnInit, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable } from 'rxjs';
import { ISSUE_PRIORITIES, IssueCoreDto } from '../../core/api/issue-api.models';
import { IssueApi } from '../../core/api/issue.api';
import { apiErrorMessage } from '../../core/http/api-error-message';
import { ConfirmDialogService } from '../../core/services/confirm-dialog.service';
import { ProjectManagementService, WorkItem } from '../../core/services/project-management.service';
import { IssueFormComponent } from '../../shared/components/issue-form/issue-form';
import { resolveIssueRoute } from './issue-route';
import { IssueAttachmentsComponent } from './sections/issue-attachments';
import { IssueCommentsComponent } from './sections/issue-comments';
import { IssueHistoryComponent } from './sections/issue-history';
import { IssueLabelsComponent } from './sections/issue-labels';
import { IssueLinksComponent } from './sections/issue-links';
import { IssueWatchButtonComponent } from './sections/issue-watch-button';

@Component({
  selector: 'app-issue-detail-page',
  imports: [CommonModule, FormsModule, RouterLink, IssueFormComponent, IssueAttachmentsComponent, IssueCommentsComponent,
    IssueWatchButtonComponent, IssueLabelsComponent, IssueLinksComponent, IssueHistoryComponent],
  templateUrl: './issue-detail-page.html',
  styleUrl: './issue-detail-page.scss'
})
export class IssueDetailPage implements OnInit {
  readonly service = inject(ProjectManagementService);
  private readonly issueApi = inject(IssueApi);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly error = signal('');
  readonly message = signal('');
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly editing = signal(false);
  readonly item = signal<WorkItem | null>(null);
  readonly core = signal<IssueCoreDto | null>(null);
  readonly historyVersion = signal(0);
  /** Mã PROJ-123 đang chờ danh sách Issue của Project tải xong để phân giải. */
  private readonly pendingKey = signal<string | null>(null);

  readonly subtasks = computed(() => this.service.workItems().filter(child => child.parentId === this.item()?.id));
  readonly priorityName = computed(() => ISSUE_PRIORITIES.find(p => p.id === this.core()?.priorityId)?.name ?? this.item()?.priority ?? '—');
  readonly estimateHours = computed(() => {
    const minutes = this.core()?.originalEstimateMinutes;
    return minutes == null ? null : Math.round(minutes / 6) / 10;
  });
  readonly metCount = computed(() => (this.item()?.criteria ?? []).filter(c => c.isMet).length);
  newCriterion = '';
  subtaskTitle = '';

  constructor() {
    effect(() => {
      const key = this.pendingKey();
      const match = key ? this.service.workItems().find(item => item.issueKey.toUpperCase() === key) : undefined;
      if (match) { this.pendingKey.set(null); this.load(match.id); }
    });
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => this.open(params.get('id') ?? ''));
  }

  open(param: string): void {
    const target = resolveIssueRoute(param, this.service.workItems());
    if (target.kind === 'invalid') { this.item.set(null); this.error.set('Đường dẫn Issue không hợp lệ.'); return; }
    if (target.kind === 'id' || target.id) { this.load(target.kind === 'id' ? target.id : target.id!); return; }
    // Mã chưa có trong danh sách đã tải: chờ workspace của Project hiện tại.
    this.loading.set(true);
    this.pendingKey.set(target.key);
    this.service.loadWorkspace();
    setTimeout(() => {
      if (this.pendingKey() === target.key) {
        this.pendingKey.set(null);
        this.loading.set(false);
        this.error.set(`Không tìm thấy ${target.key} trong Project đang chọn.`);
      }
    }, 8000);
  }

  load(id = this.item()?.id ?? ''): void {
    if (!id) return;
    this.loading.set(true);
    this.error.set('');
    this.service.getIssue(id).subscribe({
      next: task => {
        this.item.set({ ...task });
        this.historyVersion.update(v => v + 1);
        this.loading.set(false);
        if (this.service.currentProject().id !== task.projectId) {
          this.service.getProjectById(task.projectId).subscribe({ next: p => this.service.currentProject.set(p), error: e => this.fail(e) });
        }
      },
      error: e => { this.item.set(null); this.loading.set(false); this.fail(e); }
    });
    this.issueApi.getCore(id).subscribe({ next: core => this.core.set(core), error: () => this.core.set(null) });
  }

  retry(): void { this.open(this.route.snapshot.paramMap.get('id') ?? ''); }

  edited(): void { this.editing.set(false); this.message.set('Đã lưu chi tiết công việc.'); this.refresh(); }

  changeStatus(statusName: string): void {
    const task = this.item();
    if (task && statusName !== task.statusName) this.run(this.service.saveIssue({ ...task, statusName }), 'Đã chuyển trạng thái.');
  }

  changeSprint(sprintId: string): void {
    const task = this.item();
    if (task && sprintId !== (task.sprintId ?? '')) this.run(this.service.saveIssue({ ...task, sprintId }), 'Đã chuyển Sprint.');
  }

  addCriterion(): void {
    const task = this.item();
    if (task) this.run(this.service.addCriterion(task.id, this.newCriterion), 'Đã thêm tiêu chí.', () => this.newCriterion = '');
  }

  toggleCriterion(c: { id: string; content: string; isMet: boolean }): void {
    this.run(this.service.updateCriterion(c.id, c.content, !c.isMet), 'Đã lưu tiêu chí.');
  }

  addSubtask(): void {
    const task = this.item();
    if (!task) return;
    const statusName = this.service.projectStatuses().find(s => s.isInitial)?.name ?? 'To Do';
    this.run(this.service.createIssue(task.projectId, { title: this.subtaskTitle, issueType: 'Sub-task', parentId: task.id, statusName, priority: 'Medium', storyPoints: 0 }),
      'Đã tạo công việc con.', () => this.subtaskTitle = '');
  }

  remove(): void {
    const task = this.item();
    if (!task) return;
    const children = this.subtasks().length;
    this.confirm.confirm({
      title: `Xóa ${task.issueKey}?`,
      message: children ? `Issue và ${children} công việc con sẽ bị xóa (xóa mềm, vẫn giữ lịch sử).` : 'Issue sẽ bị xóa (xóa mềm, vẫn giữ lịch sử).',
      type: 'danger', confirmText: 'Xóa Issue',
      onConfirm: () => {
        this.busy.set(true);
        this.issueApi.delete(task.id).subscribe({
          next: () => { this.busy.set(false); this.service.loadWorkspace(); void this.router.navigate(['/backlog']); },
          error: e => this.fail(e)
        });
      }
    });
  }

  private refresh(): void { this.load(); this.service.loadWorkspace(); }

  private fail(error: unknown): void { this.busy.set(false); this.error.set(apiErrorMessage(error, 'Không thể thực hiện thao tác.')); }

  private run(request: Observable<unknown>, message: string, clear?: () => void): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: () => { clear?.(); this.busy.set(false); this.message.set(message); this.refresh(); },
      error: e => this.fail(e)
    });
  }
}
