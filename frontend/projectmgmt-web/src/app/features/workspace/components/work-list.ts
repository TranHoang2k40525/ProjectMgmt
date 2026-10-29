import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { WorkItemSummary } from '../data/workspace.models';
import { formatDueDate, groupWorkByDue, statusTone } from '../work-display';

/** Việc được giao, nhóm theo hạn. `showOrg` để hiện tên tổ chức trên trang chủ. */
@Component({
  selector: 'app-work-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (bucket of buckets(); track bucket.key) {
      <section class="mb-4 last:mb-0" [attr.aria-labelledby]="headingId + bucket.key">
        <h3 [id]="headingId + bucket.key" class="flex items-center gap-2 text-xs font-bold uppercase tracking-wide mb-2"
            [class.text-red-700]="bucket.key === 'overdue'" [class.text-slate-500]="bucket.key !== 'overdue'">
          {{ bucket.label }}
          <span class="tabular-nums rounded-full bg-slate-100 dark:bg-slate-800 px-2 py-0.5 text-[11px] text-slate-600 dark:text-slate-300">{{ bucket.items.length }}</span>
        </h3>
        <ul class="divide-y divide-slate-100 dark:divide-slate-800 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          @for (item of bucket.items; track item.id) {
            <li>
              <button type="button" (click)="selected.emit(item)"
                      class="w-full text-left flex flex-col sm:flex-row sm:items-center gap-1 sm:gap-3 px-3 py-2.5 hover:bg-slate-50 dark:hover:bg-slate-800/60 focus-visible:outline-2 focus-visible:outline-primary-container rounded-xl">
                <span class="font-mono text-xs font-semibold text-slate-500 shrink-0 tabular-nums">{{ item.issueKey }}</span>
                <span class="flex-1 min-w-0 text-sm font-medium text-slate-900 dark:text-slate-100 truncate">{{ item.title }}</span>
                <span class="flex items-center gap-2 text-xs text-slate-500 shrink-0">
                  <span class="truncate max-w-[12rem]">{{ showOrg() ? item.orgName + ' · ' : '' }}{{ item.projectName }}</span>
                  <span class="rounded border px-1.5 py-0.5 font-medium" [class]="tone(item)">{{ item.statusName }}</span>
                  <span class="tabular-nums" [class.text-red-700]="bucket.key === 'overdue'" [class.font-semibold]="bucket.key === 'overdue'">
                    <span class="sr-only">Hạn: </span>{{ due(item) }}
                  </span>
                </span>
              </button>
            </li>
          }
        </ul>
      </section>
    } @empty {
      <p class="rounded-xl border border-dashed border-slate-300 dark:border-slate-700 px-4 py-6 text-sm text-slate-500 text-center text-pretty">
        {{ emptyText() }}
      </p>
    }
  `
})
export class WorkListComponent {
  readonly items = input.required<WorkItemSummary[]>();
  readonly showOrg = input(false);
  readonly emptyText = input('Bạn không có việc nào đang chờ. Việc được giao sẽ hiện ở đây.');
  readonly selected = output<WorkItemSummary>();

  readonly headingId = `work-${Math.random().toString(36).slice(2, 8)}-`;
  readonly buckets = computed(() => groupWorkByDue(this.items()));

  tone(item: WorkItemSummary): string {
    return statusTone(item.statusCategory);
  }

  due(item: WorkItemSummary): string {
    return formatDueDate(item.dueDate);
  }
}
