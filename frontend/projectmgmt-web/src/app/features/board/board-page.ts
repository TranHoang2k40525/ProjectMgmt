import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { gsap } from 'gsap';
import { Flip } from 'gsap/Flip';
import { ProjectManagementService, WorkItem } from '../../core/services/project-management.service';
import { ToastService } from '../../core/services/toast.service';
import { ContextMenuComponent, ContextMenuItemAction } from '../../shared/components/context-menu.component';

gsap.registerPlugin(Flip);

@Component({
  selector: 'app-board-page',
  standalone: true,
  imports: [CommonModule, FormsModule, DragDropModule, ContextMenuComponent],
  templateUrl: './board-page.html',
  styleUrl: './board-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoardPage implements OnDestroy {
  private el = inject<ElementRef<HTMLElement>>(ElementRef);
  private flipAnimation: gsap.core.Timeline | null = null;
  readonly projectService = inject(ProjectManagementService);
  readonly toastService = inject(ToastService);

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

  readonly filteredItems = computed(() => {
    let list = this.items();
    const query = this.searchQuery().trim().toLowerCase();
    const assignee = this.selectedAssigneeFilter();
    const type = this.selectedTypeFilter();
    const statusTab = this.selectedStatusTab();

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

    if (statusTab) {
      list = list.filter(i => i.statusName === statusTab);
    }

    return list;
  });

  readonly toDoItems = computed(() => this.filteredItems().filter(i => i.statusName === 'To Do'));
  readonly inProgressItems = computed(() => this.filteredItems().filter(i => i.statusName === 'In Progress'));
  readonly codeReviewItems = computed(() => this.filteredItems().filter(i => i.statusName === 'Code Review'));
  readonly doneItems = computed(() => this.filteredItems().filter(i => i.statusName === 'Done'));

  ngOnDestroy(): void {
    this.flipAnimation?.kill();
  }

  // CDK Drag & Drop Handler
  onDrop(event: CdkDragDrop<WorkItem[]>, targetStatus: string): void {
    const item = event.item.data as WorkItem;
    if (item && item.statusName !== targetStatus) {
      this.animateBoardChange(() => this.projectService.updateWorkItemStatus(item.id, targetStatus));
      this.toastService.success('Kéo Thả Thành Công', `Đã chuyển [${item.issueKey}] sang ${targetStatus}`);
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
        if (event.value) {
          const statusMap: Record<string, string> = {
            'TO_DO': 'To Do',
            'IN_PROGRESS': 'In Progress',
            'CODE_REVIEW': 'Code Review',
            'DONE': 'Done'
          };
          const targetStatus = statusMap[event.value] || event.value;
          this.animateBoardChange(() => this.projectService.updateWorkItemStatus(item.id, targetStatus));
          this.toastService.success('Đã Chuyển Trạng Thái', `Thẻ [${item.issueKey}] -> ${targetStatus}`);
        }
        break;

      case 'assign':
        this.projectService.updateWorkItemAssignee(item.id, 'Trần Văn Hoàng');
        this.toastService.success('Đã Gán Việc', `Công việc [${item.issueKey}] đã gán cho bạn`);
        break;

      case 'ai-breakdown':
        this.projectService.activeDrawerTask.set(item);
        this.toastService.info('AI Breakdown', `Đang mở gợi ý phân rã Sub-task cho [${item.issueKey}]`);
        break;

      case 'ai-assign':
        this.projectService.activeDrawerTask.set(item);
        this.toastService.info('AI Assign', `Đang tính toán điểm ứng viên cho [${item.issueKey}]`);
        break;

      case 'copy':
        navigator.clipboard?.writeText?.(`${item.issueKey}: ${item.title}`);
        this.toastService.success('Đã Sao Chép', `Mã thẻ [${item.issueKey}] đã được lưu vào bộ nhớ tạm`);
        break;

      case 'delete':
        this.animateBoardChange(() => this.projectService.deleteWorkItem(item.id));
        this.toastService.warning('Đã Xóa', `Đã xóa công việc [${item.issueKey}]`);
        break;
    }
  }

  // Double Click for Quick Inline Editing
  startInlineEdit(item: WorkItem, event?: Event): void {
    if (event) event.stopPropagation();
    this.inlineEditingTaskId.set(item.id);
    this.inlineTitleValue.set(item.title);
  }

  saveInlineEdit(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    const newTitle = this.inlineTitleValue().trim();
    if (newTitle) {
      const item = this.items().find(i => i.id === taskId);
      if (item) {
        this.projectService.updateWorkItem({ ...item, title: newTitle });
        this.toastService.success('Đã Cập Nhật', 'Tiêu đề công việc đã được lưu');
      }
    }
    this.inlineEditingTaskId.set(null);
  }

  cancelInlineEdit(): void {
    this.inlineEditingTaskId.set(null);
  }

  openTaskDrawer(task: WorkItem, event?: Event): void {
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
    const statuses: string[] = ['To Do', 'In Progress', 'Code Review', 'Done'];
    const idx = statuses.indexOf(task.statusName);
    if (direction === 'next' && idx < statuses.length - 1) {
      const nextStatus = statuses[idx + 1];
      this.animateBoardChange(() => this.projectService.updateWorkItemStatus(task.id, nextStatus));
      this.toastService.success('Đã Chuyển Cột', `Công việc [${task.issueKey}] sang ${nextStatus}`);
    } else if (direction === 'prev' && idx > 0) {
      const prevStatus = statuses[idx - 1];
      this.animateBoardChange(() => this.projectService.updateWorkItemStatus(task.id, prevStatus));
      this.toastService.info('Lùi Trạng Thái', `Công việc [${task.issueKey}] về ${prevStatus}`);
    }
  }

  updateInlineStatus(taskId: string, statusName: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.animateBoardChange(() => this.projectService.updateWorkItemStatus(taskId, statusName));
    this.toastService.success('Cập Nhật Trạng Thái', `Đã chuyển sang ${statusName}`);
  }

  updateInlineAssignee(taskId: string, assigneeName: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.updateWorkItemAssignee(taskId, assigneeName);
    this.toastService.info('Gán Người Xử Lý', `Đã gán cho ${assigneeName}`);
  }

  updateInlinePriority(taskId: string, priority: WorkItem['priority'], event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.updateWorkItemPriority(taskId, priority);
    this.toastService.info('Đổi Mức Ưu Tiên', `Độ ưu tiên: ${priority}`);
  }

  getSubtaskCount(parentId: string): { total: number; done: number } {
    const subs = this.items().filter(i => i.parentId === parentId);
    const done = subs.filter(i => i.statusName === 'Done').length;
    return { total: subs.length, done };
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
    this.projectService.updateWorkItemAssignee(taskId, 'Trần Văn Hoàng');
    this.toastService.success('Đã Gán Cho Tôi', 'Công việc đã được đưa vào danh sách của bạn.');
    this.activeMenuTaskId.set(null);
  }

  cloneItem(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    const cloned = this.projectService.cloneWorkItem(taskId);
    if (cloned) {
      this.toastService.success('Nhân Bản Thành Công', `Đã tạo bản sao [${cloned.issueKey}]`);
    }
    this.activeMenuTaskId.set(null);
  }

  deleteItem(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
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

