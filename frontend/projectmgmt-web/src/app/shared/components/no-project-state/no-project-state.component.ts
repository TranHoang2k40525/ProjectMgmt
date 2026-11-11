import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Project, ProjectManagementService } from '../../../core/services/project-management.service';

@Component({
  selector: 'app-no-project-state',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="no-project-wrapper flex items-center justify-center p-6 sm:p-12 min-h-[60vh]">
      <div class="max-w-xl w-full bg-white dark:bg-slate-900 border border-slate-200/90 dark:border-slate-800 rounded-3xl p-8 sm:p-10 shadow-xl text-center flex flex-col items-center gap-5">
        
        <!-- Jira-grade Status Monogram -->
        <div class="w-16 h-16 rounded-2xl bg-emerald-50 dark:bg-emerald-950/50 border border-emerald-200/80 dark:border-emerald-800 text-[#1b4332] dark:text-emerald-300 flex items-center justify-center shadow-inner">
          <span class="material-symbols-outlined text-[32px]">folder_off</span>
        </div>

        <!-- Heading & Context -->
        <div class="space-y-2">
          <div class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 text-xs font-bold tracking-wide uppercase">
            <span class="w-2 h-2 rounded-full bg-amber-500"></span>
            Chưa kích hoạt không gian dự án
          </div>
          <h2 class="text-2xl font-bold text-slate-900 dark:text-white tracking-tight">
            Bạn chưa chọn dự án nào để làm việc
          </h2>
          <p class="text-sm text-slate-600 dark:text-slate-400 leading-relaxed max-w-md mx-auto">
            Không gian <span class="font-semibold text-slate-800 dark:text-slate-200">{{ featureName }}</span> chỉ khả dụng khi bạn đang làm việc trong một dự án cụ thể. Vui lòng chọn một dự án từ danh sách hoặc khởi tạo dự án mới để bắt đầu quy trình Scrum.
          </p>
        </div>

        <!-- Available Projects Quick-Picker (If any exist) -->
        @if (projects().length > 0) {
          <div class="w-full pt-4 pb-2 border-t border-slate-100 dark:border-slate-800 flex flex-col gap-2.5">
            <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">
              Chọn nhanh một dự án có sẵn:
            </span>
            <div class="flex flex-wrap gap-2 justify-center max-h-48 overflow-y-auto p-1">
              @for (proj of projects(); track proj.id) {
                <button
                  type="button"
                  (click)="selectProject(proj)"
                  class="px-3.5 py-2 rounded-xl bg-slate-50 dark:bg-slate-800/80 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 border border-slate-200 dark:border-slate-700 hover:border-emerald-300 text-xs font-bold text-slate-800 dark:text-slate-200 flex items-center gap-2 transition-all cursor-pointer group"
                >
                  <span class="w-5 h-5 rounded bg-[#1b4332] text-white flex items-center justify-center font-mono text-[10px]">
                    {{ proj.projectKey.slice(0, 2) }}
                  </span>
                  <span class="group-hover:text-[#1b4332] dark:group-hover:text-emerald-300">{{ proj.name }}</span>
                  <span class="text-slate-400 font-mono text-[10px]">({{ proj.projectKey }})</span>
                </button>
              }
            </div>
          </div>
        }

        <!-- Primary Action Buttons -->
        <div class="flex flex-col sm:flex-row items-center gap-3 w-full justify-center pt-2">
          <a
            routerLink="/projects/new"
            class="w-full sm:w-auto px-5 py-2.5 rounded-xl bg-[#1b4332] hover:bg-[#2d6a4f] text-white text-sm font-bold shadow-md shadow-emerald-900/10 flex items-center justify-center gap-2 transition-all cursor-pointer"
          >
            <span class="material-symbols-outlined text-[18px]">add_circle</span>
            <span>Khởi tạo Dự Án Mới</span>
          </a>

          <a
            routerLink="/projects"
            class="w-full sm:w-auto px-5 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 text-sm font-bold flex items-center justify-center gap-2 transition-all cursor-pointer"
          >
            <span class="material-symbols-outlined text-[18px]">folder_open</span>
            <span>Danh Sách Dự Án</span>
          </a>
        </div>

      </div>
    </div>
  `
})
export class NoProjectStateComponent {
  @Input() featureName = 'Tính năng dự án';

  private readonly projectService = inject(ProjectManagementService);
  readonly projects = this.projectService.allProjectsList;

  selectProject(proj: Project): void {
    this.projectService.selectProject(proj);
  }
}
