import {
  AfterViewInit,
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
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import {
  AiAgentService,
  AiConversationTurnDto,
  AiExcelExportSpecDto,
  AiSprintProposalDto,
  AiStepProgressEvent,
  AiToolCallEvent,
  AiTurnTraceItemDto,
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
import { ExcelDataService } from '../../../../core/services/excel-data.service';
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
  /** Ý định hội thoại do Trợ Lý AI phân loại ở backend (sprint_plan, project_qa, export_excel...) */
  intent?: string | null;
  /** Hành động được kích hoạt trong lượt trả lời (export_excel, push_backlog, apply_sprint) */
  actionType?: string | null;
  /** Danh sách công cụ dữ liệu thật đã gọi trong lượt trả lời này */
  toolCalls?: AiToolCallEvent[];
  /** Chuỗi hành động chuẩn hóa theo phong cách Thinking Log */
  traceItems?: AiTurnTraceItemDto[];
  /** Tóm tắt một dòng trên thanh Thinking khi thu gọn */
  thinkingSummary?: string | null;
  /** Tổng thời gian suy luận / xử lý lượt này (ms) */
  durationMs?: number | null;
  /** Tóm tắt suy luận sạch nếu có */
  thoughts?: string | null;
  /** Các thẻ trí nhớ (#mem-X) mà Agent đã trỏ tới */
  referencedMemoryAnchors?: string[];
  /** Lượt trả lời này sinh ra / cập nhật bảng đề xuất công việc */
  hasProposalTable?: boolean;
  /** Đặc tả phạm vi & bộ lọc xuất tệp Excel (.xlsx) gắn riêng cho lượt này */
  exportSpec?: AiExcelExportSpecDto | null;
}

export interface SpecDocPreview {
  docId?: string | null;
  title: string;
  section?: string | null;
  content?: string;
  highlightedHtml?: SafeHtml;
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
export class AiDecomposeModalComponent implements OnInit, AfterViewInit, OnDestroy {
  private static readonly MESSAGE_PAGE_SIZE = 12;
  private static readonly MAX_DOM_TURNS = 24;

  private readonly aiService = inject(AiAgentService);
  private readonly toastService = inject(ToastService);
  private readonly router = inject(Router);
  private readonly projectManagementService = inject(ProjectManagementService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly excelDataService = inject(ExcelDataService);
  private loadingOlderGuard = false;

  @ViewChild('promptTextarea') promptTextarea?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('chatScrollBody') chatScrollBody?: ElementRef<HTMLElement>;

  @Input({ required: true }) projectId = '';
  @Input() projectName = 'Dự án';
  @Input() projectKey = 'PRJ';
  @Input() members: ProjectMember[] = [];
  @Input() epics: EpicOption[] = [];
  @Input() sprints: Sprint[] = [];
  @Input() canCommitSprint = true;

  @Output() closed = new EventEmitter<void>();
  @Output() applied = new EventEmitter<{ sprintId: string; sprintName: string; createdCount: number }>();
  /** Phát ra khi dữ liệu dự án thay đổi nhưng vẫn giữ nguyên đoạn hội thoại (ví dụ: đẩy vào kho Backlog) */
  @Output() dataChanged = new EventEmitter<void>();

  // Chat & Input state
  readonly promptInput = signal<string>('');
  readonly pastedDocumentText = signal<string>('');
  readonly attachedFile = signal<File | null>(null);
  readonly isDragOver = signal<boolean>(false);
  readonly canSubmitPrompt = computed(
    () =>
      !this.isStreaming() &&
      (this.promptInput().trim().length > 0 ||
        this.pastedDocumentText().trim().length > 0 ||
        this.attachedFile() !== null)
  );
  readonly chatHistory = signal<ChatTurn[]>([]);
  readonly hasOlderMessages = signal<boolean>(false);
  readonly oldestLoadedIndex = signal<number | null>(null);
  readonly totalMessagesCount = signal<number>(0);
  readonly isLoadingOlderMessages = signal<boolean>(false);
  readonly showScrollToBottom = signal<boolean>(false);
  readonly remainingOlderCount = computed(() => Math.max(0, this.oldestLoadedIndex() ?? 0));
  readonly expandedTurnTraces = signal<Record<string, boolean>>({});
  readonly isPushingBacklog = signal<boolean>(false);

  /** Lượt trả lời gần nhất của Trợ Lý AI có sinh/cập nhật bảng đề xuất, nơi bảng công việc được gắn liền mạch vào hội thoại */
  readonly proposalAnchorTurnId = computed<string | null>(() => {
    const list = this.chatHistory();
    for (let i = list.length - 1; i >= 0; i--) {
      const t = list[i];
      if (t.role === 'agent' && (t.hasProposalTable || t.executiveSummary)) return t.id;
    }
    return null;
  });

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

  // Modern Thinking Accordion State (ALWAYS collapsed by default)
  readonly isThinkingExpanded = signal<boolean>(false);
  readonly thinkingElapsedSeconds = signal<number>(0);
  readonly expandedTraceRows = signal<Record<string, boolean>>({});
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

  // Applied project workflow & priority options (sourced from DB / active config)
  readonly availableWorkflowStatuses = computed<string[]>(() => {
    const svc = this.projectManagementService.projectStatuses().map((s) => s.name);
    if (svc.length > 0) return svc;
    const applied = this.proposal()?.appliedWorkflowStatuses ?? [];
    if (applied.length > 0) return applied;
    return ['To Do', 'In Progress', 'Code Review', 'Done'];
  });

  readonly defaultInitialStatus = computed<string>(() => {
    const svcInitial = this.projectManagementService.projectStatuses().find((s) => s.isInitial)?.name;
    return svcInitial || this.proposal()?.initialStatus || this.availableWorkflowStatuses()[0] || 'To Do';
  });

  readonly availablePriorities = computed<string[]>(() => {
    const svc = this.projectManagementService.projectPriorities().map((p) => p.name);
    if (svc.length > 0) return svc;
    const applied = this.proposal()?.appliedPriorities ?? [];
    if (applied.length > 0) return applied;
    return ['Urgent', 'High', 'Medium', 'Low'];
  });

  readonly defaultPriorityName = computed<string>(() => {
    const list = this.availablePriorities();
    return (
      list.find((p) => ['medium', 'normal', 'major'].includes(p.toLowerCase())) ??
      list[Math.floor(list.length / 2)] ??
      'Medium'
    );
  });

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
  private thinkingStartTimeMs = 0;
  private thinkingTimerId: ReturnType<typeof setInterval> | null = null;

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
      return [];
    }
    return Array.from(citeSet);
  });

  ngOnInit(): void {
    this.restoreSession();
  }

  ngAfterViewInit(): void {
    if (this.chatHistory().length > 0 || this.proposal()) {
      this.scrollChatToBottom(false);
    }
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

    const isMeetingNoteCite = /cuộc họp|ghi chú|biên bản|meeting|notes|yêu cầu/i.test(docTitle);
    const pastedText = this.pastedDocumentText() || this.promptInput();

    // 1. Nếu có documentId hợp lệ (từ backend hoặc Spec Hub)
    if (documentId && documentId !== 'meeting_notes') {
      this.projectManagementService.getFileContent(documentId).subscribe({
        next: (res) => {
          const rawContent = res.content || 'Tài liệu không có nội dung văn bản hoặc ở định dạng nhị phân.';
          this.activeSpecDocPreview.update((prev) =>
            prev
              ? {
                  ...prev,
                  title: docTitle,
                  content: rawContent,
                  highlightedHtml: this.buildHighlightHtml(rawContent, section),
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
      return;
    }

    // 2. Nếu là ghi chú cuộc họp / biên bản người dùng dán vào
    if (isMeetingNoteCite && pastedText) {
      this.activeSpecDocPreview.update((prev) =>
        prev
          ? {
              ...prev,
              docId: 'meeting_notes',
              title: 'Ghi chú & Biên bản cuộc họp đã nạp',
              content: pastedText,
              highlightedHtml: this.buildHighlightHtml(pastedText, section),
              isLoading: false,
            }
          : null
      );
      this.scrollPreviewToSection(section);
      return;
    }

    // 3. Tra cứu trong cây thư mục Spec Hub của dự án
    const projId = this.projectId || this.projectManagementService.currentProject()?.id || (typeof localStorage !== 'undefined' ? localStorage.getItem('pm_active_project_id') : null);
    if (projId) {
      this.projectManagementService.getSpecTree(projId).subscribe({
        next: (nodes) => {
          const found = this.searchNodeInTree(nodes, docTitle);
          if (found && !found.isDirectory) {
            this.projectManagementService.getFileContent(found.id).subscribe({
              next: (res) => {
                const rawContent = res.content || 'Tài liệu không có nội dung văn bản.';
                this.activeSpecDocPreview.update((prev) =>
                  prev
                    ? {
                        ...prev,
                        docId: found.id,
                        title: found.name,
                        content: rawContent,
                        highlightedHtml: this.buildHighlightHtml(rawContent, section),
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
            // Nếu không tìm thấy tệp trong Spec Hub
            if (pastedText && (isMeetingNoteCite || /biên bản|cuộc họp/i.test(docTitle))) {
              this.activeSpecDocPreview.update((prev) =>
                prev
                  ? {
                      ...prev,
                      docId: 'meeting_notes',
                      title: 'Ghi chú & Biên bản cuộc họp đã nạp',
                      content: pastedText,
                      highlightedHtml: this.buildHighlightHtml(pastedText, section),
                      isLoading: false,
                    }
                  : null
              );
              this.scrollPreviewToSection(section);
            } else if (docTitle === 'GEMINI.md') {
              const geminiText = `# Tiêu chuẩn Quy tắc Dự án (GEMINI.md)\n\n1. BẢO MẬT TUYỆT ĐỐI & CHỐNG RÒ RỈ SECRET/KEY/URL/IP\n- Nghiêm cấm đưa bất kỳ thông tin nhạy cảm nào vào source code.\n- Secret lưu tại appsettings.Local.json hoặc .env.\n\n2. TỐI ƯU TỐC ĐỘ PHẢN HỒI & HIỆU SUẤT AGENT\n- Đi thẳng vào trọng tâm, giải phóng file lock IIS với app_offline.htm.\n\n3. QUY TẮC BẮT BUỘC VỀ ICON & EMOJI\n- Tuyệt đối không dùng emoji từ bàn phím.\n- Bắt buộc sử dụng icon Google Material Symbols Outlined.\n\n4. QUY TRÌNH PHÂN QUYỀN SCRUM TỪ TRÊN XUỐNG\n- Org Owner > Lead/Creator > Scrum Master > Dev/QA > Viewer.\n\n5. TIÊU CHUẨN GIAO DIỆN & TRẢI NGHIỆM NGƯỜI DÙNG\n- Responsive đa màn hình, không tràn lề.`;
              this.activeSpecDocPreview.update((prev) =>
                prev
                  ? {
                      ...prev,
                      title: 'GEMINI.md (Quy chuẩn dự án)',
                      content: geminiText,
                      highlightedHtml: this.buildHighlightHtml(geminiText, section),
                      isLoading: false,
                    }
                  : null
              );
              this.scrollPreviewToSection(section);
            } else {
              const fallbackMsg = `Tài liệu: ${docTitle}\nPhân đoạn trích dẫn: ${section || 'Toàn văn'}\n\nTrích dẫn trực tiếp từ phiên làm việc và ngữ cảnh dự án.`;
              this.activeSpecDocPreview.update((prev) =>
                prev
                  ? {
                      ...prev,
                      isLoading: false,
                      content: fallbackMsg,
                      highlightedHtml: this.buildHighlightHtml(fallbackMsg, section),
                    }
                  : null
              );
              this.scrollPreviewToSection(section);
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

    if (docId === 'meeting_notes' || /cuộc họp|ghi chú|biên bản/i.test(cleanName)) {
      this.toastService.info('Spec Hub', 'Đây là ghi chú phiên làm việc của bạn, không thuộc tệp Spec Hub.');
      return;
    }

    const queryParams: Record<string, string> = {};
    if (docId) queryParams['docId'] = docId;
    if (cleanName) queryParams['docName'] = cleanName;
    if (section) queryParams['section'] = section;

    const projId = this.projectId || this.projectManagementService.currentProject()?.id || (typeof localStorage !== 'undefined' ? localStorage.getItem('pm_active_project_id') : null);

    const urlTree = this.router.createUrlTree(['/projects', projId, 'documents'], { queryParams });
    const url = this.router.serializeUrl(urlTree);
    window.open(url, '_blank');
  }

  private extractDocTitle(citation?: string): string {
    if (!citation) return 'Tài liệu đặc tả';
    let clean = citation.trim();

    // 1. Nếu có phần mở rộng tệp (.md, .txt, .docx, .xlsx, .pdf, .doc, .json, .yml, .yaml, .csv)
    const fileMatch = clean.match(/([\p{L}\w\.\-_ ]+\.(?:md|txt|docx|xlsx|pdf|doc|json|yml|yaml|csv))/iu);
    if (fileMatch) {
      return fileMatch[1].trim();
    }

    // 2. Bỏ các tiền tố thường gặp ở đầu
    clean = clean.replace(/^(tài liệu đặc tả|tài liệu spec hub|tài liệu|spec hub|ghi chú cuộc họp|ghi chú|biên bản|yêu cầu)[\s:]+/i, '');

    // 3. Tách theo dấu phân cách có khoảng trắng bao quanh (\s+[-–—:]\s+)
    const sepMatch = clean.match(/^(.+?)\s+[-–—:]\s+/);
    if (sepMatch) {
      return sepMatch[1].trim();
    }

    // 4. Nếu có dấu hai chấm
    const colonIdx = clean.indexOf(':');
    if (colonIdx > 0 && colonIdx < clean.length - 1) {
      return clean.substring(0, colonIdx).trim();
    }

    return clean;
  }

  private extractSection(citation?: string): string | null {
    if (!citation) return null;
    let clean = citation.trim();

    // 1. Nếu có tên file với đuôi, section là phần nằm sau tên file đó
    const fileMatch = clean.match(/[\p{L}\w\.\-_ ]+\.(?:md|txt|docx|xlsx|pdf|doc|json|yml|yaml|csv)/iu);
    if (fileMatch && fileMatch.index !== undefined) {
      const afterFile = clean.substring(fileMatch.index + fileMatch[0].length).trim();
      const stripped = afterFile.replace(/^[\s\-–—:]+/, '').trim();
      if (stripped) return stripped;
    }

    // 2. Tách theo dấu phân cách có khoảng trắng: " - ", " – ", " — ", " : "
    const parts = clean.split(/\s+[-–—:]\s+/);
    if (parts.length > 1) {
      return parts.slice(1).join(' - ').trim();
    }

    // 3. Tách theo dấu ":" nếu có
    const colonIdx = clean.indexOf(':');
    if (colonIdx > 0 && colonIdx < clean.length - 1) {
      const prefix = clean.substring(0, colonIdx).toLowerCase();
      if (!/^(tài liệu|spec hub|ghi chú|biên bản)/.test(prefix)) {
        return clean.substring(colonIdx + 1).trim();
      }
    }

    return null;
  }

  private searchNodeInTree(nodes: SpecTreeNode[], searchStr: string): SpecTreeNode | null {
    if (!searchStr) return null;
    const lower = searchStr.toLowerCase().trim();
    const stripExt = (name: string) => name.replace(/\.[^/.]+$/, '').trim().toLowerCase();
    const targetBase = stripExt(lower);

    // BƯỚC 1: Tìm kiếm khớp chính xác (Exact Match) tên tệp hoặc tên gốc bỏ đuôi trên toàn bộ cây
    const findExact = (list: SpecTreeNode[]): SpecTreeNode | null => {
      for (const node of list) {
        if (!node.isDirectory) {
          const nodeLower = node.name.toLowerCase();
          const nodeBase = stripExt(nodeLower);
          if (nodeLower === lower || nodeBase === targetBase) {
            return node;
          }
        } else if (node.children?.length) {
          const childFound = findExact(node.children);
          if (childFound) return childFound;
        }
      }
      return null;
    };

    const exactMatch = findExact(nodes);
    if (exactMatch) return exactMatch;

    // BƯỚC 2: Chỉ khi không có bất kỳ tệp nào khớp chính xác, mới tìm kiếm chuỗi con (Partial Match)
    if (lower.length > 3) {
      const findPartial = (list: SpecTreeNode[]): SpecTreeNode | null => {
        for (const node of list) {
          if (!node.isDirectory) {
            const nodeLower = node.name.toLowerCase();
            const nodeBase = stripExt(nodeLower);
            if (nodeLower.includes(lower) || lower.includes(nodeLower) || nodeBase.includes(targetBase) || targetBase.includes(nodeBase)) {
              return node;
            }
          } else if (node.children?.length) {
            const childFound = findPartial(node.children);
            if (childFound) return childFound;
          }
        }
        return null;
      };
      return findPartial(nodes);
    }
    return null;
  }

  private buildHighlightHtml(content: string, section?: string | null): SafeHtml | undefined {
    if (!content) return undefined;
    const escapeHtml = (str: string) =>
      str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');

    if (!section) {
      return this.sanitizer.bypassSecurityTrustHtml(escapeHtml(content));
    }

    const cleanSection = section.trim();
    const candidates: string[] = [cleanSection];

    // Bỏ các tiền tố thường gặp "Mục", "Phần", "Chương", "Điều", "Epic"...
    const strippedPrefix = cleanSection.replace(/^(mục|phần|chương|điều|epic|bước|step|section)\s*[:\.]?\s*/i, '').trim();
    if (strippedPrefix && strippedPrefix.length >= 2 && !candidates.includes(strippedPrefix)) {
      candidates.push(strippedPrefix);
    }

    // Các cụm từ chia theo dấu hai chấm hoặc gạch nối
    const subParts = cleanSection.split(/[:\-–—]/).map((p) => p.trim()).filter((p) => p.length >= 3);
    for (const part of subParts) {
      if (!candidates.includes(part)) {
        candidates.push(part);
      }
    }

    const lowerContent = content.toLowerCase();
    let bestStart = -1;
    let matchLen = 0;

    // Ưu tiên 1: Tìm dòng tiêu đề Markdown (bắt đầu dòng với #, ** hoặc phân đoạn rõ ràng)
    const headingPatterns: RegExp[] = [];
    for (const cand of candidates) {
      const escaped = cand.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      headingPatterns.push(new RegExp(`(?:^|\\n)\\s*(?:#{1,6}\\s*|\\*\\*\\s*)?(?:mục\\s*)?(${escaped}[^\\n]{0,60})`, 'i'));
    }

    for (const pattern of headingPatterns) {
      const match = pattern.exec(content);
      if (match && match.index !== undefined) {
        // Khớp vị trí cụm từ chính
        const fullMatch = match[0];
        const captured = match[1] || fullMatch;
        const offsetInMatch = fullMatch.indexOf(captured);
        bestStart = match.index + (offsetInMatch >= 0 ? offsetInMatch : 0);
        matchLen = captured.length;
        break;
      }
    }

    // Ưu tiên 2: Tìm kiếm chuỗi đầy đủ ở bất kỳ đâu trong tài liệu nếu chưa thấy tiêu đề
    if (bestStart < 0) {
      for (const cand of candidates) {
        const lowerCand = cand.toLowerCase();
        const idx = lowerContent.indexOf(lowerCand);
        if (idx >= 0) {
          bestStart = idx;
          matchLen = cand.length;
          break;
        }
      }
    }

    // Nếu chưa tìm thấy cụm từ, thử với các từ khóa có độ dài >= 4 hoặc số La Mã
    if (bestStart < 0) {
      const keywords = cleanSection.split(/[\s:,\-]+/).filter((w) => w.length >= 4 || /^[ivxlcdm]+$/i.test(w));
      for (const kw of keywords) {
        const idx = lowerContent.indexOf(kw.toLowerCase());
        if (idx >= 0) {
          bestStart = idx;
          matchLen = kw.length;
          break;
        }
      }
    }

    if (bestStart >= 0) {
      const before = escapeHtml(content.substring(0, bestStart));
      const matched = escapeHtml(content.substring(bestStart, bestStart + matchLen));
      const after = escapeHtml(content.substring(bestStart + matchLen));
      const html = `${before}<mark id="spec-target-highlight" class="spec-highlight-mark">${matched}</mark>${after}`;
      return this.sanitizer.bypassSecurityTrustHtml(html);
    }

    return this.sanitizer.bypassSecurityTrustHtml(escapeHtml(content));
  }

  private scrollPreviewToSection(section?: string | null): void {
    const tryScroll = (): boolean => {
      const container = document.querySelector('.doc-preview-body') as HTMLElement | null;
      if (!container) return false;

      const highlightEl = container.querySelector('#spec-target-highlight') as HTMLElement | null;
      if (highlightEl) {
        highlightEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
        highlightEl.classList.add('spec-highlight-pulse');
        setTimeout(() => highlightEl?.classList.remove('spec-highlight-pulse'), 3500);
        return true;
      }

      // Fallback: nếu không có thẻ mark (ví dụ tài liệu thuần pre), thử cuộn ước tính
      if (section) {
        const preEl = container.querySelector('.preview-content-text') as HTMLElement | null;
        if (!preEl) return false;
        const text = (preEl.innerText || '').toLowerCase();
        const clean = section.trim().toLowerCase();
        let targetIdx = text.indexOf(clean);
        if (targetIdx < 0) {
          const kw = clean.split(/[\s:,\-]+/).find((w) => w.length > 3);
          if (kw) targetIdx = text.indexOf(kw);
        }
        if (targetIdx >= 0) {
          const lineNum = text.substring(0, targetIdx).split('\n').length;
          const totalLines = Math.max(1, text.split('\n').length);
          const targetScrollTop = (lineNum / totalLines) * preEl.scrollHeight;
          container.scrollTo({ top: Math.max(0, targetScrollTop - 80), behavior: 'smooth' });
          preEl.classList.add('spec-highlight-pulse');
          setTimeout(() => preEl?.classList.remove('spec-highlight-pulse'), 3500);
          return true;
        }
      }
      return false;
    };

    setTimeout(() => {
      if (!tryScroll()) {
        setTimeout(() => {
          if (!tryScroll()) {
            setTimeout(tryScroll, 600);
          }
        }, 250);
      }
    }, 120);
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
      const fullList = this.chatHistory();
      const tailChat = fullList.slice(-AiDecomposeModalComponent.MESSAGE_PAGE_SIZE);
      const effectiveTotal = Math.max(this.totalMessagesCount(), fullList.length);
      const sessionId = this.activeSessionId();
      const savedOldest =
        effectiveTotal > tailChat.length
          ? effectiveTotal - tailChat.length
          : tailChat.length > 0
            ? 0
            : null;
      const savedHasOlder = Boolean(sessionId && savedOldest !== null && savedOldest > 0);

      const data = {
        chatHistory: tailChat,
        proposal: this.proposal(),
        toolCallsLog: this.toolCallsLog(),
        streamedThoughts: this.streamedThoughts(),
        activeSessionId: sessionId,
        selectedModel: this.selectedModel(),
        totalMessagesCount: effectiveTotal,
        oldestLoadedIndex: savedOldest,
        hasOlderMessages: savedHasOlder,
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
          const rawList = data.chatHistory as ChatTurn[];
          rawList.forEach((turn: ChatTurn) => {
            if (turn.role === 'user' && !turn.fullContent && turn.text) {
              turn.fullContent = turn.text;
            }
          });
          const tail = rawList.slice(-AiDecomposeModalComponent.MESSAGE_PAGE_SIZE);
          const effectiveTotal =
            typeof data.totalMessagesCount === 'number'
              ? Math.max(data.totalMessagesCount, rawList.length)
              : rawList.length;
          const computedOldest = Math.max(0, effectiveTotal - tail.length);
          const restoredOldest =
            typeof data.oldestLoadedIndex === 'number'
              ? Math.max(data.oldestLoadedIndex, computedOldest)
              : computedOldest;

          this.chatHistory.set(tail);
          this.totalMessagesCount.set(effectiveTotal);
          this.oldestLoadedIndex.set(restoredOldest);
          this.hasOlderMessages.set(
            typeof data.hasOlderMessages === 'boolean'
              ? data.hasOlderMessages || (Boolean(data.activeSessionId) && restoredOldest > 0)
              : Boolean(data.activeSessionId && restoredOldest > 0)
          );
        }
        if (data.proposal) {
          this.proposal.set(data.proposal);
        }
        if (Array.isArray(data.toolCallsLog) && data.toolCallsLog.length > 0) {
          this.toolCallsLog.set(data.toolCallsLog);
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
        // Đồng bộ chỉ số phân trang nếu dữ liệu cache cũ chưa lưu tổng số lượt
        if (data.activeSessionId && typeof data.totalMessagesCount !== 'number') {
          this.aiService
            .getSessionMessages(data.activeSessionId, this.projectId, undefined, AiDecomposeModalComponent.MESSAGE_PAGE_SIZE)
            .subscribe({
              next: (page) => {
                if (this.activeSessionId() !== data.activeSessionId) return;
                this.totalMessagesCount.set(page.totalCount);
                this.oldestLoadedIndex.set(page.oldestLoadedIndex ?? 0);
                this.hasOlderMessages.set(page.hasOlderMessages);
                if (page.messages.length > 0) {
                  const currentHasTable = this.proposal()?.tasks?.length ? true : false;
                  this.chatHistory.set(this.mapDtoTurnsToChatTurns(page.messages, currentHasTable));
                  this.scrollChatToBottom(false);
                }
              },
            });
        }
        if (this.chatHistory().length > 0 || this.proposal()) {
          this.scrollChatToBottom(false);
        }
      }
    } catch {
      // Ignore malformed storage
    }
  }

  private resetDraftUiState(): void {
    this.cancelStream();
    this.chatHistory.set([]);
    this.hasOlderMessages.set(false);
    this.oldestLoadedIndex.set(null);
    this.totalMessagesCount.set(0);
    this.isLoadingOlderMessages.set(false);
    this.showScrollToBottom.set(false);
    this.loadingOlderGuard = false;
    this.proposal.set(null);
    this.toolCallsLog.set([]);
    this.streamedThoughts.set('');
    this.activeSessionId.set('');
    this.expandedTurnTraces.set({});
    this.expandedTraceRows.set({});
    this.thinkingElapsedSeconds.set(0);
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
  }

  /**
   * Tạo mới một cuộc hội thoại trống trên giao diện mà KHÔNG xóa các phiên làm việc đã lưu trong lịch sử.
   */
  startNewSession(): void {
    this.resetDraftUiState();
    this.toastService?.success('Đã tạo cuộc hội thoại mới.');
  }

  private startThinkingTimer(): void {
    this.stopThinkingTimer();
    this.thinkingStartTimeMs = Date.now();
    this.thinkingElapsedSeconds.set(0);
    this.thinkingTimerId = setInterval(() => {
      if (this.thinkingStartTimeMs > 0) {
        this.thinkingElapsedSeconds.set((Date.now() - this.thinkingStartTimeMs) / 1000);
      }
    }, 100);
  }

  private stopThinkingTimer(): void {
    if (this.thinkingTimerId !== null) {
      clearInterval(this.thinkingTimerId);
      this.thinkingTimerId = null;
    }
    if (this.thinkingStartTimeMs > 0) {
      this.thinkingElapsedSeconds.set((Date.now() - this.thinkingStartTimeMs) / 1000);
    }
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
    this.stopThinkingTimer();
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
      this.validateAndAttachFile(file);
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
      this.validateAndAttachFile(file);
    }
  }

  onClipboardPaste(event: ClipboardEvent): void {
    const files = event.clipboardData?.files;
    if (files && files.length > 0) {
      event.preventDefault();
      this.validateAndAttachFile(files[0]);
      return;
    }

    const pastedText = event.clipboardData?.getData('text/plain');
    if (pastedText && pastedText.length > 80 && !this.pastedDocumentText()) {
      event.preventDefault();
      this.pastedDocumentText.set(pastedText.trim());
      this.toastService.info('Dán nội dung', 'Đã tiếp nhận văn bản từ clipboard. Bạn có thể bấm "Xem & Sửa" để kiểm tra.');
    }
  }

  private validateAndAttachFile(file: File): void {
    const allowedExts = ['.docx', '.pdf', '.txt', '.md', '.xlsx', '.json', '.csv'];
    const lowerName = (file.name || '').toLowerCase();
    const hasAllowedExt = allowedExts.some((ext) => lowerName.endsWith(ext));
    if (!hasAllowedExt) {
      const msg = `Định dạng tệp "${file.name}" không được hỗ trợ. Chỉ chấp nhận: ${allowedExts.join(', ')}.`;
      this.errorMessage.set(msg);
      this.toastService.error('Tệp không hợp lệ', msg);
      return;
    }

    if (file.size <= 0) {
      const msg = `Tệp "${file.name}" bị rỗng (0 byte). Vui lòng chọn tệp có nội dung.`;
      this.errorMessage.set(msg);
      this.toastService.error('Tệp rỗng', msg);
      return;
    }

    const maxBytes = 30 * 1024 * 1024;
    if (file.size > maxBytes) {
      const msg = `Tệp "${file.name}" vượt quá giới hạn dung lượng 30 MB.`;
      this.errorMessage.set(msg);
      this.toastService.error('Tệp quá lớn', msg);
      return;
    }

    this.attachedFile.set(file);
    this.errorMessage.set(null);
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
        text: promptText
          ? (promptText.length > 1500 ? `${promptText.slice(0, 280).trim()}...` : promptText)
          : (docText ? `Đã dán nội dung tài liệu (${docText.length} ký tự)` : `Phân tích tệp đính kèm: ${file?.name}`),
        fullContent: combinedText && combinedText !== promptText ? combinedText : (promptText.length > 1500 ? promptText : undefined),
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
    // Mặc định Thinking luôn đóng gọn để không che nội dung trao đổi
    this.isThinkingExpanded.set(false);
    this.startThinkingTimer();

    this.abortController = new AbortController();
    this.scrollChatToBottom();

    // Nội dung gõ rất dài (biên bản họp dán thẳng vào ô chat) được coi là tài liệu, câu ngắn được coi là câu lệnh hội thoại
    const promptIsDocument = promptText.length > 1500;
    const effectiveDoc = [docText, promptIsDocument ? promptText : ''].filter(Boolean).join('\n\n');
    const effectivePrompt = promptIsDocument ? '' : promptText;

    try {
      await this.aiService.streamDecomposeSprint(
        {
          projectId: this.projectId,
          // Chỉ gửi nội dung tài liệu thật người dùng dán vào; không chèn câu mặc định để bộ phân loại ý định đọc đúng câu hỏi
          text: effectiveDoc || undefined,
          prompt: effectivePrompt || (effectiveDoc || file ? 'Phân tích tài liệu dự án và lập kế hoạch Sprint tiếp theo.' : undefined),
          file,
          model: this.selectedModel(),
          // Hội thoại có trạng thái: tiếp tục cùng một phiên Redis để Trợ Lý AI nhớ toàn bộ ngữ cảnh trước đó
          sessionId: this.activeSessionId() || undefined,
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
            this.handleAssistantTurn(prop);
          },
          onCompleted: (done) => {
            this.stopThinkingTimer();
            this.isStreaming.set(false);
            this.isThinkingExpanded.set(false);
            if (done?.totalDurationMs && done.totalDurationMs > 0) {
              this.chatHistory.update((list) => {
                if (list.length === 0) return list;
                const copy = [...list];
                for (let i = copy.length - 1; i >= 0; i--) {
                  if (copy[i].role === 'agent') {
                    const updated = { ...copy[i] };
                    if (!updated.durationMs || updated.durationMs <= 0) {
                      updated.durationMs = done.totalDurationMs;
                    }
                    if (!updated.thinkingSummary) {
                      updated.thinkingSummary = this.buildFallbackThinkingSummary(updated);
                    }
                    copy[i] = updated;
                    break;
                  }
                }
                return copy;
              });
            }
            this.saveSession();
            this.loadProjectSessions();
          },
          onError: (err) => {
            this.stopThinkingTimer();
            this.isThinkingExpanded.set(false);
            this.errorMessage.set(err.message || 'Đã xảy ra lỗi khi phân tách công việc.');
            this.isStreaming.set(false);
          },
        },
        this.abortController.signal
      );
    } catch (err: any) {
      this.stopThinkingTimer();
      this.isThinkingExpanded.set(false);
      if (err?.name !== 'AbortError') {
        this.errorMessage.set('Kết nối luồng SSE bị gián đoạn. Vui lòng thử lại.');
      }
      this.isStreaming.set(false);
    }
  }

  /**
   * Xử lý một lượt trả lời của Trợ Lý AI trong hội thoại có trạng thái.
   * - Các ý định sinh/cập nhật kế hoạch sẽ làm mới bảng đề xuất và gắn bảng vào lượt trả lời này.
   * - Các ý định hỏi đáp, xuất Excel, từ chối ngoài phạm vi giữ nguyên bảng đề xuất đang có.
   * - Các ý định hành động (đẩy Backlog / áp dụng Sprint) tự động thực thi trên đề xuất hiện có.
   */
  private handleAssistantTurn(prop: AiSprintProposalDto): void {
    this.normalizeProposal(prop);
    const intent = (prop.intent || 'sprint_plan').toString();
    const action = (prop.triggerAction || 'none').toString();
    const producesTable = ['sprint_plan', 'backlog_plan', 'refine_proposal'].includes(intent) && prop.tasks.length > 0;

    // Backend luôn bảo toàn danh sách đề xuất của phiên (kể cả lượt hỏi đáp / xuất Excel) nên có thể gán trực tiếp
    this.proposal.set(prop);
    if (prop.sessionId) this.activeSessionId.set(prop.sessionId);

    if (producesTable) {
      const autoExpand: Record<string, boolean> = {};
      for (const t of prop.tasks) {
        if (t.subtasks && t.subtasks.length > 0) autoExpand[t.id] = true;
      }
      this.expandedSubtasks.set(autoExpand);
      this.selectedTaskIds.set({});
    }

    const subCount = prop.tasks.reduce((sum, t) => sum + (t.subtasks?.length || 0), 0);
    const fallbackReply = producesTable
      ? `Tôi đã lập ${prop.tasks.length} hạng mục công việc chính và ${subCount} việc con (${prop.totalStoryPoints} SP) dựa trên dữ liệu thực của dự án. Bạn có thể chỉnh sửa trực tiếp trong bảng bên dưới, yêu cầu tôi điều chỉnh tiếp, đẩy vào kho Backlog hoặc áp dụng vào Sprint tiếp theo.`
      : 'Tôi đã xử lý xong yêu cầu của bạn.';

    const historyArr = prop.conversationHistory ?? [];
    const lastBackendAssistantTurn = [...historyArr]
      .reverse()
      .find((h) => h.role === 'assistant');

    const traceItems =
      lastBackendAssistantTurn?.traceItems && lastBackendAssistantTurn.traceItems.length > 0
        ? lastBackendAssistantTurn.traceItems
        : this.buildTraceItemsFromLog(this.toolCallsLog());
    const durationMs =
      lastBackendAssistantTurn?.durationMs ??
      Math.max(100, Math.round(this.thinkingElapsedSeconds() * 1000));
    const thoughts =
      (lastBackendAssistantTurn?.thoughts || '').trim() ||
      this.cleanStreamedThoughts() ||
      null;
    const referencedMemoryAnchors = lastBackendAssistantTurn?.referencedMemoryAnchors ?? [];
    const turnExportSpec = lastBackendAssistantTurn?.exportSpec ?? prop.exportSpec ?? null;

    const doneTime = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    const turnId = lastBackendAssistantTurn?.id || `agent_${Date.now()}`;
    const newTurn: ChatTurn = {
      id: turnId,
      role: 'agent',
      text: (prop.assistantReply || '').trim() || fallbackReply,
      timestamp: doneTime,
      intent,
      actionType: action,
      toolCalls: [...this.toolCallsLog()],
      traceItems,
      durationMs,
      thoughts,
      referencedMemoryAnchors,
      hasProposalTable: producesTable,
      exportSpec: action === 'export_excel' ? turnExportSpec : null,
    };
    newTurn.thinkingSummary =
      (lastBackendAssistantTurn?.thinkingSummary || '').trim() ||
      this.buildFallbackThinkingSummary(newTurn);

    this.chatHistory.update((list) => {
      const appended = [...list, newTurn];
      if (appended.length > AiDecomposeModalComponent.MAX_DOM_TURNS && this.activeSessionId()) {
        return appended.slice(-AiDecomposeModalComponent.MAX_DOM_TURNS);
      }
      return appended;
    });
    const serverTotal = prop.totalConversationTurns ?? prop.conversationHistory?.length ?? 0;
    const loadedLen = this.chatHistory().length;
    const nextTotal =
      serverTotal > 0
        ? Math.max(serverTotal, loadedLen)
        : Math.max(this.totalMessagesCount() + 2, loadedLen);
    this.totalMessagesCount.set(nextTotal);
    if (this.activeSessionId() && nextTotal > loadedLen) {
      const newOldest = nextTotal - loadedLen;
      if (this.oldestLoadedIndex() === null || this.oldestLoadedIndex()! < newOldest) {
        this.oldestLoadedIndex.set(newOldest);
        this.hasOlderMessages.set(newOldest > 0);
      }
    }
    this.saveSession();
    this.scrollChatToBottom(true);

    // Thực thi hành động hội thoại trên dữ liệu thật
    if (action === 'export_excel') {
      void this.exportProjectExcelFromChat(turnExportSpec);
    } else if (action === 'push_backlog') {
      void this.commitToBacklogProposal();
    } else if (action === 'apply_sprint') {
      if (!this.canCommitSprint) {
        this.appendAssistantNote('Bạn chưa có quyền quản trị Sprint để áp dụng trực tiếp vào Sprint. Hãy dùng nút "Đẩy vào Kho Backlog" hoặc nhờ Quản lý dự án/Scrum Master áp dụng.');
        return;
      }
      void this.commitSprintProposal();
    }
  }

  toggleTurnTrace(turnId: string): void {
    this.expandedTurnTraces.update((prev) => ({ ...prev, [turnId]: !prev[turnId] }));
  }

  toggleTraceRow(rowKey: string, event?: Event): void {
    event?.stopPropagation();
    this.expandedTraceRows.update((prev) => ({ ...prev, [rowKey]: !prev[rowKey] }));
  }

  buildTraceItemsFromLog(tools?: AiToolCallEvent[]): AiTurnTraceItemDto[] {
    if (!tools || tools.length === 0) return [];
    return tools.map((t) => ({
      kind: 'mcp',
      actionVerb: t.actionVerb || this.inferActionVerb(t.toolName),
      target: t.targetLabel || this.inferTargetLabel(t.toolName),
      summary: t.summaryVi || t.toolName,
      detail: t.detailPreview ?? null,
      durationMs: t.durationMs ?? null,
      status: 'done',
    }));
  }

  buildFallbackThinkingSummary(turn: ChatTurn): string {
    const items =
      turn.traceItems && turn.traceItems.length > 0
        ? turn.traceItems
        : this.buildTraceItemsFromLog(turn.toolCalls);
    const secNum = Math.max(0.1, (turn.durationMs ?? 0) / 1000);
    const secLabel = turn.durationMs && turn.durationMs > 0 ? `${secNum.toFixed(1)}s` : '';
    const opCount = items.length;
    if (secLabel && opCount > 0) {
      return `Đã suy nghĩ xong trong ${secLabel} · ${opCount} thao tác`;
    }
    if (opCount > 0) {
      return `Đã suy nghĩ xong · ${opCount} thao tác`;
    }
    return secLabel ? `Đã suy nghĩ xong trong ${secLabel}` : 'Đã suy nghĩ xong';
  }

  getActionRowIcon(verb?: string | null): string {
    const v = (verb || '').toLowerCase();
    if (v.includes('phân tích') || v.includes('đọc')) return 'manage_search';
    if (v.includes('trí nhớ') || v.includes('trỏ')) return 'bookmarks';
    if (v.includes('mcp') || v.includes('truy vấn')) return 'database';
    if (v.includes('suy luận')) return 'psychology';
    if (v.includes('kiểm')) return 'verified_user';
    return 'bolt';
  }

  private inferActionVerb(toolName?: string): string {
    const k = (toolName || '').toLowerCase();
    if (k.includes('intent')) return 'Phân tích';
    if (k.includes('memory')) return 'Trỏ trí nhớ';
    if (k.includes('gemini') || k.includes('reason')) return 'Suy luận AI';
    if (k.includes('guardrail') || k.includes('audit')) return 'Kiểm chuẩn';
    if (k.includes('project') || k.includes('backlog') || k.includes('spec') || k.includes('sprint') || k.includes('team')) {
      return 'Truy vấn MCP';
    }
    return 'Thực thi';
  }

  private inferTargetLabel(toolName?: string): string {
    const k = (toolName || '').toLowerCase();
    if (k.includes('intent')) return 'Ngữ cảnh hội thoại & Ý định';
    if (k.includes('memory')) return 'Chỉ mục ghi nhớ phiên (#mem-X)';
    if (k.includes('active_config')) return 'Cấu hình Active (Workflow, Priority, Type, Label)';
    if (k.includes('backlog')) return 'Kho Backlog tồn đọng';
    if (k.includes('spechub')) return 'Tài liệu Đặc tả (Spec Hub)';
    if (k.includes('sprint')) return 'Sprint & Năng lực thực thi';
    if (k.includes('team')) return 'Thành viên & Tải công việc';
    if (k.includes('gemini')) return 'Mô hình Trợ lý AI';
    if (k.includes('guardrail')) return 'Bộ kiểm chuẩn & Ngữ cảnh dự án';
    return toolName || 'Công cụ hệ thống';
  }

  getIntentLabel(intent?: string | null): string {
    switch (intent) {
      case 'sprint_plan':
        return 'Lập kế hoạch Sprint';
      case 'backlog_plan':
        return 'Lập danh sách cho kho Backlog';
      case 'refine_proposal':
        return 'Điều chỉnh đề xuất';
      case 'project_qa':
        return 'Tra cứu dự án';
      case 'export_excel':
        return 'Xuất Excel';
      case 'push_backlog':
        return 'Đẩy vào kho Backlog';
      case 'apply_sprint':
        return 'Áp dụng vào Sprint';
      case 'out_of_scope_refusal':
        return 'Ngoài phạm vi';
      default:
        return '';
    }
  }

  scrollToLatestMessage(smooth = true): void {
    this.scrollChatToBottom(smooth);
  }

  onChatScroll(event: Event): void {
    const el = event.target as HTMLElement | null;
    if (!el) return;

    const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    this.showScrollToBottom.set(distanceFromBottom > 180 && this.chatHistory().length > 0);

    if (
      el.scrollTop <= 64 &&
      this.hasOlderMessages() &&
      !this.isLoadingOlderMessages() &&
      !this.isStreaming() &&
      !this.loadingOlderGuard
    ) {
      this.loadOlderMessages();
    }
  }

  loadOlderMessages(): void {
    const sessionId = this.activeSessionId();
    const beforeIdx = this.oldestLoadedIndex();
    if (!sessionId || !this.projectId || !this.hasOlderMessages() || this.isLoadingOlderMessages()) {
      return;
    }
    if (beforeIdx === null || beforeIdx <= 0) {
      this.hasOlderMessages.set(false);
      return;
    }

    this.isLoadingOlderMessages.set(true);
    this.loadingOlderGuard = true;

    const scrollEl = this.chatScrollBody?.nativeElement;
    const prevScrollHeight = scrollEl?.scrollHeight ?? 0;
    const prevScrollTop = scrollEl?.scrollTop ?? 0;

    this.aiService
      .getSessionMessages(sessionId, this.projectId, beforeIdx, AiDecomposeModalComponent.MESSAGE_PAGE_SIZE)
      .subscribe({
        next: (page) => {
          const olderTurns = this.mapDtoTurnsToChatTurns(page.messages ?? [], false);
          if (olderTurns.length > 0) {
            this.chatHistory.update((curr) => {
              const existingIds = new Set(curr.map((t) => t.id));
              const uniqueOlder = olderTurns.filter((t) => !existingIds.has(t.id));
              return uniqueOlder.length > 0 ? [...uniqueOlder, ...curr] : curr;
            });
          }
          this.totalMessagesCount.set(page.totalCount);
          this.oldestLoadedIndex.set(page.oldestLoadedIndex ?? 0);
          this.hasOlderMessages.set(page.hasOlderMessages);

          // Giữ nguyên vị trí đang đọc sau khi chèn thêm tin nhắn cũ ở đỉnh danh sách
          requestAnimationFrame(() => {
            const el = this.chatScrollBody?.nativeElement;
            if (el) {
              const heightDelta = el.scrollHeight - prevScrollHeight;
              el.scrollTop = Math.max(0, prevScrollTop + heightDelta);
            }
            this.isLoadingOlderMessages.set(false);
            setTimeout(() => {
              this.loadingOlderGuard = false;
            }, 180);
          });
        },
        error: () => {
          this.isLoadingOlderMessages.set(false);
          setTimeout(() => {
            this.loadingOlderGuard = false;
          }, 300);
          this.toastService.error('Tải tin nhắn cũ', 'Không thể tải thêm lịch sử hội thoại cũ hơn.');
        },
      });
  }

  private mapDtoTurnsToChatTurns(dtos: AiConversationTurnDto[], markLatestProposalTable: boolean): ChatTurn[] {
    const mapped: ChatTurn[] = dtos.map((h, idx) => {
      const turn: ChatTurn = {
        id: h.id || `restored_${idx}_${Date.now()}`,
        role: h.role === 'user' ? 'user' : 'agent',
        text: h.content || '',
        fullContent: h.role === 'user' && h.fullContent && h.fullContent !== h.content ? h.fullContent : undefined,
        fileName: h.fileName || undefined,
        timestamp: h.timestampUtc
          ? new Date(h.timestampUtc).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
          : '',
        intent: h.intent ?? undefined,
        actionType: h.actionType ?? undefined,
        traceItems: h.traceItems ?? [],
        thinkingSummary: h.thinkingSummary ?? null,
        durationMs: h.durationMs ?? null,
        thoughts: h.thoughts ?? null,
        referencedMemoryAnchors: h.referencedMemoryAnchors ?? [],
        exportSpec: h.exportSpec ?? null,
      };
      if (turn.role === 'agent' && !turn.thinkingSummary && ((turn.traceItems?.length ?? 0) > 0 || (turn.durationMs ?? 0) > 0)) {
        turn.thinkingSummary = this.buildFallbackThinkingSummary(turn);
      }
      return turn;
    });

    if (markLatestProposalTable) {
      for (let i = mapped.length - 1; i >= 0; i--) {
        if (mapped[i].role === 'agent' && ['sprint_plan', 'backlog_plan', 'refine_proposal'].includes(mapped[i].intent || '')) {
          mapped[i].hasProposalTable = true;
          break;
        }
      }
    }
    return mapped;
  }

  private scrollChatToBottom(smooth = true): void {
    const performScroll = () => {
      const el = this.chatScrollBody?.nativeElement;
      if (!el) return;
      el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
      this.showScrollToBottom.set(false);
    };

    requestAnimationFrame(() => {
      performScroll();
      if (!smooth) {
        setTimeout(performScroll, 60);
        setTimeout(performScroll, 180);
      } else {
        setTimeout(performScroll, 120);
      }
    });
  }

  private appendAssistantNote(text: string, actionType: string = 'none'): void {
    const doneTime = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    this.chatHistory.update((list) => [
      ...list,
      { id: `agent_note_${Date.now()}`, role: 'agent', text, timestamp: doneTime, actionType },
    ]);
    this.saveSession();
    this.scrollChatToBottom();
  }

  /** Xuất tệp Excel theo đúng phạm vi, bộ lọc hoặc chế độ chi tiết mà Trợ Lý AI đã phân tích cho lượt hội thoại này */
  async exportProjectExcelFromChat(spec?: AiExcelExportSpecDto | null): Promise<void> {
    try {
      let items = [...this.projectManagementService.workItems()];
      const allSprints = this.sprints?.length ? this.sprints : this.projectManagementService.sprints();
      const targetDetailId = spec?.scope === 'task_detail' && spec.searchKeyword !== 'ai_proposal'
        ? spec.issueIds?.[0]
        : undefined;

      if (targetDetailId) {
        try {
          const freshItem = await firstValueFrom(this.projectManagementService.getIssue(targetDetailId));
          if (freshItem?.id) {
            const exists = items.some((i) => i.id?.toLowerCase() === freshItem.id.toLowerCase());
            items = exists
              ? items.map((i) => (i.id?.toLowerCase() === freshItem.id.toLowerCase() ? freshItem : i))
              : [...items, freshItem];
          }
        } catch {
          // Bỏ qua lỗi mạng nếu dữ liệu đã có sẵn trong tín hiệu cục bộ
        }
      }

      const today = new Date().toISOString().slice(0, 10).replace(/-/g, '');
      const safeKey = (this.projectKey || 'ProjectMgmt').replace(/[^\w\-]/g, '');
      const effectiveSpec: AiExcelExportSpecDto = spec ?? {
        scope: 'project_all',
        title: `BÁO CÁO TỔNG HỢP DỰ ÁN - ${(this.projectName || safeKey).toUpperCase()}`,
        suggestedFileName: `${safeKey}_Bao_Cao_Du_An_${today}.xlsx`,
        includeSubtasks: true,
        includeLegend: true,
      };

      const res = this.excelDataService.exportFromAiSpec(items, allSprints, effectiveSpec, {
        projectName: this.projectName || safeKey,
        projectKey: safeKey,
        projectStatuses: this.projectManagementService.projectStatuses(),
        proposal: this.proposal(),
      });
      this.toastService.success('Xuất Excel thành công', `Đã tạo tệp ${res.filename} (${res.itemCount} mục).`);
    } catch {
      this.errorMessage.set('Không thể tạo tệp Excel từ dữ liệu dự án. Vui lòng thử lại.');
    }
  }

  /** Đẩy toàn bộ đề xuất hiện có vào kho Product Backlog (SprintId = null), chuyển hẳn các mục lấy từ Backlog cũ thay vì nhân bản */
  async commitToBacklogProposal(): Promise<void> {
    const prop = this.proposal();
    if (!prop || prop.tasks.length === 0 || this.isPushingBacklog() || this.isApplying()) return;

    this.isPushingBacklog.set(true);
    this.errorMessage.set(null);
    try {
      const res = await firstValueFrom(
        this.aiService.applyProposal({
          sessionId: this.activeSessionId() || prop.sessionId,
          projectId: this.projectId,
          targetSprintId: null,
          sprintGoal: prop.sprintGoal || '',
          tasks: prop.tasks,
          pushToBacklog: true,
        })
      );
      const committedCount = prop.tasks.length;
      this.appendAssistantNote(
        res.message ||
          `Đã lưu ${res.createdTaskCount} công việc và ${res.createdSubtaskCount} việc con vào kho Product Backlog của dự án.`,
        'push_backlog_done'
      );
      // Các mục đã lưu thật vào dự án: dọn khỏi bảng đề xuất để không lưu trùng ở lượt sau
      this.proposal.set({ ...prop, tasks: [], totalStoryPoints: 0, totalEstimatedHours: 0 });
      this.persistProposalEdits();
      this.saveSession();
      this.dataChanged.emit();
      this.toastService.show(`Đã đẩy ${committedCount} hạng mục vào kho Backlog.`, 'success');
    } catch (err: any) {
      const msg = err?.error?.message || 'Không thể đẩy đề xuất vào kho Backlog.';
      this.errorMessage.set(msg);
      this.appendAssistantNote(`Chưa thể lưu vào kho Backlog: ${msg}`);
    } finally {
      this.isPushingBacklog.set(false);
    }
  }

  private normalizeProposal(prop: AiSprintProposalDto): void {
    if (!prop.tasks) prop.tasks = [];
    const fallbackStatus = prop.initialStatus || this.defaultInitialStatus();
    const fallbackPriority = this.defaultPriorityName();
    prop.tasks.forEach((t, idx) => {
      if (!t.id) t.id = `task_${idx + 1}`;
      if (!t.status) t.status = fallbackStatus;
      if (!t.priority) t.priority = fallbackPriority;
      if (t.isReady === undefined) t.isReady = true;
      if (!t.remainingHours || t.remainingHours <= 0) t.remainingHours = t.estimatedHours || 8;
      if (!t.acceptanceCriteria) t.acceptanceCriteria = [];
      if (!t.documentCitations) t.documentCitations = [];
      if (!t.subtasks) t.subtasks = [];

      // Normalize each subtask as a full task entity
      t.subtasks.forEach((sub, sIdx) => {
        if (!sub.id) sub.id = `${t.id}_sub_${sIdx + 1}`;
        if (!sub.priority) sub.priority = t.priority || fallbackPriority;
        if (!sub.status) sub.status = t.status || fallbackStatus;
        if (!sub.storyPoints || sub.storyPoints <= 0) sub.storyPoints = 1;
        if (!sub.estimatedHours || sub.estimatedHours <= 0) sub.estimatedHours = 4;
        if (!sub.role) sub.role = 'Kỹ thuật';
        if (!sub.assigneeName) sub.assigneeName = t.assigneeName;
        if (!sub.assigneeUserId) sub.assigneeUserId = t.assigneeUserId;
        // Không tự bịa mô tả / tiêu chí nghiệm thu / trích dẫn cho việc con: chỉ hiển thị đúng dữ liệu AI sinh ra từ nguồn thật
        if (!sub.acceptanceCriteria) sub.acceptanceCriteria = [];
        if (!sub.documentCitations || sub.documentCitations.length === 0) {
          sub.documentCitations = t.documentCitations && t.documentCitations.length > 0 ? [...t.documentCitations] : [];
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
    if (!subtask.acceptanceCriteria) subtask.acceptanceCriteria = [];
    if (!subtask.documentCitations || subtask.documentCitations.length === 0) {
      subtask.documentCitations = parentTask.documentCitations && parentTask.documentCitations.length > 0
        ? [...parentTask.documentCitations]
        : [];
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
    const scale = this.availablePriorities();
    const curIdx = scale.findIndex((p) => p.toLowerCase() === (subtask.priority || '').toLowerCase());
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
    const titleLabel = session.title || session.sprintGoal || 'Phiên làm việc';
    this.toastService.info('Phiên làm việc', `Đang khôi phục phiên "${titleLabel}"...`);
    this.aiService.getStagedProposal(session.sessionId, AiDecomposeModalComponent.MESSAGE_PAGE_SIZE).subscribe({
      next: (prop: AiSprintProposalDto) => {
        this.normalizeProposal(prop);
        this.proposal.set(prop);
        this.activeSessionId.set(prop.sessionId);
        // Mặc định Thinking luôn đóng gọn kể cả khi mở lại phiên cũ
        this.isThinkingExpanded.set(false);
        this.expandedTurnTraces.set({});
        this.expandedTraceRows.set({});
        this.toolCallsLog.set([]);
        this.streamedThoughts.set('');
        this.isLoadingOlderMessages.set(false);
        this.showScrollToBottom.set(false);
        this.loadingOlderGuard = false;

        const dtoHistory = prop.conversationHistory ?? [];
        const totalTurns = prop.totalConversationTurns ?? dtoHistory.length;
        this.totalMessagesCount.set(totalTurns);
        this.hasOlderMessages.set(Boolean(prop.hasOlderMessages));
        this.oldestLoadedIndex.set(
          prop.oldestLoadedTurnIndex ?? (dtoHistory.length > 0 ? Math.max(0, totalTurns - dtoHistory.length) : null)
        );

        // Khôi phục trang tin nhắn mới nhất theo kiểu Messenger cùng Thinking Log & #mem-X
        if (dtoHistory.length > 0) {
          const restored = this.mapDtoTurnsToChatTurns(dtoHistory, prop.tasks.length > 0);
          if (prop.tasks.length > 0 && !restored.some((t) => t.hasProposalTable)) {
            for (let i = restored.length - 1; i >= 0; i--) {
              if (restored[i].role === 'agent') {
                restored[i].hasProposalTable = true;
                break;
              }
            }
          }
          this.chatHistory.set(restored);
        } else {
          this.chatHistory.set([]);
        }
        const autoExpand: Record<string, boolean> = {};
        for (const t of prop.tasks) {
          if (t.subtasks && t.subtasks.length > 0) autoExpand[t.id] = true;
        }
        this.expandedSubtasks.set(autoExpand);
        this.saveSession();
        // Luôn tự cuộn ngay xuống đoạn hội thoại mới nhất trong phiên vừa mở
        this.scrollChatToBottom(false);
        this.toastService.success('Khôi phục phiên', 'Đã nạp các tin nhắn mới nhất và tự động cuộn xuống cuối phiên.');
      },
      error: () => {
        this.toastService.error('Khôi phục', 'Không thể mở phiên này (không tồn tại hoặc không thuộc quyền sở hữu của bạn).');
      },
    });
  }

  deleteSessionFromHistory(session: any, event: Event): void {
    event.stopPropagation();
    if (!this.projectId || !session?.sessionId) return;
    this.aiService.deleteSession(session.sessionId, this.projectId).subscribe({
      next: () => {
        this.projectSessions.update((list) => list.filter((s) => s.sessionId !== session.sessionId));
        if (this.activeSessionId() === session.sessionId) {
          this.resetDraftUiState();
        }
        this.toastService.success('Xóa phiên', 'Đã xóa phiên hội thoại cũ khỏi dự án.');
      },
      error: () => {
        this.toastService.error('Xóa phiên', 'Không thể xóa phiên làm việc này.');
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
    const order = this.availablePriorities();
    const idx = order.findIndex((p) => p.toLowerCase() === (task.priority || '').toLowerCase());
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
        priority: this.defaultPriorityName(),
        status: this.defaultInitialStatus(),
        description: '',
        acceptanceCriteria: [],
        documentCitations: task.documentCitations && task.documentCitations.length > 0
          ? [...task.documentCitations]
          : [],
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
      this.appendAssistantNote(
        res.message || `Đã áp dụng ${res.createdTaskCount} công việc vào ${res.sprintName}.`,
        'apply_sprint_done'
      );
      // Đề xuất đã được lưu thật vào Sprint: dọn khỏi bảng để mở lại phiên không bị lưu trùng
      this.proposal.set({ ...prop, tasks: [], totalStoryPoints: 0, totalEstimatedHours: 0 });
      this.persistProposalEdits();
      this.saveSession();
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
