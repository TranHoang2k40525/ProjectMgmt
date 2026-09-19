import { ChangeDetectionStrategy, Component, AfterViewInit, ElementRef, inject, signal, computed, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { gsap } from 'gsap';
import { ProjectManagementService, WorkItem, EpicItem, Sprint } from '../../core/services/project-management.service';
import { ExcelDataService } from '../../core/services/excel-data.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-backlog-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './backlog-page.html',
  styleUrl: './backlog-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BacklogPage implements AfterViewInit {
  private el = inject(ElementRef);
  readonly projectService = inject(ProjectManagementService);
  readonly excelService = inject(ExcelDataService);
  readonly toastService = inject(ToastService);

  // Search & Filter State
  searchQuery = signal<string>('');
  selectedTypeFilter = signal<string | null>(null);
  selectedPriorityFilter = signal<string | null>(null);
  selectedAssigneeFilter = signal<string | null>(null);
  selectedEpicFilter = signal<string | null>(null);
  onlyMineFilter = signal<boolean>(false);

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

  // Inline Task Title Edit State
  editingTaskId = signal<string | null>(null);
  editingTaskTitle = signal<string>('');

  // Inline Sprint Edit State
  editingSprintId = signal<string | null>(null);
  editingSprintName = signal<string>('');
  editingStartDate = signal<string>('');
  editingEndDate = signal<string>('');

  // Bulk Selection State
  selectedTaskIds = signal<string[]>([]);

  // Service Signals
  readonly sprints = this.projectService.sprints;
  readonly workItems = this.projectService.workItems;
  readonly epics = this.projectService.epics;

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
      items = items.filter(i => i.assigneeName === 'Trần Văn Hoàng');
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

  ngAfterViewInit(): void {
    gsap.from('.backlog-row-item', {
      opacity: 0,
      y: 6,
      duration: 0.25,
      stagger: 0.03,
      ease: 'power2.out'
    });
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
    this.activeMenuTaskId.set(null);
    this.activeStatusDropdownTaskId.set(null);
    this.activeEpicDropdownTaskId.set(null);
    this.activeInlineSprintId.set(null);
    this.activeSubtaskInputId.set(null);
    this.editingSprintId.set(null);
    this.editingTaskId.set(null);
  }

  startEditingTaskTitle(item: WorkItem, event?: Event): void {
    if (event) event.stopPropagation();
    this.editingTaskId.set(item.id);
    this.editingTaskTitle.set(item.title);
  }

  saveEditingTaskTitle(taskId: string): void {
    const title = this.editingTaskTitle().trim();
    if (title) {
      this.workItems.update(items =>
        items.map(item => item.id === taskId ? { ...item, title } : item)
      );
      this.toastService.success('Đã Cập Nhật', 'Đã đổi tên công việc thành công.');
    }
    this.editingTaskId.set(null);
  }

  updateInlineSP(taskId: string, event?: Event): void {
    if (event) event.stopPropagation();
    const val = prompt('Nhập số điểm Story Points (SP):');
    if (val !== null && !isNaN(Number(val))) {
      this.projectService.updateWorkItemStoryPoints(taskId, Number(val));
      this.toastService.success('Cập Nhật SP', `Đã cập nhật ${val} SP.`);
    }
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
      'Story': '📗',
      'Task': '📘',
      'Bug': '📕',
      'Use-Case': '🩵',
      'Epic': '⚡',
      'Sub-task': '📙'
    };
    return map[type] || '📌';
  }

  startEditingSprint(sprint: Sprint, event?: Event): void {
    if (event) event.stopPropagation();
    this.editingSprintId.set(sprint.id);
    this.editingSprintName.set(sprint.name);
    this.editingStartDate.set(sprint.startDate || '');
    this.editingEndDate.set(sprint.endDate || '');
  }

  saveEditingSprint(sprintId: string): void {
    const name = this.editingSprintName().trim();
    if (name) {
      this.projectService.updateSprintInfo(sprintId, name, this.editingStartDate(), this.editingEndDate());
      this.toastService.success('Đã Cập Nhật Sprint', `Cập nhật thông tin cho ${name}`);
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

  createSprint(): void {
    const newSprint = this.projectService.addNewSprint();
    this.toastService.success('Tạo Sprint Mới', `Đã khởi tạo Sprint ${newSprint.name}`);
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
    const created = this.projectService.addEpic(this.newEpicName.trim(), this.newEpicColor);
    this.toastService.success('Đã Tạo Epic Mới', `Epic "${created.name}" [${created.key}] đã sẵn sàng!`);
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

    const sub = this.projectService.createSubTask(parentId, title.trim());
    this.toastService.success('Tạo Sub-task', `Đã thêm công việc con [${sub.issueKey}]`);
    this.subtaskTitleMap[parentId] = '';
    this.activeSubtaskInputId.set(null);
  }

  // --- Inline Quick Task Creation per Sprint ---
  createInlineSprintTask(sprintId: string, sprintName: string): void {
    const title = this.inlineTaskTitleMap[sprintId];
    if (!title || !title.trim()) return;

    const created = this.projectService.addWorkItem({
      title: title.trim(),
      sprintId,
      sprintName,
      statusName: 'To Do',
      issueType: 'Task',
      priority: 'Medium',
      storyPoints: 3,
      assigneeName: 'Trần Văn Hoàng'
    });

    this.toastService.success('Tạo Công Việc', `Đã thêm [${created.issueKey}] vào ${sprintName}`);
    this.inlineTaskTitleMap[sprintId] = '';
    this.activeInlineSprintId.set(null);
  }

  // --- Sprint Header Controls ---
  toggleSprintStatus(sprint: Sprint, event?: Event): void {
    if (event) event.stopPropagation();
    if (sprint.status === 'future') {
      this.projectService.updateSprintStatus(sprint.id, 'active');
      this.toastService.success('Kích Hoạt Sprint', `Sprint "${sprint.name}" đã bắt đầu.`);
    } else if (sprint.status === 'active') {
      this.projectService.updateSprintStatus(sprint.id, 'completed');
      this.toastService.success('Hoàn Thành Sprint', `Đã hoàn thành Sprint "${sprint.name}".`);
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
    this.toastService.success('Cập Nhật Trạng Thái', `Đã đổi sang ${statusName}`);
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
    this.projectService.updateWorkItemAssignee(taskId, 'Trần Văn Hoàng');
    this.toastService.success('Đã Gán Cho Tôi', 'Đã nhận công việc.');
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
    this.toastService.success('Thao Tác Hàng Loạt', `Đã cập nhật ${ids.length} công việc sang ${newStatus}`);
    this.clearSelection();
  }

  bulkChangeSprint(sprintId: string, sprintName: string): void {
    const ids = this.selectedTaskIds();
    if (ids.length === 0) return;
    this.projectService.bulkUpdateSprint(ids, sprintId, sprintName);
    this.toastService.success('Thao Tác Hàng Loạt', `Đã chuyển ${ids.length} công việc sang ${sprintName}`);
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
    this.excelService.exportToExcel(this.workItems(), 'HUCE_Backlog_Export.csv');
    this.toastService.success('Xuất Dữ Liệu Excel', 'Đã tải xuống file CSV/Excel thành công.');
  }

  triggerAiBreakdown(): void {
    this.toastService.info('Phân Rã Tự Động', 'Hệ thống đang tự động phân rã các User Stories thành Subtasks.');
  }
}
