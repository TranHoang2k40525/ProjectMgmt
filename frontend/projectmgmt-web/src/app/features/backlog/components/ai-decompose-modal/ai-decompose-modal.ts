import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  ViewChild,
  inject,
  signal,
  computed,
  OnInit,
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
  ProjectManagementService,
  ProjectMember,
  SpecTreeNode,
  Sprint,
} from '../../../../core/services/project-management.service';
import { ToastService } from '../../../../core/services/toast.service';
import { firstValueFrom } from 'rxjs';

export interface EpicOption {
  id: string;
  key?: string;
  name: string;
}

export interface ExecutiveSummaryMetrics {
  sprintGoal: string;
  totalStoryPoints: number;
  totalEstimatedHours: number;
  taskCount: number;
  subtaskCount: number;
  roleBreakdown: Record<string, number>;
  specGrounding: string;
}

export interface ChatTurn {
  id: string;
  role: 'user' | 'agent';
  text: string;
  fullContent?: string;
  isContentExpanded?: boolean;
  fileName?: string;
  fileSizeLabel?: string;
  timestamp: string;
  executiveSummary?: ExecutiveSummaryMetrics;
}

export interface SpecDocPreview {
  docId?: string | null;
  title: string;
  section?: string | null;
  content?: string;
  isLoading: boolean;
}

@Component({
  selector: 'app-ai-decompose-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-decompose-modal.html',
  styleUrl: './ai-decompose-modal.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiDecomposeModalComponent implements OnInit, OnDestroy {
  private readonly aiService = inject(AiAgentService);
  private readonly toastService = inject(ToastService);
  private readonly router = inject(Router);
  private readonly projectManagementService = inject(ProjectManagementService);

  @ViewChild('promptTextarea') promptTextarea?: ElementRef<HTMLTextAreaElement>;

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

  // Inline Spec Document Preview state
  readonly activeSpecDocPreview = signal<SpecDocPreview | null>(null);

  // Pasted Document / Meeting Notes Review modal state
  readonly isReviewingPastedDoc = signal<boolean>(false);
  readonly editablePastedText = signal<string>('');

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
  readonly selectedModel = signal<string>('Gemini 3.6 Flash');

  // Modern Thinking Accordion State (Image 7)
  readonly isThinkingExpanded = signal<boolean>(true);
  readonly isThinkingVisible = computed(
    () => this.isStreaming() || this.toolCallsLog().length > 0 || !!this.cleanStreamedThoughts() || !!this.proposal()
  );
  readonly cleanStreamedThoughts = computed(() => {
    const raw = this.streamedThoughts()?.trim();
    if (!raw) return '';
    // Nếu nội dung chứa cấu trúc JSON kỹ thuật thô, ẩn đi để giữ giao diện chuẩn enterprise
    if (
      raw.startsWith('{') ||
      raw.startsWith('[') ||
      raw.includes('"tasks"') ||
      raw.includes('"storyPoints"') ||
      raw.includes('"assigneeName"') ||
      raw.includes('"acceptanceCriteria"')
    ) {
      return '';
    }
    return raw;
  });
  readonly totalToolDurationMs = computed(() => this.toolCallsLog().reduce((acc, t) => acc + (t.durationMs || 0), 0));

  // Proposal & Selection state
  readonly proposal = signal<AiSprintProposalDto | null>(null);
  readonly selectedTaskIds = signal<Record<string, boolean>>({});
  readonly expandedSubtasks = signal<Record<string, boolean>>({});
  readonly inlineEditingTitleId = signal<string | null>(null);
  readonly inlineEditingSpId = signal<string | null>(null);
  readonly isSprintGoalInfoExpanded = signal<boolean>(false);

  // Session History State (Image & Summary requirement)
  readonly isSessionHistoryOpen = signal<boolean>(false);
  readonly projectSessions = signal<any[]>([]);
  readonly sessionsLoading = signal<boolean>(false);

  // Detail Drawer state (Reference Image 5 & Subtask Details)
  readonly activeDrawerTaskId = signal<string | null>(null);
  readonly activeDrawerSubtask = signal<{ parentTask: ProposedTaskDto; subtask: ProposedSubTaskDto } | null>(null);
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

  ngOnInit(): void {
    this.restoreSession();
  }

  toggleToolLog(idx: number): void {
    this.expandedToolIndices.update((prev) => ({ ...prev, [idx]: !prev[idx] }));
  }

  toggleThinkingAccordion(): void {
    this.isThinkingExpanded.update((v) => !v);
  }

  onModelChange(modelName: string): void {
    this.selectedModel.set(modelName);
    this.saveSession();
  }

  openSpecDoc(citationText?: string, documentId?: string | null): void {
    const rawText = citationText || 'Tài liệu Đặc tả Spec Hub';
    const section = this.extractSection(rawText);
    const docTitle = this.extractDocTitle(rawText);

    // Mở Drawer xem tài liệu ngay bên trong modal - KHÔNG làm mất đoạn chat và phiên làm việc
    this.activeSpecDocPreview.set({
      docId: documentId,
      title: docTitle,
      section,
      content: '',
      isLoading: true,
    });

    if (documentId) {
      this.projectManagementService.getFileContent(documentId).subscribe({
        next: (res) => {
          this.activeSpecDocPreview.update((prev) =>
            prev
              ? {
                  ...prev,
                  content: res.content || 'Tài liệu không có nội dung văn bản hoặc ở định dạng nhị phân.',
                  isLoading: false,
                }
              : null
          );
          this.scrollPreviewToSection(section);
        },
        error: () => {
          this.activeSpecDocPreview.update((prev) =>
            prev
              ? {
                  ...prev,
                  content: 'Không thể nạp nội dung tài liệu này từ máy chủ.',
                  isLoading: false,
                }
              : null
          );
        },
      });
    } else if (this.projectId) {
      this.projectManagementService.getSpecTree(this.projectId).subscribe({
        next: (nodes) => {
          const found = this.searchNodeInTree(nodes, docTitle);
          if (found && !found.isDirectory) {
            this.projectManagementService.getFileContent(found.id).subscribe({
              next: (res) => {
                this.activeSpecDocPreview.update((prev) =>
                  prev
                    ? {
                        ...prev,
                        docId: found.id,
                        title: found.name,
                        content: res.content || 'Tài liệu không có nội dung văn bản.',
                        isLoading: false,
                      }
                    : null
                );
                this.scrollPreviewToSection(section);
              },
              error: () => {
                this.activeSpecDocPreview.update((prev) =>
                  prev
                    ? {
                        ...prev,
                        docId: found.id,
                        title: found.name,
                        isLoading: false,
                        content: `Tài liệu: ${found.name}\n(Trích dẫn từ Spec Hub trong quá trình lập kế hoạch Sprint)`,
                      }
                    : null
                );
              },
            });
          } else {
            // Nếu không tìm thấy tên chính xác, tìm tệp văn bản đầu tiên trong kho Spec Hub
            const firstFile = this.findFirstNonDirFile(nodes);
            if (firstFile) {
              this.projectManagementService.getFileContent(firstFile.id).subscribe({
                next: (res) => {
                  this.activeSpecDocPreview.update((prev) =>
                    prev
                      ? {
                          ...prev,
                          docId: firstFile.id,
                          title: firstFile.name,
                          content: res.content || 'Tài liệu không có nội dung văn bản.',
                          isLoading: false,
                        }
                      : null
                  );
                  this.scrollPreviewToSection(section);
                },
                error: () => {
                  this.activeSpecDocPreview.update((prev) =>
                    prev
                      ? {
                          ...prev,
                          docId: firstFile.id,
                          title: firstFile.name,
                          isLoading: false,
                          content: `Tài liệu: ${docTitle}\nTrích dẫn trực tiếp từ phiên làm việc và ngữ cảnh dự án.`,
                        }
                      : null
                  );
                },
              });
            } else {
              this.activeSpecDocPreview.update((prev) =>
                prev
                  ? {
                      ...prev,
                      isLoading: false,
                      content: `Tài liệu: ${docTitle}\nTrích dẫn trực tiếp từ phiên làm việc và ngữ cảnh dự án.`,
                    }
                  : null
              );
            }
          }
        },
        error: () => {
          this.activeSpecDocPreview.update((prev) =>
            prev
              ? {
                  ...prev,
                  isLoading: false,
                  content: `Tài liệu: ${docTitle}`,
                }
              : null
          );
        },
      });
    }
  }

  closeSpecDocPreview(): void {
    this.activeSpecDocPreview.set(null);
  }

  openSpecDocInNewTab(citationText?: string, documentId?: string | null, sectionParam?: string | null): void {
    const preview = this.activeSpecDocPreview();
    const docId = documentId || preview?.docId || null;
    const title = citationText || preview?.title || '';
    const cleanName = this.extractDocTitle(title);
    const section = sectionParam || preview?.section || this.extractSection(citationText);

    const queryParams: Record<string, string> = {};
    if (docId) queryParams['docId'] = docId;
    if (cleanName) queryParams['docName'] = cleanName;
    if (section) queryParams['section'] = section;

    const urlTree = this.router.createUrlTree(['/projects', this.projectId, 'documents'], { queryParams });
    const url = this.router.serializeUrl(urlTree);
    window.open(url, '_blank');
  }

  private extractDocTitle(citation?: string): string {
    if (!citation) return 'Tài liệu đặc tả';
    let clean = citation.trim();
    // Bỏ các tiền tố thường gặp
    clean = clean.replace(/^(tài liệu spec hub|tài liệu|spec hub|ghi chú cuộc họp|ghi chú|biên bản)[\s:]+/i, '');
    const dashIdx = clean.indexOf('-');
    if (dashIdx > 0) {
      clean = clean.substring(0, dashIdx).trim();
    }
    return clean;
  }

  private extractSection(citation?: string): string | null {
    if (!citation) return null;
    const parts = citation.split('-');
    if (parts.length > 1) {
      return parts.slice(1).join('-').trim();
    }
    const colonParts = citation.split(':');
    if (colonParts.length > 1) {
      return colonParts.slice(1).join(':').trim();
    }
    return null;
  }

  private searchNodeInTree(nodes: SpecTreeNode[], searchStr: string): SpecTreeNode | null {
    if (!searchStr) return null;
    const lower = searchStr.toLowerCase().trim();
    const stripExt = (name: string) => name.replace(/\.[^/.]+$/, '').trim().toLowerCase();
    const targetBase = stripExt(lower);

    // 1. Khớp chính xác hoặc khớp tên gốc bỏ đuôi
    for (const node of nodes) {
      if (node.isDirectory) {
        if (node.children?.length) {
          const found = this.searchNodeInTree(node.children, searchStr);
          if (found) return found;
        }
        continue;
      }
      const nodeLower = node.name.toLowerCase();
      const nodeBase = stripExt(nodeLower);
      if (nodeLower === lower || nodeBase === targetBase) {
        return node;
      }
    }

    // 2. Khớp chuỗi con
    for (const node of nodes) {
      if (node.isDirectory) {
        if (node.children?.length) {
          const found = this.searchNodeInTree(node.children, searchStr);
          if (found) return found;
        }
        continue;
      }
      const nodeLower = node.name.toLowerCase();
      const nodeBase = stripExt(nodeLower);
      if (nodeLower.includes(lower) || lower.includes(nodeLower) || nodeBase.includes(targetBase) || targetBase.includes(nodeBase)) {
        return node;
      }
    }
    return null;
  }

  private findFirstNonDirFile(nodes: SpecTreeNode[]): SpecTreeNode | null {
    for (const node of nodes) {
      if (!node.isDirectory) return node;
      if (node.children?.length) {
        const found = this.findFirstNonDirFile(node.children);
        if (found) return found;
      }
    }
    return null;
  }

  private scrollPreviewToSection(section?: string | null): void {
    if (!section) return;
    setTimeout(() => {
      const container = document.querySelector('.doc-preview-body');
      if (!container) return;
      const clean = section.trim().toLowerCase();
      const preEl = container.querySelector('pre') as HTMLElement | null;
      if (preEl) {
        const text = (preEl.innerText || '').toLowerCase();
        const idx = text.indexOf(clean);
        if (idx >= 0) {
          const lineNum = text.substring(0, idx).split('\n').length;
          preEl.scrollTop = Math.max(0, (lineNum - 2) * 22);
        }
      }
    }, 350);
  }

  openReviewPastedDoc(): void {
    this.editablePastedText.set(this.pastedDocumentText() || this.promptInput());
    this.isReviewingPastedDoc.set(true);
  }

  saveReviewedPastedDoc(): void {
    const text = this.editablePastedText().trim();
    this.pastedDocumentText.set(text);
    this.isReviewingPastedDoc.set(false);
    this.saveSession();
    this.toastService.success('Tài liệu', 'Đã lưu nội dung văn bản cuộc họp.');
  }

  closeReviewPastedDoc(): void {
    this.isReviewingPastedDoc.set(false);
  }

  onPromptChange(val: string): void {
    this.promptInput.set(val);
    this.adjustTextareaHeight();
  }

  onPromptKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.submitDecompose();
    }
  }

  adjustTextareaHeight(): void {
    setTimeout(() => {
      const el = this.promptTextarea?.nativeElement;
      if (!el) return;
      el.style.height = 'auto';
      const nextHeight = Math.min(Math.max(el.scrollHeight, 44), 180);
      el.style.height = `${nextHeight}px`;
    });
  }

  private getStorageKey(): string {
    return `projectmgmt.ai-agent-session.${this.projectId || 'default'}`;
  }

  saveSession(): void {
    if (typeof localStorage === 'undefined' || !this.projectId) return;
    try {
      const data = {
        chatHistory: this.chatHistory(),
        proposal: this.proposal(),
        toolCallsLog: this.toolCallsLog(),
        streamedThoughts: this.streamedThoughts(),
        activeSessionId: this.activeSessionId(),
        selectedModel: this.selectedModel(),
      };
      localStorage.setItem(this.getStorageKey(), JSON.stringify(data));
    } catch {
      // Ignore storage quota limits
    }
  }

  private restoreSession(): void {
    if (typeof localStorage === 'undefined' || !this.projectId) return;
    try {
      const serialized = localStorage.getItem(this.getStorageKey());
      if (!serialized) return;
      const data = JSON.parse(serialized);
      if (data) {
        if (Array.isArray(data.chatHistory) && data.chatHistory.length > 0) {
          data.chatHistory.forEach((turn: ChatTurn) => {
            if (turn.role === 'user' && !turn.fullContent && turn.text) {
              turn.fullContent = turn.text;
            }
          });
          this.chatHistory.set(data.chatHistory);
        }
        if (data.proposal) {
          this.proposal.set(data.proposal);
        }
        if (Array.isArray(data.toolCallsLog) && data.toolCallsLog.length > 0) {
          this.toolCallsLog.set(data.toolCallsLog);
        } else if (data.proposal) {
          this.toolCallsLog.set([
            { toolName: 'spec_hub_search', summaryVi: 'Tìm kiếm tài liệu đặc tả liên quan trong Spec Hub', durationMs: 7 },
            { toolName: 'spec_hub_read_document', summaryVi: 'Đọc phân đoạn tài liệu đặc tả yêu cầu dự án', durationMs: 14 },
            { toolName: 'backlog_get_stories', summaryVi: 'Lấy danh sách các User Story đang có trong Backlog', durationMs: 13 },
            { toolName: 'sprint_get_history_and_velocity', summaryVi: 'Truy xuất vận tốc Scrum và các hạng mục dở dang', durationMs: 37 },
            { toolName: 'team_get_dev_capacities', summaryVi: 'Phân tích kỹ năng và số giờ khả dụng của thành viên đội Scrum', durationMs: 25 },
          ]);
        }
        if (data.streamedThoughts) {
          this.streamedThoughts.set(data.streamedThoughts);
        }
        if (data.activeSessionId) {
          this.activeSessionId.set(data.activeSessionId);
        }
        if (data.selectedModel) {
          this.selectedModel.set(data.selectedModel);
        }
      }
    } catch {
      // Ignore malformed storage
    }
  }

  startNewSession(): void {
    this.cancelStream();
    this.chatHistory.set([]);
    this.proposal.set(null);
    this.toolCallsLog.set([]);
    this.streamedThoughts.set('');
    this.activeSessionId.set('');
    this.errorMessage.set(null);
    this.promptInput.set('');
    this.pastedDocumentText.set('');
    this.attachedFile.set(null);
    this.selectedTaskIds.set({});
    this.expandedSubtasks.set({});
    this.isThinkingExpanded.set(false);
    if (typeof localStorage !== 'undefined' && this.projectId) {
      localStorage.removeItem(this.getStorageKey());
    }
    this.toastService?.success('Đã khởi tạo phiên làm việc AI mới.');
  }

  ngOnDestroy(): void {
    this.cancelStream();
  }

  closeModal(): void {
    this.saveSession();
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
    if (pastedText && pastedText.length > 80 && !this.pastedDocumentText()) {
      event.preventDefault();
      this.pastedDocumentText.set(pastedText.trim());
      this.toastService.info('Dán nội dung', 'Đã tiếp nhận văn bản từ clipboard. Bạn có thể bấm "Xem & Sửa" để kiểm tra.');
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
        fullContent: combinedText || promptText || docText,
        isContentExpanded: false,
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
    this.isThinkingExpanded.set(true);

    this.abortController = new AbortController();

    try {
      await this.aiService.streamDecomposeSprint(
        {
          projectId: this.projectId,
          text: combinedText || 'Phân tích tài liệu dự án và lập kế hoạch Sprint tiếp theo.',
          prompt: promptText || 'Tạo Sprint Goal, Tasks và Subtasks chuẩn Scrum.',
          file,
          model: this.selectedModel(),
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
            if (!delta) return;
            // Loại bỏ hoàn toàn các chuỗi JSON payload kỹ thuật khỏi hiển thị luồng suy luận
            const lower = delta.toLowerCase();
            if (
              lower.includes('"storypoints"') ||
              lower.includes('"tasks"') ||
              lower.includes('"epictitle"') ||
              lower.includes('"assigneename"') ||
              lower.includes('"estimatedhours"') ||
              lower.includes('"acceptancecriteria"') ||
              lower.includes('"subtasks"') ||
              lower.includes('"documentcitations"') ||
              (delta.includes('{') && delta.includes('}'))
            ) {
              return;
            }
            this.streamedThoughts.update((prev) => (prev + delta).slice(-2500));
          },
          onProposalReady: (prop) => {
            this.normalizeProposal(prop);
            this.proposal.set(prop);
            this.activeSessionId.set(prop.sessionId);

            // Auto-expand all subtasks so tree wire connectors are immediately visible
            const autoExpand: Record<string, boolean> = {};
            for (const t of prop.tasks) {
              if (t.subtasks && t.subtasks.length > 0) {
                autoExpand[t.id] = true;
              }
            }
            this.expandedSubtasks.set(autoExpand);

            const doneTime = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });

            // Calculate role breakdown & subtask metrics for Executive Summary
            const roleBreakdown: Record<string, number> = {};
            let subCount = 0;
            for (const t of prop.tasks) {
              if (t.subtasks && t.subtasks.length > 0) {
                subCount += t.subtasks.length;
                for (const s of t.subtasks) {
                  const r = s.role || 'Kỹ thuật';
                  roleBreakdown[r] = (roleBreakdown[r] || 0) + 1;
                }
              } else {
                const r = t.type === 'Bug' ? 'QA' : (t.tags?.find((tag) => tag.includes('Front') || tag.includes('Back')) || 'Chung');
                roleBreakdown[r] = (roleBreakdown[r] || 0) + 1;
              }
            }

            this.chatHistory.update((list) => [
              ...list,
              {
                id: `agent_${Date.now()}`,
                role: 'agent',
                text: `Đã hoàn tất phân tách ${prop.tasks.length} công việc (${prop.totalStoryPoints} SP) với Sprint Goal: "${prop.sprintGoal}".`,
                timestamp: doneTime,
                executiveSummary: {
                  sprintGoal: prop.sprintGoal,
                  totalStoryPoints: prop.totalStoryPoints,
                  totalEstimatedHours: prop.totalEstimatedHours,
                  taskCount: prop.tasks.length,
                  subtaskCount: subCount,
                  roleBreakdown,
                  specGrounding: prop.tasks[0]?.documentCitations?.[0] || 'Tài liệu Đặc tả Kiến trúc Spec Hub',
                },
              },
            ]);
            this.saveSession();
          },
          onCompleted: () => {
            this.isStreaming.set(false);
            this.isThinkingExpanded.set(true); // Always keep workflow trace open so user can inspect anytime
            this.saveSession();
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

      // Normalize each subtask as a full task entity
      t.subtasks.forEach((sub, sIdx) => {
        if (!sub.id) sub.id = `${t.id}_sub_${sIdx + 1}`;
        if (!sub.priority) sub.priority = t.priority || 'Medium';
        if (!sub.status) sub.status = 'To Do';
        if (!sub.storyPoints || sub.storyPoints <= 0) sub.storyPoints = 1;
        if (!sub.estimatedHours || sub.estimatedHours <= 0) sub.estimatedHours = 4;
        if (!sub.role) sub.role = 'Kỹ thuật';
        if (!sub.assigneeName) sub.assigneeName = t.assigneeName;
        if (!sub.assigneeUserId) sub.assigneeUserId = t.assigneeUserId;
        if (!sub.description) {
          sub.description = `Triển khai phân hệ kỹ thuật chuyên biệt cho "${sub.title}" theo đúng tiêu chuẩn kiến trúc và tài liệu đặc tả của dự án.`;
        }
        if (!sub.acceptanceCriteria || sub.acceptanceCriteria.length === 0) {
          sub.acceptanceCriteria = [
            `Hoàn tất các bước mã nguồn/kịch bản cho '${sub.title}' đúng yêu cầu kỹ thuật`,
            `Thực hiện kiểm thử đơn vị hoặc kiểm thử hồi quy đạt 100% không phát sinh lỗi`
          ];
        }
        if (!sub.documentCitations || sub.documentCitations.length === 0) {
          sub.documentCitations = t.documentCitations && t.documentCitations.length > 0
            ? [...t.documentCitations]
            : ['Tài liệu kiến trúc & đặc tả kỹ thuật dự án'];
        }
      });
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
    this.activeDrawerSubtask.set(null);
    this.activeDrawerTaskId.set(task.id);
    this.drawerRegenerateFeedback.set('');
  }

  closeTaskDrawer(): void {
    this.activeDrawerTaskId.set(null);
    this.persistProposalEdits();
  }

  openSubtaskDrawer(parentTask: ProposedTaskDto, subtask: ProposedSubTaskDto, event?: Event): void {
    event?.stopPropagation();
    this.activeDrawerTaskId.set(null);
    if (!subtask.description) {
      subtask.description = `Triển khai phân hệ kỹ thuật chuyên biệt cho "${subtask.title}" theo đúng tiêu chuẩn kiến trúc và tài liệu đặc tả của dự án.`;
    }
    if (!subtask.acceptanceCriteria || subtask.acceptanceCriteria.length === 0) {
      subtask.acceptanceCriteria = [
        `Hoàn tất các bước mã nguồn/kịch bản cho '${subtask.title}' đúng yêu cầu kỹ thuật`,
        `Thực hiện kiểm thử đơn vị hoặc kiểm thử hồi quy đạt 100% không phát sinh lỗi`
      ];
    }
    if (!subtask.documentCitations || subtask.documentCitations.length === 0) {
      subtask.documentCitations = parentTask.documentCitations && parentTask.documentCitations.length > 0
        ? [...parentTask.documentCitations]
        : ['Tài liệu kiến trúc & đặc tả kỹ thuật dự án'];
    }
    this.activeDrawerSubtask.set({ parentTask, subtask });
  }

  closeSubtaskDrawer(): void {
    this.activeDrawerSubtask.set(null);
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  onSubtaskAssigneeChange(subtask: ProposedSubTaskDto, memberDisplayName: string): void {
    const matched = this.members.find((m) => m.displayName === memberDisplayName);
    subtask.assigneeName = matched ? matched.displayName : memberDisplayName || null;
    subtask.assigneeUserId = matched?.userId ?? null;
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  cycleSubtaskPriority(subtask: ProposedSubTaskDto, event: Event): void {
    event.stopPropagation();
    const scale = ['Low', 'Medium', 'High', 'Highest'];
    const curIdx = scale.indexOf(subtask.priority || 'Medium');
    subtask.priority = scale[(curIdx + 1) % scale.length];
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  deleteProposedSubtask(parentTask: ProposedTaskDto, subtaskId: string, event?: Event): void {
    event?.stopPropagation();
    parentTask.subtasks = (parentTask.subtasks || []).filter((s) => s.id !== subtaskId);
    if (this.activeDrawerSubtask()?.subtask?.id === subtaskId) {
      this.activeDrawerSubtask.set(null);
    }
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  addSubtaskAc(subtask: ProposedSubTaskDto): void {
    if (!subtask.acceptanceCriteria) subtask.acceptanceCriteria = [];
    subtask.acceptanceCriteria.push('Hoàn tất và vượt qua kiểm thử tính năng');
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  deleteSubtaskAc(subtask: ProposedSubTaskDto, index: number): void {
    if (!subtask.acceptanceCriteria) return;
    subtask.acceptanceCriteria.splice(index, 1);
    this.onTaskFieldChanged();
    this.persistProposalEdits();
  }

  toggleSessionHistoryDropdown(event: MouseEvent): void {
    event.stopPropagation();
    this.isSessionHistoryOpen.update((v) => !v);
    if (this.isSessionHistoryOpen() && this.projectId) {
      this.loadProjectSessions();
    }
  }

  loadProjectSessions(): void {
    if (!this.projectId) return;
    this.sessionsLoading.set(true);
    this.aiService.getProjectSessions(this.projectId).subscribe({
      next: (sessions) => {
        this.projectSessions.set(sessions);
        this.sessionsLoading.set(false);
      },
      error: () => {
        this.sessionsLoading.set(false);
      },
    });
  }

  restoreSessionFromHistory(session: any): void {
    this.isSessionHistoryOpen.set(false);
    this.toastService.info('Phiên làm việc', `Đang khôi phục phiên "${session.sprintGoal}"...`);
    this.aiService.getStagedProposal(session.sessionId).subscribe({
      next: (prop: AiSprintProposalDto) => {
        this.normalizeProposal(prop);
        this.proposal.set(prop);
        this.activeSessionId.set(prop.sessionId);
        const autoExpand: Record<string, boolean> = {};
        for (const t of prop.tasks) {
          if (t.subtasks && t.subtasks.length > 0) autoExpand[t.id] = true;
        }
        this.expandedSubtasks.set(autoExpand);
        this.saveSession();
        this.toastService.success('Khôi phục phiên', 'Đã nạp lại kế hoạch đề xuất thành công.');
      },
      error: () => {
        this.toastService.error('Khôi phục', 'Phiên đề xuất này đã hết hạn lưu trữ trên máy chủ.');
      },
    });
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
        storyPoints: 1,
        priority: 'Medium',
        status: 'To Do',
        description: `Triển khai các yêu cầu kỹ thuật chi tiết cho ${task.title}.`,
        acceptanceCriteria: ['Hoàn tất triển khai mã nguồn', 'Kiểm thử đạt 100% không phát sinh lỗi'],
        documentCitations: task.documentCitations && task.documentCitations.length > 0
          ? [...task.documentCitations]
          : ['Tài liệu kiến trúc & đặc tả kỹ thuật dự án'],
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

  copyToClipboard(text?: string): void {
    if (!text) return;
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(text);
      this.toastService?.success('Đã sao chép nội dung vào bộ nhớ tạm.');
    }
  }
}
