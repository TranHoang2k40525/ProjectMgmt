import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ProjectManagementService, Project, WorkItem } from '../../core/services/project-management.service';

@Component({
  selector: 'app-for-you-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="flex flex-col gap-6 max-w-6xl mx-auto animate-fade-in font-body text-slate-900 dark:text-slate-100">
      
      <!-- Recommended Spaces Section -->
      <div class="flex flex-col gap-3">
        <div class="flex items-center justify-between">
          <h2 class="text-base font-bold text-slate-900 dark:text-white uppercase tracking-wider">Dự án & Không gian làm việc</h2>
          <a routerLink="/project/summary" class="text-sm font-semibold text-primary hover:underline">Xem tổng quan dự án &rarr;</a>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          @for (proj of projects(); track proj.id) {
            <div
              (click)="selectProject(proj)"
              (keydown.enter)="selectProject(proj)"
              (keydown.space)="$event.preventDefault(); selectProject(proj)"
              role="button"
              tabindex="0"
              class="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs hover:shadow-md transition-all cursor-pointer flex flex-col gap-3 group border-l-4 border-l-primary"
            >
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center font-bold text-base shrink-0 group-hover:scale-105 transition-transform">
                  <span class="material-symbols-outlined text-[24px]">apartment</span>
                </div>
                <div class="flex flex-col truncate">
                  <span class="text-sm font-bold text-slate-900 dark:text-white truncate group-hover:text-primary transition-colors">{{ proj.name }}</span>
                  <span class="text-xs text-slate-500 font-medium truncate">{{ proj.type || 'Scrum Project' }}</span>
                </div>
              </div>
            </div>
          }
        </div>
      </div>

      <!-- Personalized Work Items Tabs & List -->
      <div class="flex flex-col gap-4 mt-2">
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-200 dark:border-slate-800 pb-3">
          <h2 class="text-lg font-bold text-slate-900 dark:text-white">Dành cho bạn (For You)</h2>

          <!-- Tabs Switcher (Min 14px font) -->
          <div class="flex items-center gap-1.5 bg-slate-100 dark:bg-slate-800 p-1.5 rounded-xl text-sm font-semibold">
            <button
              (click)="activeTab.set('assigned')"
              [class.bg-white]="activeTab() === 'assigned'"
              [class.dark:bg-slate-700]="activeTab() === 'assigned'"
              [class.text-primary]="activeTab() === 'assigned'"
              [class.shadow-2xs]="activeTab() === 'assigned'"
              class="px-3.5 py-1.5 rounded-lg text-slate-600 dark:text-slate-300 transition-all flex items-center gap-2"
            >
              <span>Tôi xử lý</span>
              <span class="px-2 py-0.5 rounded-full bg-primary/20 text-primary text-xs font-mono font-bold">{{ assignedCount() }}</span>
            </button>

            <button
              (click)="activeTab.set('worked')"
              [class.bg-white]="activeTab() === 'worked'"
              [class.dark:bg-slate-700]="activeTab() === 'worked'"
              [class.text-primary]="activeTab() === 'worked'"
              [class.shadow-2xs]="activeTab() === 'worked'"
              class="px-3.5 py-1.5 rounded-lg text-slate-600 dark:text-slate-300 transition-all"
            >
              Đã xử lý gần đây
            </button>

            <button
              (click)="activeTab.set('starred')"
              [class.bg-white]="activeTab() === 'starred'"
              [class.dark:bg-slate-700]="activeTab() === 'starred'"
              [class.text-primary]="activeTab() === 'starred'"
              [class.shadow-2xs]="activeTab() === 'starred'"
              class="px-3.5 py-1.5 rounded-lg text-slate-600 dark:text-slate-300 transition-all"
            >
              Đã đánh dấu ⭐
            </button>
          </div>
        </div>

        <!-- Work Items Table List -->
        <div class="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col gap-3">
          <span class="text-xs font-bold text-slate-400 uppercase tracking-wider px-2">Danh sách công việc đang thực hiện</span>

          <div class="divide-y divide-slate-100 dark:divide-slate-800">
            @for (item of filteredWorkItems(); track item.id) {
              <div
                (click)="openDrawer(item)"
                (keydown.enter)="openDrawer(item)"
                (keydown.space)="$event.preventDefault(); openDrawer(item)"
                role="button"
                tabindex="0"
                class="flex items-center justify-between py-3.5 px-3 rounded-xl hover:bg-slate-50 dark:hover:bg-slate-800/50 cursor-pointer transition-colors group"
              >
                <div class="flex items-center gap-3.5 min-w-0">
                  <div class="w-6 h-6 rounded border border-slate-300 dark:border-slate-600 flex items-center justify-center text-slate-400 shrink-0">
                    <span class="material-symbols-outlined text-[16px]">check</span>
                  </div>

                  <div class="flex flex-col truncate">
                    <span class="text-sm font-semibold text-slate-900 dark:text-white truncate group-hover:text-primary transition-colors leading-snug">
                      {{ item.title }}
                    </span>
                    <div class="flex items-center gap-2 text-xs text-slate-400">
                      <span class="font-mono font-bold text-primary text-xs">{{ item.issueKey }}</span>
                      <span>•</span>
                      <span>{{ item.sprintName }}</span>
                    </div>
                  </div>
                </div>

                <div class="flex items-center gap-4 shrink-0 text-sm">
                  <span class="px-3 py-1 rounded-lg text-xs font-bold bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300">
                    {{ item.statusName }}
                  </span>
                  <div class="w-7 h-7 rounded-full bg-emerald-600 text-white font-bold text-xs flex items-center justify-center">
                    TH
                  </div>
                  <span class="text-xs text-slate-400 font-mono w-24 text-right">{{ item.createdAt }}</span>
                </div>
              </div>
            }
          </div>

        </div>

      </div>

    </div>
  `
})
export class ForYouPageComponent {
  private readonly projectService = inject(ProjectManagementService);

  readonly projects = this.projectService.allProjectsList;
  readonly workItems = this.projectService.workItems;

  activeTab = signal<'assigned' | 'worked' | 'starred'>('assigned');

  readonly assignedCount = computed(() => this.workItems().filter(i => i.assigneeName === 'Trần Văn Hoàng').length);

  filteredWorkItems = computed(() => {
    const tab = this.activeTab();
    if (tab === 'assigned') {
      return this.workItems().filter(i => i.assigneeName === 'Trần Văn Hoàng');
    }
    if (tab === 'worked') {
      return this.workItems().filter(i => i.statusName === 'In Progress' || i.statusName === 'Done');
    }
    return this.workItems().slice(0, 5);
  });

  selectProject(proj: Project): void {
    this.projectService.currentProject.set(proj);
  }

  openDrawer(item: WorkItem): void {
    this.projectService.activeDrawerTask.set(item);
  }
}
