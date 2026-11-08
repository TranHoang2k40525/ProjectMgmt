import { AfterViewInit, Component, ElementRef, OnDestroy, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { gsap } from 'gsap';
import { ProjectManagementService } from '../../core/services/project-management.service';
import { CardTiltDirective } from '../../shared/motion/card-tilt.directive';
import { NoProjectStateComponent } from '../../shared/components/no-project-state/no-project-state.component';

@Component({
  selector: 'app-project-summary-page',
  standalone: true,
  imports: [CommonModule, RouterLink, NoProjectStateComponent, CardTiltDirective],
  template: `
    @if (!projectService.hasActiveProject()) {
      <app-no-project-state featureName="Tổng quan Dự án"></app-no-project-state>
    } @else {
    <div class="project-summary-page flex flex-col gap-6 animate-fade-in">
      
      <!-- Top Banner Header -->
      <div class="summary-hero flex flex-col md:flex-row md:items-center justify-between gap-4 p-6 rounded-2xl bg-[#0f172a] text-white border border-slate-800 shadow-lg relative overflow-hidden">
        <div class="summary-hero-copy flex items-center gap-4 z-10">
          <div class="w-14 h-14 rounded-2xl bg-white/10 ring-1 ring-white/20 shrink-0 flex items-center justify-center text-xl font-bold font-mono text-white shadow-inner">
            {{ currentProject().projectKey ? currentProject().projectKey.slice(0, 2) : 'PJ' }}
          </div>
          <div class="flex flex-col">
            <div class="flex items-center gap-2">
              <span class="px-2.5 py-0.5 rounded-md text-[10px] font-bold uppercase tracking-wider bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                {{ currentProject().workflowName || currentProject().boardTabTitle || 'Agile Workflow' }}
              </span>
              <span class="text-xs text-slate-300 font-mono">Mã dự án: {{ currentProject().projectKey || '--' }}</span>
            </div>
            <h1 class="text-xl font-extrabold tracking-tight mt-1 text-white">{{ currentProject().name || 'Chưa chọn dự án' }}</h1>
            <p class="text-xs text-slate-400 max-w-2xl mt-0.5">{{ currentProject().description || 'Không gian quản lý tiến độ, backlog và thực thi nhiệm vụ dự án.' }}</p>
          </div>
        </div>

        <div class="summary-hero-action flex items-center gap-3 z-10">
          <a routerLink="/board" class="animate-ui-btn-primary text-xs shadow-md">
            <span class="material-symbols-outlined text-[18px]">view_kanban</span>
            Mở Bảng
          </a>
          <a routerLink="/backlog" class="animate-ui-btn-secondary text-xs">
            <span class="material-symbols-outlined text-[18px]">format_list_bulleted</span>
            Xem Backlog
          </a>
        </div>
      </div>

      <!-- Bento Grid Layout Container with 3D Tilt Feedback -->
      <div class="summary-kpi-grid grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        
        <!-- KPI Tile 1: Done -->
        <div appCardTilt class="animate-ui-card p-5 flex flex-col justify-between cursor-default">
          <div class="flex items-center justify-between text-emerald-600 dark:text-emerald-400">
            <span class="text-xs font-bold uppercase tracking-wider">Đã hoàn thành</span>
            <div class="w-9 h-9 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 flex items-center justify-center border border-emerald-200/60 dark:border-emerald-800/40">
              <span class="material-symbols-outlined text-[20px]">check_circle</span>
            </div>
          </div>
          <div class="mt-4 flex items-baseline gap-2">
            <span class="text-3xl font-extrabold text-slate-900 dark:text-white">{{ completedCount() }}</span>
            <span class="text-xs text-slate-400">/ {{ totalCount() }} công việc</span>
          </div>
        </div>

        <!-- KPI Tile 2: In Progress -->
        <div appCardTilt class="animate-ui-card p-5 flex flex-col justify-between cursor-default">
          <div class="flex items-center justify-between text-blue-600 dark:text-blue-400">
            <span class="text-xs font-bold uppercase tracking-wider">Đang thực hiện</span>
            <div class="w-9 h-9 rounded-xl bg-blue-50 dark:bg-blue-950/60 flex items-center justify-center border border-blue-200/60 dark:border-blue-800/40">
              <span class="material-symbols-outlined text-[20px]">sync</span>
            </div>
          </div>
          <div class="mt-4 flex items-baseline gap-2">
            <span class="text-3xl font-extrabold text-slate-900 dark:text-white">{{ inProgressCount() }}</span>
            <span class="text-xs text-slate-400">công việc active</span>
          </div>
        </div>

        <!-- KPI Tile 3: Active Sprint -->
        <div appCardTilt class="animate-ui-card p-5 flex flex-col justify-between cursor-default">
          <div class="flex items-center justify-between text-slate-700 dark:text-slate-300">
            <span class="text-xs font-bold uppercase tracking-wider">Sprint Hiện Tại</span>
            <div class="w-9 h-9 rounded-xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center border border-slate-200 dark:border-slate-700">
              <span class="material-symbols-outlined text-[20px]">directions_run</span>
            </div>
          </div>
          <div class="mt-4 flex flex-col">
            @if (activeSprint(); as sp) {
              <span class="text-base font-bold text-slate-900 dark:text-white truncate">{{ sp.name }}</span>
              <span class="text-[11px] text-slate-400 mt-0.5">{{ sp.startDate || 'N/A' }} - {{ sp.endDate || 'N/A' }} ({{ sp.totalStoryPoints }} Story Pts)</span>
            } @else {
              <span class="text-base font-bold text-slate-900 dark:text-white">Chưa có Sprint</span>
              <span class="text-[11px] text-slate-400 mt-0.5">Chưa có chu kỳ chạy</span>
            }
          </div>
        </div>

        <!-- KPI Tile 4: Scope Progress -->
        <div appCardTilt class="animate-ui-card p-5 flex flex-col justify-between cursor-default">
          <div class="flex items-center justify-between text-teal-600 dark:text-teal-400">
            <span class="text-xs font-bold uppercase tracking-wider">Tiến Độ Dự Án</span>
            <div class="w-9 h-9 rounded-xl bg-teal-50 dark:bg-teal-950/60 flex items-center justify-center border border-teal-200/60 dark:border-teal-800/40">
              <span class="material-symbols-outlined text-[20px]">donut_large</span>
            </div>
          </div>
          <div class="mt-4 flex items-baseline gap-2">
            <span class="text-3xl font-extrabold text-slate-900 dark:text-white">
              {{ totalCount() > 0 ? ((completedCount() / totalCount()) * 100 | number:'1.0-0') + '%' : '0%' }}
            </span>
            <span class="text-xs text-slate-400">{{ completedCount() }}/{{ totalCount() }} hoàn thành</span>
          </div>
        </div>

      </div>

      <!-- Bento Big Tiles: Status Donut Breakdown & Activity Stream -->
      <div class="summary-main-grid grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        <!-- Tile Left: Status Overview Donut Visual -->
        <div class="lg:col-span-2 animate-ui-card p-6 flex flex-col gap-6">
          <div class="summary-status-header flex items-center justify-between">
            <div class="flex flex-col">
              <h3 class="text-base font-bold text-slate-900 dark:text-white">Tổng Quan Trạng Thái Công Việc</h3>
              <p class="text-xs text-slate-400">Phân bố công việc theo trạng thái trong dự án</p>
            </div>
            <a routerLink="/task-list" class="text-xs font-bold text-primary hover:underline flex items-center gap-1">
              Xem tất cả công việc
              <span class="material-symbols-outlined text-[16px]">arrow_forward</span>
            </a>
          </div>

          <div class="flex flex-col md:flex-row items-center justify-around gap-8 py-4">
            <!-- Dynamic Donut Ring Chart -->
            <div class="relative w-44 h-44 flex items-center justify-center">
              <svg class="w-full h-full transform -rotate-90" viewBox="0 0 36 36">
                <!-- Background Ring -->
                <path class="text-slate-100 dark:text-slate-800" stroke-width="3.8" stroke="currentColor" fill="none" d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                @if (donePercent() > 0) {
                  <!-- Done Segment -->
                  <path class="text-emerald-500" [attr.stroke-dasharray]="donePercent() + ', 100'" stroke-width="3.8" stroke="currentColor" fill="none" d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                }
                @if (inProgressPercent() > 0) {
                  <!-- In Progress Segment -->
                  <path class="text-blue-500" [attr.stroke-dasharray]="inProgressPercent() + ', 100'" [attr.stroke-dashoffset]="-donePercent()" stroke-width="3.8" stroke="currentColor" fill="none" d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
                }
              </svg>
              <div class="absolute flex flex-col items-center justify-center text-center">
                <span class="text-2xl font-black text-slate-900 dark:text-white">{{ totalCount() }}</span>
                <span class="text-[10px] text-slate-400 font-medium">Tổng số công việc</span>
              </div>
            </div>

            <!-- Legend Items -->
            <div class="flex flex-col gap-3">
              <div class="flex items-center gap-3">
                <span class="w-3.5 h-3.5 rounded-md bg-slate-400"></span>
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300">To Do (Cần làm): {{ toDoCount() }}</span>
              </div>
              <div class="flex items-center gap-3">
                <span class="w-3.5 h-3.5 rounded-md bg-blue-500"></span>
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300">In Progress (Đang làm): {{ inProgressCount() }}</span>
              </div>
              <div class="flex items-center gap-3">
                <span class="w-3.5 h-3.5 rounded-md bg-teal-500"></span>
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300">Code Review (Duyệt): {{ codeReviewCount() }}</span>
              </div>
              <div class="flex items-center gap-3">
                <span class="w-3.5 h-3.5 rounded-md bg-emerald-500"></span>
                <span class="text-xs font-semibold text-slate-700 dark:text-slate-300">Done (Hoàn thành): {{ completedCount() }}</span>
              </div>
            </div>
          </div>

          <div class="summary-priority-box pt-4 border-t border-slate-100 dark:border-slate-800">
            <h4 class="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2">Phân bổ theo Độ ưu tiên</h4>
            <div class="flex flex-wrap items-center gap-4 text-xs font-medium">
              <span class="text-rose-500 font-semibold flex items-center gap-1">
                <span class="w-2 h-2 rounded-full bg-rose-500"></span>
                Khẩn cấp: {{ urgentCount() }}
              </span>
              <span class="text-amber-500 font-semibold flex items-center gap-1">
                <span class="w-2 h-2 rounded-full bg-amber-500"></span>
                Cao: {{ highCount() }}
              </span>
              <span class="text-blue-500 font-semibold flex items-center gap-1">
                <span class="w-2 h-2 rounded-full bg-blue-500"></span>
                Trung bình: {{ mediumCount() }}
              </span>
              <span class="text-slate-500 font-semibold flex items-center gap-1">
                <span class="w-2 h-2 rounded-full bg-slate-400"></span>
                Thấp: {{ lowCount() }}
              </span>
            </div>
          </div>
        </div>

        <!-- Tile Right: Activity Stream -->
        <div class="animate-ui-card p-6 flex flex-col gap-4">
          <h3 class="text-base font-bold text-slate-900 dark:text-white">Dòng Hoạt Động Mới</h3>

          <div class="flex flex-col gap-3">
            @for (act of recentActivities(); track act.id) {
              <div class="flex items-start gap-3 p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800/60 hover:bg-slate-100/70 dark:hover:bg-slate-800 transition-colors">
                @if (getActorAvatar(act.actorName, act.actorAvatar); as avatarUrl) {
                  <img [src]="avatarUrl" [alt]="act.actorName" class="w-8 h-8 rounded-full object-cover shrink-0 ring-1 ring-slate-200 dark:ring-slate-700" />
                } @else {
                  <div class="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center text-xs font-bold shrink-0">
                    {{ act.actorName.charAt(0) }}
                  </div>
                }
                <div class="flex flex-col text-xs min-w-0">
                  <span class="font-bold text-slate-900 dark:text-white truncate">{{ act.actorName }}</span>
                  <span class="text-slate-600 dark:text-slate-300">{{ act.action }} <span class="font-semibold text-primary font-mono">[{{ act.issueKey }}]</span></span>
                  <span class="text-[10px] text-slate-400 mt-1">{{ act.timestamp }}</span>
                </div>
              </div>
            } @empty {
              <div class="py-8 text-center text-slate-400 text-xs flex flex-col items-center justify-center gap-2">
                <span class="material-symbols-outlined text-3xl text-slate-300">history_toggle_off</span>
                <span>Chưa có hoạt động nào được ghi nhận trong dự án</span>
              </div>
            }
          </div>
        </div>

      </div>

    </div>
    }
  `,
  styles: [`
    @media (max-width: 1279px) {
      .project-summary-page, .summary-hero, .summary-hero-copy, .summary-hero-copy > div { min-width: 0; }
      .summary-kpi-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .summary-main-grid { grid-template-columns: minmax(0, 1fr); }
      .summary-main-grid > * { grid-column: auto !important; }
    }

    @media (max-width: 767px) {
      .project-summary-page { gap: 1rem; }
      .summary-hero { align-items: stretch; padding: 1.25rem; }
      .summary-hero-copy { align-items: flex-start; }
      .summary-hero-copy h1 { line-height: 1.2; }
      .summary-hero-action { flex-direction: column; width: 100%; }
      .summary-hero-action a { width: 100%; justify-content: center; min-height: 40px; }
      .summary-kpi-grid { grid-template-columns: minmax(0, 1fr); gap: .75rem; }
      .summary-status-header { align-items: flex-start; flex-direction: column; gap: .75rem; }
    }
  `]
})
export class ProjectSummaryPageComponent implements AfterViewInit, OnDestroy {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly projectService = inject(ProjectManagementService);
  private kpiTween: gsap.core.Tween | null = null;

  readonly currentProject = this.projectService.currentProject;
  readonly items = this.projectService.workItems;
  readonly activeSprint = computed(() => this.projectService.sprints().find(s => s.status === 'active') ?? null);

  readonly totalCount = computed(() => this.items().length);
  readonly completedCount = computed(() => {
    const doneNames = new Set(this.projectService.projectStatuses().filter(s => s.category === 'Done').map(s => s.name));
    return this.items().filter(i => i.statusName === 'Done' || doneNames.has(i.statusName)).length;
  });
  readonly inProgressCount = computed(() => {
    const inProgNames = new Set(this.projectService.projectStatuses().filter(s => s.category === 'InProgress' && s.name !== 'Code Review').map(s => s.name));
    return this.items().filter(i => i.statusName === 'In Progress' || inProgNames.has(i.statusName)).length;
  });
  readonly codeReviewCount = computed(() => this.items().filter(i => i.statusName === 'Code Review').length);
  readonly toDoCount = computed(() => {
    const todoNames = new Set(this.projectService.projectStatuses().filter(s => s.category === 'ToDo').map(s => s.name));
    return this.items().filter(i => i.statusName === 'To Do' || todoNames.has(i.statusName)).length;
  });

  readonly donePercent = computed(() => this.totalCount() > 0 ? Math.round((this.completedCount() / this.totalCount()) * 100) : 0);
  readonly inProgressPercent = computed(() => this.totalCount() > 0 ? Math.round((this.inProgressCount() / this.totalCount()) * 100) : 0);

  readonly urgentCount = computed(() => this.items().filter(i => i.priority === 'Urgent').length);
  readonly highCount = computed(() => this.items().filter(i => i.priority === 'High').length);
  readonly mediumCount = computed(() => this.items().filter(i => i.priority === 'Medium').length);
  readonly lowCount = computed(() => this.items().filter(i => i.priority === 'Low').length);

  readonly recentActivities = computed(() => {
    const list: { id: string; actorName: string; actorAvatar?: string; action: string; timestamp: string; issueKey: string }[] = [];
    for (const item of this.items()) {
      for (const act of item.activities ?? []) {
        list.push({ ...act, issueKey: item.issueKey });
      }
    }
    return list.slice(0, 5);
  });

  getActorAvatar(actorName: string, actorAvatar?: string): string | null {
    return this.projectService.getMemberAvatar(actorName, actorAvatar);
  }

  ngAfterViewInit(): void {
    if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      const kpis = this.el.nativeElement.querySelectorAll<HTMLElement>('.summary-kpi-grid > div');
      if (kpis.length) {
        this.kpiTween = gsap.fromTo(
          kpis,
          { opacity: 0, y: 16 },
          {
            opacity: 1,
            y: 0,
            duration: 0.4,
            stagger: 0.08,
            ease: 'power2.out',
            clearProps: 'transform,opacity'
          }
        );
      }
    }
  }

  ngOnDestroy(): void {
    this.kpiTween?.kill();
  }
}
