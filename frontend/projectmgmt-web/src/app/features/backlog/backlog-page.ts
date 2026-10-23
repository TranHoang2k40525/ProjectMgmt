import { ChangeDetectionStrategy, Component, HostListener, inject, signal, computed, OnInit, AfterViewInit, OnDestroy, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConnectedPosition, OverlayModule } from '@angular/cdk/overlay';
import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { ActivatedRoute, Router } from '@angular/router';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { gsap } from 'gsap';
import { ProjectManagementService, WorkItem, Sprint, SprintMessage } from '../../core/services/project-management.service';
import { IdentityService } from '../../core/services/identity.service';
import { ExcelDataService } from '../../core/services/excel-data.service';
import { ToastService } from '../../core/services/toast.service';
import { ContextMenuComponent, ContextMenuItemAction } from '../../shared/components/context-menu.component';

import { NoProjectStateComponent } from '../../shared/components/no-project-state/no-project-state.component';

@Component({
  selector: 'app-backlog-page',
  standalone: true,
  imports: [CommonModule, FormsModule, OverlayModule, DragDropModule, ContextMenuComponent, NoProjectStateComponent],
  templateUrl: './backlog-page.html',
  styleUrl: './backlog-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BacklogPage implements OnInit, AfterViewInit, OnDestroy {
  private el = inject<ElementRef<HTMLElement>>(ElementRef);
  private entranceTween: gsap.core.Tween | null = null;
  private readonly identityService = inject(IdentityService);
  readonly projectService = inject(ProjectManagementService);
  readonly excelService = inject(ExcelDataService);
  readonly toastService = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly realtimeService = inject(RealtimeService);

  // Sprint Chat & Real-time Discussion State
  readonly sprintMessages = signal<SprintMessage[]>([]);
  readonly isChatLoading = signal<boolean>(false);
  readonly isSendingMessage = signal<boolean>(false);
  readonly chatInputText = signal<string>('');
  private sprintChatUnsubscribe?: () => void;

  readonly projectMembers = computed(() => this.projectService.currentProject()?.members ?? []);

  // Search & Filter State
  searchQuery = signal<string>('');
  selectedTypeFilter = signal<string | null>(null);
  selectedPriorityFilter = signal<string | null>(null);
  selectedAssigneeFilter = signal<string | null>(null);
  selectedEpicFilter = signal<string | null>(null);
  onlyMineFilter = signal<boolean>(false);

  // Context Menu State
  contextMenuVisible = signal<boolean>(false);
  contextMenuX = signal<number>(0);
  contextMenuY = signal<number>(0);
  contextMenuItem = signal<WorkItem | null>(null);

  // Filter Popover Menu Toggle
  isFilterMenuOpen = signal<boolean>(false);

  // Epic Panel State
  isEpicPanelOpen = signal<boolean>(false);
  showNewEpicModal = signal<boolean>(false);
  newEpicName = '';
  newEpicColor = 'purple';

  // Sub-task Inline Expansion State
  expandedSubtasks = signal<Record<string, boolean>>({});
  activeSubtaskInputId = signal<string | null>(null);
  subtaskTitleMap: Record<string, string> = {};

  // Inline Quick Task Creation per Sprint
  inlineTaskTitleMap: Record<string, string> = {};
  activeInlineSprintId = signal<string | null>(null);

  // Quick Action Row Dropdown Menu State
  activeMenuTaskId = signal<string | null>(null);
  activeStatusDropdownTaskId = signal<string | null>(null);
  activeEpicDropdownTaskId = signal<string | null>(null);

  readonly startAlignedOverlayPositions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 8 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -8 },
    { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 8 },
    { originX: 'end', originY: 'top', overlayX: 'end', overlayY: 'bottom', offsetY: -8 },
  ];

  readonly endAlignedOverlayPositions: ConnectedPosition[] = [
    { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 8 },
    { originX: 'end', originY: 'top', overlayX: 'end', overlayY: 'bottom', offsetY: -8 },
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 8 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -8 },
  ];

  readonly hasActivePopover = computed(() => {
    return !!(this.activeMenuTaskId() || this.activeStatusDropdownTaskId() || this.activeEpicDropdownTaskId());
  });

  isRowPopoverActive(taskId: string): boolean {
    return this.activeMenuTaskId() === taskId ||
           this.activeStatusDropdownTaskId() === taskId ||
           this.activeEpicDropdownTaskId() === taskId;
  }

  hasSprintActivePopover(sprintId: string): boolean {
    const activeId = this.activeMenuTaskId() || this.activeStatusDropdownTaskId() || this.activeEpicDropdownTaskId();
    if (!activeId) return false;
    const task = this.workItems().find(w => w.id === activeId);
    return task?.sprintId === sprintId;
  }

  // Inline Task Title Edit State
  editingTaskId = signal<string | null>(null);
  editingTaskTitle = signal<string>('');
  editingStoryPointsTaskId = signal<string | null>(null);
  editingStoryPointsValue = signal<number>(0);

  // Inline Sprint Edit State
  editingSprintId = signal<string | null>(null);
  editingSprintName = signal<string>('');
  editingSprintGoal = signal<string>('');
  editingStartDate = signal<string>('');
  editingEndDate = signal<string>('');

  // Bulk Selection State
  selectedTaskIds = signal<string[]>([]);

  // Service Signals
  readonly sprints = this.projectService.sprints;
  readonly workItems = this.projectService.workItems;
  readonly epics = this.projectService.epics;
  readonly statusNames = computed(() => {
    const s = this.projectService.projectStatuses().map(st => st.name);
    return s.length > 0 ? s : ['To Do', 'In Progress', 'Code Review', 'Done'];
  });
  readonly issueTypes = computed(() => {
    const custom = this.projectService.customIssueTypes().map(t => t.name);
    return Array.from(new Set(['Story', 'Task', 'Bug', 'Use-Case', 'Epic', 'Sub-task', ...custom]));
  });

  ngOnInit(): void {
    const currentProj = this.projectService.currentProject();
    if (currentProj?.id && (!currentProj.members || currentProj.members.length === 0)) {
      this.projectService.loadProjectMembers(currentProj.id).subscribe();
    }

    this.route.queryParams.subscribe(params => {
      const sprintId = params['sprintId'];
      const openChat = params['openChat'];
      if (sprintId && openChat === 'true') {
        const targetSprint = this.sprints().find(s => s.id === sprintId);
        if (targetSprint) {
          this.openSprintDetail(targetSprint);
        }
      }
    });
  }

  ngAfterViewInit(): void {
    if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      const sections = this.el.nativeElement.querySelectorAll<HTMLElement>('.sprint-section');
      if (sections.length) {
        this.entranceTween = gsap.fromTo(
          sections,
          { opacity: 0, y: 16 },
          {
            opacity: 1,
            y: 0,
            duration: 0.4,
            stagger: 0.08,
            ease: 'power2.out',
            clearProps: 'transform,opacity'
          }
        );
      }
    }
  }

  ngOnDestroy(): void {
    this.entranceTween?.kill();
    this.closeSprintDetail();
  }

  // Has active filter applied
  readonly hasActiveFilters = computed(() => {
    return !!(
      this.selectedTypeFilter() ||
      this.selectedPriorityFilter() ||
      this.selectedAssigneeFilter() ||
      this.selectedEpicFilter() ||
      this.onlyMineFilter()
    );
  });

  // Filtered WorkItems List
  readonly filteredWorkItems = computed(() => {
    let items = this.workItems();
    const query = this.searchQuery().trim().toLowerCase();
    const typeFilter = this.selectedTypeFilter();
    const priorityFilter = this.selectedPriorityFilter();
    const assigneeFilter = this.selectedAssigneeFilter();
    const epicFilter = this.selectedEpicFilter();
    const onlyMine = this.onlyMineFilter();

    if (query) {
      items = items.filter(i =>
        i.title.toLowerCase().includes(query) ||
        i.issueKey.toLowerCase().includes(query) ||
        (i.epicName && i.epicName.toLowerCase().includes(query))
      );
    }

    if (onlyMine) {
      const myName = this.identityService.authState().currentUser?.displayName;
      if (myName) {
        items = items.filter(i => i.assigneeName === myName);
      }
    }

    if (assigneeFilter) {
      items = items.filter(i => i.assigneeName === assigneeFilter);
    }

    if (typeFilter) {
      items = items.filter(i => i.issueType === typeFilter);
    }

    if (priorityFilter) {
      items = items.filter(i => i.priority === priorityFilter);
    }

    if (epicFilter) {
      items = items.filter(i => i.epicName === epicFilter);
    }

    return items;
  });

  // CDK Drag and Drop across Sprints
  onSprintDrop(event: CdkDragDrop<WorkItem[]>, targetSprintId: string, targetSprintName: string): void {
    if (event.previousContainer === event.container) {
      const ordered = [...event.container.data]; moveItemInArray(ordered, event.previousIndex, event.currentIndex);
      this.projectService.reorderWorkItems(ordered.map(i => i.id)); return;
    }
    const item = event.item.data as WorkItem;
    if (item && item.sprintId !== targetSprintId) {
      this.projectService.updateWorkItemSprint(item.id, targetSprintId, targetSprintName);
      this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    }
  }

  // Context Menu Handlers
  onContextMenu(event: MouseEvent, item: WorkItem): void {
    event.preventDefault();
    event.stopPropagation();
    this.contextMenuX.set(event.clientX);
    this.contextMenuY.set(event.clientY);
    this.contextMenuItem.set(item);
    this.contextMenuVisible.set(true);
  }

  onContextMenuAction(event: ContextMenuItemAction): void {
    const item = this.contextMenuItem();
    if (!item) return;

    switch (event.action) {
      case 'status':
        if (event.value) {
          const val = event.value;
          const statusMap: Record<string, string> = {
            'TO_DO': 'To Do',
            'IN_PROGRESS': 'In Progress',
            'CODE_REVIEW': 'Code Review',
            'DONE': 'Done'
          };
          const targetStatus = statusMap[val] || val;
          this.projectService.updateWorkItemStatus(item.id, targetStatus);
          this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
        }
        break;

      case 'assign': {
        const myName = this.identityService.authState().currentUser?.displayName || 'Tôi';
        this.projectService.updateWorkItemAssignee(item.id, myName);
        this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
        break;
      }

      case 'ai-breakdown':
        this.projectService.activeDrawerTask.set(item);
        this.toastService.info('AI Breakdown', `Mở xem gợi ý Sub-task cho [${item.issueKey}]`);
        break;

      case 'ai-assign':
        this.projectService.activeDrawerTask.set(item);
        this.toastService.info('AI Assign', `Đang gợi ý người xử lý cho [${item.issueKey}]`);
        break;

      case 'copy':
        navigator.clipboard?.writeText?.(`${item.issueKey}: ${item.title}`);
        this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
        break;

      case 'delete':
        this.projectService.deleteWorkItem(item.id);
        this.toastService.warning('Đã Xóa', `Đã xóa [${item.issueKey}]`);
        break;
    }
  }

  resetFilters(): void {
    this.selectedTypeFilter.set(null);
    this.selectedPriorityFilter.set(null);
    this.selectedAssigneeFilter.set(null);
    this.selectedEpicFilter.set(null);
    this.onlyMineFilter.set(false);
    this.searchQuery.set('');
    this.isFilterMenuOpen.set(false);
  }

  closeAllPopups(): void {
    this.isFilterMenuOpen.set(false);
    this.isEpicPanelOpen.set(false);
    this.contextMenuVisible.set(false);
    this.activeMenuTaskId.set(null);
    this.activeStatusDropdownTaskId.set(null);
    this.activeEpicDropdownTaskId.set(null);
    this.activeInlineSprintId.set(null);
    this.activeSubtaskInputId.set(null);
    this.editingSprintId.set(null);
    this.editingTaskId.set(null);
    this.editingStoryPointsTaskId.set(null);
  }

  startEditingTaskTitle(item: WorkItem, event?: Event): void {
    if (event) event.stopPropagation();
    this.editingTaskId.set(item.id);
    this.editingTaskTitle.set(item.title);
  }

  saveEditingTaskTitle(taskId: string): void {
    const title = this.editingTaskTitle().trim();
    if (title) {
      const item = this.workItems().find(task => task.id === taskId);
      if (item) this.projectService.updateWorkItem({ ...item, title });
      this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    }
    this.editingTaskId.set(null);
  }

  startEditingStoryPoints(item: WorkItem, event?: Event): void {
    event?.stopPropagation();
    this.editingStoryPointsTaskId.set(item.id);
    this.editingStoryPointsValue.set(item.storyPoints ?? 0);
  }

  saveStoryPoints(taskId: string, event?: Event): void {
    event?.stopPropagation();
    const value = Math.max(0, Math.min(999, Number(this.editingStoryPointsValue()) || 0));
    this.projectService.updateWorkItemStoryPoints(taskId, value);
    this.editingStoryPointsTaskId.set(null);
    this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
  }

  updateInlinePriorityNext(taskId: string, currentPriority: WorkItem['priority'], event?: Event): void {
    if (event) event.stopPropagation();
    const map: Record<WorkItem['priority'], WorkItem['priority']> = {
      'Low': 'Medium',
      'Medium': 'High',
      'High': 'Urgent',
      'Urgent': 'Low'
    };
    const nextP = map[currentPriority] || 'Medium';
    this.projectService.updateWorkItemPriority(taskId, nextP);
  }

  toggleStatusDropdown(taskId: string, event: Event): void {
    event.stopPropagation();
    this.activeStatusDropdownTaskId.update(curr => curr === taskId ? null : taskId);
    this.activeEpicDropdownTaskId.set(null);
    this.activeMenuTaskId.set(null);
  }

  toggleEpicDropdown(taskId: string, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    this.activeEpicDropdownTaskId.update(curr => curr === taskId ? null : taskId);
    this.activeStatusDropdownTaskId.set(null);
    this.activeMenuTaskId.set(null);
  }

  toggleFilterMenu(event?: Event): void {
    event?.stopPropagation();
    this.isFilterMenuOpen.update(value => !value);
    this.activeStatusDropdownTaskId.set(null);
    this.activeEpicDropdownTaskId.set(null);
    this.activeMenuTaskId.set(null);
  }

  selectTaskEpic(taskId: string, epicName: string | null, event?: Event): void {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    const epic = this.epics().find(e => e.name === epicName);
    this.projectService.updateWorkItemEpic(taskId, epicName || '', epic?.color || 'indigo');
    this.activeEpicDropdownTaskId.set(null);
    this.toastService.info('Cập Nhật Epic', epicName ? `Gán vào Epic: ${epicName}` : 'Đã bỏ gán Epic');
  }

  getIssueTypeIcon(type: string): string {
    const map: Record<string, string> = {
      'Story': 'bookmark',
      'Task': 'task',
      'Bug': 'bug_report',
      'Use-Case': 'schema',
      'Epic': 'bolt',
      'Sub-task': 'subdirectory_arrow_right'
    };
    return map[type] || 'task_alt';
  }

  readonly activeSprintDetail = signal<Sprint | null>(null);

  onSprintHeaderClick(sprint: Sprint, event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (target.closest('.sprint-actions') || target.closest('.sprint-edit-row') || target.closest('input') || target.closest('select')) {
      return;
    }
    this.openSprintDetail(sprint);
  }

  onWorkItemRowClick(item: WorkItem, event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (
      target.closest('.no-drawer') ||
      target.closest('input') ||
      target.closest('select') ||
      target.closest('textarea') ||
      target.closest('.cell-select') ||
      target.closest('.cell-epic') ||
      target.closest('.cell-priority') ||
      target.closest('.cell-points') ||
      target.closest('.cell-status') ||
      target.closest('.cell-menu') ||
      target.closest('.edit-title-button')
    ) {
      return;
    }
    this.openTask(item, event);
  }

  openSprintDetail(sprint: Sprint, event?: Event): void {
    if (event) event.stopPropagation();
    this.activeSprintDetail.set(sprint);
    this.editingSprintName.set(sprint.name);
    this.editingSprintGoal.set(sprint.goal || '');
    this.editingStartDate.set(sprint.startDate || '');
    this.editingEndDate.set(sprint.endDate || '');

    // Tự động kết nối và tải luồng Trao đổi & Bình luận của Sprint
    this.loadSprintDiscussion(sprint.id);
  }

  saveSprintDetail(): void {
    const sprint = this.activeSprintDetail();
    if (!sprint) return;
    const name = this.editingSprintName().trim();
    if (name) {
      this.projectService.updateSprintInfo(
        sprint.id,
        name,
        this.editingStartDate(),
        this.editingEndDate(),
        this.editingSprintGoal()
      );
      this.toastService.success('Cập nhật Sprint', `Đã lưu thông tin chu kỳ "${name}".`);
    }
  }

  closeSprintDetail(): void {
    const sp = this.activeSprintDetail();
    if (sp) {
      void this.realtimeService.leaveSprint(sp.id);
    }
    if (this.sprintChatUnsubscribe) {
      this.sprintChatUnsubscribe();
      this.sprintChatUnsubscribe = undefined;
    }
    this.activeSprintDetail.set(null);
    this.sprintMessages.set([]);
    this.chatInputText.set('');
  }

  loadSprintDiscussion(sprintId: string): void {
    this.isChatLoading.set(true);
    this.chatInputText.set('');

    void this.realtimeService.joinSprint(sprintId);

    if (this.sprintChatUnsubscribe) {
      this.sprintChatUnsubscribe();
    }

    const offMsg = this.realtimeService.on<SprintMessage>('ReceiveSprintMessage', (msg) => {
      if (msg.sprintId === this.activeSprintDetail()?.id) {
        this.sprintMessages.update(list => {
          if (list.some(m => m.id === msg.id)) return list;
          return [...list, msg];
        });
        this.scrollChatToBottom();
      }
    });

    const offDel = this.realtimeService.on<string>('SprintMessageDeleted', (msgId) => {
      this.sprintMessages.update(list => list.filter(m => m.id !== msgId));
    });

    this.sprintChatUnsubscribe = () => {
      offMsg();
      offDel();
    };

    this.projectService.getSprintMessages(sprintId).subscribe({
      next: (messages) => {
        this.sprintMessages.set(messages);
        this.isChatLoading.set(false);
        this.scrollChatToBottom();
      },
      error: () => {
        this.isChatLoading.set(false);
      }
    });
  }

  sendSprintChatMessage(): void {
    const sp = this.activeSprintDetail();
    const text = this.chatInputText().trim();
    if (!sp || !text || this.isSendingMessage()) return;

    this.isSendingMessage.set(true);
    this.projectService.sendSprintMessage(sp.id, text).subscribe({
      next: (sent) => {
        this.chatInputText.set('');
        this.isSendingMessage.set(false);
        this.sprintMessages.update(list => {
          if (list.some(m => m.id === sent.id)) return list;
          return [...list, sent];
        });
        this.scrollChatToBottom();
      },
      error: (err) => {
        this.isSendingMessage.set(false);
        this.toastService.error('Không thể gửi bình luận', err?.error?.message || 'Lỗi kết nối máy chủ.');
      }
    });
  }

  deleteChatMessage(messageId: string): void {
    const sp = this.activeSprintDetail();
    if (!sp) return;
    this.projectService.deleteSprintMessage(sp.id, messageId).subscribe({
      next: () => {
        this.sprintMessages.update(list => list.filter(m => m.id !== messageId));
        this.toastService.info('Xóa bình luận', 'Bình luận đã được xóa khỏi chu kỳ Sprint.');
      },
      error: () => {
        this.toastService.error('Lỗi', 'Không thể xóa bình luận.');
      }
    });
  }

  readonly currentUserInitial = computed(() => {
    const name = this.identityService.authState().currentUser?.displayName || this.identityService.authState().currentUser?.email;
    return name ? name.charAt(0).toUpperCase() : 'U';
  });

  onChatKeydown(event: Event): void {
    const kEvent = event as KeyboardEvent;
    if (kEvent.key === 'Enter' && !kEvent.shiftKey) {
      kEvent.preventDefault();
      this.sendSprintChatMessage();
    }
  }

  private scrollChatToBottom(): void {
    setTimeout(() => {
      const container = this.el.nativeElement.querySelector('.sprint-chat-messages');
      if (container) {
        container.scrollTop = container.scrollHeight;
      }
    }, 50);
  }

  startEditingSprint(sprint: Sprint, event?: Event): void {
    if (event) event.stopPropagation();
    this.editingSprintId.set(sprint.id);
    this.editingSprintName.set(sprint.name);
    this.editingSprintGoal.set(sprint.goal || '');
    this.editingStartDate.set(sprint.startDate || '');
    this.editingEndDate.set(sprint.endDate || '');
  }

  saveEditingSprint(sprintId: string): void {
    const name = this.editingSprintName().trim();
    if (name) {
      this.projectService.updateSprintInfo(sprintId, name, this.editingStartDate(), this.editingEndDate(), this.editingSprintGoal());
      this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    }
    this.editingSprintId.set(null);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.no-drawer') && !target.closest('button') && !target.closest('select') && !target.closest('input')) {
      this.closeAllPopups();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeAllPopups();
    this.showNewEpicModal.set(false);
  }

  openTask(item: WorkItem, event?: Event): void {
    event?.stopPropagation();
    this.projectService.activeDrawerTask.set(item);
  }

  createSprint(): void {
    this.projectService.addNewSprint();
    this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
  }

  getAssigneeInitials(name?: string): string {
    if (!name) return 'U';
    const parts = name.trim().split(' ');
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[parts.length - 2][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  getSprintAssignees(sprintId: string): string[] {
    const items = this.workItems().filter(i => i.sprintId === sprintId && i.assigneeName);
    const set = new Set<string>();
    items.forEach(i => {
      if (i.assigneeName) set.add(i.assigneeName);
    });
    return Array.from(set);
  }

  // --- Epic Sidebar Methods ---
  toggleEpicPanel(): void {
    this.isEpicPanelOpen.update(v => !v);
  }

  filterByEpic(epicName: string | null): void {
    if (epicName === null) {
      this.selectedEpicFilter.set(null);
      this.toastService.info('Tất Cả Công Việc', 'Đã bỏ bộ lọc Epic');
    } else if (this.selectedEpicFilter() === epicName) {
      this.selectedEpicFilter.set(null);
    } else {
      this.selectedEpicFilter.set(epicName);
      this.toastService.info('Lọc theo Epic', `Đang xem công việc thuộc Epic: ${epicName}`);
    }
  }

  createEpic(): void {
    if (!this.newEpicName.trim()) return;
    this.projectService.addEpic(this.newEpicName.trim(), this.newEpicColor);
    this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    this.newEpicName = '';
    this.showNewEpicModal.set(false);
  }

  getEpicProgress(epicName: string): { total: number; done: number; percent: number; points: number } {
    const items = this.workItems().filter(i => i.epicName === epicName);
    const total = items.length;
    const done = items.filter(i => i.statusName === 'Done').length;
    const points = items.reduce((acc, curr) => acc + (curr.storyPoints || 0), 0);
    const percent = total > 0 ? Math.round((done / total) * 100) : 0;
    return { total, done, percent, points };
  }

  // --- Sprint & Task Filtering ---
  getSprintItems(sprintId: string): WorkItem[] {
    return this.filteredWorkItems().filter(item => item.sprintId === sprintId && !item.parentId);
  }

  getBacklogItems(): WorkItem[] {
    return this.filteredWorkItems().filter(item => (!item.sprintId || item.sprintId === 'sp-backlog') && !item.parentId);
  }

  // --- Sub-task Methods ---
  getSubtasks(parentId: string): WorkItem[] {
    return this.workItems().filter(item => item.parentId === parentId);
  }

  getSubtaskProgress(parentId: string): { total: number; done: number; percent: number } {
    const subs = this.getSubtasks(parentId);
    const total = subs.length;
    const done = subs.filter(s => s.statusName === 'Done').length;
    const percent = total > 0 ? Math.round((done / total) * 100) : 0;
    return { total, done, percent };
  }

  toggleSubtasks(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.expandedSubtasks.update(map => ({
      ...map,
      [taskId]: !map[taskId]
    }));
  }

  openSubtaskInput(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.activeSubtaskInputId.set(taskId);
    if (!this.expandedSubtasks()[taskId]) {
      this.expandedSubtasks.update(m => ({ ...m, [taskId]: true }));
    }
  }

  createInlineSubtask(parentId: string): void {
    const title = this.subtaskTitleMap[parentId];
    if (!title || !title.trim()) return;

    this.projectService.createSubTask(parentId, title.trim());
    this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    this.subtaskTitleMap[parentId] = '';
    this.activeSubtaskInputId.set(null);
  }

  // --- Inline Quick Task Creation per Sprint ---
  createInlineSprintTask(sprintId: string, sprintName: string): void {
    const title = this.inlineTaskTitleMap[sprintId];
    if (!title || !title.trim()) return;

    const currentUserName = this.identityService.authState().currentUser?.displayName || '';
    const normalizedSprintId = sprintId === 'sp-backlog' ? undefined : sprintId;
    this.projectService.addWorkItem({
      title: title.trim(),
      sprintId: normalizedSprintId,
      sprintName: normalizedSprintId ? sprintName : 'Backlog Pool',
      statusName: 'To Do',
      issueType: 'Task',
      priority: 'Medium',
      storyPoints: 3,
      assigneeName: currentUserName
    });

    this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    this.inlineTaskTitleMap[sprintId] = '';
    this.activeInlineSprintId.set(null);
  }

  // --- Sprint Header Controls ---
  toggleSprintStatus(sprint: Sprint, event?: Event): void {
    if (event) event.stopPropagation();
    if (sprint.status === 'future') {
      this.projectService.updateSprintStatus(sprint.id, 'active');
      this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    } else if (sprint.status === 'active') {
      this.projectService.updateSprintStatus(sprint.id, 'completed');
      this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    } else {
      this.projectService.updateSprintStatus(sprint.id, 'future');
      this.toastService.info('Mở Lại Sprint', `Sprint "${sprint.name}" đưa về kế hoạch.`);
    }
  }

  moveSprintToBacklog(sprint: Sprint, event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.moveSprintItemsToBacklog(sprint.id);
    this.toastService.info('Chuyển Công Việc', `Công việc trong ${sprint.name} đã chuyển về Backlog.`);
  }

  // --- Inline Row Actions ---
  selectIssue(issue: WorkItem, event?: Event): void {
    if (event) {
      const target = event.target as HTMLElement;
      if (target.closest('select') || target.closest('button') || target.closest('input') || target.closest('.no-drawer')) {
        return;
      }
    }
    this.projectService.activeDrawerTask.set(issue);
  }

  updateInlineStatus(taskId: string, statusName: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.updateWorkItemStatus(taskId, statusName);
    this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
  }

  updateInlineAssignee(taskId: string, assigneeName: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.updateWorkItemAssignee(taskId, assigneeName);
    this.toastService.info('Gán Người Xử Lý', `Gán cho ${assigneeName}`);
  }

  updateInlinePriority(taskId: string, priority: WorkItem['priority'], event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.updateWorkItemPriority(taskId, priority);
  }

  updateInlineType(taskId: string, issueType: WorkItem['issueType'], event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.updateWorkItemIssueType(taskId, issueType);
  }

  updateInlineEpic(taskId: string, epicName: string, event?: Event): void {
    if (event) event.stopPropagation();
    const epic = this.epics().find(e => e.name === epicName);
    this.projectService.updateWorkItemEpic(taskId, epicName, epic?.color || 'indigo');
  }

  updateWorkItemSprint(taskId: string, sprintId: string, sprintName: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.updateWorkItemSprint(taskId, sprintId, sprintName);
    this.toastService.info('Chuyển Sprint', `Đã gán vào ${sprintName}`);
  }

  toggleRowMenu(taskId: string, event: Event): void {
    event.stopPropagation();
    this.activeMenuTaskId.update(curr => curr === taskId ? null : taskId);
  }

  assignToMe(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    const myName = this.identityService.authState().currentUser?.displayName || 'Tôi';
    this.projectService.updateWorkItemAssignee(taskId, myName);
    this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    this.activeMenuTaskId.set(null);
  }

  cloneItem(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    const cloned = this.projectService.cloneWorkItem(taskId);
    if (cloned) {
      this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    }
    this.activeMenuTaskId.set(null);
  }

  deleteItem(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.deleteWorkItem(taskId);
    this.toastService.warning('Đã Xóa Công Việc', 'Đã xóa công việc khỏi hệ thống.');
    this.activeMenuTaskId.set(null);
  }

  // --- Bulk Operations ---
  toggleTaskSelection(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.selectedTaskIds.update(list =>
      list.includes(taskId) ? list.filter(id => id !== taskId) : [...list, taskId]
    );
  }

  isTaskSelected(taskId: string): boolean {
    return this.selectedTaskIds().includes(taskId);
  }

  clearSelection(): void {
    this.selectedTaskIds.set([]);
  }

  bulkChangeStatus(newStatus: string): void {
    const ids = this.selectedTaskIds();
    if (ids.length === 0) return;
    this.projectService.bulkUpdateStatus(ids, newStatus);
    this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    this.clearSelection();
  }

  bulkChangeSprint(sprintId: string, fallbackSprintName: string = 'Sprint đã chọn'): void {
    const ids = this.selectedTaskIds();
    if (ids.length === 0) return;
    const sprintName = this.sprints().find(s => s.id === sprintId)?.name ?? fallbackSprintName;
    this.projectService.bulkUpdateSprint(ids, sprintId, sprintName);
    this.toastService.info('Đang lưu thay đổi', 'Vui lòng chờ xác nhận từ máy chủ.');
    this.clearSelection();
  }

  bulkDeleteTasks(): void {
    const ids = this.selectedTaskIds();
    if (ids.length === 0) return;
    this.projectService.bulkDelete(ids);
    this.toastService.warning('Xóa Hàng Loạt', `Đã xóa ${ids.length} công việc.`);
    this.clearSelection();
  }

  // --- Quick Exports & AI ---
  exportExcel(): void {
    this.excelService.exportToExcel(this.workItems(), 'Project_Backlog_Export.csv');
    this.toastService.success('Xuất Dữ Liệu Excel', 'Đã tải xuống file CSV/Excel thành công.');
  }

  triggerAiBreakdown(): void {
    this.toastService.info('Phân Rã Tự Động', 'Hệ thống đang tự động phân rã các User Stories thành Subtasks.');
  }
}

