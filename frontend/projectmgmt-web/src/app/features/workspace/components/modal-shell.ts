import { A11yModule } from '@angular/cdk/a11y';
import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, inject, input, output } from '@angular/core';

let nextModalId = 0;

/**
 * Khung modal dùng chung: giữ focus trong hộp thoại, Esc hoặc bấm nền để đóng, trả focus về nút đã mở khi đóng.
 * Nội dung đặt vào thân; nút hành động đặt vào `[modal-footer]`.
 * Phần tử được chuyển ra `document.body` vì vùng nội dung trang có transform (hiệu ứng chuyển trang),
 * nếu để trong đó thì `position: fixed` bị tính theo vùng nội dung thay vì cửa sổ.
 */
@Component({
  selector: 'app-modal-shell',
  imports: [A11yModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'requestClose()' },
  template: `
    <div class="fixed inset-0 z-[60] flex items-end sm:items-center justify-center p-0 sm:p-4">
      <button type="button" tabindex="-1" aria-hidden="true" class="absolute inset-0 w-full h-full bg-slate-900/50 border-0 cursor-default"
              (click)="requestClose()"></button>
      <div role="dialog" aria-modal="true" [attr.aria-labelledby]="titleId" [attr.aria-describedby]="description() ? descriptionId : null"
           cdkTrapFocus [cdkTrapFocusAutoCapture]="true"
           class="relative z-10 flex max-h-[92dvh] w-full flex-col overflow-hidden rounded-t-2xl sm:rounded-2xl bg-white dark:bg-slate-900 shadow-xl border border-slate-200 dark:border-slate-800"
           [class]="size() === 'lg' ? 'sm:max-w-3xl' : 'sm:max-w-lg'">
        <header class="flex items-start justify-between gap-4 border-b border-slate-100 dark:border-slate-800 px-5 py-4">
          <div class="min-w-0">
            <h2 [id]="titleId" class="text-lg font-bold text-slate-900 dark:text-white text-balance">{{ heading() }}</h2>
            @if (description()) {
              <p [id]="descriptionId" class="mt-0.5 text-sm text-slate-500 text-pretty">{{ description() }}</p>
            }
          </div>
          <button type="button" (click)="requestClose()" aria-label="Đóng"
                  class="flex size-8 shrink-0 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800">
            <span class="material-symbols-outlined text-[20px]" aria-hidden="true">close</span>
          </button>
        </header>
        <div class="flex-1 overflow-y-auto px-5 py-4">
          <ng-content />
        </div>
        <footer class="flex flex-wrap items-center justify-end gap-2 border-t border-slate-100 dark:border-slate-800 px-5 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
          <ng-content select="[modal-footer]" />
        </footer>
      </div>
    </div>
  `
})
export class ModalShellComponent {
  readonly heading = input.required<string>();
  readonly description = input<string>('');
  readonly size = input<'md' | 'lg'>('md');
  readonly closeRequested = output<void>();

  private readonly id = nextModalId++;
  readonly titleId = `modal-title-${this.id}`;
  readonly descriptionId = `modal-desc-${this.id}`;

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    inject(DOCUMENT).body.appendChild(host);
    inject(DestroyRef).onDestroy(() => host.remove());
  }

  requestClose(): void {
    this.closeRequested.emit();
  }
}
