import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RecentItem } from '../data/workspace.models';
import { RECENT_ICONS, RECENT_KIND_LABELS, timeAgo } from '../work-display';

@Component({
  selector: 'app-recent-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (items().length > 0) {
      <ul class="space-y-1">
        @for (item of items(); track item.kind + item.id) {
          <li>
            <button type="button" (click)="selected.emit(item)"
                    class="w-full flex items-center gap-3 rounded-lg px-2 py-2 text-left hover:bg-slate-50 dark:hover:bg-slate-800/60 focus-visible:outline-2 focus-visible:outline-primary-container">
              <span class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary-fixed text-primary dark:bg-slate-800 dark:text-emerald-300">
                <span class="material-symbols-outlined text-[18px]" aria-hidden="true">{{ icons[item.kind] }}</span>
              </span>
              <span class="min-w-0 flex-1">
                <span class="block truncate text-sm font-medium text-slate-900 dark:text-slate-100">{{ item.title }}</span>
                <span class="block truncate text-xs text-slate-500">
                  <span class="sr-only">{{ kindLabels[item.kind] }}: </span>{{ item.subtitle }}
                </span>
              </span>
              <span class="shrink-0 text-[11px] text-slate-400 tabular-nums">{{ ago(item) }}</span>
            </button>
          </li>
        }
      </ul>
    } @else {
      <p class="rounded-xl border border-dashed border-slate-300 dark:border-slate-700 px-4 py-6 text-sm text-slate-500 text-center text-pretty">
        {{ emptyText() }}
      </p>
    }
  `
})
export class RecentListComponent {
  readonly items = input.required<RecentItem[]>();
  readonly emptyText = input('Tổ chức, dự án và công việc bạn mở sẽ hiện ở đây.');
  readonly selected = output<RecentItem>();

  protected readonly icons = RECENT_ICONS;
  protected readonly kindLabels = RECENT_KIND_LABELS;

  ago(item: RecentItem): string {
    return timeAgo(item.visitedAt);
  }
}
