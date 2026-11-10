import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  inject,
  signal,
  computed,
  OnDestroy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AiAgentService,
  AiSprintProposalDto,
  AiStepProgressEvent,
  AiToolCallEvent,
  ProposedTaskDto,
  ProposedSubTaskDto,
} from '../../../../core/services/ai-agent.service';
import {
  ProjectMember,
  Sprint,
} from '../../../../core/services/project-management.service';
import { ToastService } from '../../../../core/services/toast.service';
import { firstValueFrom } from 'rxjs';

export interface EpicOption {
  id: string;
  key?: string;
  name: string;
}

export interface ChatTurn {
  id: string;
  role: 'user' | 'agent';
  text: string;
  fileName?: string;
  fileSizeLabel?: string;
  timestamp: string;
}

@Component({
  selector: 'app-ai-decompose-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-decompose-modal.html',
  styleUrl: './ai-decompose-modal.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiDecomposeModalComponent implements OnDestroy {
  private readonly aiService = inject(AiAgentService);
  private readonly toastService = inject(ToastService);
  private readonly router = inject(Router);

  @Input({ required: true }) projectId = '';
  @Input() projectName = 'Dự án';
  @Input() projectKey = 'PRJ';
  @Input() members: ProjectMember[] = [];
  @Input() epics: EpicOption[] = [];
  @Input() sprints: Sprint[] = [];
  @Input() canCommitSprint = true;

  @Output() closed = new EventEmitter<void>();
  @Output() applied = new EventEmitter<{ sprintId: string; sprintName: string; createdCount: number }>();

  // Chat & Input state
  readonly promptInput = signal<string>('');
  readonly pastedDocumentText = signal<string>('');
  readonly attachedFile = signal<File | null>(null);
  readonly isDragOver = signal<boolean>(false);
  readonly chatHistory = signal<ChatTurn[]>([]);

  // Streaming & Agent progress state
  readonly isStreaming = signal<boolean>(false);
  readonly activeSessionId = signal<string>('');
  readonly currentStepIndex = signal<number>(0);
  readonly currentPercent = signal<number>(0);
  readonly currentStepLabel = signal<string>('');
  readonly currentStepDetail = signal<string>('');
  readonly stepsLog = signal<AiStepProgressEvent[]>([]);
  readonly toolCallsLog = signal<AiToolCallEvent[]>([]);
  readonly expandedToolIndices = signal<Record<number, boolean>>({});
  readonly streamedThoughts = signal<string>('');
  readonly errorMessage = signal<string | null>(null);
  readonly selectedModel = signal<string>('Gemini 3.5 Flash');

  // Proposal & Selection state
  readonly proposal = signal<AiSprintProposalDto | null>(null);
  readonly selectedTaskIds = signal<Record<string, boolean>>({});
  readonly expandedSubtasks = signal<Record<string, boolean>>({});
  readonly inlineEditingTitleId = signal<string | null>(null);
  readonly inlineEditingSpId = signal<string | null>(null);

  // Detail Drawer state (Reference Image 5)
  readonly activeDrawerTaskId = signal<string | null>(null);
  readonly drawerRegenerateFeedback = signal<string>('');
  readonly isRegeneratingSingle = signal<boolean>(false);
  readonly isRegeneratingBatch = signal<boolean>(false);
  readonly isApplying = signal<boolean>(false);
  readonly regenerateAllFeedback = signal<string>('');
  readonly showRegenerateAllBar = signal<boolean>(false);

  private abortController: AbortController | null = null;

  readonly pipelineStepDefinitions = [
    { index: 1, code: 'INGEST_DOCUMENTS', label: 'Tiếp nhận & Guardrail tài liệu', icon: 'shield_lock' },
    { index: 2, code: 'QUERY_CONTEXT', label: 'Truy vấn MCP Spec Hub & Lịch sử Sprint', icon: 'manage_search' },
    { index: 3, code: 'EVALUATE_CAPACITY', label: 'Đánh giá Năng lực & Tải công việc Đội ngũ', icon: 'groups' },
    { index: 4, code: 'DECOMPOSE_SPRINT', label: 'Phân rã Sprint Goal, Epics, Tasks & Subtasks', icon: 'account_tree' },
    { index: 5, code: 'FINALIZE_PROPOSAL', label: 'Tự kiểm tra (Self-Check) & Lưu Staging', icon: 'fact_check' },
  ];

  readonly plannedSprints = computed(() =>
    (this.sprints || []).filter((s) => s.status === 'future' || s.status === 'active')
  );

  readonly selectedTasksCount = computed(() => {
    const map = this.selectedTaskIds();
    return Object.values(map).filter(Boolean).length;
  });

  readonly activeDrawerTask = computed<ProposedTaskDto | null>(() => {
    const prop = this.proposal();
    const id = this.activeDrawerTaskId();
    if (!prop || !id) return null;
    return prop.tasks.find((t) => t.id === id) ?? null;
  });

  readonly totalCalculatedSp = computed(() => {
    const prop = this.proposal();
    if (!prop) return 0;
    return prop.tasks.reduce((sum, t) => sum + (Number(t.storyPoints) || 0), 0);
  });

  readonly totalCalculatedHours = computed(() => {
    const prop = this.proposal();
    if (!prop) return 0;
    return prop.tasks.reduce((sum, t) => sum + (Number(t.estimatedHours) || 0), 0);
  });

  readonly proposalTags = computed<string[]>(() => {
    const prop = this.proposal();
    if (!prop) return [];
    if (prop.tags && prop.tags.length > 0) return prop.tags;
    const tagSet = new Set<string>();
    tagSet.add('#ScrumSprint');
    tagSet.add('#SpecHub');
    for (const t of prop.tasks) {
      if (t.tags && t.tags.length > 0) {
        for (const tg of t.tags) tagSet.add(tg.startsWith('#') ? tg : `#${tg}`);
      } else {
        tagSet.add(`#${t.type}`);
      }
    }
    return Array.from(tagSet).slice(0, 8);
  });

  readonly allUniqueCitations = computed<string[]>(() => {
    const prop = this.proposal();
    if (!prop) return [];
    const citeSet = new Set<string>();
    for (const t of prop.tasks) {
      if (t.documentCitations) {
        for (const c of t.documentCitations) {
          if (c && c.trim()) citeSet.add(c.trim());
        }
      }
    }
    if (citeSet.size === 0) {
      citeSet.add('Tài liệu Đặc tả Kiến trúc Spec Hub');
    }
    return Array.from(citeSet);
  });

  toggleToolLog(idx: number): void {
    this.expandedToolIndices.update((prev) => ({ ...prev, [idx]: !prev[idx] }));
  }

  openSpecDoc(citationText?: string, documentId?: string | null): void {
    if (documentId) {
      this.router.navigate(['/projects', this.projectId, 'documents'], { queryParams: { docId: documentId } });
    } else {
      this.router.navigate(['/projects', this.projectId, 'documents']);
    }
  }

  ngOnDestroy(): void {
    this.cancelStream();
  }

  closeModal(): void {
    this.cancelStream();
    this.closed.emit();
  }

  cancelStream(): void {
    if (this.abortController) {
      this.abortController.abort();
      this.abortController = null;
    }
    this.isStreaming.set(false);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.attachedFile.set(file);
      this.errorMessage.set(null);
    }
    input.value = '';
  }

  removeAttachedFile(): void {
    this.attachedFile.set(null);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.attachedFile.set(file);
      this.errorMessage.set(null);
    }
  }

  onClipboardPaste(event: ClipboardEvent): void {
    const files = event.clipboardData?.files;
    if (files && files.length > 0) {
      event.preventDefault();
      this.attachedFile.set(files[0]);
      this.errorMessage.set(null);
      return;
    }

    const pastedText = event.clipboardData?.getData('text/plain');
    if (pastedText && pastedText.length > 240 && !this.pastedDocumentText()) {
      event.preventDefault();
      this.pastedDocumentText.set(pastedText.trim());
    }
  }

  clearPastedDocument(): void {
    this.pastedDocumentText.set('');
  }

  useProjectSpecContext(): void {
    this.promptInput.set(
      'Phân tích tài liệu đặc tả trong Spec Hub của dự án Quản lý dự án, lập kế hoạch Sprint tiếp theo kèm Sprint Goal, phân rã Epics, Tasks, Subtasks kỹ thuật và phân bổ cho thành viên theo đúng năng lực.'
    );
    this.submitDecompose();
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  async submitDecompose(): Promise<void> {
    if (this.isStreaming()) return;

    const promptText = this.promptInput().trim();
    const docText = this.pastedDocumentText().trim();
    const file = this.attachedFile();

    const currentProposal = this.proposal();
    const checkedIds = Object.entries(this.selectedTaskIds())
      .filter(([, checked]) => checked)
      .map(([id]) => id);

    // If a proposal already exists and user typed follow-up instructions in chat:
    // - If specific tasks are checked, regenerate only those selected tasks!
    // - Otherwise, if no file is attached and proposal exists, regenerate full proposal or run new stream!
    if (currentProposal && !file && !docText && promptText && checkedIds.length > 0) {
      await this.regenerateSelectedTasksWithFeedback(promptText);
      return;
    }

    const combinedText = [docText, promptText].filter(Boolean).join('\n\n');
    if (!file && !combinedText) {
      this.errorMessage.set('Vui lòng nhập yêu cầu, dán nội dung cuộc họp hoặc đính kèm tệp tài liệu (.docx, .pdf, .txt, .md).');
      return;
    }

    this.errorMessage.set(null);
    const nowTime = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

    this.chatHistory.update((list) => [
      ...list,
      {
        id: `user_${Date.now()}`,
        role: 'user',
        text: promptText || (docText ? `Đã dán nội dung tài liệu (${docText.length} ký tự)` : `Phân tích tệp đính kèm: ${file?.name}`),
        fileName: file?.name,
        fileSizeLabel: file ? this.formatFileSize(file.size) : undefined,
        timestamp: nowTime,
      },
    ]);

    this.promptInput.set('');
    this.pastedDocumentText.set('');
    this.attachedFile.set(null);

    this.isStreaming.set(true);
    this.currentStepIndex.set(1);
    this.currentPercent.set(10);
    this.currentStepLabel.set('Khởi tạo phiên làm việc AI Agent...');
    this.currentStepDetail.set('Đang kiểm tra bảo mật Input Guardrail & Project Scope Guard...');
    this.stepsLog.set([]);
    this.toolCallsLog.set([]);
    this.streamedThoughts.set('');

    this.abortController = new AbortController();

    try {
      await this.aiService.streamDecomposeSprint(
        {
          projectId: this.projectId,
          text: combinedText || 'Phân tích tài liệu dự án và lập kế hoạch Sprint tiếp theo.',
          prompt: promptText || 'Tạo Sprint Goal, Tasks và Subtasks chuẩn Scrum.',
          file,
        },
        {
          onSessionInit: (init) => {
            this.activeSessionId.set(init.sessionId);
          },
          onStepProgress: (step) => {
            this.currentStepIndex.set(step.stepIndex);
            this.currentPercent.set(step.percent);
            this.currentStepLabel.set(step.labelVi);
            this.currentStepDetail.set(step.detail ?? '');
            this.stepsLog.update((prev) => {
              const filtered = prev.filter((s) => s.stepIndex !== step.stepIndex);
              return [...filtered, step].sort((a, b) => a.stepIndex - b.stepIndex);
            });
          },
          onToolCall: (tool) => {
            this.toolCallsLog.update((prev) => [...prev, tool]);
          },
          onThoughtDelta: (delta) => {
            this.streamedThoughts.update((prev) => (prev + delta).slice(-1800));
          },
          onProposalReady: (prop) => {
            this.normalizeProposal(prop);
            this.proposal.set(prop);
            this.activeSessionId.set(prop.sessionId);
            const doneTime = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
            this.chatHistory.update((list) => [
              ...list,
              {
                id: `agent_${Date.now()}`,
                role: 'agent',
                text: `Đã hoàn tất phân tách ${prop.tasks.length} công việc (${prop.totalStoryPoints} SP) với Sprint Goal: "${prop.sprintGoal}". Bạn có thể nhấn vào từng công việc để xem chi tiết, chỉnh sửa hoặc yêu cầu làm lại.`,
                timestamp: doneTime,
              },
            ]);
          },
          onCompleted: () => {
            this.isStreaming.set(false);
          },
          onError: (err) => {
            this.errorMessage.set(err.message || 'Đã xảy ra lỗi khi phân tách công việc.');
            this.isStreaming.set(false);
          },
        },
        this.abortController.signal
      );
    } catch (err: any) {
      if (err?.name !== 'AbortError') {
        this.errorMessage.set('Kết nối luồng SSE bị gián đoạn. Vui lòng thử lại.');
      }
      this.isStreaming.set(false);
    }
  }

  private normalizeProposal(prop: AiSprintProposalDto): void {
    if (!prop.tasks) prop.tasks = [];
    prop.tasks.forEach((t, idx) => {
      if (!t.id) t.id = `task_${idx + 1}`;
      if (!t.status) t.status = 'To Do';
      if (t.isReady === undefined) t.isReady = true;
      if (!t.remainingHours || t.remainingHours <= 0) t.remainingHours = t.estimatedHours || 8;
      if (!t.acceptanceCriteria) t.acceptanceCriteria = [];
      if (!t.documentCitations) t.documentCitations = [];
      if (!t.subtasks) t.subtasks = [];
    });
  }

  getTaskDisplayCode(index: number): string {
    return `${this.projectKey || 'PRJ'}-AI${index + 1}`;
  }

  getStepStatus(stepIndex: number): 'done' | 'active' | 'pending' {
    if (!this.isStreaming() && this.proposal()) return 'done';
    const curr = this.currentStepIndex();
    if (stepIndex < curr) return 'done';
    if (stepIndex === curr) return 'active';
    return 'pending';
  }

  toggleTaskCheckbox(taskId: string, event: Event): void {
    event.stopPropagation();
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedTaskIds.update((prev) => ({ ...prev, [taskId]: checked }));
  }

  toggleSelectAllTasks(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const prop = this.proposal();
    if (!prop) return;
    const next: Record<string, boolean> = {};
    if (checked) {
      for (const t of prop.tasks) next[t.id] = true;
    }
    this.selectedTaskIds.set(next);
  }

  toggleSubtaskExpand(taskId: string, event: Event): void {
    event.stopPropagation();
    this.expandedSubtasks.update((prev) => ({ ...prev, [taskId]: !prev[taskId] }));
  }

  openTaskDrawer(task: ProposedTaskDto, event?: Event): void {
    event?.stopPropagation();
    this.activeDrawerTaskId.set(task.id);
    this.drawerRegenerateFeedback.set('');
  }

  closeTaskDrawer(): void {
    this.activeDrawerTaskId.set(null);
    this.persistProposalEdits();
  }

  onTaskFieldChanged(): void {
    const prop = this.proposal();
    if (!prop) return;
    prop.totalStoryPoints = prop.tasks.reduce((sum, t) => sum + (Number(t.storyPoints) || 0), 0);
    prop.totalEstimatedHours = prop.tasks.reduce((sum, t) => sum + (Number(t.estimatedHours) || 0), 0);
    this.proposal.set({ ...prop, tasks: [...prop.tasks] });
  }

  onAssigneeChange(task: ProposedTaskDto, memberDisplayName: string): void {
    const matched = this.members.find((m) => m.displayName === memberDisplayName);
    task.assigneeName = matched ? matched.displayName : memberDisplayName || null;
    task.assigneeUserId = matched?.userId ?? null;
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  onEpicChange(task: ProposedTaskDto, epicValue: string): void {
    const matched = this.epics.find((e) => e.name === epicValue || e.id === epicValue);
    if (matched) {
      task.epicId = matched.id;
      task.epicTitle = matched.name;
    } else {
      task.epicId = null;
      task.epicTitle = epicValue || null;
    }
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  cyclePriority(task: ProposedTaskDto, event: Event): void {
    event.stopPropagation();
    const order = ['Low', 'Medium', 'High', 'Highest'];
    const idx = order.indexOf(task.priority || 'Medium');
    task.priority = order[(idx + 1) % order.length];
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  addSubtask(task: ProposedTaskDto): void {
    const nextIdx = (task.subtasks?.length || 0) + 1;
    task.subtasks = [
      ...(task.subtasks || []),
      {
        id: `${task.id}_sub_${nextIdx}_${Date.now()}`,
        title: 'Công việc kỹ thuật mới',
        role: 'Backend',
        estimatedHours: 4,
        assigneeUserId: task.assigneeUserId,
        assigneeName: task.assigneeName,
      },
    ];
    this.onTaskFieldChanged();
  }

  removeSubtask(task: ProposedTaskDto, subIndex: number): void {
    task.subtasks.splice(subIndex, 1);
    this.onTaskFieldChanged();
  }

  addAcceptanceCriterion(task: ProposedTaskDto): void {
    task.acceptanceCriteria = [...(task.acceptanceCriteria || []), 'Tiêu chí nghiệm thu mới'];
    this.onTaskFieldChanged();
  }

  removeAcceptanceCriterion(task: ProposedTaskDto, index: number): void {
    task.acceptanceCriteria.splice(index, 1);
    this.onTaskFieldChanged();
  }

  trackByIndex(index: number): number {
    return index;
  }

  deleteProposedTask(taskId: string, event?: Event): void {
    event?.stopPropagation();
    const prop = this.proposal();
    if (!prop) return;
    prop.tasks = prop.tasks.filter((t) => t.id !== taskId);
    if (this.activeDrawerTaskId() === taskId) {
      this.activeDrawerTaskId.set(null);
    }
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  async regenerateSingleTaskInDrawer(): Promise<void> {
    const task = this.activeDrawerTask();
    const sessionId = this.activeSessionId();
    const feedback = this.drawerRegenerateFeedback().trim();
    if (!task || !sessionId || !feedback || this.isRegeneratingSingle()) return;

    this.isRegeneratingSingle.set(true);
    try {
      const res = await firstValueFrom(this.aiService.regenerateSingleTask(sessionId, task.id, feedback));
      const prop = this.proposal();
      if (prop && res.updatedTask) {
        const idx = prop.tasks.findIndex((t) => t.id === task.id);
        if (idx >= 0) {
          res.updatedTask.id = task.id;
          if (!res.updatedTask.remainingHours) res.updatedTask.remainingHours = res.updatedTask.estimatedHours || 8;
          prop.tasks[idx] = res.updatedTask;
          this.onTaskFieldChanged();
        }
      }
      this.drawerRegenerateFeedback.set('');
      this.toastService.show(`Đã làm lại công việc "${res.updatedTask?.title || task.title}" thành công.`, 'success');
    } catch {
      this.toastService.show('Không thể làm lại công việc này. Vui lòng thử lại.', 'error');
    } finally {
      this.isRegeneratingSingle.set(false);
    }
  }

  async regenerateSelectedTasksWithFeedback(feedbackText?: string): Promise<void> {
    const prop = this.proposal();
    const sessionId = this.activeSessionId();
    const feedback = (feedbackText ?? this.regenerateAllFeedback()).trim() || 'Chia nhỏ chi tiết hơn và tối ưu lại Story Points';
    const checkedIds = Object.entries(this.selectedTaskIds())
      .filter(([, checked]) => checked)
      .map(([id]) => id);

    if (!prop || !sessionId || checkedIds.length === 0 || this.isRegeneratingBatch()) return;

    this.isRegeneratingBatch.set(true);
    try {
      for (const taskId of checkedIds) {
        const res = await firstValueFrom(this.aiService.regenerateSingleTask(sessionId, taskId, feedback));
        if (res.updatedTask) {
          const idx = prop.tasks.findIndex((t) => t.id === taskId);
          if (idx >= 0) {
            res.updatedTask.id = taskId;
            if (!res.updatedTask.remainingHours) res.updatedTask.remainingHours = res.updatedTask.estimatedHours || 8;
            prop.tasks[idx] = res.updatedTask;
          }
        }
      }
      this.onTaskFieldChanged();
      this.selectedTaskIds.set({});
      this.promptInput.set('');
      this.regenerateAllFeedback.set('');
      this.toastService.show(`Đã làm lại ${checkedIds.length} công việc đã chọn mà không ảnh hưởng các việc khác.`, 'success');
    } catch {
      this.toastService.show('Không thể làm lại các công việc đã chọn.', 'error');
    } finally {
      this.isRegeneratingBatch.set(false);
    }
  }

  async submitRegenerateAll(): Promise<void> {
    const prop = this.proposal();
    const sessionId = this.activeSessionId();
    const feedback = this.regenerateAllFeedback().trim() || 'Tái cấu trúc lại toàn bộ danh sách công việc và Sprint Goal chi tiết hơn';
    if (!prop || !sessionId || this.isRegeneratingBatch()) return;

    this.isRegeneratingBatch.set(true);
    try {
      const updated = await firstValueFrom(this.aiService.regenerateFullProposal(sessionId, feedback));
      this.normalizeProposal(updated);
      this.proposal.set(updated);
      this.showRegenerateAllBar.set(false);
      this.regenerateAllFeedback.set('');
      this.toastService.show('Đã làm lại toàn bộ đề xuất Sprint thành công.', 'success');
    } catch {
      this.toastService.show('Không thể làm lại toàn bộ đề xuất.', 'error');
    } finally {
      this.isRegeneratingBatch.set(false);
    }
  }

  persistProposalEdits(): void {
    const prop = this.proposal();
    const sessionId = this.activeSessionId();
    if (!prop || !sessionId) return;
    this.aiService.updateStagedProposal(sessionId, prop).subscribe({ error: () => {} });
  }

  async commitSprintProposal(): Promise<void> {
    const prop = this.proposal();
    if (!prop || this.isApplying()) return;

    if (!this.canCommitSprint) {
      this.errorMessage.set('Bạn không có quyền áp dụng kế hoạch vào Sprint (Chỉ Giám đốc dự án, Chủ tổ chức hoặc Scrum Master mới có quyền).');
      return;
    }

    this.isApplying.set(true);
    this.errorMessage.set(null);

    try {
      const res = await firstValueFrom(
        this.aiService.applyProposal({
          sessionId: this.activeSessionId() || prop.sessionId,
          projectId: this.projectId,
          targetSprintId: prop.targetSprintId,
          sprintGoal: prop.sprintGoal,
          tasks: prop.tasks,
        })
      );

      this.toastService.show(
        res.message || `Đã đưa ${res.createdTaskCount} việc vào ${res.sprintName}.`,
        'success'
      );
      this.applied.emit({
        sprintId: res.sprintId,
        sprintName: res.sprintName,
        createdCount: res.createdIssueCount,
      });
    } catch (err: any) {
      const msg = err?.error?.message || 'Không thể áp dụng đề xuất vào Sprint tiếp theo.';
      this.errorMessage.set(msg);
    } finally {
      this.isApplying.set(false);
    }
  }

  getMemberAvatar(assigneeName?: string | null): string | null {
    if (!assigneeName) return null;
    const found = this.members.find((m) => m.displayName === assigneeName);
    return found?.avatarUrl || null;
  }

  getMemberInitials(assigneeName?: string | null): string {
    if (!assigneeName) return 'U';
    const parts = assigneeName.trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return assigneeName.slice(0, 2).toUpperCase();
  }
}
