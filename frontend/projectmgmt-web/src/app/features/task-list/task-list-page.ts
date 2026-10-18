import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectManagementService, WorkItem } from '../../core/services/project-management.service';
import { ExcelDataService } from '../../core/services/excel-data.service';

@Component({
  selector: 'app-task-list-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="task-list-page flex flex-col gap-6 animate-fade-in font-body text-slate-900 dark:text-slate-100">
      
      <!-- Top Action Bar -->
      <div class="task-list-header flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-200 dark:border-slate-800">
        <div class="flex flex-col">
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <span class="material-symbols-outlined text-[24px] text-primary">table_rows</span>
            Danh Sách Công Việc (Task List View)
          </h1>
          <p class="text-sm text-slate-500">Bảng danh sách công việc trực quan kiểu Bkav eTask & Jira List</p>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="exportExcel()" class="px-4 py-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800 hover:bg-emerald-100 font-semibold text-sm transition-colors flex items-center gap-2 shadow-2xs cursor-pointer">
            <span class="material-symbols-outlined text-[20px]">download</span>
            Xuất Excel
          </button>
        </div>
      </div>

      <!-- Main Layout: Left Group Filter + Right Work Items Table -->
      <div class="task-list-layout grid grid-cols-1 lg:grid-cols-4 gap-6">
        
        <!-- Left Filter Panel -->
        <div class="task-list-filters p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col gap-4 select-none">
          <div class="flex flex-col gap-1.5">
            <span class="text-xs font-bold uppercase tracking-wider text-slate-400 px-2">Phân loại công việc</span>
            <nav class="flex flex-col gap-1">
              <button
                (click)="selectedGroup.set('all')"
                [class.bg-primary/10]="selectedGroup() === 'all'"
                [class.text-primary]="selectedGroup() === 'all'"
                class="flex items-center justify-between px-3.5 py-2.5 rounded-xl text-sm font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors w-full text-left"
              >
                <div class="flex items-center gap-2.5">
                  <span class="material-symbols-outlined text-[20px]">history</span>
                  <span>Tất cả công việc</span>
                </div>
                <span class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300">
                  {{ allWorkItems().length }}
                </span>
              </button>

              <button
                (click)="selectedGroup.set('my')"
                [class.bg-primary/10]="selectedGroup() === 'my'"
                [class.text-primary]="selectedGroup() === 'my'"
                class="flex items-center justify-between px-3.5 py-2.5 rounded-xl text-sm font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors w-full text-left"
              >
                <div class="flex items-center gap-2.5">
                  <span class="material-symbols-outlined text-[20px]">star</span>
                  <span>Tôi xử lý</span>
                </div>
              </button>
            </nav>
          </div>

          <div class="flex flex-col gap-1.5 border-t border-slate-200/60 dark:border-slate-800 pt-3">
            <span class="text-xs font-bold uppercase tracking-wider text-slate-400 px-2">Phân nhóm hệ thống</span>
            <nav class="flex flex-col gap-1">
              <button class="flex items-center gap-2.5 px-3.5 py-2.5 rounded-xl text-sm font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors">
                <span class="w-3 h-3 rounded-full bg-blue-500"></span>
                <span>Scrum AI Engine</span>
              </button>
              <button class="flex items-center gap-2.5 px-3.5 py-2.5 rounded-xl text-sm font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors">
                <span class="w-3 h-3 rounded-full bg-indigo-500"></span>
                <span>Delivery Intelligence</span>
              </button>
              <button class="flex items-center gap-2.5 px-3.5 py-2.5 rounded-xl text-sm font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors">
                <span class="w-3 h-3 rounded-full bg-purple-500"></span>
                <span>Identity & RBAC</span>
              </button>
            </nav>
          </div>
        </div>

        <!-- Right Main Work Items Table -->
        <div class="task-list-panel lg:col-span-3 p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col gap-4">
          
          <!-- Search & Filter Controls -->
          <div class="task-list-controls flex items-center justify-between gap-4">
            <div class="relative flex-1">
              <span class="material-symbols-outlined absolute left-3 top-2.5 text-[20px] text-slate-400 pointer-events-none">search</span>
              <input
                type="text"
                [(ngModel)]="searchTerm"
                placeholder="Lọc công việc theo tên hoặc mã task..."
                class="w-full h-10 pl-10 pr-4 rounded-xl bg-slate-100 dark:bg-slate-800 text-sm text-slate-800 dark:text-slate-200 focus:outline-none focus:border-primary border border-transparent"
              />
            </div>
          </div>

          <!-- Table Container (Font size min 14px text-sm) -->
          <div class="task-table-wrap overflow-x-auto">
            <table class="task-table w-full text-left text-sm border-collapse">
              <thead>
                <tr class="border-b border-slate-200 dark:border-slate-800 text-xs uppercase font-bold text-slate-400 bg-slate-50/50 dark:bg-slate-950/50">
                  <th class="py-3.5 px-4">STT</th>
                  <th class="py-3.5 px-4">Mã Task</th>
                  <th class="py-3.5 px-4">Tên công việc</th>
                  <th class="py-3.5 px-4">Người xử lý</th>
                  <th class="py-3.5 px-4">Trạng thái</th>
                  <th class="py-3.5 px-4">Sprint</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100 dark:divide-slate-800/60">
                @for (item of filteredItems(); track item.id; let i = $index) {
                  <tr
                    (click)="openDrawer(item)"
                    class="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition-colors group"
                  >
                    <td data-label="STT" class="py-3.5 px-4 font-mono text-slate-400 text-sm">{{ i + 1 }}</td>
                    <td data-label="Mã Task" class="py-3.5 px-4 font-mono font-bold text-primary text-sm">{{ item.issueKey }}</td>
                    <td data-label="Công việc" class="py-3.5 px-4 font-semibold text-slate-900 dark:text-white group-hover:text-primary transition-colors text-sm">
                      {{ item.title }}
                    </td>
                    <td data-label="Người xử lý" class="py-3.5 px-4 text-sm">
                      <div class="flex items-center gap-2 text-slate-700 dark:text-slate-300">
                        <div class="w-6 h-6 rounded-full bg-primary/20 text-primary flex items-center justify-center text-xs font-bold">
                          {{ (item.assigneeName || 'U')[0] }}
                        </div>
                        <span>{{ item.assigneeName }}</span>
                      </div>
                    </td>
                    <td data-label="Trạng thái" class="py-3.5 px-4">
                      <span
                        [class.bg-amber-100]="item.statusName === 'To Do'"
                        [class.text-amber-800]="item.statusName === 'To Do'"
                        [class.bg-blue-100]="item.statusName === 'In Progress'"
                        [class.text-blue-800]="item.statusName === 'In Progress'"
                        [class.bg-purple-100]="item.statusName === 'Code Review'"
                        [class.text-purple-800]="item.statusName === 'Code Review'"
                        [class.bg-emerald-100]="item.statusName === 'Done'"
                        [class.text-emerald-800]="item.statusName === 'Done'"
                        class="px-3 py-1 rounded-lg text-xs font-bold"
                      >
                        {{ item.statusName }}
                      </span>
                    </td>
                    <td data-label="Sprint" class="py-3.5 px-4 text-slate-500 font-medium text-sm">
                      {{ item.sprintName }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

        </div>

      </div>

    </div>
  `,
  styles: [`
    @media (max-width: 1279px) {
      .task-list-page, .task-list-layout, .task-list-panel, .task-table-wrap { min-width: 0; }
      .task-list-layout { grid-template-columns: minmax(0, 1fr); }
      .task-list-panel { grid-column: auto !important; }
      .task-list-filters { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .task-table { min-width: 760px; }
      .task-table-wrap { overscroll-behavior-inline: contain; scrollbar-width: thin; }
    }

    @media (max-width: 767px) {
      .task-list-page { gap: 1rem; }
      .task-list-header { align-items: stretch; }
      .task-list-header > div:last-child,
      .task-list-header button { width: 100%; }
      .task-list-header button { justify-content: center; min-height: 44px; }
      .task-list-layout { gap: 1rem; }
      .task-list-filters { display: flex; padding: .75rem; }
      .task-list-filters button { min-height: 42px; }
      .task-list-panel { padding: .75rem; }
      .task-table-wrap { overflow: visible; }
      .task-table { display: block; min-width: 0; width: 100%; }
      .task-table thead { display: none; }
      .task-table tbody { display: grid; gap: .75rem; }
      .task-table tr { display: grid; gap: 0; overflow: hidden; border: 1px solid #e2e8f0; border-radius: .75rem; background: #fff; }
      .task-table td { display: grid; grid-template-columns: 88px minmax(0, 1fr); align-items: center; gap: .75rem; min-width: 0; padding: .55rem .75rem; border: 0; border-bottom: 1px solid #f1f5f9; white-space: normal; overflow-wrap: anywhere; }
      .task-table td:last-child { border-bottom: 0; }
      .task-table td::before { content: attr(data-label); color: #64748b; font-size: .7rem; font-weight: 700; letter-spacing: .03em; text-transform: uppercase; }
      .task-table td > * { min-width: 0; }
    }
  `]
})
export class TaskListPageComponent {
  private readonly projectService = inject(ProjectManagementService);
  private readonly excelService = inject(ExcelDataService);

  readonly allWorkItems = this.projectService.workItems;
  selectedGroup = signal<'all' | 'my'>('all');
  searchTerm = '';

  filteredItems = computed(() => {
    let list = this.allWorkItems();
    if (this.selectedGroup() === 'my') {
      list = list.filter(i => i.assigneeName === 'Trần Văn Hoàng');
    }
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      list = list.filter(i => i.title.toLowerCase().includes(term) || i.issueKey.toLowerCase().includes(term));
    }
    return list;
  });

  openDrawer(task: WorkItem): void {
    this.projectService.activeDrawerTask.set(task);
  }

  exportExcel(): void {
    this.excelService.exportToExcel(this.filteredItems(), 'HUCE_Task_List_Export.csv');
  }
}
