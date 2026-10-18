import { AfterViewInit, Component, ElementRef, OnDestroy, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { gsap } from 'gsap';
import { ProjectManagementService, Sprint } from '../../core/services/project-management.service';
import { NoProjectStateComponent } from '../../shared/components/no-project-state/no-project-state.component';

@Component({
  selector: 'app-reports-page',
  standalone: true,
  imports: [CommonModule, NoProjectStateComponent],
  template: `
    @if (!projectService.hasActiveProject()) {
      <app-no-project-state featureName="Báo Cáo Tiến Độ"></app-no-project-state>
    } @else {
    <div class="reports-page flex flex-col gap-6 animate-fade-in font-body text-slate-900 dark:text-slate-100">
      
      <!-- Top Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-200 dark:border-slate-800">
        <div class="flex flex-col">
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <span class="material-symbols-outlined text-[24px] text-primary">analytics</span>
            Báo Cáo Tiến Độ Dự Án
          </h1>
          <p class="text-sm text-slate-500">Biểu đồ Burndown Chart và Sprint Velocity phân tích tiến độ thực tế</p>
        </div>
      </div>

      <!-- Charts Grid Container -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        @if (sprints().length === 0) {
          <div class="report-card p-12 col-span-full rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 text-center text-slate-400">
            <span class="material-symbols-outlined text-[48px] block mb-2 opacity-60">insights</span>
            <h3 class="text-base font-bold text-slate-700 dark:text-slate-200">Chưa có báo cáo tiến độ</h3>
            <p class="text-xs text-slate-400 mt-1">Dự án hiện chưa có chu kỳ Sprint nào để tạo biểu đồ Burndown và Velocity.</p>
          </div>
        } @else {
          <!-- Burndown Chart Tile -->
          <div class="report-card p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col gap-4">
            <div class="flex items-center justify-between">
              <div class="flex flex-col">
                <h3 class="text-lg font-bold text-slate-900 dark:text-white">Burndown Chart ({{ activeSprint()?.name || 'Sprint' }})</h3>
                <p class="text-sm text-slate-500">So sánh tiến độ đốt điểm Story Points thực tế vs kế hoạch lý thuyết</p>
              </div>
              <span class="px-3 py-1 rounded-full bg-emerald-100 text-emerald-800 font-bold text-xs">
                {{ burndownStatusLabel() }}
              </span>
            </div>

            @if (totalSprintPoints() === 0 && sprintWorkItems().length === 0) {
              <div class="p-8 text-center text-slate-400 bg-slate-50/50 dark:bg-slate-950/40 rounded-xl border border-slate-200 dark:border-slate-800 flex flex-col items-center justify-center min-h-[16rem]">
                <span class="material-symbols-outlined text-[36px] block mb-2 opacity-60">hourglass_empty</span>
                <p class="text-sm font-semibold text-slate-700 dark:text-slate-300">Sprint này chưa có dữ liệu Story Points</p>
                <p class="text-xs text-slate-400 mt-1">Gán Story Points và đưa các công việc vào Sprint để hệ thống hiển thị Burndown Chart thực tế.</p>
              </div>
            } @else {
              <!-- SVG Burndown Visualization -->
              <div class="burndown-chart h-64 w-full relative flex items-end justify-between pt-8 pb-6 px-4 border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40 rounded-xl">
                <svg class="chart-svg chart-svg-desktop absolute inset-0 w-full h-full p-4 overflow-visible" viewBox="0 0 480 240" preserveAspectRatio="none">
                  <!-- Ideal Line (Dotted Gray) -->
                  <line x1="48" y1="48" x2="432" y2="204" stroke="#94a3b8" stroke-width="2" stroke-dasharray="6,6" />
                  <!-- Actual Burn Line (Solid Blue) -->
                  <polyline fill="none" stroke="#2563eb" stroke-width="3" [attr.points]="burndownPoints()" />
                </svg>
                <svg class="chart-svg chart-svg-responsive absolute inset-0 w-full h-full p-4" viewBox="0 0 480 240" preserveAspectRatio="none" aria-hidden="true">
                  <line x1="48" y1="48" x2="432" y2="204" stroke="#94a3b8" stroke-width="2" stroke-dasharray="6,6" />
                  <polyline fill="none" stroke="#2563eb" stroke-width="3" [attr.points]="burndownPoints()" />
                </svg>

                <!-- X Axis Labels -->
                <div class="absolute bottom-1.5 left-4 right-4 flex justify-between text-xs text-slate-500 font-mono font-medium">
                  <span>Khởi đầu ({{ totalSprintPoints() }} SP)</span>
                  <span>50%</span>
                  <span>Còn lại ({{ remainingPoints() }} SP)</span>
                </div>
              </div>

              <div class="chart-legend flex items-center justify-center gap-6 text-sm font-semibold pt-1">
                <div class="flex items-center gap-2">
                  <span class="w-4 h-0.5 bg-slate-400 border-t border-dashed border-slate-600"></span>
                  <span class="text-slate-500">Đường lý thuyết (Ideal)</span>
                </div>
                <div class="flex items-center gap-2">
                  <span class="w-4 h-1 bg-blue-600 rounded"></span>
                  <span class="text-slate-800 dark:text-slate-200">Thực tế thực hiện (Actual: {{ completedSprintPoints() }}/{{ totalSprintPoints() }} SP)</span>
                </div>
              </div>
            }
          </div>

          <!-- Velocity Chart Tile -->
          <div class="report-card p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col gap-4">
            <div class="flex items-center justify-between">
              <div class="flex flex-col">
                <h3 class="text-lg font-bold text-slate-900 dark:text-white">Năng suất Sprint (Sprint Velocity)</h3>
                <p class="text-sm text-slate-500">Khối lượng điểm Story Points hoàn thành qua các Sprint</p>
              </div>
            </div>

            <!-- Bar Chart Container -->
            <div class="velocity-chart h-64 w-full flex items-end justify-around p-4 border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40 rounded-xl gap-4">
              @for (sprint of sprints(); track sprint.id) {
                <div class="flex flex-col items-center gap-2 h-full justify-end flex-1">
                  <span class="text-xs font-mono font-bold text-primary">{{ sprint.totalStoryPoints }} SP</span>
                  <div
                    [style.height.%]="sprint.totalStoryPoints > 0 ? (sprint.totalStoryPoints / maxVelocityPoints()) * 100 : 4"
                    class="velocity-bar w-full max-w-[48px] bg-indigo-600 dark:bg-indigo-500 rounded-t-xl hover:brightness-110 transition-all cursor-pointer"
                  ></div>
                  <span class="velocity-label text-xs font-bold text-slate-700 dark:text-slate-300 truncate w-full text-center">{{ sprint.name }}</span>
                </div>
              }
            </div>

          </div>
        }
      </div>

    </div>
    }
  `,
  styles: [`
    .chart-svg-responsive { display: none; }

    @media (max-width: 1279px) {
      .chart-svg-desktop { display: none; }
      .chart-svg-responsive { display: block; }
      .reports-page { min-width: 0; }
      .report-card { min-width: 0; }
      .burndown-chart, .velocity-chart { min-width: 0; overflow: hidden; }
      .chart-legend { flex-wrap: wrap; }
    }

    @media (max-width: 767px) {
      .reports-page { gap: 1rem; }
      .report-card { padding: 1rem; }
      .report-card > div:first-child { align-items: flex-start; gap: .75rem; }
      .burndown-chart, .velocity-chart { height: 15rem; padding-inline: .5rem; }
      .burndown-chart > div { left: .5rem; right: .5rem; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .25rem; }
      .burndown-chart > div span { min-width: 0; white-space: normal; text-align: center; font-size: .625rem; line-height: 1.2; }
      .chart-legend { align-items: flex-start; justify-content: flex-start; gap: .75rem 1rem; }
      .chart-legend > div { min-width: 0; }
      .velocity-chart { gap: .5rem; }
      .velocity-label { overflow: visible; white-space: normal; line-height: 1.2; }
    }
  `]
})
export class ReportsPageComponent implements AfterViewInit, OnDestroy {
  readonly projectService = inject(ProjectManagementService);
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);
  private motionMedia: ReturnType<typeof gsap.matchMedia> | null = null;
  readonly sprints = this.projectService.sprints;

  readonly activeSprint = computed<Sprint | null>(() => {
    return this.sprints().find(s => s.status === 'active') ?? this.sprints()[0] ?? null;
  });

  readonly sprintWorkItems = computed(() => {
    const sp = this.activeSprint();
    if (!sp) return [];
    return this.projectService.workItems().filter(i => i.sprintId === sp.id);
  });

  readonly totalSprintPoints = computed(() => {
    const items = this.sprintWorkItems();
    const sum = items.reduce((acc, i) => acc + (i.storyPoints || 0), 0);
    return sum || this.activeSprint()?.totalStoryPoints || 0;
  });

  readonly completedSprintPoints = computed(() => {
    return this.sprintWorkItems()
      .filter(i => i.statusName === 'Done')
      .reduce((acc, i) => acc + (i.storyPoints || 0), 0);
  });

  readonly remainingPoints = computed(() => {
    return Math.max(0, this.totalSprintPoints() - this.completedSprintPoints());
  });

  readonly burndownPoints = computed(() => {
    const total = this.totalSprintPoints();
    const completed = this.completedSprintPoints();
    if (total <= 0) return '48,204 432,204';
    const startY = 48;
    const bottomY = 204;
    const currentY = Math.round(startY + (completed / total) * (bottomY - startY));
    const midY = Math.round((startY + currentY) / 2);
    return `48,${startY} 240,${midY} 432,${currentY}`;
  });

  readonly burndownStatusLabel = computed(() => {
    const total = this.totalSprintPoints();
    const completed = this.completedSprintPoints();
    if (total === 0) return 'Chưa có điểm';
    if (completed >= total) return 'Đã hoàn thành';
    const ratio = completed / total;
    if (ratio >= 0.5) return 'Tiến độ tốt';
    return 'Đang triển khai';
  });

  readonly maxVelocityPoints = computed(() => {
    const pts = this.sprints().map(s => s.totalStoryPoints || 0);
    return Math.max(...pts, 10);
  });

  ngAfterViewInit(): void {
    const root = this.elementRef.nativeElement;
    this.motionMedia = gsap.matchMedia(root);
    this.motionMedia.add('(prefers-reduced-motion: no-preference)', () => {
      const strokes = gsap.utils.toArray<SVGGeometryElement>('.chart-svg line, .chart-svg polyline', root);
      strokes.forEach(stroke => {
        const length = Math.max(stroke.getTotalLength(), 1);
        gsap.set(stroke, { strokeDasharray: length, strokeDashoffset: length });
      });

      const timeline = gsap.timeline({ defaults: { ease: 'power3.out' } });
      timeline
        .to(strokes, { strokeDashoffset: 0, duration: 0.9, stagger: 0.08, clearProps: 'strokeDasharray,strokeDashoffset' })
        .from('.velocity-bar', { scaleY: 0, transformOrigin: 'bottom center', duration: 0.72, stagger: 0.09 }, '<0.15')
        .from('.velocity-label', { autoAlpha: 0, y: 8, duration: 0.35, stagger: 0.06 }, '-=0.35');

      return () => timeline.kill();
    });
  }

  ngOnDestroy(): void {
    this.motionMedia?.revert();
  }
}

