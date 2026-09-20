import { ChangeDetectionStrategy, Component, AfterViewInit, ElementRef, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { gsap } from 'gsap';
import { ProjectManagementService, WorkItem } from '../../core/services/project-management.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-board-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './board-page.html',
  styleUrl: './board-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoardPage implements AfterViewInit {
  private el = inject(ElementRef);
  readonly projectService = inject(ProjectManagementService);
  readonly toastService = inject(ToastService);

  searchQuery = signal<string>('');
  selectedAssigneeFilter = signal<string | null>(null);
  selectedTypeFilter = signal<string | null>(null);
  selectedStatusTab = signal<string | null>(null);
  activeMenuTaskId = signal<string | null>(null);

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

  ngAfterViewInit(): void {
    gsap.from('.board-column', {
      opacity: 0,
      y: 20,
      duration: 0.4,
      stagger: 0.08,
      ease: 'power2.out'
    });
  }

  openTaskDrawer(task: WorkItem, event?: Event): void {
    if (event) {
      const target = event.target as HTMLElement;
      if (target.closest('select') || target.closest('button') || target.closest('.no-drawer')) {
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
      this.projectService.updateWorkItemStatus(task.id, nextStatus);
      this.toastService.success('Đã Chuyển Cột', `Công việc [${task.issueKey}] sang ${nextStatus}`);
    } else if (direction === 'prev' && idx > 0) {
      const prevStatus = statuses[idx - 1];
      this.projectService.updateWorkItemStatus(task.id, prevStatus);
      this.toastService.info('Lùi Trạng Thái', `Công việc [${task.issueKey}] về ${prevStatus}`);
    }
  }

  updateInlineStatus(taskId: string, statusName: string, event?: Event): void {
    if (event) event.stopPropagation();
    this.projectService.updateWorkItemStatus(taskId, statusName);
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
    this.projectService.deleteWorkItem(taskId);
    this.toastService.warning('Đã Xóa Công Việc', 'Công việc đã xóa khỏi bảng.');
    this.activeMenuTaskId.set(null);
  }
}
