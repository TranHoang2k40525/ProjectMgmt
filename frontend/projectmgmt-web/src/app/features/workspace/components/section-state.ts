import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { UI } from './ui-classes';

/** Khung trạng thái tải / lỗi cho một khu nội dung; chỉ hiện nội dung khi không tải và không lỗi. */
@Component({
  selector: 'app-section-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="space-y-2" aria-busy="true">
        <span class="sr-only">Đang tải…</span>
        @for (row of rowsArray(); track $index) {
          <div class="h-11 rounded-xl bg-slate-100 dark:bg-slate-800 motion-safe:animate-pulse"></div>
        }
      </div>
    } @else if (error()) {
      <div role="alert" class="flex flex-col sm:flex-row sm:items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">
        <span class="flex-1 text-pretty">{{ error() }}</span>
        <button type="button" [class]="ui.secondaryButton" (click)="retry.emit()">Thử lại</button>
      </div>
    } @else {
      <ng-content />
    }
  `
})
export class SectionStateComponent {
  readonly loading = input(false);
  readonly error = input('');
  readonly rows = input(3);
  readonly retry = output<void>();

  protected readonly ui = UI;

  rowsArray(): number[] {
    return Array.from({ length: this.rows() }, (_, index) => index);
  }
}
