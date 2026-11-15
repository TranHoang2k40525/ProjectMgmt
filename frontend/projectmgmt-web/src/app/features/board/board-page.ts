import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, OnInit, AfterViewInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { gsap } from 'gsap';
import { Flip } from 'gsap/Flip';
import { apiErrorMessage } from '../../core/http/api-error-message';
import { ProjectManagementService, WorkItem } from '../../core/services/project-management.service';
import { IdentityService } from '../../core/services/identity.service';
import { ToastService } from '../../core/services/toast.service';
import { AiQuickActionService } from '../../core/services/ai-quick-action.service';
import { ContextMenuComponent, ContextMenuItemAction } from '../../shared/components/context-menu.component';

gsap.registerPlugin(Flip);

import { NoProjectStateComponent } from '../../shared/components/no-project-state/no-project-state.component';
import { CardTiltDirective } from '../../shared/motion/card-tilt.directive';

@Component({
  selector: 'app-board-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DragDropModule, ContextMenuComponent, NoProjectStateComponent, CardTiltDirective],
  templateUrl: './board-page.html',
  styleUrl: './board-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoardPage implements OnInit, AfterViewInit, OnDestroy {
  private el = inject<ElementRef<HTMLElement>>(ElementRef);
  private flipAnimation: gsap.core.Timeline | null = null;
  private entranceTween: gsap.core.Tween | null = null;
  private readonly identityService = inject(IdentityService);
  readonly projectService = inject(ProjectManagementService);
  readonly toastService = inject(ToastService);
  readonly aiQuickAction = inject(AiQuickActionService);

  readonly projectMembers = computed(() => this.projectService.currentProject()?.members ?? []);
  readonly activeSprint = computed(() => this.projectService.sprints().find(s => s.status === 'active') ?? null);

  searchQuery = signal<string>('');
  selectedAssigneeFilter = signal<string | null>(null);
  selectedTypeFilter = signal<string | null>(null);
  selectedStatusTab = signal<string | null>(null);
  activeMenuTaskId = signal<string | null>(null);

  // Context Menu State
  contextMenuVisible = signal<boolean>(false);
  contextMenuX = signal<number>(0);
  contextMenuY = signal<number>(0);
  contextMenuItem = signal<WorkItem | null>(null);

  // Inline editing state
  inlineEditingTaskId = signal<string | null>(null);
  inlineTitleValue = signal<string>('');

  readonly items = this.projectService.workItems;
  readonly epics = this.projectService.epics;
  readonly canCreateIssue = this.projectService.canCreateIssue;
  readonly canEditIssue = this.projectService.canEditIssue;
  readonly canDeleteIssue = this.projectService.canDeleteIssue;
  readonly canTransitionIssue = this.projectService.canTransitionIssue;
  readonly canAssignIssue = this.projectService.canAssignIssue;
  readonly canRankBacklog = this.projectService.canRankBacklog;
  readonly canAiBreakdown = this.projectService.canAiBreakdown;
  readonly canAiAssign = this.projectService.canAiAssign;

  readonly boardTitle = computed(() => {
    const p = this.projectService.currentProject();
    if ((p as any)?.boardTabTitle) {
      const t = (p as any).boardTabTitle;
      return t.startsWith('Bảng ') ? t : `Bảng ${t}`;
    }
    if ((p as any)?.workflowName) {
      const w = (p as any).workflowName;
      return w.startsWith('Bảng ') ? w : `Bảng ${w}`;
    }
    if ((p as any)?.boardName) {
      const bn = (p as any).boardName;
      return bn.startsWith('Bảng ') ? bn : `Bảng ${bn}`;
    }
    if (typeof localStorage !== 'undefined' && p?.id) {
      const saved = localStorage.getItem(`projectmgmt.workflow-name.${p.id}`)
        || localStorage.getItem(`projectmgmt.workflow-name.${p.id.toLowerCase()}`);
      if (saved) return saved.startsWith('Bảng ') ? saved : `Bảng ${saved}`;
    }
    if (typeof sessionStorage !== 'undefined' && p?.id) {
      const saved = sessionStorage.getItem(`projectmgmt.workflow-name.${p.id}`)
        || sessionStorage.getItem(`projectmgmt.workflow-name.${p.id.toLowerCase()}`);
      if (saved) return saved.startsWith('Bảng ') ? saved : `Bảng ${saved}`;
    }
    if ((p as any)?.boardName) return (p as any).boardName;
    if ((p as any)?.managementType === 'Kanban' || p?.type === 'Kanban') return 'Bảng Kanban';
    if ((p as any)?.managementType) return `Bảng ${(p as any).managementType}`;
    return 'Bảng Scrum';
  });

  readonly isKanbanBoard = computed(() => {
    const p = this.projectService.currentProject();
    const title = this.boardTitle().toLowerCase();
    const wName = ((p as any)?.workflowName || '').toLowerCase();
    const tId = ((p as any)?.workflowTemplateId || '').toLowerCase();
    return (p as any)?.boardType === 'Kanban' || (p as any)?.managementType === 'Kanban' || p?.type === 'Kanban' || title.includes('kanban') || wName.includes('kanban') || tId.includes('kanban');
  });

  readonly currentWorkflowName = computed(() => this.boardTitle().replace(/^Bảng\s+/i, '').trim());

  private isBoardCardEligible(item: WorkItem): boolean {
    const typeLower = (item.issueType || '').trim().toLowerCase();
    return !item.parentId && typeLower !== 'epic' && typeLower !== 'sub-task' && typeLower !== 'subtask';
  }

  /**
   * Phạm vi thẻ hiển thị trên Bảng:
   * - Kanban: toàn bộ đầu việc thực thi (loại trừ Epic, Sub-task con và các việc thuộc Sprint đã đóng).
   * - Scrum: chỉ hiển thị đầu việc thuộc Sprint đang chạy; nếu chưa có Sprint đang chạy thì trả về rỗng.
   */
  readonly boardScopeItems = computed(() => {
    const eligible = this.items().filter(i => this.isBoardCardEligible(i));
    if (this.isKanbanBoard()) {
      const completedSprintIds = new Set(
        this.projectService.sprints()
          .filter(s => s.status === 'completed')
          .map(s => s.id)
      );
      return completedSprintIds.size > 0
        ? eligible.filter(i => !i.sprintId || !completedSprintIds.has(i.sprintId))
        : eligible;
    }

    const active = this.activeSprint();
    if (!active) {
      return [];
    }
    return eligible.filter(i => i.sprintId === active.id);
  });

  readonly hasActiveFilters = computed(() => {
    return !!(
      this.searchQuery().trim() ||
      this.selectedAssigneeFilter() ||
      this.selectedTypeFilter() ||
      this.selectedStatusTab()
    );
  });

  readonly searchAndFacetFilteredItems = computed(() => {
    let list = this.boardScopeItems();
    const query = this.searchQuery().trim().toLowerCase();
    const assignee = this.selectedAssigneeFilter();
    const type = this.selectedTypeFilter();

    if (query) {
      list = list.filter(i =>
        i.title.toLowerCase().includes(query) ||
        i.issueKey.toLowerCase().includes(query) ||
        (i.epicName && i.epicName.toLowerCase().includes(query))
      );
    }

    if (assignee) {
      list = list.filter(i => i.assigneeName === assignee);
    }

    if (type) {
      list = list.filter(i => i.issueType === type);
    }

    return list;
  });

  readonly filteredItems = computed(() => {
    const list = this.searchAndFacetFilteredItems();
    const statusTab = this.selectedStatusTab();
    if (!statusTab) {
      return list;
    }
    const normalizedTab = statusTab.trim().toLowerCase();
    return list.filter(i => (i.statusName || '').trim().toLowerCase() === normalizedTab);
  });

  readonly workflowStatuses = this.projectService.orderedStatuses;

  readonly boardColumns = computed(() => {
    const statuses = this.workflowStatuses();
    if (statuses.length === 0) {
      return [
        { id: 'st-1', name: 'To Do', colorHex: '#64748B' },
        { id: 'st-2', name: 'In Progress', colorHex: '#3B82F6' },
        { id: 'st-3', name: 'Code Review', colorHex: '#8B5CF6' },
        { id: 'st-4', name: 'Done', colorHex: '#10B981' }
      ];
    }
    return statuses;
  });

  getTabCount(statusName: string): number {
    const normalized = statusName.trim().toLowerCase();
    return this.searchAndFacetFilteredItems().filter(i => (i.statusName || '').trim().toLowerCase() === normalized).length;
  }

  getColumnItems(statusName: string): WorkItem[] {
    const all = this.filteredItems().filter(i => (i.statusName || '').trim().toLowerCase() === statusName.trim().toLowerCase());
    return [...all].sort((a, b) => {
      if (a.isBlocked && !b.isBlocked) return -1;
      if (!a.isBlocked && b.isBlocked) return 1;
      return 0;
    });
  }

  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedAssigneeFilter.set(null);
    this.selectedTypeFilter.set(null);
    this.selectedStatusTab.set(null);
  }

  readonly issueTypes = computed(() => {
    const custom = this.projectService.customIssueTypes()
      .filter(t => !t.isSubtask && !['epic', 'sub-task', 'subtask'].includes(t.name.trim().toLowerCase()))
      .map(t => t.name);
    return custom.length > 0 ? custom : ['Story', 'Task', 'Bug'];
  });

  getIssueTypeIcon(type: string): string {
    const typeName = type || '';
    const custom = this.projectService.customIssueTypes().find(t =>
      t.name.toLowerCase() === typeName.toLowerCase() ||
      ((typeName.toLowerCase() === 'sub-task' || typeName.toLowerCase() === 'subtask') && (t.isSubtask || t.name.toLowerCase() === 'subtask' || t.name.toLowerCase() === 'sub-task'))
    );
    if (custom?.icon && custom.icon !== 'task') return custom.icon;
    const map: Record<string, string> = {
      'story': 'bookmark',
      'task': 'task_alt',
      'bug': 'bug_report',
      'use-case': 'schema',
      'epic': 'bolt',
      'sub-task': 'subdirectory_arrow_right',
      'subtask': 'subdirectory_arrow_right'
    };
    return map[typeName.toLowerCase()] || custom?.icon || 'task_alt';
  }

  getIssueTypeColor(type: string): string {
    const typeName = type || '';
    const custom = this.projectService.customIssueTypes().find(t =>
      t.name.toLowerCase() === typeName.toLowerCase() ||
      ((typeName.toLowerCase() === 'sub-task' || typeName.toLowerCase() === 'subtask') && (t.isSubtask || t.name.toLowerCase() === 'subtask' || t.name.toLowerCase() === 'sub-task'))
    );
    if (custom?.color) return custom.color;
    const map: Record<string, string> = {
      'story': '#65BA43',
      'task': '#4BADE8',
      'bug': '#E5493A',
      'epic': '#904EE2',
      'sub-task': '#4BADE8',
      'subtask': '#4BADE8'
    };
    return map[typeName.toLowerCase()] || '#4BADE8';
  }

  // --- Blocker Management ---
  readonly showBlockModal = signal<boolean>(false);
  readonly blockingItem = signal<WorkItem | null>(null);
  blockReasonInput = '';

  openBlockModal(item: WorkItem, event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canEditIssue()) return;
    this.blockingItem.set(item);
    this.blockReasonInput = '';
    this.showBlockModal.set(true);
  }

  confirmBlock(): void {
    if (!this.canEditIssue()) return;
    const item = this.blockingItem();
    const reason = this.blockReasonInput.trim();
    if (!item || !reason) return;
    this.projectService.blockIssue(item.id, reason).subscribe({
      next: () => {
        this.showBlockModal.set(false);
        this.toastService.warning('Báo Chặn (Blocker)', `Đã gắn cờ cản trở cho ${item.issueKey}`);
      },
      error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể báo chặn.'))
    });
  }

  unblock(item: WorkItem, event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canEditIssue()) return;
    this.projectService.unblockIssue(item.id).subscribe({
      next: () => this.toastService.success('Đã Gỡ Chặn', `Đã mở lại tiến độ cho ${item.issueKey}`),
      error: (err) => this.toastService.error('Lỗi', apiErrorMessage(err, 'Không thể gỡ chặn.'))
    });
  }

  readonly toDoItems = computed(() => this.getColumnItems('To Do'));
  readonly inProgressItems = computed(() => this.getColumnItems('In Progress'));
  readonly codeReviewItems = computed(() => this.getColumnItems('Code Review'));
  readonly doneItems = computed(() => this.getColumnItems('Done'));

  ngOnInit(): void {
    const currentProj = this.projectService.currentProject();
    if (currentProj?.id) {
      if (!currentProj.members || currentProj.members.length === 0) {
        this.projectService.loadProjectMembers(currentProj.id).subscribe();
      }
      this.projectService.loadWorkspace(currentProj.id);
    }
  }

  ngAfterViewInit(): void {
    if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      const columns = this.el.nativeElement.querySelectorAll<HTMLElement>('.board-column');
      if (columns.length) {
        this.entranceTween = gsap.fromTo(
          columns,
          { opacity: 0, y: 18 },
          {
            opacity: 1,
            y: 0,
            duration: 0.45,
            stagger: 0.08,
            ease: 'power2.out',
            clearProps: 'transform,opacity'
          }
        );
      }
    }
  }

  ngOnDestroy(): void {
    this.flipAnimation?.kill();
    this.entranceTween?.kill();
  }

  // CDK Drag & Drop Handler
  onDrop(event: CdkDragDrop<WorkItem[]>, targetStatus: string): void {
    if (event.previousContainer === event.container) {
      if (!this.canRankBacklog()) return;
      const ordered = [...event.container.data]; moveItemInArray(ordered, event.previousIndex, event.currentIndex);
      this.projectService.reorderWorkItems(ordered.map(i => i.id)); return;
    }
    if (!this.canTransitionIssue()) return;
    const item = event.item.data as WorkItem;
    if (item && item.statusName !== targetStatus) {
      this.animateBoardChange(() => this.projectService.updateWorkItemStatus(item.id, targetStatus));
    }
  }

  // Right-Click Context Menu Trigger
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
        if (event.value && this.canTransitionIssue()) {
          const targetStatus = event.value;
          this.animateBoardChange(() => this.projectService.updateWorkItemStatus(item.id, targetStatus));
        }
        break;

      case 'assign': {
        if (!this.canAssignIssue()) break;
        const myName = this.identityService.authState().currentUser?.displayName || 'Tôi';
        this.projectService.updateWorkItemAssignee(item.id, myName);
        break;
      }

      case 'ai-breakdown':
        if (!this.canAiBreakdown()) break;
        this.aiQuickAction.triggerBreakdown(item);
        break;

      case 'ai-assign':
        if (!this.canAiAssign()) break;
        this.aiQuickAction.triggerAssignSuggestion(item);
        break;

      case 'copy':
        navigator.clipboard?.writeText?.(`${item.issueKey}: ${item.title}`);
        this.toastService.success('Đã sao chép', `Đã chép [${item.issueKey}] vào bộ nhớ tạm.`);
        break;

      case 'delete':
        if (!this.canDeleteIssue()) break;
        this.animateBoardChange(() => this.projectService.deleteWorkItem(item.id));
        this.toastService.warning('Đã Xóa', `Đã xóa công việc [${item.issueKey}]`);
        break;
    }
  }

  // Double Click for Quick Inline Editing
  startInlineEdit(item: WorkItem, event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canEditIssue()) return;
    this.projectService.activeDrawerTask.set(null);
    this.inlineEditingTaskId.set(item.id);
    this.inlineTitleValue.set(item.title);
  }

  saveInlineEdit(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canEditIssue()) {
      this.inlineEditingTaskId.set(null);
      return;
    }
    const newTitle = this.inlineTitleValue().trim();
    if (newTitle) {
      const item = this.items().find(i => i.id === taskId);
      if (item) {
        this.projectService.updateWorkItem({ ...item, title: newTitle });
      }
    }
    this.inlineEditingTaskId.set(null);
  }

  cancelInlineEdit(): void {
    this.inlineEditingTaskId.set(null);
  }

  openTaskDrawer(task: WorkItem, event?: Event): void {
    if (this.inlineEditingTaskId() !== null) {
      return;
    }
    if (event) {
      const target = event.target as HTMLElement;
      if (target.closest('select') || target.closest('button') || target.closest('input') || target.closest('.no-drawer')) {
        return;
      }
    }
    this.projectService.activeDrawerTask.set(task);
  }

  // Quick Inline Status Shift
  moveStatus(task: WorkItem, direction: 'prev' | 'next', event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canTransitionIssue()) return;
    const statuses = this.boardColumns().map(s => s.name);
    const normalized = (task.statusName || '').trim().toLowerCase();
    const idx = statuses.findIndex(s => s.trim().toLowerCase() === normalized);
    if (direction === 'next' && idx < statuses.length - 1) {
      const nextStatus = statuses[idx + 1];
      this.animateBoardChange(() => this.projectService.updateWorkItemStatus(task.id, nextStatus));
    } else if (direction === 'prev' && idx > 0) {
      const prevStatus = statuses[idx - 1];
      this.animateBoardChange(() => this.projectService.updateWorkItemStatus(task.id, prevStatus));
    }
  }

  updateInlineStatus(taskId: string, statusName: string, event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canTransitionIssue()) return;
    this.animateBoardChange(() => this.projectService.updateWorkItemStatus(taskId, statusName));
  }

  updateInlineAssignee(taskId: string, assigneeName: string, event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canAssignIssue()) return;
    this.projectService.updateWorkItemAssignee(taskId, assigneeName);
    this.toastService.info('Gán Người Xử Lý', `Đã gán cho ${assigneeName}`);
  }

  updateInlinePriority(taskId: string, priority: WorkItem['priority'], event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canEditIssue()) return;
    this.projectService.updateWorkItemPriority(taskId, priority);
    this.toastService.info('Đổi Mức Ưu Tiên', `Độ ưu tiên: ${priority}`);
  }

  isDoneStatus(statusName?: string | null): boolean {
    return this.projectService.isDoneStatusName(statusName);
  }

  getSubtaskCount(parentId: string): { total: number; done: number } {
    const subs = this.items().filter(i => i.parentId === parentId);
    const done = subs.filter(i => i.isDone || this.isDoneStatus(i.statusName)).length;
    return { total: subs.length, done };
  }

  getAssigneeAvatar(name?: string, itemAvatar?: string): string | null {
    return this.projectService.getMemberAvatar(name, itemAvatar);
  }

  getAssigneeInitials(name?: string): string {
    if (!name) return 'U';
    const parts = name.trim().split(' ');
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[parts.length - 2][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  closeAllPopups(): void {
    this.activeMenuTaskId.set(null);
    this.contextMenuVisible.set(false);
  }

  toggleCardMenu(taskId: string, event: Event): void {
    event.stopPropagation();
    this.activeMenuTaskId.update(curr => curr === taskId ? null : taskId);
  }

  assignToMe(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canAssignIssue()) {
      this.activeMenuTaskId.set(null);
      return;
    }
    const myName = this.identityService.authState().currentUser?.displayName || 'Tôi';
    this.projectService.updateWorkItemAssignee(taskId, myName);
    this.activeMenuTaskId.set(null);
  }

  cloneItem(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canCreateIssue()) {
      this.activeMenuTaskId.set(null);
      return;
    }
    const cloned = this.projectService.cloneWorkItem(taskId);
    if (cloned) {
      this.toastService.success('Nhân bản', 'Đã tạo bản sao công việc thành công.');
    }
    this.activeMenuTaskId.set(null);
  }

  deleteItem(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    if (!this.canDeleteIssue()) {
      this.activeMenuTaskId.set(null);
      return;
    }
    this.animateBoardChange(() => this.projectService.deleteWorkItem(taskId));
    this.toastService.warning('Đã Xóa Công Việc', 'Công việc đã xóa khỏi bảng.');
    this.activeMenuTaskId.set(null);
  }

  private animateBoardChange(update: () => void): void {
    const cards = this.el.nativeElement.querySelectorAll<HTMLElement>('.board-task-card');
    if (!cards.length || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      update();
      return;
    }

    const state = Flip.getState(cards);
    this.flipAnimation?.kill();
    update();

    requestAnimationFrame(() => {
      this.flipAnimation = Flip.from(state, {
        duration: 0.52,
        ease: 'power3.inOut',
        absolute: true,
        prune: true,
        stagger: 0.012,
        onEnter: elements => gsap.fromTo(elements, { autoAlpha: 0, scale: 0.92 }, { autoAlpha: 1, scale: 1, duration: 0.3 }),
        onLeave: elements => gsap.to(elements, { autoAlpha: 0, scale: 0.92, duration: 0.2 })
      });
    });
  }
}

