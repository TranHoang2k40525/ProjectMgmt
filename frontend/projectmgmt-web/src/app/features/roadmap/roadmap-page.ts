import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProjectManagementService, Sprint } from '../../core/services/project-management.service';
import { NoProjectStateComponent } from '../../shared/components/no-project-state/no-project-state.component';

@Component({
  selector: 'app-roadmap-page',
  standalone: true,
  imports: [CommonModule, NoProjectStateComponent],
  template: `
    @if (!projectService.hasActiveProject()) {
      <app-no-project-state featureName="Lộ Trình Dự Án (Roadmap)"></app-no-project-state>
    } @else {
    <div class="roadmap-page flex flex-col gap-6 animate-fade-in font-body text-slate-900 dark:text-slate-100">
      
      <!-- Top Action Bar -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-200 dark:border-slate-800">
        <div class="flex flex-col">
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <span class="material-symbols-outlined text-[24px] text-primary">timeline</span>
            Lộ Trình Dự Án (Project Roadmap)
          </h1>
          <p class="text-sm text-slate-500">Biểu đồ thời gian tiến độ các Sprint & Mốc phát triển</p>
        </div>
      </div>

      <!-- Timeline Controls & Chart Panel -->
      <div class="roadmap-panel p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col gap-6 overflow-x-auto">
        <p class="roadmap-scroll-hint">Vuốt ngang để xem đầy đủ trục thời gian</p>
        
        <!-- Month Header Track -->
        <div class="roadmap-controls flex items-center justify-between border-b border-slate-200 dark:border-slate-800 pb-4">
          <div class="flex items-center gap-2">
            <span class="text-sm font-bold text-slate-900 dark:text-white uppercase tracking-wider">Trục thời gian các Sprint</span>
          </div>

          <div class="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1.5 rounded-xl text-sm font-semibold">
            <button class="px-3.5 py-1.5 rounded-lg bg-white dark:bg-slate-700 text-slate-900 dark:text-white shadow-2xs">Tuần</button>
            <button class="px-3.5 py-1.5 rounded-lg text-slate-500 hover:text-slate-900">Tháng</button>
            <button class="px-3.5 py-1.5 rounded-lg text-slate-500 hover:text-slate-900">Quý</button>
          </div>
        </div>

        <!-- Gantt Rows Container (Min 14px font text-sm) -->
        <div class="roadmap-track flex flex-col gap-4 min-w-[700px]">
          @for (sprint of sprints(); track sprint.id) {
            <div class="flex flex-col gap-2.5 p-4 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/60 dark:border-slate-700/60">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-3">
                  <span
                    [class.bg-emerald-500]="sprint.status === 'completed'"
                    [class.bg-blue-500]="sprint.status === 'active'"
                    [class.bg-slate-400]="sprint.status === 'future'"
                    class="w-3.5 h-3.5 rounded-full"
                  ></span>
                  <span class="text-sm font-bold text-slate-900 dark:text-white">{{ sprint.name }}</span>
                  <span class="text-xs text-slate-500 font-medium">({{ sprint.startDate }} &rarr; {{ sprint.endDate }})</span>
                </div>
                <span class="text-sm font-mono font-bold text-primary">{{ sprint.totalStoryPoints }} Points</span>
              </div>

              @let prog = getSprintProgress(sprint);
              <!-- Gantt Progress Bar Track -->
              <div class="w-full h-6 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden flex relative mt-1">
                <div
                  [class]="prog.bgClass"
                  [style.width.%]="prog.percent"
                  class="h-full rounded-full flex items-center justify-center text-xs font-bold text-white transition-all duration-300 px-2 truncate min-w-[2.5rem]"
                >
                  {{ prog.label }}
                </div>
              </div>
            </div>
          } @empty {
            <div class="p-8 text-center text-slate-400">
              <span class="material-symbols-outlined text-[36px] block mb-2 opacity-60">route</span>
              <p class="text-sm font-medium">Chưa có Sprint nào trong lộ trình</p>
              <p class="text-xs text-slate-400 mt-1">Các chu kỳ Sprint được tạo sẽ xuất hiện tự động dưới dạng biểu đồ Gantt tại đây.</p>
            </div>
          }
        </div>

      </div>

    </div>
    }
  `,
  styles: [`
    .roadmap-scroll-hint { display: none; }

    @media (max-width: 1279px) {
      .roadmap-page, .roadmap-panel { min-width: 0; }
      .roadmap-panel { max-width: 100%; overscroll-behavior-inline: contain; scroll-snap-type: x proximity; scrollbar-width: thin; }
      .roadmap-scroll-hint { display: block; position: sticky; left: 0; width: max-content; max-width: 100%; margin: 0; color: #64748b; font-size: .75rem; font-weight: 600; }
      .roadmap-track { scroll-snap-align: start; }
    }

    @media (max-width: 767px) {
      .roadmap-page { gap: 1rem; }
      .roadmap-panel { gap: 1rem; padding: 1rem; }
      .roadmap-controls { align-items: flex-start; flex-direction: column; gap: .75rem; }
      .roadmap-controls > div:last-child button { min-height: 40px; }
      .roadmap-track { min-width: 620px; }
    }

    @media (max-width: 932px) and (orientation: landscape) and (max-height: 520px) {
      .roadmap-panel { padding: .75rem; }
      .roadmap-track { min-width: 700px; }
    }
  `]
})
export class RoadmapPageComponent {
  readonly projectService = inject(ProjectManagementService);
  readonly sprints = this.projectService.sprints;

  getSprintProgress(sprint: Sprint): { percent: number; label: string; bgClass: string } {
    if (sprint.status === 'completed') {
      return { percent: 100, label: '100% Hoàn thành', bgClass: 'bg-emerald-500' };
    }
    const sprintItems = this.projectService.workItems().filter(w => w.sprintId === sprint.id);
    if (sprintItems.length === 0) {
      if (sprint.status === 'active') {
        return { percent: 0, label: 'Đang chạy (chưa có công việc)', bgClass: 'bg-blue-500' };
      }
      return { percent: 0, label: 'Kế hoạch', bgClass: 'bg-slate-400' };
    }
    const doneCount = sprintItems.filter(w => w.statusName === 'Done').length;
    const pct = Math.round((doneCount / sprintItems.length) * 100);
    return {
      percent: Math.max(pct, 10),
      label: `${pct}% Hoàn thành (${doneCount}/${sprintItems.length} việc)`,
      bgClass: sprint.status === 'active' ? 'bg-blue-500' : 'bg-slate-400'
    };
  }
}
