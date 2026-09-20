import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ProjectManagementService } from '../../core/services/project-management.service';

@Component({
  selector: 'app-project-summary-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="flex flex-col gap-6 animate-fade-in">
      
      <!-- Top Banner Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 p-6 rounded-2xl bg-gradient-to-r from-slate-900 via-indigo-950 to-slate-900 text-white shadow-xl relative overflow-hidden">
        <div class="absolute -right-10 -bottom-10 w-64 h-64 bg-primary/20 rounded-full blur-3xl pointer-events-none"></div>

        <div class="flex items-center gap-4 z-10">
          <div class="w-14 h-14 rounded-2xl bg-white/10 backdrop-blur-md p-1.5 ring-1 ring-white/20 shrink-0 flex items-center justify-center">
            <img src="assets/images/huce-branding/huce-official-logo.png" alt="HUCE Logo" class="w-full h-full object-contain" />
          </div>
          <div class="flex flex-col">
            <div class="flex items-center gap-2">
              <span class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-white/20 text-white">Scrum Agile</span>
              <span class="text-xs text-indigo-300">Mã dự án: SCRUMAI</span>
            </div>
            <h1 class="text-xl font-extrabold tracking-tight mt-0.5">HUCE AI Lab Project Management</h1>
            <p class="text-xs text-slate-300">Nền tảng Quản lý Dự án Agile tích hợp AI dành cho Giảng viên & Sinh viên HUCE</p>
          </div>
        </div>

        <div class="flex items-center gap-3 z-10">
          <a routerLink="/board" class="px-4 py-2 rounded-xl bg-primary hover:bg-primary-container text-white text-xs font-bold shadow-md transition-all flex items-center gap-1.5">
            <span class="material-symbols-outlined text-[18px]">view_kanban</span>
            Mở Bảng Scrum
          </a>
        </div>
      </div>

      <!-- Bento Grid Layout Container -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        
        <!-- KPI Tile 1: Done -->
        <div class="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-sm flex flex-col justify-between hover:shadow-md transition-shadow">
          <div class="flex items-center justify-between text-emerald-600 dark:text-emerald-400">
            <span class="text-xs font-bold uppercase tracking-wider">Đã hoàn thành</span>
            <div class="w-8 h-8 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 flex items-center justify-center">
              <span class="material-symbols-outlined text-[20px]">check_circle</span>
            </div>
          </div>
          <div class="mt-4 flex items-baseline gap-2">
            <span class="text-3xl font-extrabold text-slate-900 dark:text-white">{{ completedCount() }}</span>
            <span class="text-xs text-slate-400">/ {{ totalCount() }} công việc</span>
          </div>
        </div>

        <!-- KPI Tile 2: In Progress -->
        <div class="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-sm flex flex-col justify-between hover:shadow-md transition-shadow">
          <div class="flex items-center justify-between text-blue-600 dark:text-blue-400">
            <span class="text-xs font-bold uppercase tracking-wider">Đang thực hiện</span>
            <div class="w-8 h-8 rounded-xl bg-blue-50 dark:bg-blue-950/60 flex items-center justify-center">
              <span class="material-symbols-outlined text-[20px]">sync</span>
            </div>
          </div>
          <div class="mt-4 flex items-baseline gap-2">
            <span class="text-3xl font-extrabold text-slate-900 dark:text-white">{{ inProgressCount() }}</span>
            <span class="text-xs text-slate-400">công việc active</span>
          </div>
        </div>

        <!-- KPI Tile 3: Active Sprint -->
        <div class="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-sm flex flex-col justify-between hover:shadow-md transition-shadow">
          <div class="flex items-center justify-between text-indigo-600 dark:text-indigo-400">
            <span class="text-xs font-bold uppercase tracking-wider">Sprint Hiện Tại</span>
            <div class="w-8 h-8 rounded-xl bg-indigo-50 dark:bg-indigo-950/60 flex items-center justify-center">
              <span class="material-symbols-outlined text-[20px]">directions_run</span>
            </div>
          </div>
          <div class="mt-4 flex flex-col">
            <span class="text-base font-bold text-slate-900 dark:text-white">SCRUMAI Sprint 2</span>
            <span class="text-[11px] text-slate-400">25 Aug - 08 Sep (28 Story Pts)</span>
          </div>
        </div>

        <!-- KPI Tile 4: AI Health Score -->
        <div class="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-sm flex flex-col justify-between hover:shadow-md transition-shadow">
          <div class="flex items-center justify-between text-purple-600 dark:text-purple-400">
            <span class="text-xs font-bold uppercase tracking-wider">AI Sprint Health</span>
            <div class="w-8 h-8 rounded-xl bg-purple-50 dark:bg-purple-950/60 flex items-center justify-center">
              <span class="material-symbols-outlined text-[20px]">psychology</span>
            </div>
          </div>
          <div class="mt-4 flex items-baseline gap-2">
            <span class="text-3xl font-extrabold text-emerald-500">96%</span>
            <span class="text-xs text-slate-400">Tiến độ On-track</span>
          </div>
        </div>

      </div>

      <!-- Bento Big Tiles: Status Donut Breakdown & Activity Stream -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        <!-- Tile Left: Status Overview Donut Visual -->
        <div class="lg:col-span-2 p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-sm flex flex-col gap-6">
          <div class="flex items-center justify-between">
            <div class="flex flex-col">
              <h3 class="text-base font-bold text-slate-900 dark:text-white">Tổng Quan Trạng Thái Công Việc</h3>
              <p class="text-xs text-slate-400">Phân bố công việc theo trạng thái trong dự án</p>
            </div>
            <a routerLink="/task-list" class="text-xs font-bold text-primary hover:underline">Xem tất cả công việc &rarr;</a>
          </div>

          <div class="flex flex-col md:flex-row items-center justify-around gap-8 py-4">
            <!-- Simulated Donut Ring Chart -->
            <div class="relative w-44 h-44 flex items-center justify-center">
              <svg class="w-full h-full transform -rotate-90" viewBox="0 0 36 36">
                <path class="text-slate-100 dark:text-slate-800" stroke-width="3.8" stroke="currentColor" fill="none" d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                <!-- Done Segment -->
                <path class="text-emerald-500" stroke-dasharray="35, 100" stroke-width="3.8" stroke="currentColor" fill="none" d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                <!-- In Progress Segment -->
                <path class="text-blue-500" stroke-dasharray="25, 100" stroke-dashoffset="-35" stroke-width="3.8" stroke="currentColor" fill="none" d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
              </svg>
              <div class="absolute flex flex-col items-center justify-center text-center">
                <span class="text-2xl font-black text-slate-900 dark:text-white">{{ totalCount() }}</span>
                <span class="text-[10px] text-slate-400 font-medium">Tổng số Work Items</span>
              </div>
            </div>

            <!-- Legend Items -->
            <div class="flex flex-col gap-3">
              <div class="flex items-center gap-3">
                <span class="w-3.5 h-3.5 rounded-md bg-amber-400"></span>
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300">To Do (Cần làm): {{ toDoCount() }}</span>
              </div>
              <div class="flex items-center gap-3">
                <span class="w-3.5 h-3.5 rounded-md bg-blue-500"></span>
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300">In Progress (Đang làm): {{ inProgressCount() }}</span>
              </div>
              <div class="flex items-center gap-3">
                <span class="w-3.5 h-3.5 rounded-md bg-purple-500"></span>
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300">Code Review (Duyệt): {{ codeReviewCount() }}</span>
              </div>
              <div class="flex items-center gap-3">
                <span class="w-3.5 h-3.5 rounded-md bg-emerald-500"></span>
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300">Done (Hoàn thành): {{ completedCount() }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Tile Right: Activity Stream (Bkav eTask style) -->
        <div class="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-sm flex flex-col gap-4">
          <h3 class="text-base font-bold text-slate-900 dark:text-white">Dòng Hoạt Động Mới</h3>

          <div class="flex flex-col gap-3">
            <div class="flex items-start gap-3 p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40">
              <div class="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center text-xs font-bold shrink-0">TH</div>
              <div class="flex flex-col text-xs">
                <span class="font-bold text-slate-900 dark:text-white">Trần Văn Hoàng</span>
                <span class="text-slate-600 dark:text-slate-300">Đã đổi trạng thái <span class="font-semibold text-blue-600">SCRUMAI-201</span> sang In Progress</span>
                <span class="text-[10px] text-slate-400 mt-1">10 phút trước</span>
              </div>
            </div>

            <div class="flex items-start gap-3 p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40">
              <div class="w-8 h-8 rounded-full bg-indigo-600 text-white flex items-center justify-center text-xs font-bold shrink-0">TH</div>
              <div class="flex flex-col text-xs">
                <span class="font-bold text-slate-900 dark:text-white">Nguyễn Thanh Hà</span>
                <span class="text-slate-600 dark:text-slate-300">Đã thêm bình luận vào <span class="font-semibold text-indigo-600">SCRUMAI-204</span></span>
                <span class="text-[10px] text-slate-400 mt-1">1 giờ trước</span>
              </div>
            </div>

            <div class="flex items-start gap-3 p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40">
              <div class="w-8 h-8 rounded-full bg-emerald-600 text-white flex items-center justify-center text-xs font-bold shrink-0">PĐ</div>
              <div class="flex flex-col text-xs">
                <span class="font-bold text-slate-900 dark:text-white">Phạm Đức Anh</span>
                <span class="text-slate-600 dark:text-slate-300">Đã xuất báo cáo Excel cho Sprint 2</span>
                <span class="text-[10px] text-slate-400 mt-1">Hôm qua</span>
              </div>
            </div>
          </div>
        </div>

      </div>

    </div>
  `
})
export class ProjectSummaryPageComponent {
  private readonly projectService = inject(ProjectManagementService);

  readonly items = this.projectService.workItems;

  readonly totalCount = computed(() => this.items().length);
  readonly completedCount = computed(() => this.items().filter(i => i.statusName === 'Done').length);
  readonly inProgressCount = computed(() => this.items().filter(i => i.statusName === 'In Progress').length);
  readonly codeReviewCount = computed(() => this.items().filter(i => i.statusName === 'Code Review').length);
  readonly toDoCount = computed(() => this.items().filter(i => i.statusName === 'To Do').length);
}
