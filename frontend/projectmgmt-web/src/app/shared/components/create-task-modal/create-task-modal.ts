import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectManagementService } from '../../../core/services/project-management.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-create-task-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs animate-fade-in font-body text-slate-900 dark:text-slate-100">
      <div class="w-full max-w-lg bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col">
        
        <!-- Header -->
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/50">
          <div class="flex items-center gap-2">
            <span class="material-symbols-outlined text-[20px] text-primary">add_task</span>
            <h3 class="text-base font-bold text-slate-900 dark:text-white">Tạo Công Việc / Epic / Use-Case Mới</h3>
          </div>

          <button (click)="close.emit()" class="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-slate-200 dark:hover:bg-slate-800 text-slate-500">
            <span class="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        <!-- Form Fields Body (Min 14px text-sm) -->
        <div class="p-6 flex flex-col gap-4">
          
          <!-- Title -->
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Tên công việc <span class="text-red-500">*</span></label>
            <input
              type="text"
              [(ngModel)]="title"
              placeholder="Nhập tên ngắn gọn mô tả công việc..."
              class="w-full h-10 px-3.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-medium text-slate-900 dark:text-white focus:outline-none focus:border-primary"
            />
          </div>

          <!-- Type & Priority Grid -->
          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-1.5">
              <div class="flex items-center justify-between">
                <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Loại công việc</label>
                <button (click)="toggleNewTypeForm()" class="text-xs text-primary font-semibold hover:underline">
                  {{ showNewTypeForm() ? 'Hủy' : '+ Tạo Loại Mới' }}
                </button>
              </div>

              @if (showNewTypeForm()) {
                <div class="flex items-center gap-2 p-2 rounded-xl bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
                  <input type="text" [(ngModel)]="customTypeName" placeholder="Tên loại mới..." class="h-8 px-2 text-xs rounded border border-slate-300 w-full" />
                  <select [(ngModel)]="customTypeIcon" class="h-8 text-xs rounded border border-slate-300">
                    <option value="⚡">⚡ Epic</option>
                    <option value="🛡️">🛡️ Security</option>
                    <option value="🎨">🎨 Design</option>
                    <option value="📈">📈 Telemetry</option>
                    <option value="📋">📋 Research</option>
                    <option value="🧪">🧪 Test</option>
                  </select>
                  <button (click)="saveCustomIssueType()" class="px-2 py-1 text-xs bg-primary text-white rounded font-bold shrink-0">Tạo</button>
                </div>
              } @else {
                <select
                  [(ngModel)]="issueType"
                  class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-semibold text-slate-900 dark:text-white focus:outline-none focus:border-primary"
                >
                  @for (t of customIssueTypes(); track t.id) {
                    <option [value]="t.name">{{ t.icon }} {{ t.name }}</option>
                  }
                </select>
              }
            </div>

            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Mức độ ưu tiên</label>
              <select
                [(ngModel)]="priority"
                class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-semibold text-slate-900 dark:text-white focus:outline-none focus:border-primary"
              >
                <option value="Low">Low (Thấp)</option>
                <option value="Medium">Medium (Trung bình)</option>
                <option value="High">High (Cao)</option>
                <option value="Urgent">Urgent (Khẩn cấp)</option>
              </select>
            </div>
          </div>

          <!-- Sprint & Epic Grid -->
          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Sprint</label>
              <select
                [(ngModel)]="sprintName"
                class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-semibold text-slate-900 dark:text-white focus:outline-none focus:border-primary"
              >
                <option value="SCRUMAI Sprint 2">SCRUMAI Sprint 2 (Đang chạy)</option>
                <option value="SCRUMAI Sprint 3">SCRUMAI Sprint 3 (Kế hoạch)</option>
                <option value="SCRUMAI Sprint 4">SCRUMAI Sprint 4 (Tương lai)</option>
                <option value="Backlog Pool">Backlog Pool (Chưa gán)</option>
              </select>
            </div>

            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Gán Epic</label>
              <select
                [(ngModel)]="epicName"
                class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-semibold text-slate-900 dark:text-white focus:outline-none focus:border-primary"
              >
                <option value="">Không gán Epic</option>
                @for (epic of epics(); track epic.id) {
                  <option [value]="epic.name">{{ epic.name }}</option>
                }
              </select>
            </div>
          </div>

          <!-- Story Points & Assignee Grid -->
          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Story Points</label>
              <input
                type="number"
                min="1"
                max="21"
                [(ngModel)]="storyPoints"
                class="h-10 px-3.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-mono font-bold text-slate-900 dark:text-white focus:outline-none focus:border-primary"
              />
            </div>

            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Người xử lý</label>
              <select
                [(ngModel)]="assigneeName"
                class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-semibold text-slate-900 dark:text-white focus:outline-none focus:border-primary"
              >
                <option value="Trần Văn Hoàng">Trần Văn Hoàng (Lead)</option>
                <option value="Nguyễn Thanh Hà">Nguyễn Thanh Hà (Scrum Master)</option>
                <option value="Phạm Đức Anh">Phạm Đức Anh (Developer)</option>
                <option value="Lê Minh Khiêm">Lê Minh Khiêm (QA)</option>
              </select>
            </div>
          </div>

          <!-- Description -->
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Mô tả chi tiết</label>
            <textarea
              rows="3"
              [(ngModel)]="description"
              placeholder="Mô tả yêu cầu hoặc ghi chú thêm cho người xử lý..."
              class="w-full p-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-medium text-slate-900 dark:text-white focus:outline-none focus:border-primary leading-relaxed"
            ></textarea>
          </div>

        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/50 flex items-center justify-end gap-3">
          <button (click)="close.emit()" class="px-4 py-2 rounded-xl text-sm font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-800 transition-colors cursor-pointer">
            Hủy
          </button>
          <button
            [disabled]="!title.trim()"
            (click)="submitTask()"
            class="px-5 py-2 rounded-xl bg-primary hover:bg-primary-container text-white text-sm font-bold shadow-xs disabled:opacity-50 disabled:cursor-not-allowed transition-all cursor-pointer"
          >
            Tạo công việc
          </button>
        </div>

      </div>
    </div>
  `
})
export class CreateTaskModalComponent {
  @Output() close = new EventEmitter<void>();

  private readonly projectService = inject(ProjectManagementService);
  private readonly toastService = inject(ToastService);

  readonly epics = this.projectService.epics;
  readonly customIssueTypes = this.projectService.customIssueTypes;

  showNewTypeForm = signal<boolean>(false);
  customTypeName = '';
  customTypeIcon = '🛡️';

  title = '';
  issueType = 'Task';
  priority: 'Low' | 'Medium' | 'High' | 'Urgent' = 'Medium';
  sprintName = 'SCRUMAI Sprint 2';
  epicName = '';
  storyPoints = 3;
  assigneeName = 'Trần Văn Hoàng';
  description = '';

  toggleNewTypeForm(): void {
    this.showNewTypeForm.update(v => !v);
  }

  saveCustomIssueType(): void {
    if (!this.customTypeName.trim()) return;
    const newType = this.projectService.addCustomIssueType(this.customTypeName.trim(), this.customTypeIcon, 'indigo');
    this.toastService.success('Tạo Loại Issue Mới', `Đã tạo loại [${newType.icon} ${newType.name}] thành công!`);
    this.issueType = newType.name;
    this.showNewTypeForm.set(false);
    this.customTypeName = '';
  }

  submitTask(): void {
    if (!this.title.trim()) return;

    this.projectService.addWorkItem({
      title: this.title,
      description: this.description,
      issueType: this.issueType,
      priority: this.priority,
      storyPoints: this.storyPoints,
      sprintName: this.sprintName,
      epicName: this.epicName || undefined,
      epicColor: this.epicName ? 'purple' : undefined,
      statusName: 'To Do',
      assigneeName: this.assigneeName
    });

    this.toastService.success('Tạo Công Việc Thành Công', `Đã khởi tạo công việc "${this.title}" trong ${this.sprintName}`);
    this.close.emit();
  }
}
