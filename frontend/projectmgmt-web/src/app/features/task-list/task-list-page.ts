import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProjectManagementService, WorkItem } from '../../core/services/project-management.service';
import { IdentityService } from '../../core/services/identity.service';
import { ExcelDataService } from '../../core/services/excel-data.service';
import { NoProjectStateComponent } from '../../shared/components/no-project-state/no-project-state.component';

@Component({
  selector: 'app-task-list-page',
  standalone: true,
  imports: [CommonModule, FormsModule, NoProjectStateComponent],
  template: `
    @if (!projectService.hasActiveProject()) {
      <app-no-project-state featureName="Danh Sách Công Việc"></app-no-project-state>
    } @else {
    <div class="task-list-page flex flex-col gap-3 md:gap-3.5 animate-fade-in font-body text-slate-900 dark:text-slate-100">
      
      <!-- Top Action Bar (Cố định phía trên khi cuộn bảng nội bộ) -->
      <div class="task-list-header shrink-0 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5 pb-2 border-b border-slate-200 dark:border-slate-800">
        <div class="flex flex-col">
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <span class="material-symbols-outlined text-[24px] text-primary">table_rows</span>
            Danh Sách Công Việc (Task List View)
          </h1>
          <p class="text-sm text-slate-500">Bảng danh sách công việc trực quan kiểu Bkav eTask & Jira List</p>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="exportExcel()" class="px-3 py-1.5 rounded-md bg-slate-900 hover:bg-slate-800 text-white font-medium text-xs transition-colors flex items-center gap-1.5 shadow-2xs cursor-pointer">
            <span class="material-symbols-outlined text-[16px]">download</span>
            Xuất Excel
          </button>
        </div>
      </div>

      <!-- Main Layout: Left Group Filter + Right Work Items Table -->
      <div class="task-list-layout flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-4 gap-3.5">
        
        <!-- Left Filter Panel -->
        <div class="task-list-filters min-h-0 p-3.5 rounded-md bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-2xs flex flex-col gap-3 select-none">
          <div class="flex flex-col gap-1">
            <span class="text-[11px] font-bold uppercase tracking-wider text-slate-400 px-2">Phân loại công việc</span>
            <nav class="flex flex-col gap-1">
              <button
                (click)="selectedGroup.set('all')"
                [class.bg-slate-100]="selectedGroup() === 'all'"
                [class.dark:bg-slate-800]="selectedGroup() === 'all'"
                [class.text-slate-900]="selectedGroup() === 'all'"
                [class.dark:text-white]="selectedGroup() === 'all'"
                class="flex items-center justify-between px-3 py-2 rounded-md text-xs font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors w-full text-left"
              >
                <div class="flex items-center gap-2">
                  <span class="material-symbols-outlined text-[18px]">history</span>
                  <span>Tất cả công việc</span>
                </div>
                <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300">
                  {{ allWorkItems().length }}
                </span>
              </button>

              <button
                (click)="selectedGroup.set('my')"
                [class.bg-slate-100]="selectedGroup() === 'my'"
                [class.dark:bg-slate-800]="selectedGroup() === 'my'"
                [class.text-slate-900]="selectedGroup() === 'my'"
                [class.dark:text-white]="selectedGroup() === 'my'"
                class="flex items-center justify-between px-3 py-2 rounded-md text-xs font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors w-full text-left"
              >
                <div class="flex items-center gap-2">
                  <span class="material-symbols-outlined text-[18px]">star</span>
                  <span>Tôi xử lý</span>
                </div>
              </button>
            </nav>
          </div>

          <div class="flex flex-col gap-1 border-t border-slate-200/80 dark:border-slate-800 pt-3">
            <span class="text-[11px] font-bold uppercase tracking-wider text-slate-400 px-2">Phân loại theo Epic</span>
            <nav class="flex flex-col gap-1">
              @for (epic of epics(); track epic.id) {
                <button
                  type="button"
                  (click)="toggleEpicFilter(epic.name)"
                  [class.bg-primary/10]="selectedEpic() === epic.name"
                  [class.dark:bg-primary/20]="selectedEpic() === epic.name"
                  [class.text-primary]="selectedEpic() === epic.name"
                  [class.dark:text-emerald-400]="selectedEpic() === epic.name"
                  class="flex items-center gap-2 px-3 py-1.5 rounded-md text-xs font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors cursor-pointer"
                >
                  <span class="w-2 h-2 rounded bg-primary shrink-0"></span>
                  <span class="truncate">{{ epic.name }}</span>
                </button>
              } @empty {
                <p class="text-[11px] text-slate-400 px-3 py-1">Chưa có Epic nào trong dự án</p>
              }
            </nav>
          </div>
        </div>

        <!-- Right Main Work Items Table (Thanh tìm kiếm & tiêu đề bảng luôn cố định, phần danh sách cuộn bên trong) -->
        <div class="task-list-panel lg:col-span-3 min-h-0 p-4 rounded-md bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-2xs flex flex-col gap-3 overflow-hidden">
          
          <!-- Search & Filter Controls -->
          <div class="task-list-controls shrink-0 flex items-center justify-between gap-3">
            <div class="relative flex-1">
              <span class="material-symbols-outlined absolute left-2.5 top-2 text-[18px] text-slate-400 pointer-events-none">search</span>
              <input
                type="text"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
                placeholder="Lọc công việc theo tên hoặc mã task..."
                class="w-full h-8 pl-8 pr-8 rounded-md bg-slate-50 dark:bg-slate-800 text-xs text-slate-800 dark:text-slate-200 focus:outline-none focus:border-slate-900 border border-slate-200 dark:border-slate-700"
              />
              @if (searchTerm()) {
                <button
                  type="button"
                  (click)="searchTerm.set('')"
                  title="Xóa từ khóa lọc"
                  aria-label="Xóa từ khóa lọc"
                  class="absolute right-1.5 top-1 w-6 h-6 rounded flex items-center justify-center text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-200/60 dark:hover:bg-slate-700/60 transition-colors cursor-pointer"
                >
                  <span class="material-symbols-outlined text-[15px]" aria-hidden="true">close</span>
                </button>
              }
            </div>
            <span class="hidden sm:inline-flex items-center px-2.5 h-8 rounded-md bg-slate-100 dark:bg-slate-800 text-[11px] font-semibold text-slate-600 dark:text-slate-300 whitespace-nowrap shrink-0">
              {{ filteredItems().length }} / {{ allWorkItems().length }} việc
            </span>
          </div>

          <!-- Internal Scrollable Table Container with Sticky Header -->
          <div class="task-table-wrap flex-1 min-h-0 overflow-auto rounded-md border border-slate-200/80 dark:border-slate-800">
            <table class="task-table w-full text-left text-xs border-separate border-spacing-0">
              <thead>
                <tr class="text-[11px] uppercase font-bold text-slate-500 dark:text-slate-400">
                  <th class="sticky top-0 z-10 bg-slate-50 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 py-2.5 px-3 whitespace-nowrap">STT</th>
                  <th class="sticky top-0 z-10 bg-slate-50 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 py-2.5 px-3 whitespace-nowrap">Mã Task</th>
                  <th class="sticky top-0 z-10 bg-slate-50 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 py-2.5 px-3 whitespace-nowrap min-w-[220px]">Tên công việc</th>
                  <th class="sticky top-0 z-10 bg-slate-50 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 py-2.5 px-3 whitespace-nowrap">Người xử lý</th>
                  <th class="sticky top-0 z-10 bg-slate-50 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 py-2.5 px-3 whitespace-nowrap">Trạng thái</th>
                  <th class="sticky top-0 z-10 bg-slate-50 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 py-2.5 px-3 whitespace-nowrap">Sprint</th>
                </tr>
              </thead>
              <tbody>
                @for (item of filteredItems(); track item.id; let i = $index) {
                  <tr
                    (click)="openDrawer(item)"
                    class="hover:bg-slate-50/80 dark:hover:bg-slate-800/40 cursor-pointer transition-colors group"
                  >
                    <td data-label="STT" class="py-2.5 px-3 border-b border-slate-100 dark:border-slate-800/60 font-mono text-slate-400 text-xs whitespace-nowrap">{{ i + 1 }}</td>
                    <td data-label="Mã Task" class="py-2.5 px-3 border-b border-slate-100 dark:border-slate-800/60 font-mono font-semibold text-xs whitespace-nowrap">
                      <div class="inline-flex items-center gap-2 whitespace-nowrap">
                        <span
                          class="w-6 h-6 rounded-md inline-flex items-center justify-center text-white shrink-0 shadow-2xs"
                          [style.backgroundColor]="getIssueTypeColor(item)"
                          [title]="item.issueType"
                        >
                          <span class="material-symbols-outlined text-[15px]">{{ getIssueTypeIcon(item) }}</span>
                        </span>
                        <span [style.color]="getIssueTypeColor(item)" class="font-bold whitespace-nowrap">{{ item.issueKey }}</span>
                      </div>
                    </td>
                    <td data-label="Công việc" class="py-2.5 px-3 border-b border-slate-100 dark:border-slate-800/60 font-medium text-slate-900 dark:text-white group-hover:text-primary transition-colors text-xs">
                      {{ item.title }}
                    </td>
                    <td data-label="Người xử lý" class="py-2.5 px-3 border-b border-slate-100 dark:border-slate-800/60 text-xs whitespace-nowrap">
                      <div class="flex items-center gap-1.5 text-slate-700 dark:text-slate-300">
                        @if (getAssigneeAvatar(item); as avatarUrl) {
                          <img [src]="avatarUrl" [alt]="item.assigneeName || 'Chưa gán'" class="w-5 h-5 rounded-full object-cover shrink-0 ring-1 ring-slate-200 dark:ring-slate-700" />
                        } @else {
                          <div class="w-5 h-5 rounded-full bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-200 flex items-center justify-center text-[10px] font-bold shrink-0">
                            {{ (item.assigneeName || 'U')[0] }}
                          </div>
                        }
                        <span class="truncate max-w-[135px]">{{ item.assigneeName }}</span>
                      </div>
                    </td>
                    <td data-label="Trạng thái" class="py-2.5 px-3 border-b border-slate-100 dark:border-slate-800/60 whitespace-nowrap">
                      <span
                        [class.bg-slate-100]="item.statusName === 'To Do'"
                        [class.text-slate-700]="item.statusName === 'To Do'"
                        [class.border-slate-300]="item.statusName === 'To Do'"
                        [class.bg-blue-50]="item.statusName === 'In Progress'"
                        [class.text-blue-700]="item.statusName === 'In Progress'"
                        [class.border-blue-200]="item.statusName === 'In Progress'"
                        [class.bg-teal-50]="item.statusName === 'Code Review'"
                        [class.text-teal-700]="item.statusName === 'Code Review'"
                        [class.border-teal-200]="item.statusName === 'Code Review'"
                        [class.bg-emerald-50]="item.statusName === 'Done'"
                        [class.text-emerald-700]="item.statusName === 'Done'"
                        [class.border-emerald-200]="item.statusName === 'Done'"
                        class="px-2 py-0.5 rounded text-[11px] font-medium border whitespace-nowrap inline-block"
                      >
                        {{ item.statusName }}
                      </span>
                    </td>
                    <td data-label="Sprint" class="py-2.5 px-3 border-b border-slate-100 dark:border-slate-800/60 text-slate-500 font-medium text-xs">
                      {{ item.sprintName }}
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="py-12 text-center text-slate-400">
                      <span class="material-symbols-outlined text-[36px] block mb-2 opacity-60">task</span>
                      <p class="text-sm font-medium">Không tìm thấy công việc nào phù hợp</p>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

        </div>

      </div>

    </div>
    }
  `,
  styles: [`
    :host {
      display: block;
      min-width: 0;
    }

    .task-table-wrap,
    .task-list-filters {
      scrollbar-width: thin;
      overscroll-behavior: contain;
    }

    .task-table thead th {
      box-shadow: inset 0 -1px 0 rgba(226, 232, 240, 0.9);
    }

    :host-context(.dark) .task-table thead th {
      box-shadow: inset 0 -1px 0 rgba(30, 41, 59, 0.95);
    }

    .task-table tbody tr:last-child td {
      border-bottom: 0;
    }

    @media (min-width: 768px) {
      .task-list-page {
        height: calc(100dvh - var(--app-shell-offset, 7rem) - 3rem);
        min-height: 460px;
        overflow: hidden;
      }

      .task-list-layout {
        min-height: 0;
        align-items: stretch;
      }

      .task-list-panel {
        height: 100%;
        min-height: 0;
      }
    }

    @media (min-width: 1280px) {
      .task-list-layout {
        grid-template-rows: minmax(0, 1fr);
      }

      .task-list-filters {
        height: 100%;
        overflow-y: auto;
      }
    }

    @media (min-width: 768px) and (max-width: 1279px) {
      .task-list-layout {
        grid-template-rows: auto minmax(0, 1fr);
      }

      .task-list-filters {
        max-height: 156px;
        overflow-y: auto;
      }
    }

    @media (max-width: 1279px) {
      .task-list-page, .task-list-layout, .task-list-panel, .task-table-wrap { min-width: 0; }
      .task-list-layout { grid-template-columns: minmax(0, 1fr); }
      .task-list-panel { grid-column: auto !important; }
      .task-list-filters { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .task-table { min-width: 760px; }
      .task-table-wrap { overscroll-behavior-inline: contain; scrollbar-width: thin; }
    }

    @media (max-width: 767px) {
      .task-list-page { height: auto; min-height: 0; overflow: visible; gap: 1rem; }
      .task-list-header { align-items: stretch; }
      .task-list-header > div:last-child,
      .task-list-header button { width: 100%; }
      .task-list-header button { justify-content: center; min-height: 44px; }
      .task-list-layout { flex: none; gap: 1rem; }
      .task-list-filters { display: flex; padding: .75rem; }
      .task-list-filters button { min-height: 42px; }
      .task-list-panel { height: auto; overflow: visible; padding: .75rem; }
      .task-table-wrap { flex: none; overflow: visible; border: 0; border-radius: 0; }
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
  readonly projectService = inject(ProjectManagementService);
  private readonly identityService = inject(IdentityService);
  private readonly excelService = inject(ExcelDataService);

  readonly allWorkItems = this.projectService.workItems;
  readonly epics = this.projectService.epics;
  readonly selectedGroup = signal<'all' | 'my'>('all');
  readonly selectedEpic = signal<string | null>(null);
  readonly searchTerm = signal<string>('');

  filteredItems = computed(() => {
    let list = this.allWorkItems();
    if (this.selectedGroup() === 'my') {
      const myName = this.identityService.authState().currentUser?.displayName;
      if (myName) {
        list = list.filter(i => i.assigneeName === myName);
      }
    }
    const epic = this.selectedEpic();
    if (epic) {
      list = list.filter(i => i.epicName === epic);
    }
    const q = this.searchTerm().trim();
    if (q) {
      const term = q.toLowerCase();
      list = list.filter(i => i.title.toLowerCase().includes(term) || i.issueKey.toLowerCase().includes(term));
    }
    return list;
  });

  toggleEpicFilter(epicName: string): void {
    if (this.selectedEpic() === epicName) {
      this.selectedEpic.set(null);
    } else {
      this.selectedEpic.set(epicName);
    }
  }

  openDrawer(task: WorkItem): void {
    this.projectService.activeDrawerTask.set(task);
  }

  getAssigneeAvatar(item: WorkItem): string | null {
    return this.projectService.getMemberAvatar(item.assigneeName, item.assigneeAvatar);
  }

  getIssueTypeIcon(item: WorkItem): string {
    const typeName = item.issueType || '';
    const custom = this.projectService.customIssueTypes().find(t =>
      (item.issueTypeId && t.id === item.issueTypeId) ||
      t.name.toLowerCase() === typeName.toLowerCase() ||
      ((typeName.toLowerCase() === 'sub-task' || typeName.toLowerCase() === 'subtask') && (t.isSubtask || t.name.toLowerCase() === 'subtask' || t.name.toLowerCase() === 'sub-task'))
    );
    if (custom?.icon && custom.icon !== 'task') return custom.icon;
    if (item.issueTypeIcon && item.issueTypeIcon !== 'task') return item.issueTypeIcon;
    const map: Record<string, string> = {
      'story': 'bookmark',
      'task': 'task_alt',
      'bug': 'bug_report',
      'epic': 'bolt',
      'sub-task': 'subdirectory_arrow_right',
      'subtask': 'subdirectory_arrow_right'
    };
    return map[typeName.toLowerCase()] || 'task_alt';
  }

  getIssueTypeColor(item: WorkItem): string {
    const typeName = item.issueType || '';
    const custom = this.projectService.customIssueTypes().find(t =>
      (item.issueTypeId && t.id === item.issueTypeId) ||
      t.name.toLowerCase() === typeName.toLowerCase() ||
      ((typeName.toLowerCase() === 'sub-task' || typeName.toLowerCase() === 'subtask') && (t.isSubtask || t.name.toLowerCase() === 'subtask' || t.name.toLowerCase() === 'sub-task'))
    );
    if (custom?.color) return custom.color;
    if (item.issueTypeColor) return item.issueTypeColor;
    const map: Record<string, string> = {
      'story': '#65BA43',
      'task': '#4BADE8',
      'bug': '#E5493A',
      'epic': '#904EE2',
      'sub-task': '#4BADE8',
      'subtask': '#4BADE8'
    };
    return map[typeName.toLowerCase()] || '#4BADE8';
  }

  exportExcel(): void {
    const proj = this.projectService.currentProject();
    const projectKey = proj.projectKey || 'PROJECT';
    const dateStr = new Date().toISOString().slice(0, 10).replace(/-/g, '');
    this.excelService.exportToExcel(
      this.filteredItems(),
      `${projectKey}_Task_List_Export_${dateStr}.xlsx`,
      this.projectService.sprints(),
      proj.name || projectKey
    );
  }
}
