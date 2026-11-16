import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import {
  AiAgentService,
  AiQuickActionPlanDto,
  AiToolCallEvent,
} from './ai-agent.service';
import { ProjectManagementService, WorkItem } from './project-management.service';
import { ToastService } from './toast.service';

export type QuickActionKind = 'subtask_breakdown' | 'assignee_suggestion';
export type QuickActionPhase = 'idle' | 'running' | 'staged' | 'applying' | 'error';

@Injectable({ providedIn: 'root' })
export class AiQuickActionService {
  private readonly aiAgent = inject(AiAgentService);
  private readonly projectService = inject(ProjectManagementService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  private activeAbortController: AbortController | null = null;
  private elapsedTimer: ReturnType<typeof setInterval> | null = null;

  readonly isOpen = signal(false);
  readonly phase = signal<QuickActionPhase>('idle');
  readonly actionType = signal<QuickActionKind>('subtask_breakdown');
  readonly targetItem = signal<{ id: string; issueKey: string; title: string; parentId?: string | null } | null>(null);
  readonly progressPercent = signal(0);
  readonly progressLabel = signal('');
  readonly elapsedSeconds = signal(0);
  readonly traceItems = signal<AiToolCallEvent[]>([]);
  readonly plan = signal<AiQuickActionPlanDto | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly subtaskSelections = signal<Record<string, boolean>>({});
  readonly subtaskTitles = signal<Record<string, string>>({});
  readonly selectedAssigneeId = signal<string | null>(null);
  readonly overrideReason = signal('');

  constructor() {
    this.destroyRef.onDestroy(() => this.stopActiveStreamAndTimer());
  }

  triggerBreakdown(item: WorkItem): void {
    if (item.parentId) {
      this.toast.warning('Không thể phân rã', `[${item.issueKey}] đã là việc con (Sub-task); chỉ hỗ trợ phân rã từ công việc cha.`);
      return;
    }
    void this.startQuickAction(item, 'subtask_breakdown');
  }

  triggerAssignSuggestion(item: WorkItem): void {
    void this.startQuickAction(item, 'assignee_suggestion');
  }

  retry(): void {
    const target = this.targetItem();
    if (!target) return;
    const item = this.projectService.workItems().find(w => w.id === target.id)
      ?? ({ id: target.id, issueKey: target.issueKey, title: target.title, parentId: target.parentId } as WorkItem);
    void this.startQuickAction(item, this.actionType());
  }

  private async startQuickAction(item: WorkItem, kind: QuickActionKind): Promise<void> {
    const projectId = this.projectService.currentProject().id;
    if (!projectId) {
      this.toast.error('Chưa chọn dự án', 'Vui lòng mở một dự án trước khi gọi Trợ Lý AI.');
      return;
    }

    this.stopActiveStreamAndTimer();

    const controller = new AbortController();
    this.activeAbortController = controller;

    this.isOpen.set(true);
    this.phase.set('running');
    this.actionType.set(kind);
    this.targetItem.set({ id: item.id, issueKey: item.issueKey, title: item.title, parentId: item.parentId });
    this.progressPercent.set(10);
    this.progressLabel.set(
      kind === 'subtask_breakdown'
        ? `Đang kích hoạt Trợ Lý AI phân rã Sub-task cho [${item.issueKey}]...`
        : `Đang kích hoạt Trợ Lý AI đánh giá năng lực & phân công [${item.issueKey}]...`
    );
    this.elapsedSeconds.set(0);
    this.traceItems.set([]);
    this.plan.set(null);
    this.errorMessage.set(null);
    this.subtaskSelections.set({});
    this.subtaskTitles.set({});
    this.selectedAssigneeId.set(null);
    this.overrideReason.set('');

    const startedAt = Date.now();
    this.elapsedTimer = setInterval(() => {
      this.elapsedSeconds.set(Number(((Date.now() - startedAt) / 1000).toFixed(1)));
    }, 100);

    try {
      await this.aiAgent.streamQuickTaskAction(
        {
          projectId,
          issueId: item.id,
          actionType: kind,
        },
        {
          onStepProgress: (step) => {
            this.progressPercent.set(step.percent);
            this.progressLabel.set(step.labelVi);
          },
          onToolCall: (tool) => {
            this.traceItems.update(prev => [...prev, tool]);
          },
          onPlanReady: (stagedPlan) => {
            this.plan.set(stagedPlan);
            if (kind === 'subtask_breakdown') {
              const sel: Record<string, boolean> = {};
              const titles: Record<string, string> = {};
              for (const sub of stagedPlan.proposedSubtasks ?? []) {
                sel[sub.tempId] = sub.selected !== false;
                titles[sub.tempId] = sub.title;
              }
              this.subtaskSelections.set(sel);
              this.subtaskTitles.set(titles);
            } else {
              const topId = stagedPlan.recommendedUserId
                ?? stagedPlan.assigneeCandidates?.[0]?.userId
                ?? null;
              this.selectedAssigneeId.set(topId);
            }
            this.phase.set('staged');
            this.progressPercent.set(100);
          },
          onError: (err) => {
            this.errorMessage.set(err.message || 'Không thể chạy Trợ Lý AI cho công việc này.');
            this.phase.set('error');
          },
        },
        controller.signal
      );

      if (this.phase() === 'running' && !controller.signal.aborted) {
        this.errorMessage.set('Kết nối AI kết thúc trước khi nhận được đề xuất staged trên Redis.');
        this.phase.set('error');
      }
    } catch (err: any) {
      if (err?.name !== 'AbortError') {
        this.errorMessage.set(err?.message || 'Lỗi kết nối tới Trợ Lý AI.');
        this.phase.set('error');
      }
    } finally {
      this.clearTimer();
      if (this.activeAbortController === controller) {
        this.activeAbortController = null;
      }
    }
  }

  toggleSubtask(tempId: string): void {
    this.subtaskSelections.update(curr => ({ ...curr, [tempId]: !(curr[tempId] !== false) }));
  }

  updateSubtaskTitle(tempId: string, title: string): void {
    this.subtaskTitles.update(curr => ({ ...curr, [tempId]: title }));
  }

  selectAssignee(userId: string): void {
    this.selectedAssigneeId.set(userId);
  }

  selectedSubtaskCount(): number {
    const p = this.plan();
    if (!p) return 0;
    const sel = this.subtaskSelections();
    return p.proposedSubtasks.filter(s => sel[s.tempId] !== false).length;
  }

  confirmApply(): void {
    const currentPlan = this.plan();
    if (!currentPlan || this.phase() === 'applying') return;

    const projectId = currentPlan.projectId || this.projectService.currentProject().id;
    const selMap = this.subtaskSelections();
    const titleMap = this.subtaskTitles();

    const selectedSubtasks = currentPlan.actionType === 'subtask_breakdown'
      ? currentPlan.proposedSubtasks
          .filter(s => selMap[s.tempId] !== false)
          .map(s => ({
            tempId: s.tempId,
            title: (titleMap[s.tempId] ?? s.title).trim(),
            assigneeUserId: s.suggestedAssigneeUserId ?? null,
          }))
      : [];

    if (currentPlan.actionType === 'subtask_breakdown' && selectedSubtasks.length === 0) {
      this.toast.warning('Chưa chọn Sub-task', 'Vui lòng chọn ít nhất một Sub-task để áp dụng.');
      return;
    }

    const selectedUserId = currentPlan.actionType === 'assignee_suggestion'
      ? (this.selectedAssigneeId() ?? currentPlan.recommendedUserId ?? null)
      : null;

    if (currentPlan.actionType === 'assignee_suggestion' && !selectedUserId) {
      this.toast.warning('Chưa chọn thành viên', 'Vui lòng chọn người xử lý từ danh sách AI đề xuất.');
      return;
    }

    this.phase.set('applying');
    this.errorMessage.set(null);

    this.aiAgent.confirmQuickTaskAction({
      actionId: currentPlan.actionId,
      projectId,
      mode: 'apply',
      selectedSubtasks,
      selectedAssigneeUserId: selectedUserId,
      overrideReason: this.overrideReason().trim() || undefined,
    }).subscribe({
      next: (res) => {
        if (!res.success) {
          this.errorMessage.set(res.message || 'Không thể áp dụng đề xuất AI.');
          this.phase.set('staged');
          return;
        }

        this.toast.success('Trợ Lý AI', res.message);
        this.projectService.loadWorkspace(projectId);
        this.resetState();
      },
      error: (err) => {
        const msg = err?.error?.message || 'Không thể áp dụng thay đổi AI. Vui lòng thử lại.';
        this.errorMessage.set(msg);
        this.phase.set('staged');
      },
    });
  }

  cancelAndDismiss(): void {
    const currentPlan = this.plan();
    const projectId = currentPlan?.projectId || this.projectService.currentProject().id;

    if (currentPlan && currentPlan.status === 'pending' && this.phase() === 'staged' && projectId) {
      this.aiAgent.confirmQuickTaskAction({
        actionId: currentPlan.actionId,
        projectId,
        mode: 'cancel',
      }).subscribe({
        next: () => {
          this.toast.info('Trợ Lý AI', `Đã hủy đề xuất staged trên Redis cho [${currentPlan.issueKey}].`);
        },
        error: () => {
          // Ignore transient cancel cleanup failure
        },
      });
    }

    this.resetState();
  }

  private resetState(): void {
    this.stopActiveStreamAndTimer();
    this.isOpen.set(false);
    this.phase.set('idle');
    this.targetItem.set(null);
    this.plan.set(null);
    this.traceItems.set([]);
    this.errorMessage.set(null);
    this.progressPercent.set(0);
    this.progressLabel.set('');
    this.subtaskSelections.set({});
    this.subtaskTitles.set({});
    this.selectedAssigneeId.set(null);
    this.overrideReason.set('');
  }

  private stopActiveStreamAndTimer(): void {
    this.clearTimer();
    if (this.activeAbortController) {
      this.activeAbortController.abort();
      this.activeAbortController = null;
    }
  }

  private clearTimer(): void {
    if (this.elapsedTimer) {
      clearInterval(this.elapsedTimer);
      this.elapsedTimer = null;
    }
  }
}
