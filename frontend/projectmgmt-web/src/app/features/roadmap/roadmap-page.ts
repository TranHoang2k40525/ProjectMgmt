import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProjectManagementService } from '../../core/services/project-management.service';

@Component({
  selector: 'app-roadmap-page',
  standalone: true,
  imports: [CommonModule],
  template: `
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
            <span class="text-sm font-bold text-slate-900 dark:text-white uppercase tracking-wider">Tháng 8 - Tháng 10 (2026)</span>
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

              <!-- Gantt Progress Bar Track -->
              <div class="w-full h-6 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden flex relative mt-1">
                @if (sprint.status === 'completed') {
                  <div class="h-full bg-emerald-500 rounded-full w-full flex items-center justify-center text-xs font-bold text-white">100% Hoàn thành</div>
                } @else if (sprint.status === 'active') {
                  <div class="h-full bg-blue-500 rounded-full w-3/5 flex items-center justify-center text-xs font-bold text-white">60% Đang thực hiện</div>
                } @else {
                  <div class="h-full bg-slate-400 rounded-full w-1/5 flex items-center justify-center text-xs font-bold text-white">Kế hoạch</div>
                }
              </div>
            </div>
          }
        </div>

      </div>

    </div>
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
  private readonly projectService = inject(ProjectManagementService);
  readonly sprints = this.projectService.sprints;
}
