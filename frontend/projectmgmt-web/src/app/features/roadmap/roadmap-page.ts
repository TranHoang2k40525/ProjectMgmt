import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  EpicItem,
  ProjectManagementService,
  Sprint,
  WorkItem
} from '../../core/services/project-management.service';
import { IdentityService } from '../../core/services/identity.service';
import { ToastService } from '../../core/services/toast.service';
import { NoProjectStateComponent } from '../../shared/components/no-project-state/no-project-state.component';

export interface TimelineColumn {
  key: string;
  label: string;
  subLabel: string;
  startMs: number;
  endMs: number;
  isCurrent: boolean;
  isWeekend?: boolean;
}

export interface GanttBarLayout {
  leftPercent: number;
  widthPercent: number;
  isOutOfLeft: boolean;
  isOutOfRight: boolean;
  startLabel: string;
  endLabel: string;
  durationDays: number;
}

export interface RoadmapGroupProgress {
  percent: number;
  doneCount: number;
  inProgressCount: number;
  todoCount: number;
  totalCount: number;
  donePoints: number;
  totalPoints: number;
  label: string;
  shortLabel: string;
  statusBadgeLabel: string;
  barTrackClass: string;
  barFillClass: string;
  badgeClass: string;
  dotClass: string;
}

@Component({
  selector: 'app-roadmap-page',
  standalone: true,
  imports: [CommonModule, FormsModule, NoProjectStateComponent],
  template: `
    @if (!projectService.hasActiveProject()) {
      <app-no-project-state featureName="Lộ Trình Dự Án (Roadmap)"></app-no-project-state>
    } @else {
      <div class="roadmap-page flex flex-col gap-3.5 animate-fade-in font-body text-slate-900 dark:text-slate-100">

        <!-- Top Header & Actions -->
        <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-2.5 pb-2 border-b border-slate-200 dark:border-slate-800">
          <div class="flex flex-col gap-1">
            <div class="flex items-center gap-2.5 flex-wrap">
              <h1 class="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <span class="material-symbols-outlined text-[26px] text-primary" aria-hidden="true">timeline</span>
                Lộ Trình Dự Án (Project Roadmap)
              </h1>
              @if (projectService.currentProject().projectKey) {
                <span class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-primary/10 text-primary border border-primary/20">
                  {{ projectService.currentProject().name }} ({{ projectService.currentProject().projectKey }})
                </span>
              }
            </div>
            <p class="text-sm text-slate-500 dark:text-slate-400">
              Biểu đồ Gantt theo dõi tiến độ thực tế của các Sprint, Epic và hạng mục công việc theo thời gian thực
            </p>
          </div>

          <div class="flex items-center flex-wrap gap-2">
            <button
              type="button"
              (click)="toggleExpandAll()"
              class="inline-flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors cursor-pointer"
            >
              <span class="material-symbols-outlined text-[16px]" aria-hidden="true">
                {{ areAllExpanded() ? 'unfold_less' : 'unfold_more' }}
              </span>
              {{ areAllExpanded() ? 'Thu gọn tất cả' : 'Mở rộng công việc' }}
            </button>

            <button
              type="button"
              (click)="jumpToAnchor()"
              class="inline-flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors cursor-pointer"
              title="Đưa khung nhìn về lịch Sprint hiện tại"
            >
              <span class="material-symbols-outlined text-[16px] text-primary" aria-hidden="true">my_location</span>
              Về mốc lộ trình
            </button>

            @if (canManageSprints()) {
              <button
                type="button"
                (click)="createNewSprint()"
                class="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold bg-slate-900 dark:bg-primary text-white hover:opacity-90 transition-opacity cursor-pointer shadow-xs"
              >
                <span class="material-symbols-outlined text-[16px]" aria-hidden="true">add</span>
                Tạo Sprint mới
              </button>
            }
          </div>
        </div>

        <!-- KPI Summary Strip -->
        <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-3.5">
          <!-- Card 1: Sprints -->
          <div class="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-2xs flex items-center justify-between gap-3">
            <div class="flex flex-col gap-1">
              <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">Chu kỳ Sprint</span>
              <div class="flex items-baseline gap-2">
                <span class="text-2xl font-extrabold text-slate-900 dark:text-white">{{ kpiSummary().totalSprints }}</span>
                <span class="text-xs font-semibold text-blue-600 dark:text-blue-400">
                  {{ kpiSummary().activeSprints }} đang chạy
                </span>
              </div>
              <span class="text-xs text-slate-500">
                {{ kpiSummary().plannedSprints }} kế hoạch · {{ kpiSummary().completedSprints }} hoàn tất
              </span>
            </div>
            <div class="w-10 h-10 rounded-xl bg-blue-500/10 text-blue-600 dark:text-blue-400 flex items-center justify-center shrink-0">
              <span class="material-symbols-outlined text-[22px]" aria-hidden="true">sprint</span>
            </div>
          </div>

          <!-- Card 2: Task Progress -->
          <div class="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-2xs flex items-center justify-between gap-3">
            <div class="flex flex-col gap-1 flex-1 min-w-0">
              <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">Tiến độ công việc</span>
              <div class="flex items-baseline gap-2">
                <span class="text-2xl font-extrabold text-emerald-600 dark:text-emerald-400">{{ kpiSummary().taskCompletionPct }}%</span>
                <span class="text-xs font-semibold text-slate-600 dark:text-slate-300">
                  ({{ kpiSummary().doneTasks }}/{{ kpiSummary().totalTasks }} việc)
                </span>
              </div>
              <div class="w-full h-1.5 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden mt-1">
                <div class="h-full bg-emerald-500 rounded-full transition-all duration-300" [style.width.%]="kpiSummary().taskCompletionPct"></div>
              </div>
            </div>
            <div class="w-10 h-10 rounded-xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
              <span class="material-symbols-outlined text-[22px]" aria-hidden="true">task_alt</span>
            </div>
          </div>

          <!-- Card 3: Story Points -->
          <div class="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-2xs flex items-center justify-between gap-3">
            <div class="flex flex-col gap-1">
              <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">Story Points cam kết</span>
              <div class="flex items-baseline gap-2">
                <span class="text-2xl font-extrabold text-indigo-600 dark:text-indigo-400">{{ kpiSummary().donePoints }}/{{ kpiSummary().totalPoints }}</span>
                <span class="text-xs font-bold text-indigo-600 dark:text-indigo-400">Points</span>
              </div>
              <span class="text-xs text-slate-500">
                {{ kpiSummary().inProgressTasks }} việc đang thực hiện
              </span>
            </div>
            <div class="w-10 h-10 rounded-xl bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 flex items-center justify-center shrink-0">
              <span class="material-symbols-outlined text-[22px]" aria-hidden="true">speed</span>
            </div>
          </div>

          <!-- Card 4: Schedule Range -->
          <div class="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-2xs flex items-center justify-between gap-3">
            <div class="flex flex-col gap-1 min-w-0">
              <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">Khung lịch trình</span>
              <span class="text-sm font-bold text-slate-900 dark:text-white truncate">
                {{ kpiSummary().dateRangeLabel }}
              </span>
              <span class="text-xs text-slate-500">
                {{ epics().length }} Epic · {{ workItems().length }} hạng mục
              </span>
            </div>
            <div class="w-10 h-10 rounded-xl bg-amber-500/10 text-amber-600 dark:text-amber-400 flex items-center justify-center shrink-0">
              <span class="material-symbols-outlined text-[22px]" aria-hidden="true">calendar_month</span>
            </div>
          </div>
        </div>

        <!-- Main Roadmap Panel -->
        <div class="roadmap-panel p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col gap-4">

          <!-- Controls Toolbar -->
          <div class="flex flex-col xl:flex-row xl:items-center justify-between gap-3 pb-4 border-b border-slate-200 dark:border-slate-800">
            <!-- Left: View Mode + Search + Status Filter -->
            <div class="flex flex-wrap items-center gap-2.5">
              <!-- Grouping Mode Switcher -->
              <div class="inline-flex items-center bg-slate-100 dark:bg-slate-800 p-1 rounded-xl text-xs font-bold">
                <button
                  type="button"
                  (click)="viewMode.set('sprint')"
                  [class.bg-white]="viewMode() === 'sprint'"
                  [class.dark:bg-slate-700]="viewMode() === 'sprint'"
                  [class.text-slate-900]="viewMode() === 'sprint'"
                  [class.dark:text-white]="viewMode() === 'sprint'"
                  [class.shadow-2xs]="viewMode() === 'sprint'"
                  [class.text-slate-500]="viewMode() !== 'sprint'"
                  class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg cursor-pointer transition-all"
                >
                  <span class="material-symbols-outlined text-[15px]" aria-hidden="true">view_timeline</span>
                  Theo Sprint ({{ sprints().length }})
                </button>
                <button
                  type="button"
                  (click)="viewMode.set('epic')"
                  [class.bg-white]="viewMode() === 'epic'"
                  [class.dark:bg-slate-700]="viewMode() === 'epic'"
                  [class.text-slate-900]="viewMode() === 'epic'"
                  [class.dark:text-white]="viewMode() === 'epic'"
                  [class.shadow-2xs]="viewMode() === 'epic'"
                  [class.text-slate-500]="viewMode() !== 'epic'"
                  class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg cursor-pointer transition-all"
                >
                  <span class="material-symbols-outlined text-[15px]" aria-hidden="true">bolt</span>
                  Theo Epic ({{ epics().length }})
                </button>
              </div>

              <!-- Search Input -->
              <div class="relative min-w-[220px] flex-1 sm:flex-initial">
                <span class="material-symbols-outlined text-[16px] text-slate-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" aria-hidden="true">search</span>
                <input
                  type="text"
                  [ngModel]="searchQuery()"
                  (ngModelChange)="searchQuery.set($event)"
                  placeholder="Tìm Sprint, mã việc, tên công việc..."
                  class="w-full pl-8 pr-7 py-1.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/70 text-slate-800 dark:text-slate-100 focus:outline-none focus:border-primary"
                />
                @if (searchQuery()) {
                  <button
                    type="button"
                    (click)="searchQuery.set('')"
                    class="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 cursor-pointer"
                  >
                    <span class="material-symbols-outlined text-[14px]" aria-hidden="true">close</span>
                  </button>
                }
              </div>

              <!-- Status Filter Pills -->
              @if (viewMode() === 'sprint') {
                <div class="inline-flex items-center gap-1 bg-slate-100/80 dark:bg-slate-800/80 p-1 rounded-xl text-xs font-semibold">
                  <button
                    type="button"
                    (click)="statusFilter.set('all')"
                    [class.bg-white]="statusFilter() === 'all'"
                    [class.dark:bg-slate-700]="statusFilter() === 'all'"
                    [class.text-slate-900]="statusFilter() === 'all'"
                    [class.text-slate-500]="statusFilter() !== 'all'"
                    class="px-2.5 py-1 rounded-lg cursor-pointer transition-all"
                  >Tất cả</button>
                  <button
                    type="button"
                    (click)="statusFilter.set('active')"
                    [class.bg-white]="statusFilter() === 'active'"
                    [class.dark:bg-slate-700]="statusFilter() === 'active'"
                    [class.text-blue-600]="statusFilter() === 'active'"
                    [class.text-slate-500]="statusFilter() !== 'active'"
                    class="px-2.5 py-1 rounded-lg cursor-pointer transition-all"
                  >Đang chạy</button>
                  <button
                    type="button"
                    (click)="statusFilter.set('future')"
                    [class.bg-white]="statusFilter() === 'future'"
                    [class.dark:bg-slate-700]="statusFilter() === 'future'"
                    [class.text-slate-900]="statusFilter() === 'future'"
                    [class.text-slate-500]="statusFilter() !== 'future'"
                    class="px-2.5 py-1 rounded-lg cursor-pointer transition-all"
                  >Kế hoạch</button>
                  <button
                    type="button"
                    (click)="statusFilter.set('completed')"
                    [class.bg-white]="statusFilter() === 'completed'"
                    [class.dark:bg-slate-700]="statusFilter() === 'completed'"
                    [class.text-emerald-600]="statusFilter() === 'completed'"
                    [class.text-slate-500]="statusFilter() !== 'completed'"
                    class="px-2.5 py-1 rounded-lg cursor-pointer transition-all"
                  >Hoàn thành</button>
                </div>
              }
            </div>

            <!-- Right: Timeline Navigation + Tuần / Tháng / Quý Switcher -->
            <div class="flex flex-wrap items-center justify-between sm:justify-end gap-2.5">
              <!-- Timeline Window Pager -->
              <div class="inline-flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl text-xs font-semibold">
                <button
                  type="button"
                  (click)="shiftTimeline(-1)"
                  class="p-1.5 rounded-lg hover:bg-white dark:hover:bg-slate-700 text-slate-600 dark:text-slate-300 transition-colors cursor-pointer"
                  title="Lùi trục thời gian"
                >
                  <span class="material-symbols-outlined text-[16px] block" aria-hidden="true">chevron_left</span>
                </button>
                <span class="px-2.5 py-1 text-xs font-bold text-slate-700 dark:text-slate-200 whitespace-nowrap">
                  {{ timelineWindowLabel() }}
                </span>
                <button
                  type="button"
                  (click)="shiftTimeline(1)"
                  class="p-1.5 rounded-lg hover:bg-white dark:hover:bg-slate-700 text-slate-600 dark:text-slate-300 transition-colors cursor-pointer"
                  title="Tiến trục thời gian"
                >
                  <span class="material-symbols-outlined text-[16px] block" aria-hidden="true">chevron_right</span>
                </button>
              </div>

              <!-- Timeframe Granularity Buttons (Tuần / Tháng / Quý) -->
              <div class="inline-flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl text-xs font-bold">
                <button
                  type="button"
                  (click)="setTimeframe('week')"
                  [class.bg-white]="timeframe() === 'week'"
                  [class.dark:bg-slate-700]="timeframe() === 'week'"
                  [class.text-slate-900]="timeframe() === 'week'"
                  [class.dark:text-white]="timeframe() === 'week'"
                  [class.shadow-2xs]="timeframe() === 'week'"
                  [class.text-slate-500]="timeframe() !== 'week'"
                  class="px-3.5 py-1.5 rounded-lg cursor-pointer transition-all"
                >Tuần</button>
                <button
                  type="button"
                  (click)="setTimeframe('month')"
                  [class.bg-white]="timeframe() === 'month'"
                  [class.dark:bg-slate-700]="timeframe() === 'month'"
                  [class.text-slate-900]="timeframe() === 'month'"
                  [class.dark:text-white]="timeframe() === 'month'"
                  [class.shadow-2xs]="timeframe() === 'month'"
                  [class.text-slate-500]="timeframe() !== 'month'"
                  class="px-3.5 py-1.5 rounded-lg cursor-pointer transition-all"
                >Tháng</button>
                <button
                  type="button"
                  (click)="setTimeframe('quarter')"
                  [class.bg-white]="timeframe() === 'quarter'"
                  [class.dark:bg-slate-700]="timeframe() === 'quarter'"
                  [class.text-slate-900]="timeframe() === 'quarter'"
                  [class.dark:text-white]="timeframe() === 'quarter'"
                  [class.shadow-2xs]="timeframe() === 'quarter'"
                  [class.text-slate-500]="timeframe() !== 'quarter'"
                  class="px-3.5 py-1.5 rounded-lg cursor-pointer transition-all"
                >Quý</button>
              </div>
            </div>
          </div>

          <!-- Gantt Table Container (Scrollable horizontally on smaller viewports) -->
          <div class="overflow-x-auto rounded-xl border border-slate-200/90 dark:border-slate-800">
            <div class="min-w-[980px] flex flex-col">

              <!-- Gantt Header Row -->
              <div class="flex items-stretch bg-slate-50 dark:bg-slate-800/80 border-b border-slate-200 dark:border-slate-800 text-xs font-bold text-slate-600 dark:text-slate-300">
                <!-- Left Column Header -->
                <div class="w-[380px] shrink-0 px-4 py-3 border-r border-slate-200 dark:border-slate-800 flex items-center justify-between">
                  <span class="uppercase tracking-wider text-[11px] text-slate-500">
                    {{ viewMode() === 'sprint' ? 'Hạng mục Sprint & Công việc' : 'Hạng mục Epic & Công việc' }}
                  </span>
                  <span class="text-[11px] font-semibold text-slate-400">Tiến độ & Điểm</span>
                </div>

                <!-- Right Timeline Header Columns -->
                <div class="flex-1 grid relative" [style.gridTemplateColumns]="'repeat(' + timelineColumns().length + ', minmax(0, 1fr))'">
                  @for (col of timelineColumns(); track col.key) {
                    <div
                      class="px-1.5 py-2.5 text-center border-r last:border-r-0 border-slate-200/70 dark:border-slate-800/70 flex flex-col items-center justify-center gap-0.5"
                      [class.bg-primary/5]="col.isCurrent"
                      [class.bg-slate-100/60]="col.isWeekend && !col.isCurrent"
                      [class.dark:bg-slate-800/40]="col.isWeekend && !col.isCurrent"
                    >
                      <span
                        class="text-[11px] font-bold leading-tight"
                        [class.text-primary]="col.isCurrent"
                        [class.text-slate-700]="!col.isCurrent"
                        [class.dark:text-slate-200]="!col.isCurrent"
                      >{{ col.label }}</span>
                      <span class="text-[10px] font-medium text-slate-400 leading-tight">{{ col.subLabel }}</span>
                    </div>
                  }
                </div>
              </div>

              <!-- ================= SPRINT VIEW MODE ================= -->
              @if (viewMode() === 'sprint') {
                @for (sprint of filteredSprints(); track sprint.id) {
                  @let prog = getSprintProgress(sprint);
                  @let bar = getSprintBarLayout(sprint);
                  @let items = getSprintWorkItems(sprint.id);
                  @let isExpanded = isGroupExpanded(sprint.id);

                  <!-- Sprint Parent Row -->
                  <div class="flex items-stretch border-b border-slate-200/80 dark:border-slate-800 bg-white dark:bg-slate-900 hover:bg-slate-50/60 dark:hover:bg-slate-800/40 transition-colors">
                    <!-- Left Meta Column -->
                    <div class="w-[380px] shrink-0 px-4 py-3.5 border-r border-slate-200 dark:border-slate-800 flex flex-col gap-2">
                      <div class="flex items-start justify-between gap-2">
                        <div class="flex items-start gap-2 min-w-0">
                          <button
                            type="button"
                            (click)="toggleGroup(sprint.id)"
                            class="mt-0.5 w-6 h-6 rounded-lg hover:bg-slate-200/70 dark:hover:bg-slate-700 flex items-center justify-center text-slate-500 cursor-pointer shrink-0 transition-colors"
                            [title]="isExpanded ? 'Thu gọn danh sách việc' : 'Mở rộng danh sách việc'"
                          >
                            <span class="material-symbols-outlined text-[18px]" aria-hidden="true">
                              {{ isExpanded ? 'expand_more' : 'chevron_right' }}
                            </span>
                          </button>

                          <div class="flex flex-col min-w-0">
                            <div class="flex items-center gap-2 flex-wrap">
                              <span class="w-2.5 h-2.5 rounded-full shrink-0" [ngClass]="prog.dotClass"></span>
                              <button
                                type="button"
                                (click)="toggleGroup(sprint.id)"
                                class="text-sm font-bold text-slate-900 dark:text-white hover:text-primary text-left truncate cursor-pointer"
                              >
                                {{ sprint.name }}
                              </button>
                              <span class="px-2 py-0.5 rounded-md text-[10px] font-bold uppercase tracking-wide" [ngClass]="prog.badgeClass">
                                {{ prog.statusBadgeLabel }}
                              </span>
                            </div>

                            <div class="flex items-center gap-1.5 text-xs text-slate-500 mt-0.5 flex-wrap">
                              <span class="material-symbols-outlined text-[14px] text-slate-400" aria-hidden="true">date_range</span>
                              <span>{{ sprint.startDate || 'Chưa đặt' }}</span>
                              <span class="material-symbols-outlined text-[13px] text-slate-400" aria-hidden="true">arrow_forward</span>
                              <span>{{ sprint.endDate || 'Chưa đặt' }}</span>
                              <span class="text-slate-400">({{ bar.durationDays }} ngày)</span>
                            </div>
                          </div>
                        </div>

                        <!-- Sprint Actions & Points -->
                        <div class="flex items-center gap-1.5 shrink-0">
                          <span class="px-2 py-0.5 rounded-lg text-xs font-mono font-bold bg-primary/10 text-primary">
                            {{ prog.donePoints }}/{{ prog.totalPoints }} SP
                          </span>
                          @if (canManageSprints()) {
                            <button
                              type="button"
                              (click)="openEditSprintModal(sprint, $event)"
                              class="p-1.5 rounded-lg text-slate-400 hover:text-primary hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
                              title="Chỉnh sửa lịch & mục tiêu Sprint"
                            >
                              <span class="material-symbols-outlined text-[16px] block" aria-hidden="true">edit_calendar</span>
                            </button>
                          }
                        </div>
                      </div>

                      <!-- Unclipped Progress Bar & Explicit Text Summary -->
                      <div class="pl-8 flex flex-col gap-1">
                        <div class="flex items-center justify-between text-xs">
                          <span class="font-semibold text-slate-700 dark:text-slate-200">
                            {{ prog.percent }}% Hoàn thành ({{ prog.doneCount }}/{{ prog.totalCount }} việc)
                          </span>
                          @if (prog.inProgressCount > 0) {
                            <span class="text-[11px] font-medium text-blue-600 dark:text-blue-400">
                              {{ prog.inProgressCount }} đang làm
                            </span>
                          }
                        </div>
                        <div class="w-full h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
                          <div
                            class="h-full rounded-full transition-all duration-300"
                            [ngClass]="prog.barFillClass"
                            [style.width.%]="prog.percent"
                          ></div>
                        </div>
                      </div>
                    </div>

                    <!-- Right Timeline Track for Sprint -->
                    <div class="flex-1 relative flex items-center py-3">
                      <!-- Background Column Grid -->
                      <div class="absolute inset-0 grid pointer-events-none" [style.gridTemplateColumns]="'repeat(' + timelineColumns().length + ', minmax(0, 1fr))'">
                        @for (col of timelineColumns(); track col.key) {
                          <div
                            class="border-r last:border-r-0 border-slate-100 dark:border-slate-800/60 h-full"
                            [class.bg-primary/5]="col.isCurrent"
                            [class.bg-slate-50/70]="col.isWeekend && !col.isCurrent"
                          ></div>
                        }
                      </div>

                      <!-- Today Vertical Line -->
                      @if (todayMarkerPercent() !== null) {
                        <div
                          class="absolute top-0 bottom-0 w-0.5 bg-rose-500/80 z-10 pointer-events-none"
                          [style.left.%]="todayMarkerPercent()"
                        ></div>
                      }

                      <!-- Positioned Gantt Bar -->
                      <div
                        (click)="toggleGroup(sprint.id)"
                        class="relative z-20 h-9 rounded-xl border shadow-2xs flex items-center overflow-hidden cursor-pointer transition-all duration-300 hover:shadow-md group"
                        [ngClass]="prog.barTrackClass"
                        [style.left.%]="bar.leftPercent"
                        [style.width.%]="bar.widthPercent"
                        [title]="sprint.name + ': ' + prog.label + ' (' + bar.startLabel + ' -> ' + bar.endLabel + ')'"
                      >
                        <!-- Actual Completion Fill inside the Gantt Bar -->
                        <div
                          class="absolute left-0 top-0 bottom-0 transition-all duration-300 opacity-90"
                          [ngClass]="prog.barFillClass"
                          [style.width.%]="prog.percent"
                        ></div>

                        <!-- Unclipped Bar Label Overlay -->
                        <div class="relative z-10 px-3 flex items-center justify-between w-full gap-2 text-xs font-bold whitespace-nowrap overflow-hidden">
                          <span
                            class="truncate"
                            [class.text-white]="prog.percent >= 45 || sprint.status === 'completed'"
                            [class.text-slate-900]="prog.percent < 45 && sprint.status !== 'completed'"
                            [class.dark:text-white]="true"
                          >
                            {{ sprint.name }} · {{ prog.shortLabel }}
                          </span>
                          <span
                            class="text-[11px] font-semibold opacity-90 hidden sm:inline"
                            [class.text-white]="prog.percent >= 75 || sprint.status === 'completed'"
                            [class.text-slate-700]="prog.percent < 75 && sprint.status !== 'completed'"
                            [class.dark:text-slate-200]="true"
                          >
                            {{ bar.startLabel }} – {{ bar.endLabel }}
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!-- Expanded Child Work Items inside Sprint -->
                  @if (isExpanded) {
                    @for (item of items; track item.id) {
                      @let itemBar = getWorkItemBarLayout(item, sprint);
                      <div
                        (click)="openTaskDetail(item)"
                        class="flex items-stretch border-b border-slate-100 dark:border-slate-800/70 bg-slate-50/40 dark:bg-slate-900/60 hover:bg-blue-50/40 dark:hover:bg-slate-800/60 transition-colors cursor-pointer group"
                      >
                        <!-- Left Work Item Details -->
                        <div class="w-[380px] shrink-0 pl-11 pr-4 py-2.5 border-r border-slate-200 dark:border-slate-800 flex items-center justify-between gap-2">
                          <div class="flex items-center gap-2 min-w-0">
                            <span
                              class="material-symbols-outlined text-[16px] shrink-0"
                              [style.color]="getIssueTypeColor(item)"
                              aria-hidden="true"
                            >{{ getIssueTypeIcon(item) }}</span>
                            <span class="text-xs font-mono font-bold text-primary shrink-0">{{ item.issueKey }}</span>
                            <span
                              class="text-xs font-medium text-slate-800 dark:text-slate-200 truncate group-hover:text-primary transition-colors"
                              [class.line-through]="isWorkItemDone(item)"
                              [class.opacity-60]="isWorkItemDone(item)"
                            >{{ item.title }}</span>
                          </div>

                          <div class="flex items-center gap-1.5 shrink-0">
                            <span
                              class="px-2 py-0.5 rounded text-[10px] font-bold whitespace-nowrap"
                              [style.backgroundColor]="getStatusColor(item) + '20'"
                              [style.color]="getStatusColor(item)"
                            >
                              {{ item.statusName }}
                            </span>
                            <span class="px-1.5 py-0.5 rounded bg-slate-200/70 dark:bg-slate-800 text-[10px] font-mono font-bold text-slate-600 dark:text-slate-300">
                              {{ item.storyPoints || 0 }} SP
                            </span>
                          </div>
                        </div>

                        <!-- Right Work Item Bar on Timeline -->
                        <div class="flex-1 relative flex items-center py-2">
                          <div class="absolute inset-0 grid pointer-events-none" [style.gridTemplateColumns]="'repeat(' + timelineColumns().length + ', minmax(0, 1fr))'">
                            @for (col of timelineColumns(); track col.key) {
                              <div class="border-r last:border-r-0 border-slate-100 dark:border-slate-800/50 h-full"></div>
                            }
                          </div>

                          @if (todayMarkerPercent() !== null) {
                            <div
                              class="absolute top-0 bottom-0 w-0.5 bg-rose-500/50 z-10 pointer-events-none"
                              [style.left.%]="todayMarkerPercent()"
                            ></div>
                          }

                          <div
                            class="relative z-20 h-6 rounded-lg px-2.5 flex items-center justify-between gap-2 text-[11px] font-semibold text-white shadow-2xs transition-all group-hover:brightness-105 overflow-hidden whitespace-nowrap"
                            [style.left.%]="itemBar.leftPercent"
                            [style.width.%]="itemBar.widthPercent"
                            [style.backgroundColor]="getStatusColor(item)"
                          >
                            <span class="truncate">{{ item.issueKey }} · {{ item.title }}</span>
                            @if (item.assigneeName) {
                              <span class="text-[10px] opacity-90 hidden md:inline">{{ item.assigneeName }}</span>
                            }
                          </div>
                        </div>
                      </div>
                    } @empty {
                      <div class="flex items-center justify-between px-11 py-2.5 border-b border-slate-100 dark:border-slate-800 bg-slate-50/30 dark:bg-slate-900/40 text-xs text-slate-400">
                        <span>Sprint này chưa có công việc nào được gán.</span>
                        @if (canManageSprints()) {
                          <button
                            type="button"
                            (click)="quickAddWorkItemToSprint(sprint)"
                            class="inline-flex items-center gap-1 text-primary font-semibold hover:underline cursor-pointer"
                          >
                            <span class="material-symbols-outlined text-[14px]" aria-hidden="true">add</span>
                            Tạo việc nhanh vào {{ sprint.name }}
                          </button>
                        }
                      </div>
                    }
                  }
                } @empty {
                  <div class="p-10 text-center text-slate-400">
                    <span class="material-symbols-outlined text-[40px] block mb-2 opacity-60" aria-hidden="true">route</span>
                    <p class="text-sm font-bold text-slate-700 dark:text-slate-300">Không tìm thấy Sprint phù hợp</p>
                    <p class="text-xs text-slate-400 mt-1">Thử đổi bộ lọc trạng thái hoặc tạo Sprint mới cho dự án.</p>
                  </div>
                }
              }

              <!-- ================= EPIC VIEW MODE ================= -->
              @if (viewMode() === 'epic') {
                @for (epic of filteredEpics(); track epic.id) {
                  @let prog = getEpicProgress(epic);
                  @let bar = getEpicBarLayout(epic);
                  @let items = getEpicWorkItems(epic);
                  @let isExpanded = isGroupExpanded(epic.id);

                  <div class="flex items-stretch border-b border-slate-200/80 dark:border-slate-800 bg-white dark:bg-slate-900 hover:bg-slate-50/60 dark:hover:bg-slate-800/40 transition-colors">
                    <!-- Left Epic Column -->
                    <div class="w-[380px] shrink-0 px-4 py-3.5 border-r border-slate-200 dark:border-slate-800 flex flex-col gap-2">
                      <div class="flex items-start justify-between gap-2">
                        <div class="flex items-start gap-2 min-w-0">
                          <button
                            type="button"
                            (click)="toggleGroup(epic.id)"
                            class="mt-0.5 w-6 h-6 rounded-lg hover:bg-slate-200/70 dark:hover:bg-slate-700 flex items-center justify-center text-slate-500 cursor-pointer shrink-0"
                          >
                            <span class="material-symbols-outlined text-[18px]" aria-hidden="true">
                              {{ isExpanded ? 'expand_more' : 'chevron_right' }}
                            </span>
                          </button>
                          <div class="flex flex-col min-w-0">
                            <div class="flex items-center gap-2 flex-wrap">
                              <span class="material-symbols-outlined text-[16px] text-primary" aria-hidden="true">bolt</span>
                              <span class="text-xs font-mono font-bold text-primary">{{ epic.key }}</span>
                              <button
                                type="button"
                                (click)="toggleGroup(epic.id)"
                                class="text-sm font-bold text-slate-900 dark:text-white hover:text-primary text-left truncate cursor-pointer"
                              >{{ epic.name }}</button>
                            </div>
                            <span class="text-xs text-slate-500 mt-0.5">
                              {{ bar.startLabel }} – {{ bar.endLabel }} · {{ prog.totalCount }} công việc
                            </span>
                          </div>
                        </div>
                        <span class="px-2 py-0.5 rounded-lg text-xs font-mono font-bold bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-200 dark:border-slate-700 shrink-0">
                          {{ prog.donePoints }}/{{ prog.totalPoints }} SP
                        </span>
                      </div>

                      <div class="pl-8 flex flex-col gap-1">
                        <div class="flex items-center justify-between text-xs">
                          <span class="font-semibold text-slate-700 dark:text-slate-200">
                            {{ prog.percent }}% Hoàn thành ({{ prog.doneCount }}/{{ prog.totalCount }} việc)
                          </span>
                        </div>
                        <div class="w-full h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
                          <div class="h-full bg-primary rounded-full transition-all duration-300" [style.width.%]="prog.percent"></div>
                        </div>
                      </div>
                    </div>

                    <!-- Right Epic Timeline Bar -->
                    <div class="flex-1 relative flex items-center py-3">
                      <div class="absolute inset-0 grid pointer-events-none" [style.gridTemplateColumns]="'repeat(' + timelineColumns().length + ', minmax(0, 1fr))'">
                        @for (col of timelineColumns(); track col.key) {
                          <div class="border-r last:border-r-0 border-slate-100 dark:border-slate-800/60 h-full"></div>
                        }
                      </div>

                      <div
                        (click)="toggleGroup(epic.id)"
                        class="relative z-20 h-9 rounded-xl border border-slate-300 dark:border-slate-700 bg-slate-100/90 dark:bg-slate-800/80 shadow-2xs flex items-center overflow-hidden cursor-pointer"
                        [style.left.%]="bar.leftPercent"
                        [style.width.%]="bar.widthPercent"
                      >
                        <div class="absolute left-0 top-0 bottom-0 bg-primary transition-all duration-300 opacity-90" [style.width.%]="prog.percent"></div>
                        <div class="relative z-10 px-3 flex items-center justify-between w-full gap-2 text-xs font-bold whitespace-nowrap overflow-hidden">
                          <span
                            class="truncate"
                            [class.text-white]="prog.percent >= 45"
                            [class.text-slate-900]="prog.percent < 45"
                            [class.dark:text-white]="true"
                          >
                            {{ epic.name }} · {{ prog.shortLabel }}
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>

                  @if (isExpanded) {
                    @for (item of items; track item.id) {
                      @let itemBar = getWorkItemBarLayout(item);
                      <div
                        (click)="openTaskDetail(item)"
                        class="flex items-stretch border-b border-slate-100 dark:border-slate-800/70 bg-slate-50/40 dark:bg-slate-900/60 hover:bg-slate-100/60 dark:hover:bg-slate-800/60 transition-colors cursor-pointer"
                      >
                        <div class="w-[380px] shrink-0 pl-11 pr-4 py-2.5 border-r border-slate-200 dark:border-slate-800 flex items-center justify-between gap-2">
                          <div class="flex items-center gap-2 min-w-0">
                            <span class="material-symbols-outlined text-[16px] shrink-0" [style.color]="getIssueTypeColor(item)" aria-hidden="true">{{ getIssueTypeIcon(item) }}</span>
                            <span class="text-xs font-mono font-bold text-primary shrink-0">{{ item.issueKey }}</span>
                            <span class="text-xs font-medium text-slate-800 dark:text-slate-200 truncate">{{ item.title }}</span>
                          </div>
                          <span
                            class="px-2 py-0.5 rounded text-[10px] font-bold whitespace-nowrap shrink-0"
                            [style.backgroundColor]="getStatusColor(item) + '20'"
                            [style.color]="getStatusColor(item)"
                          >{{ item.statusName }}</span>
                        </div>
                        <div class="flex-1 relative flex items-center py-2">
                          <div
                            class="relative z-20 h-6 rounded-lg px-2.5 flex items-center text-[11px] font-semibold text-white shadow-2xs overflow-hidden whitespace-nowrap"
                            [style.left.%]="itemBar.leftPercent"
                            [style.width.%]="itemBar.widthPercent"
                            [style.backgroundColor]="getStatusColor(item)"
                          >
                            <span class="truncate">{{ item.issueKey }} · {{ item.title }}</span>
                          </div>
                        </div>
                      </div>
                    }
                  }
                } @empty {
                  <div class="p-10 text-center text-slate-400">
                    <span class="material-symbols-outlined text-[40px] block mb-2 opacity-60" aria-hidden="true">bolt</span>
                    <p class="text-sm font-bold text-slate-700 dark:text-slate-300">Chưa có Epic nào trong dự án</p>
                  </div>
                }
              }

            </div>
          </div>

          <!-- Legend Footer -->
          <div class="flex flex-wrap items-center justify-between gap-3 pt-1 text-xs text-slate-500">
            <div class="flex flex-wrap items-center gap-4">
              <span class="inline-flex items-center gap-1.5">
                <span class="w-2.5 h-2.5 rounded-full bg-blue-500"></span>
                Sprint đang chạy
              </span>
              <span class="inline-flex items-center gap-1.5">
                <span class="w-2.5 h-2.5 rounded-full bg-amber-500"></span>
                Sprint kế hoạch
              </span>
              <span class="inline-flex items-center gap-1.5">
                <span class="w-2.5 h-2.5 rounded-full bg-emerald-500"></span>
                Đã hoàn thành
              </span>
              <span class="inline-flex items-center gap-1.5">
                <span class="w-3 h-0.5 bg-rose-500"></span>
                Mốc hôm nay
              </span>
            </div>
            <span class="text-[11px] text-slate-400">
              Nhấn vào từng Sprint để mở rộng danh sách công việc · Nhấn vào công việc để mở chi tiết
            </span>
          </div>

        </div>

        <!-- Edit Sprint Schedule Modal -->
        @if (editingSprint(); as sp) {
          <div class="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-xs flex items-center justify-center p-4" (click)="closeEditSprintModal()">
            <div class="w-full max-w-md rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xl p-6 flex flex-col gap-4" (click)="$event.stopPropagation()">
              <div class="flex items-center justify-between border-b border-slate-200 dark:border-slate-800 pb-3">
                <h3 class="text-base font-bold text-slate-900 dark:text-white flex items-center gap-2">
                  <span class="material-symbols-outlined text-[20px] text-primary" aria-hidden="true">edit_calendar</span>
                  Cập nhật lịch trình Sprint
                </h3>
                <button type="button" (click)="closeEditSprintModal()" class="text-slate-400 hover:text-slate-600 cursor-pointer">
                  <span class="material-symbols-outlined text-[20px]" aria-hidden="true">close</span>
                </button>
              </div>

              <div class="flex flex-col gap-3">
                <label class="flex flex-col gap-1 text-xs font-semibold text-slate-700 dark:text-slate-300">
                  Tên Sprint
                  <input
                    type="text"
                    [(ngModel)]="editSprintName"
                    class="px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm text-slate-900 dark:text-white focus:outline-none focus:border-primary"
                  />
                </label>

                <div class="grid grid-cols-2 gap-3">
                  <label class="flex flex-col gap-1 text-xs font-semibold text-slate-700 dark:text-slate-300">
                    Ngày bắt đầu
                    <input
                      type="date"
                      [(ngModel)]="editSprintStartDate"
                      class="px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm text-slate-900 dark:text-white focus:outline-none focus:border-primary"
                    />
                  </label>
                  <label class="flex flex-col gap-1 text-xs font-semibold text-slate-700 dark:text-slate-300">
                    Ngày kết thúc
                    <input
                      type="date"
                      [(ngModel)]="editSprintEndDate"
                      class="px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm text-slate-900 dark:text-white focus:outline-none focus:border-primary"
                    />
                  </label>
                </div>

                <label class="flex flex-col gap-1 text-xs font-semibold text-slate-700 dark:text-slate-300">
                  Trạng thái chu kỳ
                  <select
                    [(ngModel)]="editSprintStatus"
                    class="px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm text-slate-900 dark:text-white focus:outline-none focus:border-primary"
                  >
                    <option value="future">Kế hoạch (Planned)</option>
                    <option value="active">Đang chạy (Active)</option>
                    <option value="completed">Hoàn thành (Completed)</option>
                  </select>
                </label>

                <label class="flex flex-col gap-1 text-xs font-semibold text-slate-700 dark:text-slate-300">
                  Mục tiêu Sprint (Sprint Goal)
                  <textarea
                    rows="2"
                    [(ngModel)]="editSprintGoal"
                    placeholder="Nhập mục tiêu cam kết của Sprint..."
                    class="px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-sm text-slate-900 dark:text-white focus:outline-none focus:border-primary"
                  ></textarea>
                </label>
              </div>

              <div class="flex items-center justify-end gap-2 pt-2 border-t border-slate-200 dark:border-slate-800">
                <button
                  type="button"
                  (click)="closeEditSprintModal()"
                  class="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
                >Hủy</button>
                <button
                  type="button"
                  (click)="saveSprintSchedule()"
                  class="px-4 py-2 rounded-xl text-xs font-bold bg-primary text-white hover:opacity-90 cursor-pointer"
                >Lưu lịch trình</button>
              </div>
            </div>
          </div>
        }

      </div>
    }
  `
})
export class RoadmapPageComponent {
  readonly projectService = inject(ProjectManagementService);
  private readonly identityService = inject(IdentityService);
  private readonly toastService = inject(ToastService);

  readonly sprints = this.projectService.sprints;
  readonly workItems = this.projectService.workItems;
  readonly epics = this.projectService.epics;

  readonly timeframe = signal<'week' | 'month' | 'quarter'>('month');
  readonly viewMode = signal<'sprint' | 'epic'>('sprint');
  readonly statusFilter = signal<'all' | 'active' | 'future' | 'completed'>('all');
  readonly searchQuery = signal<string>('');
  readonly offsetSteps = signal<number>(0);

  // Expanded groups map (default: expand all Sprints so tasks are immediately visible)
  readonly expandedGroups = signal<Record<string, boolean>>({});

  // Edit Sprint Modal State
  readonly editingSprint = signal<Sprint | null>(null);
  editSprintName = '';
  editSprintStartDate = '';
  editSprintEndDate = '';
  editSprintGoal = '';
  editSprintStatus: Sprint['status'] = 'future';

  private readonly DAY_MS = 86_400_000;

  constructor() {
    effect(() => {
      const sprintList = this.sprints();
      const epicList = this.epics();
      untracked(() => {
        const currentMap = this.expandedGroups();
        const nextMap = { ...currentMap };
        let changed = false;
        for (const s of sprintList) {
          if (nextMap[s.id] === undefined) {
            nextMap[s.id] = true;
            changed = true;
          }
        }
        for (const e of epicList) {
          if (nextMap[e.id] === undefined) {
            nextMap[e.id] = true;
            changed = true;
          }
        }
        if (changed) {
          this.expandedGroups.set(nextMap);
        }
      });
    });
  }

  readonly canManageSprints = computed(() => {
    if (this.identityService.isSystemAdmin()) return true;
    const myId = this.identityService.authState().currentUser?.id;
    if (!myId) return false;
    const proj = this.projectService.currentProject();
    if (proj.leadUserId && proj.leadUserId.toLowerCase() === myId.toLowerCase()) return true;
    const myMember = (proj.members || []).find(m => m.userId && m.userId.toLowerCase() === myId.toLowerCase());
    return !!(myMember && (myMember.role === 'Project Lead' || myMember.role === 'Scrum Master'));
  });

  readonly kpiSummary = computed(() => {
    const sprintList = this.sprints();
    const items = this.workItems();
    const doneTasks = items.filter(w => this.isWorkItemDone(w)).length;
    const inProgressTasks = items.filter(w => !this.isWorkItemDone(w) && (w.statusName || '').toLowerCase() !== 'to do').length;
    const totalTasks = items.length;
    const taskCompletionPct = totalTasks > 0 ? Math.round((doneTasks / totalTasks) * 100) : 0;

    const totalPoints = items.reduce((sum, w) => sum + (Number(w.storyPoints) || 0), 0);
    const donePoints = items
      .filter(w => this.isWorkItemDone(w))
      .reduce((sum, w) => sum + (Number(w.storyPoints) || 0), 0);

    const starts = sprintList.map(s => this.parseDateMs(s.startDate)).filter((v): v is number => v !== null);
    const ends = sprintList.map(s => this.parseDateMs(s.endDate)).filter((v): v is number => v !== null);
    const dateRangeLabel =
      starts.length && ends.length
        ? `${this.formatShortDate(Math.min(...starts))} – ${this.formatShortDate(Math.max(...ends))}`
        : 'Theo chu kỳ Sprint';

    return {
      totalSprints: sprintList.length,
      activeSprints: sprintList.filter(s => s.status === 'active').length,
      plannedSprints: sprintList.filter(s => s.status === 'future').length,
      completedSprints: sprintList.filter(s => s.status === 'completed').length,
      doneTasks,
      inProgressTasks,
      totalTasks,
      taskCompletionPct,
      donePoints,
      totalPoints,
      dateRangeLabel
    };
  });

  /**
   * Computes the base anchor date for the timeline:
   * Uses the active sprint's startDate, or earliest sprint's startDate, or today.
   */
  private readonly anchorDateMs = computed(() => {
    const sprintList = this.sprints();
    const active = sprintList.find(s => s.status === 'active' && s.startDate);
    if (active?.startDate) {
      const parsed = this.parseDateMs(active.startDate);
      if (parsed !== null) return parsed;
    }
    const withDates = sprintList
      .map(s => this.parseDateMs(s.startDate))
      .filter((v): v is number => v !== null)
      .sort((a, b) => a - b);
    if (withDates.length > 0) {
      return withDates[0];
    }
    return this.startOfDayMs(Date.now());
  });

  readonly timelineWindow = computed(() => {
    const mode = this.timeframe();
    const offset = this.offsetSteps();
    const anchor = this.anchorDateMs();

    if (mode === 'week') {
      // 14 daily columns (2 weeks), anchored 2 days before the sprint start
      const startMs = this.startOfDayMs(anchor - 2 * this.DAY_MS + offset * 7 * this.DAY_MS);
      const endMs = startMs + 14 * this.DAY_MS;
      return { startMs, endMs };
    }

    if (mode === 'month') {
      // 8 weekly columns (~56 days), anchored 7 days before the sprint start
      const startMs = this.startOfDayMs(anchor - 7 * this.DAY_MS + offset * 28 * this.DAY_MS);
      const endMs = startMs + 56 * this.DAY_MS;
      return { startMs, endMs };
    }

    // 'quarter': 6 monthly columns around the anchor month
    const baseDate = new Date(anchor);
    const startMonth = new Date(baseDate.getFullYear(), baseDate.getMonth() - 1 + offset * 3, 1);
    const endMonth = new Date(startMonth.getFullYear(), startMonth.getMonth() + 6, 1);
    return { startMs: startMonth.getTime(), endMs: endMonth.getTime() };
  });

  readonly timelineColumns = computed<TimelineColumn[]>(() => {
    const mode = this.timeframe();
    const { startMs } = this.timelineWindow();
    const nowMs = Date.now();
    const cols: TimelineColumn[] = [];
    const dayNames = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];

    if (mode === 'week') {
      for (let i = 0; i < 14; i++) {
        const colStart = startMs + i * this.DAY_MS;
        const colEnd = colStart + this.DAY_MS;
        const d = new Date(colStart);
        const dayOfWeek = d.getDay();
        cols.push({
          key: `d-${i}-${colStart}`,
          label: `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`,
          subLabel: dayNames[dayOfWeek],
          startMs: colStart,
          endMs: colEnd,
          isCurrent: nowMs >= colStart && nowMs < colEnd,
          isWeekend: dayOfWeek === 0 || dayOfWeek === 6
        });
      }
      return cols;
    }

    if (mode === 'month') {
      for (let i = 0; i < 8; i++) {
        const colStart = startMs + i * 7 * this.DAY_MS;
        const colEnd = colStart + 7 * this.DAY_MS;
        const dStart = new Date(colStart);
        const dEnd = new Date(colEnd - this.DAY_MS);
        cols.push({
          key: `w-${i}-${colStart}`,
          label: `${String(dStart.getDate()).padStart(2, '0')}/${String(dStart.getMonth() + 1).padStart(2, '0')}`,
          subLabel: `– ${String(dEnd.getDate()).padStart(2, '0')}/${String(dEnd.getMonth() + 1).padStart(2, '0')}`,
          startMs: colStart,
          endMs: colEnd,
          isCurrent: nowMs >= colStart && nowMs < colEnd
        });
      }
      return cols;
    }

    // Quarter mode: 6 months
    const firstDate = new Date(startMs);
    for (let i = 0; i < 6; i++) {
      const mStart = new Date(firstDate.getFullYear(), firstDate.getMonth() + i, 1);
      const mEnd = new Date(firstDate.getFullYear(), firstDate.getMonth() + i + 1, 1);
      const quarterNum = Math.floor(mStart.getMonth() / 3) + 1;
      cols.push({
        key: `m-${i}-${mStart.getTime()}`,
        label: `Tháng ${mStart.getMonth() + 1}`,
        subLabel: `Q${quarterNum}/${mStart.getFullYear()}`,
        startMs: mStart.getTime(),
        endMs: mEnd.getTime(),
        isCurrent: nowMs >= mStart.getTime() && nowMs < mEnd.getTime()
      });
    }
    return cols;
  });

  readonly timelineWindowLabel = computed(() => {
    const { startMs, endMs } = this.timelineWindow();
    return `${this.formatShortDate(startMs)} – ${this.formatShortDate(endMs - this.DAY_MS)}`;
  });

  readonly todayMarkerPercent = computed<number | null>(() => {
    const { startMs, endMs } = this.timelineWindow();
    const now = Date.now();
    if (now < startMs || now > endMs) return null;
    return Math.max(0, Math.min(100, ((now - startMs) / (endMs - startMs)) * 100));
  });

  readonly filteredSprints = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    const status = this.statusFilter();
    const allItems = this.workItems();

    return this.sprints().filter(s => {
      if (status !== 'all' && s.status !== status) return false;
      if (!q) return true;
      const matchSprint =
        s.name.toLowerCase().includes(q) ||
        (s.goal || '').toLowerCase().includes(q);
      if (matchSprint) return true;
      return allItems.some(
        w =>
          w.sprintId === s.id &&
          (w.title.toLowerCase().includes(q) || w.issueKey.toLowerCase().includes(q))
      );
    });
  });

  readonly filteredEpics = computed(() => {
    const q = this.searchQuery().trim().toLowerCase();
    if (!q) return this.epics();
    const allItems = this.workItems();
    return this.epics().filter(e => {
      if (e.name.toLowerCase().includes(q) || e.key.toLowerCase().includes(q)) return true;
      return allItems.some(
        w =>
          (w.epicId === e.id || w.epicName === e.name) &&
          (w.title.toLowerCase().includes(q) || w.issueKey.toLowerCase().includes(q))
      );
    });
  });

  setTimeframe(mode: 'week' | 'month' | 'quarter'): void {
    this.timeframe.set(mode);
    this.offsetSteps.set(0);
  }

  shiftTimeline(delta: number): void {
    this.offsetSteps.update(v => v + delta);
  }

  jumpToAnchor(): void {
    this.offsetSteps.set(0);
  }

  isGroupExpanded(id: string): boolean {
    return this.expandedGroups()[id] ?? true;
  }

  toggleGroup(id: string): void {
    this.expandedGroups.update(map => ({ ...map, [id]: !(map[id] ?? true) }));
  }

  areAllExpanded(): boolean {
    const ids = this.viewMode() === 'sprint'
      ? this.filteredSprints().map(s => s.id)
      : this.filteredEpics().map(e => e.id);
    if (ids.length === 0) return false;
    return ids.every(id => this.isGroupExpanded(id));
  }

  toggleExpandAll(): void {
    const target = !this.areAllExpanded();
    const next: Record<string, boolean> = { ...this.expandedGroups() };
    for (const s of this.sprints()) next[s.id] = target;
    for (const e of this.epics()) next[e.id] = target;
    this.expandedGroups.set(next);
  }

  isWorkItemDone(item: WorkItem): boolean {
    if (item.isDone) return true;
    const statusLower = (item.statusName || '').trim().toLowerCase();
    if (statusLower === 'done' || statusLower === 'hoàn thành' || statusLower === 'closed') return true;
    const matched = this.projectService.projectStatuses().find(
      s => s.id === item.statusId || s.name.toLowerCase() === statusLower
    );
    return !!(matched?.isDone || matched?.category?.toLowerCase() === 'done');
  }

  getSprintWorkItems(sprintId: string): WorkItem[] {
    const q = this.searchQuery().trim().toLowerCase();
    return this.workItems().filter(w => {
      if (w.sprintId !== sprintId) return false;
      if (!q) return true;
      return w.title.toLowerCase().includes(q) || w.issueKey.toLowerCase().includes(q);
    });
  }

  getEpicWorkItems(epic: EpicItem): WorkItem[] {
    const q = this.searchQuery().trim().toLowerCase();
    return this.workItems().filter(w => {
      const belongs = w.epicId === epic.id || (w.epicName || '').toLowerCase() === epic.name.toLowerCase();
      if (!belongs) return false;
      if (!q) return true;
      return w.title.toLowerCase().includes(q) || w.issueKey.toLowerCase().includes(q);
    });
  }

  getSprintProgress(sprint: Sprint): RoadmapGroupProgress {
    const sprintItems = this.workItems().filter(w => w.sprintId === sprint.id);
    const totalCount = sprintItems.length;
    const doneCount = sprintItems.filter(w => this.isWorkItemDone(w)).length;
    const inProgressCount = sprintItems.filter(
      w => !this.isWorkItemDone(w) && (w.statusName || '').toLowerCase() !== 'to do'
    ).length;
    const todoCount = Math.max(0, totalCount - doneCount - inProgressCount);

    const totalPoints =
      sprintItems.reduce((sum, w) => sum + (Number(w.storyPoints) || 0), 0) ||
      sprint.totalStoryPoints ||
      0;
    const donePoints = sprintItems
      .filter(w => this.isWorkItemDone(w))
      .reduce((sum, w) => sum + (Number(w.storyPoints) || 0), 0);

    const percent =
      sprint.status === 'completed'
        ? 100
        : totalCount > 0
          ? Math.round((doneCount / totalCount) * 100)
          : 0;

    const statusBadgeLabel =
      sprint.status === 'completed'
        ? 'Hoàn thành'
        : sprint.status === 'active'
          ? 'Đang chạy'
          : 'Kế hoạch';

    const dotClass =
      sprint.status === 'completed'
        ? 'bg-emerald-500'
        : sprint.status === 'active'
          ? 'bg-blue-500'
          : 'bg-amber-500';

    const badgeClass =
      sprint.status === 'completed'
        ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300'
        : sprint.status === 'active'
          ? 'bg-blue-500/15 text-blue-700 dark:text-blue-300'
          : 'bg-amber-500/15 text-amber-700 dark:text-amber-300';

    const barTrackClass =
      sprint.status === 'completed'
        ? 'bg-emerald-100 dark:bg-emerald-950/60 border-emerald-300 dark:border-emerald-700'
        : sprint.status === 'active'
          ? 'bg-blue-100/90 dark:bg-blue-950/60 border-blue-300 dark:border-blue-700'
          : 'bg-slate-200/80 dark:bg-slate-800 border-slate-300 dark:border-slate-700';

    const barFillClass =
      sprint.status === 'completed'
        ? 'bg-emerald-600'
        : sprint.status === 'active'
          ? 'bg-blue-600'
          : 'bg-amber-500';

    return {
      percent,
      doneCount,
      inProgressCount,
      todoCount,
      totalCount,
      donePoints,
      totalPoints,
      label: `${percent}% Hoàn thành (${doneCount}/${totalCount} việc)`,
      shortLabel: `${percent}% (${doneCount}/${totalCount} việc)`,
      statusBadgeLabel,
      barTrackClass,
      barFillClass,
      badgeClass,
      dotClass
    };
  }

  getEpicProgress(epic: EpicItem): RoadmapGroupProgress {
    const items = this.getEpicWorkItems(epic);
    const totalCount = items.length;
    const doneCount = items.filter(w => this.isWorkItemDone(w)).length;
    const inProgressCount = items.filter(
      w => !this.isWorkItemDone(w) && (w.statusName || '').toLowerCase() !== 'to do'
    ).length;
    const todoCount = Math.max(0, totalCount - doneCount - inProgressCount);
    const totalPoints = items.reduce((sum, w) => sum + (Number(w.storyPoints) || 0), 0);
    const donePoints = items.filter(w => this.isWorkItemDone(w)).reduce((sum, w) => sum + (Number(w.storyPoints) || 0), 0);
    const percent = totalCount > 0 ? Math.round((doneCount / totalCount) * 100) : epic.status === 'Done' ? 100 : 0;

    return {
      percent,
      doneCount,
      inProgressCount,
      todoCount,
      totalCount,
      donePoints,
      totalPoints,
      label: `${percent}% Hoàn thành (${doneCount}/${totalCount} việc)`,
      shortLabel: `${percent}% (${doneCount}/${totalCount} việc)`,
      statusBadgeLabel: epic.status,
      barTrackClass: 'bg-slate-100 dark:bg-slate-800/60 border-slate-300 dark:border-slate-700',
      barFillClass: 'bg-primary',
      badgeClass: 'bg-primary/10 text-primary dark:text-emerald-400',
      dotClass: 'bg-primary'
    };
  }

  getSprintBarLayout(sprint: Sprint, index = 0): GanttBarLayout {
    const startMs =
      this.parseDateMs(sprint.startDate) ??
      this.anchorDateMs() + index * 14 * this.DAY_MS;
    const endMs =
      this.parseDateMs(sprint.endDate) ??
      startMs + 14 * this.DAY_MS;
    return this.computeBarLayout(startMs, Math.max(endMs, startMs + this.DAY_MS));
  }

  getEpicBarLayout(epic: EpicItem): GanttBarLayout {
    const items = this.getEpicWorkItems(epic);
    const sprintIds = new Set(items.map(i => i.sprintId).filter(Boolean));
    const matchedSprints = this.sprints().filter(s => sprintIds.has(s.id));
    const starts = matchedSprints.map(s => this.parseDateMs(s.startDate)).filter((v): v is number => v !== null);
    const ends = matchedSprints.map(s => this.parseDateMs(s.endDate)).filter((v): v is number => v !== null);

    const startMs = starts.length > 0 ? Math.min(...starts) : this.anchorDateMs();
    const endMs = ends.length > 0 ? Math.max(...ends) : startMs + 21 * this.DAY_MS;
    return this.computeBarLayout(startMs, Math.max(endMs, startMs + this.DAY_MS));
  }

  getWorkItemBarLayout(item: WorkItem, parentSprint?: Sprint): GanttBarLayout {
    const sprint = parentSprint ?? this.sprints().find(s => s.id === item.sprintId);
    const sprintStart = sprint ? this.parseDateMs(sprint.startDate) : null;
    const sprintEnd = sprint ? this.parseDateMs(sprint.endDate) : null;

    const startMs = sprintStart ?? this.parseDateMs(item.createdAt) ?? this.anchorDateMs();
    const endMs = sprintEnd ?? startMs + 7 * this.DAY_MS;
    return this.computeBarLayout(startMs, Math.max(endMs, startMs + this.DAY_MS));
  }

  private computeBarLayout(itemStartMs: number, itemEndMs: number): GanttBarLayout {
    const { startMs: winStart, endMs: winEnd } = this.timelineWindow();
    const winSpan = Math.max(this.DAY_MS, winEnd - winStart);
    const durationDays = Math.max(1, Math.round((itemEndMs - itemStartMs) / this.DAY_MS));

    const clampedStart = Math.max(winStart, Math.min(winEnd, itemStartMs));
    const clampedEnd = Math.max(winStart, Math.min(winEnd, itemEndMs));

    let leftPercent = ((clampedStart - winStart) / winSpan) * 100;
    let widthPercent = ((clampedEnd - clampedStart) / winSpan) * 100;

    if (clampedEnd <= winStart) {
      leftPercent = 1;
      widthPercent = 18;
    } else if (clampedStart >= winEnd) {
      leftPercent = 81;
      widthPercent = 18;
    } else {
      widthPercent = Math.max(widthPercent, 16);
      if (leftPercent + widthPercent > 99) {
        leftPercent = Math.max(1, 99 - widthPercent);
      }
    }

    return {
      leftPercent,
      widthPercent,
      isOutOfLeft: itemEndMs < winStart,
      isOutOfRight: itemStartMs > winEnd,
      startLabel: this.formatShortDate(itemStartMs),
      endLabel: this.formatShortDate(itemEndMs),
      durationDays
    };
  }

  getIssueTypeIcon(item: WorkItem): string {
    if (item.issueTypeIcon) return item.issueTypeIcon;
    const custom = this.projectService.customIssueTypes().find(
      t => t.id === item.issueTypeId || t.name.toLowerCase() === (item.issueType || '').toLowerCase()
    );
    if (custom?.icon) return custom.icon;
    switch ((item.issueType || '').toLowerCase()) {
      case 'story': return 'bookmark';
      case 'bug': return 'bug_report';
      case 'epic': return 'bolt';
      case 'sub-task':
      case 'subtask': return 'subdirectory_arrow_right';
      default: return 'task_alt';
    }
  }

  getIssueTypeColor(item: WorkItem): string {
    if (item.issueTypeColor) return item.issueTypeColor;
    const custom = this.projectService.customIssueTypes().find(
      t => t.id === item.issueTypeId || t.name.toLowerCase() === (item.issueType || '').toLowerCase()
    );
    if (custom?.color) return custom.color;
    switch ((item.issueType || '').toLowerCase()) {
      case 'story': return '#10B981';
      case 'bug': return '#EF4444';
      case 'epic': return '#8B5CF6';
      default: return '#2563EB';
    }
  }

  getStatusColor(item: WorkItem): string {
    if (item.statusColor) return item.statusColor;
    const matched = this.projectService.projectStatuses().find(
      s => s.id === item.statusId || s.name.toLowerCase() === (item.statusName || '').toLowerCase()
    );
    if (matched?.colorHex) return matched.colorHex;
    if (this.isWorkItemDone(item)) return '#10B981';
    if ((item.statusName || '').toLowerCase() === 'to do') return '#64748B';
    return '#2563EB';
  }

  openTaskDetail(item: WorkItem): void {
    this.projectService.activeDrawerTask.set(item);
  }

  createNewSprint(): void {
    const created = this.projectService.addNewSprint();
    this.expandedGroups.update(m => ({ ...m, [created.id]: true }));
    this.toastService.success('Đã tạo Sprint mới', `Đã thêm ${created.name} vào lộ trình dự án.`);
  }

  quickAddWorkItemToSprint(sprint: Sprint): void {
    const title = `Công việc mới trong ${sprint.name}`;
    const item = this.projectService.addWorkItem({
      title,
      sprintId: sprint.id,
      sprintName: sprint.name,
      issueType: 'Task',
      priority: 'Medium',
      storyPoints: 3
    });
    this.projectService.activeDrawerTask.set(item);
  }

  openEditSprintModal(sprint: Sprint, event: Event): void {
    event.stopPropagation();
    this.editingSprint.set(sprint);
    this.editSprintName = sprint.name;
    this.editSprintStartDate = (sprint.startDate || '').slice(0, 10);
    this.editSprintEndDate = (sprint.endDate || '').slice(0, 10);
    this.editSprintGoal = sprint.goal || '';
    this.editSprintStatus = sprint.status;
  }

  closeEditSprintModal(): void {
    this.editingSprint.set(null);
  }

  saveSprintSchedule(): void {
    const sp = this.editingSprint();
    if (!sp) return;
    const trimmedName = this.editSprintName.trim() || sp.name;
    this.projectService.updateSprintInfo(
      sp.id,
      trimmedName,
      this.editSprintStartDate || undefined,
      this.editSprintEndDate || undefined,
      this.editSprintGoal
    );
    if (this.editSprintStatus !== sp.status) {
      this.projectService.updateSprintStatus(sp.id, this.editSprintStatus);
    }
    this.toastService.success('Đã cập nhật lộ trình Sprint', `Đã lưu lịch trình cho ${trimmedName}.`);
    this.closeEditSprintModal();
  }

  private parseDateMs(dateStr?: string | null): number | null {
    if (!dateStr) return null;
    const ms = Date.parse(dateStr);
    return Number.isNaN(ms) ? null : this.startOfDayMs(ms);
  }

  private startOfDayMs(ms: number): number {
    const d = new Date(ms);
    d.setHours(0, 0, 0, 0);
    return d.getTime();
  }

  private formatShortDate(ms: number): string {
    const d = new Date(ms);
    return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
  }
}
