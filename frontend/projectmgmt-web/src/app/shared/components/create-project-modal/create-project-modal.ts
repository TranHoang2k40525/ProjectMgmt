import { Component, EventEmitter, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectManagementService } from '../../../core/services/project-management.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-create-project-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs animate-fade-in font-body text-slate-900 dark:text-slate-100">
      <div class="w-full max-w-lg bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col">
        
        <!-- Header -->
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/50">
          <div class="flex items-center gap-2.5">
            <div class="w-8 h-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center">
              <span class="material-symbols-outlined text-[20px]">add_business</span>
            </div>
            <div class="flex flex-col">
              <h3 class="text-sm font-bold text-slate-900 dark:text-white">Tạo Dự Án Mới (Create Space)</h3>
              <p class="text-sm text-slate-500">Khởi tạo không gian làm việc Scrum/Kanban mới</p>
            </div>
          </div>

          <button (click)="close.emit()" class="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-slate-200 dark:hover:bg-slate-800 text-slate-500">
            <span class="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        <!-- Body Form -->
        <div class="p-6 flex flex-col gap-4">
          
          <!-- Project Name -->
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Tên dự án <span class="text-red-500">*</span></label>
            <input
              type="text"
              [(ngModel)]="name"
              (ngModelChange)="onNameChange($event)"
              placeholder="ví dụ: BTP - Hệ thống báo cáo eForm"
              class="w-full h-10 px-3.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-medium text-slate-900 dark:text-white focus:outline-none focus:border-primary"
            />
          </div>

          <!-- Project Key & Type Grid -->
          <div class="grid grid-cols-2 gap-4">
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-bold uppercase tracking-wider text-slate-500">Mã Key dự án <span class="text-red-500">*</span></label>
              <input
                type="text"
                [(ngModel)]="projectKey"
                placeholder="ví dụ: EFORM"
                class="w-full h-10 px-3.5 rounded-xl font-mono uppercase font-bold border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm text-primary focus:outline-none focus:border-primary"
              />
            </div>

            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-bold uppercase tracking-wider text-slate-500">Loại dự án</label>
              <select
                [(ngModel)]="type"
                class="h-10 px-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-medium text-slate-900 dark:text-white focus:outline-none focus:border-primary"
              >
                <option value="Software space">Scrum Software Space</option>
                <option value="Business space">Business Process Space</option>
                <option value="AI Research space">AI Research Space</option>
              </select>
            </div>
          </div>

          <!-- Workflow Selection Field (Quy trình làm việc) -->
          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-bold uppercase tracking-wider text-slate-500 flex items-center justify-between">
              <span>Quy trình làm việc (Workflow)</span>
              <span class="text-[11px] text-primary font-semibold">Mặc định</span>
            </label>
            <select
              [(ngModel)]="workflow"
              class="h-10 px-3 rounded-xl border border-primary/40 bg-slate-50 dark:bg-slate-800 text-sm font-semibold text-slate-900 dark:text-white focus:outline-none focus:border-primary"
            >
              <option value="Standard Scrum Workflow">⚡ Quy trình Scrum Chuẩn (Mặc định: To Do &rarr; In Progress &rarr; Code Review &rarr; Done)</option>
              <option value="Simple Kanban Workflow">📋 Quy trình Kanban Đơn Giản (Backlog &rarr; In Progress &rarr; Done)</option>
              <option value="QA Bug Tracking Workflow">🐞 Quy trình Kiểm Thử QA/Bug (New &rarr; Investigating &rarr; In Fix &rarr; Verified)</option>
              <option value="Enterprise Approval Workflow">🏢 Quy trình Phê Duyệt Doanh Nghiệp (Draft &rarr; Pending &rarr; Approved)</option>
            </select>
          </div>

          <!-- Description -->
          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-bold uppercase tracking-wider text-slate-500">Mô tả mục tiêu dự án</label>
            <textarea
              rows="3"
              [(ngModel)]="description"
              placeholder="Mô tả phạm vi dự án và mục tiêu Sprint..."
              class="w-full p-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm font-medium text-slate-900 dark:text-white focus:outline-none focus:border-primary"
            ></textarea>
          </div>

        </div>

        <!-- Footer -->
        <div class="px-6 py-4 border-t border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/50 flex items-center justify-end gap-3">
          <button (click)="close.emit()" class="px-4 py-2 rounded-xl text-sm font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-800 transition-colors">
            Hủy
          </button>
          <button
            [disabled]="!name.trim() || !projectKey.trim()"
            (click)="submitProject()"
            class="px-5 py-2 rounded-xl bg-primary hover:bg-primary-container text-white text-sm font-bold shadow-sm disabled:opacity-50 disabled:cursor-not-allowed transition-all"
          >
            Khởi tạo dự án
          </button>
        </div>

      </div>
    </div>
  `
})
export class CreateProjectModalComponent {
  @Output() close = new EventEmitter<void>();

  private readonly projectService = inject(ProjectManagementService);
  private readonly toastService = inject(ToastService);

  name = '';
  projectKey = '';
  type = 'Software space';
  workflow = 'Standard Scrum Workflow';
  description = '';

  onNameChange(val: string): void {
    if (!this.projectKey || this.projectKey.length <= 4) {
      const words = val.trim().split(/\s+/);
      if (words.length >= 2) {
        this.projectKey = (words[0][0] + words[1][0] + (words[2] ? words[2][0] : '')).toUpperCase();
      } else if (words[0].length >= 3) {
        this.projectKey = words[0].substring(0, 4).toUpperCase();
      }
    }
  }

  submitProject(): void {
    if (!this.name.trim() || !this.projectKey.trim()) return;

    this.projectService.addNewProject({
      name: this.name,
      projectKey: this.projectKey,
      type: this.type,
      description: this.description
    });

    this.toastService.success('Tạo Dự Án Thành Công', `Khởi tạo không gian dự án "${this.name}" (${this.projectKey})`);
    this.close.emit();
  }
}
