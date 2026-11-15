import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { RecentItem } from '../data/workspace.models';
import { RECENT_ICONS, RECENT_KIND_LABELS, timeAgo } from '../work-display';

@Component({
  selector: 'app-recent-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (items().length > 0) {
      <div class="space-y-3">
        <ul class="space-y-1">
          @for (item of paginatedItems(); track item.kind + item.id) {
            <li>
              <button type="button" (click)="selected.emit(item)" [title]="item.title + ' — ' + item.subtitle"
                      class="w-full flex items-center gap-3 rounded-lg px-2 py-2 text-left hover:bg-slate-50 dark:hover:bg-slate-800/60 focus-visible:outline-2 focus-visible:outline-primary-container transition-colors cursor-pointer">
                <span class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary-fixed text-primary dark:bg-slate-800 dark:text-emerald-300">
                  <span class="material-symbols-outlined text-[18px]" aria-hidden="true">{{ icons[item.kind] }}</span>
                </span>
                <span class="min-w-0 flex-1">
                  <span class="block truncate text-sm font-medium text-slate-900 dark:text-slate-100">{{ item.title }}</span>
                  <span class="block truncate text-xs text-slate-500">
                    <span class="sr-only">{{ kindLabels[item.kind] }}: </span>{{ item.subtitle }}
                  </span>
                </span>
                <span class="shrink-0 text-[11px] text-slate-500 tabular-nums">{{ ago(item) }}</span>
              </button>
            </li>
          }
        </ul>

        <!-- THANH PHÂN TRANG & CHỌN SỐ LƯỢNG MỤC (PAGINATION FOOTER) -->
        <div class="pt-2.5 border-t border-slate-100 dark:border-slate-800 flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500">
          <div class="flex items-center gap-1.5 shrink-0">
            <span class="text-slate-500">Hiển thị</span>
            <select [value]="pageSize()" (change)="onPageSizeChange($event)"
                    aria-label="Số lượng mục gần đây hiển thị mỗi trang"
                    class="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs px-2 py-1 font-semibold text-slate-800 dark:text-slate-200 shadow-2xs hover:border-slate-300 dark:hover:border-slate-600 focus:border-primary focus:outline-none cursor-pointer">
              @for (opt of pageSizeOptions; track opt) {
                <option [value]="opt">{{ opt }}</option>
              }
            </select>
            <span class="text-slate-400 font-normal">({{ startIndex() }}-{{ endIndex() }} / {{ totalItems() }})</span>
          </div>

          @if (totalPages() > 1) {
            <div class="flex items-center gap-1 shrink-0">
              <button type="button" (click)="prevPage()" [disabled]="safePage() === 1"
                      class="flex items-center justify-center w-7 h-7 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 disabled:opacity-25 disabled:hover:bg-transparent transition-colors cursor-pointer disabled:cursor-not-allowed"
                      title="Trang trước" aria-label="Trang trước">
                <span class="material-symbols-outlined text-[18px]">chevron_left</span>
              </button>

              @for (p of pageNumbers(); track p) {
                <button type="button" (click)="goToPage(p)"
                        class="min-w-[28px] h-7 px-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer"
                        [class.bg-primary]="safePage() === p"
                        [class.text-white]="safePage() === p"
                        [class.shadow-2xs]="safePage() === p"
                        [class.text-slate-700]="safePage() !== p"
                        [class.dark:text-slate-300]="safePage() !== p"
                        [class.hover:bg-slate-100]="safePage() !== p"
                        [class.dark:hover:bg-slate-800]="safePage() !== p"
                        [attr.aria-label]="'Đến trang ' + p"
                        [attr.aria-current]="safePage() === p ? 'page' : null">
                  {{ p }}
                </button>
              }

              <button type="button" (click)="nextPage()" [disabled]="safePage() === totalPages()"
                      class="flex items-center justify-center w-7 h-7 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 disabled:opacity-25 disabled:hover:bg-transparent transition-colors cursor-pointer disabled:cursor-not-allowed"
                      title="Trang tiếp theo" aria-label="Trang tiếp theo">
                <span class="material-symbols-outlined text-[18px]">chevron_right</span>
              </button>
            </div>
          }
        </div>
      </div>
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

  readonly pageSizeOptions = [10, 20, 30, 50, 100];
  readonly pageSize = signal<number>(10);
  readonly currentPage = signal<number>(1);

  readonly totalItems = computed(() => this.items().length);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalItems() / this.pageSize())));

  readonly safePage = computed(() => {
    const cur = this.currentPage();
    const max = this.totalPages();
    if (cur > max) return max;
    if (cur < 1) return 1;
    return cur;
  });

  readonly paginatedItems = computed(() => {
    const page = this.safePage();
    const size = this.pageSize();
    const start = (page - 1) * size;
    return this.items().slice(start, start + size);
  });

  readonly startIndex = computed(() => {
    if (this.totalItems() === 0) return 0;
    return (this.safePage() - 1) * this.pageSize() + 1;
  });

  readonly endIndex = computed(() => {
    return Math.min(this.safePage() * this.pageSize(), this.totalItems());
  });

  readonly pageNumbers = computed(() => {
    const total = this.totalPages();
    const current = this.safePage();
    if (total <= 5) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }
    let start = Math.max(1, current - 2);
    let end = Math.min(total, current + 2);
    if (start === 1) end = Math.min(total, 5);
    if (end === total) start = Math.max(1, total - 4);
    const pages: number[] = [];
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  });

  onPageSizeChange(event: Event): void {
    const target = event.target as HTMLSelectElement;
    const val = parseInt(target.value, 10);
    if (!isNaN(val) && this.pageSizeOptions.includes(val)) {
      this.pageSize.set(val);
      this.currentPage.set(1);
    }
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  prevPage(): void {
    if (this.safePage() > 1) {
      this.currentPage.set(this.safePage() - 1);
    }
  }

  nextPage(): void {
    if (this.safePage() < this.totalPages()) {
      this.currentPage.set(this.safePage() + 1);
    }
  }

  ago(item: RecentItem): string {
    return timeAgo(item.visitedAt);
  }
}
