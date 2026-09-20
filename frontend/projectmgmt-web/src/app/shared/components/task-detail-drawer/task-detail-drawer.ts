import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectManagementService, WorkItem } from '../../../core/services/project-management.service';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';

@Component({
  selector: 'app-task-detail-drawer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (task) {
      <div class="fixed inset-0 z-50 flex justify-end bg-slate-900/40 backdrop-blur-xs transition-opacity animate-fade-in font-body text-slate-900 dark:text-slate-100">
        <!-- Backdrop close click -->
        <button type="button" aria-label="Đóng chi tiết công việc" class="flex-1 bg-transparent border-0" (click)="dismissed.emit()"></button>

        <!-- Slide-over Right Drawer Container (Resizable Width, Full Width on Mobile) -->
        <div
          [style.width.px]="drawerWidth()"
          class="jira-drawer relative bg-white dark:bg-slate-900 border-l border-slate-200 dark:border-slate-800 shadow-2xl flex flex-col h-full overflow-hidden animate-slide-left transition-all duration-75 w-full max-w-full sm:max-w-[90vw]"
        >
          <!-- Resizable Left Edge Drag Handle (Hidden on Mobile) -->
          <div
            (mousedown)="startResizing($event)"
            title="Kéo thả để mở rộng hoặc thu gọn độ rộng bảng chi tiết"
            class="hidden sm:flex absolute left-0 top-0 bottom-0 w-2 hover:w-3 bg-slate-200 hover:bg-primary dark:bg-slate-800 cursor-col-resize z-50 items-center justify-center transition-all group"
          >
            <div class="w-0.5 h-8 bg-slate-400 group-hover:bg-white rounded-full"></div>
          </div>
          
          <!-- Drawer Top Navigation Header -->
          <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200/80 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40">
            <div class="flex items-center gap-3">
              <span class="px-3 py-1 rounded-md text-sm font-mono font-bold bg-primary/10 text-primary border border-primary/20">
                {{ task.issueKey }}
              </span>
              <span class="text-sm text-slate-500 font-medium">Dự án {{ task.sprintName || 'HUCE Scrum Platform' }}</span>
            </div>

            <div class="flex items-center gap-2">
              <button (click)="cloneTask()" class="p-1.5 rounded-lg hover:bg-slate-200 dark:hover:bg-slate-800 text-slate-500 transition-colors" title="Nhân bản công việc">
                <span class="material-symbols-outlined text-[20px]">content_copy</span>
              </button>
              <button (click)="deleteTask()" class="p-1.5 rounded-lg hover:bg-red-50 text-red-500 transition-colors" title="Xóa công việc">
                <span class="material-symbols-outlined text-[20px]">delete</span>
              </button>
              <div class="h-4 w-px bg-slate-200 dark:bg-slate-800"></div>
              <button (click)="dismissed.emit()" class="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-slate-200 dark:hover:bg-slate-800 text-slate-500 transition-colors">
                <span class="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>
          </div>

          <!-- Drawer Main Content Body -->
          <div class="flex-1 overflow-y-auto p-6 flex flex-col gap-6">
            
            <!-- Issue Title & Type -->
            <div class="flex flex-col gap-2">
              <div class="flex items-center gap-2 flex-wrap">
                <!-- Issue Type Selector -->
                <select
                  [(ngModel)]="task.issueType"
                  (ngModelChange)="onFieldChange()"
                  class="px-2.5 py-1 rounded text-xs font-bold uppercase tracking-wider bg-indigo-100 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300 border border-indigo-200 focus:outline-none cursor-pointer"
                >
                  <option value="Task">Task</option>
                  <option value="Story">Story</option>
                  <option value="Bug">Bug</option>
                  <option value="Use-Case">Use-Case</option>
                  <option value="Epic">Epic</option>
                  <option value="Sub-task">Sub-task</option>
                </select>

                <!-- Epic Selector -->
                <select
                  [(ngModel)]="task.epicName"
                  (ngModelChange)="onFieldChange()"
                  class="px-2.5 py-1 rounded text-xs font-bold uppercase bg-purple-100 text-purple-700 dark:bg-purple-950 dark:text-purple-300 border border-purple-200 focus:outline-none cursor-pointer"
                >
                  <option value="">+ Gán nhãn Epic</option>
                  @for (epic of epics(); track epic.id) {
                    <option [value]="epic.name">{{ epic.name }}</option>
                  }
                </select>

                <span class="text-xs text-slate-400">Tạo ngày {{ task.createdAt }}</span>
              </div>

              <input
                type="text"
                [(ngModel)]="task.title"
                (ngModelChange)="onFieldChange()"
                class="text-xl font-bold text-slate-900 dark:text-white bg-transparent border-b border-transparent hover:border-slate-300 focus:border-primary focus:outline-none py-1 transition-all"
              />
            </div>

            <!-- Metadata Quick Control Grid (Min 14px font text-sm) -->
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 p-4 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/60 dark:border-slate-700/60">
              <!-- Status Dropdown -->
              <div class="flex flex-col gap-1.5">
                <span class="text-sm font-bold uppercase tracking-wider text-slate-400">Trạng thái</span>
                <select
                  [(ngModel)]="task.statusName"
                  (ngModelChange)="onFieldChange()"
                  class="h-9 px-3 rounded-lg text-sm font-semibold border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary"
                >
                  <option value="To Do">To Do</option>
                  <option value="In Progress">In Progress</option>
                  <option value="Code Review">Code Review</option>
                  <option value="Done">Done</option>
                </select>
              </div>

              <!-- Priority -->
              <div class="flex flex-col gap-1.5">
                <span class="text-sm font-bold uppercase tracking-wider text-slate-400">Độ ưu tiên</span>
                <select
                  [(ngModel)]="task.priority"
                  (ngModelChange)="onFieldChange()"
                  class="h-9 px-3 rounded-lg text-sm font-semibold border border-amber-200 dark:border-amber-900 bg-amber-50 text-amber-800 dark:bg-amber-950 dark:text-amber-300 focus:outline-none"
                >
                  <option value="Low">Low</option>
                  <option value="Medium">Medium</option>
                  <option value="High">High</option>
                  <option value="Urgent">Urgent</option>
                </select>
              </div>

              <!-- Assignee -->
              <div class="flex flex-col gap-1.5">
                <span class="text-sm font-bold uppercase tracking-wider text-slate-400">Người xử lý</span>
                <select
                  [(ngModel)]="task.assigneeName"
                  (ngModelChange)="onFieldChange()"
                  class="h-9 px-3 rounded-lg text-sm font-semibold border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none"
                >
                  <option value="Trần Văn Hoàng">Trần Văn Hoàng</option>
                  <option value="Nguyễn Thanh Hà">Nguyễn Thanh Hà</option>
                  <option value="Phạm Đức Anh">Phạm Đức Anh</option>
                  <option value="Lê Minh Khiêm">Lê Minh Khiêm</option>
                </select>
              </div>

              <!-- Story Points -->
              <div class="flex flex-col gap-1.5">
                <span class="text-sm font-bold uppercase tracking-wider text-slate-400">Story Points</span>
                <input
                  type="number"
                  min="1"
                  max="21"
                  [(ngModel)]="task.storyPoints"
                  (ngModelChange)="onFieldChange()"
                  class="h-9 w-20 px-3 rounded-lg text-sm font-mono font-bold border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 focus:outline-none"
                />
              </div>
            </div>

            <!-- Task Description -->
            <div class="flex flex-col gap-2">
              <span class="text-sm font-bold uppercase tracking-wider text-slate-500">Mô tả công việc</span>
              <textarea
                rows="4"
                [(ngModel)]="task.description"
                (ngModelChange)="onFieldChange()"
                placeholder="Nhập chi tiết mô tả công việc..."
                class="w-full p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-700 text-sm text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary transition-all leading-relaxed"
              ></textarea>
            </div>

            <!-- Sub-tasks Section inside Drawer -->
            <div class="flex flex-col gap-3 pt-4 border-t border-slate-200 dark:border-slate-800">
              <div class="flex items-center justify-between">
                <span class="text-sm font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300 flex items-center gap-2">
                  <span class="material-symbols-outlined text-[20px] text-amber-500">account_tree</span>
                  Danh sách Sub-tasks ({{ drawerSubtasks.length }})
                </span>
                <button (click)="isSubtaskComposerOpen.set(!isSubtaskComposerOpen())" class="text-sm font-bold text-primary hover:underline flex items-center gap-1">
                  <span class="material-symbols-outlined text-[16px]">add</span>
                  <span>Thêm Sub-task</span>
                </button>
              </div>

              @if (isSubtaskComposerOpen()) {
                <div class="flex flex-col sm:flex-row gap-2 p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-700">
                  <input
                    type="text"
                    [ngModel]="newSubtaskTitle()"
                    (ngModelChange)="newSubtaskTitle.set($event)"
                    (keyup.enter)="addSubtaskFromDrawer()"
                    placeholder="Nhập tên Sub-task mới"
                    class="flex-1 h-9 px-3 rounded-lg bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 text-sm focus:outline-none focus:border-primary"
                  />
                  <button (click)="addSubtaskFromDrawer()" class="h-9 px-3 rounded-lg bg-primary text-white text-sm font-semibold">Thêm</button>
                  <button (click)="cancelSubtaskComposer()" class="h-9 px-3 rounded-lg bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-200 text-sm font-semibold">Hủy</button>
                </div>
              }

              <div class="space-y-2">
                @for (sub of drawerSubtasks; track sub.id) {
                  <div class="flex items-center justify-between p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-700/60 text-sm">
                    <div class="flex items-center gap-2">
                      <span class="font-mono font-bold text-amber-600">{{ sub.issueKey }}</span>
                      <span class="font-medium text-slate-800 dark:text-slate-200">{{ sub.title }}</span>
                    </div>
                    <div class="flex items-center gap-2">
                      <span class="px-2 py-0.5 rounded text-xs font-bold bg-slate-200 dark:bg-slate-700">{{ sub.statusName }}</span>
                      <span class="text-xs text-slate-500">{{ sub.assigneeName }}</span>
                    </div>
                  </div>
                }

                @if (drawerSubtasks.length === 0) {
                  <p class="text-xs text-slate-400 italic">Chưa có công việc con nào. Nhấn "+ Thêm Sub-task" để tạo mới.</p>
                }
              </div>
            </div>

            <!-- Discussion & Activities Section -->
            <div class="flex flex-col gap-4 pt-4 border-t border-slate-200 dark:border-slate-800">
              <div class="flex items-center justify-between">
                <span class="text-sm font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300 flex items-center gap-2">
                  <span class="material-symbols-outlined text-[20px]">forum</span>
                  Trao đổi & Bình luận ({{ (task.comments?.length || 0) }})
                </span>
              </div>

              <!-- Comment Input Box -->
              <div class="flex gap-3">
                <div class="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center text-xs font-bold shrink-0">
                  TH
                </div>
                <div class="flex-1 flex flex-col gap-2">
                  <input
                    type="text"
                    [(ngModel)]="newCommentText"
                    (keyup.enter)="submitComment()"
                    placeholder="Viết bình luận hoặc trao đổi... (Enter để gửi)"
                    class="w-full h-10 px-4 rounded-xl bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary"
                  />
                  <div class="flex justify-end">
                    <button
                      (click)="submitComment()"
                      class="px-4 py-1.5 rounded-lg bg-primary text-white text-sm font-semibold hover:bg-primary-container transition-all cursor-pointer"
                    >
                      Gửi bình luận
                    </button>
                  </div>
                </div>
              </div>

              <!-- Comments List -->
              <div class="flex flex-col gap-3 mt-2">
                @for (comment of task.comments; track comment.id) {
                  <div class="flex gap-3 p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/50 dark:border-slate-700/50">
                    <div class="w-8 h-8 rounded-full bg-indigo-600 text-white flex items-center justify-center text-xs font-bold shrink-0">
                      {{ comment.authorName[0] }}
                    </div>
                    <div class="flex flex-col gap-1 text-sm">
                      <div class="flex items-center gap-2">
                        <span class="font-bold text-slate-900 dark:text-white">{{ comment.authorName }}</span>
                        <span class="text-xs text-slate-400">{{ comment.createdAt }}</span>
                      </div>
                      <p class="text-slate-700 dark:text-slate-300 leading-relaxed text-sm">{{ comment.content }}</p>
                    </div>
                  </div>
                }
              </div>
            </div>

          </div>

          <!-- Drawer Footer Controls -->
          <div class="px-6 py-3.5 border-t border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-950 flex justify-end gap-3">
            <button (click)="dismissed.emit()" class="px-4 py-2 rounded-xl text-sm font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-800 transition-colors cursor-pointer">
              Đóng
            </button>
          </div>

        </div>
      </div>
    }
  `
})
export class TaskDetailDrawerComponent {
  @Input() task: WorkItem | null = null;
  @Output() dismissed = new EventEmitter<void>();

  private readonly projectService = inject(ProjectManagementService);
  private readonly toastService = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  readonly drawerWidth = signal<number>(640);
  private isResizing = false;

  readonly epics = this.projectService.epics;
  newCommentText = '';
  readonly isSubtaskComposerOpen = signal(false);
  readonly newSubtaskTitle = signal('');

  startResizing(event: MouseEvent): void {
    event.preventDefault();
    this.isResizing = true;
    const onMouseMove = (moveEvent: MouseEvent) => {
      if (!this.isResizing) return;
      const newWidth = window.innerWidth - moveEvent.clientX;
      if (newWidth >= 380 && newWidth <= window.innerWidth * 0.85) {
        this.drawerWidth.set(newWidth);
      }
    };
    const onMouseUp = () => {
      this.isResizing = false;
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };
    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  }

  get drawerSubtasks(): WorkItem[] {
    if (!this.task) return [];
    return this.projectService.workItems().filter(w => w.parentId === this.task?.id);
  }

  onFieldChange(): void {
    if (this.task) {
      this.projectService.updateWorkItem(this.task);
      this.toastService.success('Đã Cập Nhật Công Việc', `Thay đổi thông tin cho công việc [${this.task.issueKey}]`);
    }
  }

  addSubtaskFromDrawer(): void {
    if (!this.task) return;
    const title = this.newSubtaskTitle().trim();
    if (!title) return;
    const created = this.projectService.createSubTask(this.task.id, title);
    this.toastService.success('Tạo Sub-task', `Đã tạo công việc con [${created.issueKey}]`);
    this.cancelSubtaskComposer();
  }

  cancelSubtaskComposer(): void {
    this.newSubtaskTitle.set('');
    this.isSubtaskComposerOpen.set(false);
  }

  cloneTask(): void {
    if (!this.task) return;
    const cloned = this.projectService.cloneWorkItem(this.task.id);
    if (cloned) {
      this.toastService.success('Nhân Bản Thành Công', `Đã tạo bản sao [${cloned.issueKey}]`);
    }
  }

  deleteTask(): void {
    if (!this.task) return;
    const taskId = this.task.id;
    const issueKey = this.task.issueKey;
    this.confirmDialog.confirm({
      title: 'Xóa công việc',
      message: `Bạn có chắc chắn muốn xóa công việc [${issueKey}]? Hành động này không thể hoàn tác.`,
      type: 'danger',
      confirmText: 'Xóa công việc',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.projectService.deleteWorkItem(taskId);
        this.toastService.warning('Đã Xóa Công Việc', `Công việc [${issueKey}] đã được xóa.`);
        this.dismissed.emit();
      }
    });
  }

  submitComment(): void {
    if (this.task && this.newCommentText.trim()) {
      this.projectService.addCommentToTask(this.task.id, this.newCommentText);
      this.toastService.info('Đã Gửi Bình Luận', `Đã thêm trao đổi vào [${this.task.issueKey}]`);
      this.newCommentText = '';
    }
  }
}
