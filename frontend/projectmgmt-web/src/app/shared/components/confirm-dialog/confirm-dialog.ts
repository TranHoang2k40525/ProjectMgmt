import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'app-confirm-dialog',
  template: `
    <section role="alertdialog" aria-modal="true" [attr.aria-label]="title()">
      <h2>{{ title() }}</h2><p>{{ message() }}</p>
      <div><button type="button" (click)="cancelled.emit()">Hủy</button><button type="button" class="primary" (click)="confirmed.emit()">Xác nhận</button></div>
    </section>
  `,
  styles: `section{background:#fff;border:1px solid #dfe6ee;border-radius:12px;max-width:460px;padding:24px}div{display:flex;gap:8px;justify-content:flex-end}button{border:1px solid #cad4df;border-radius:7px;padding:8px 14px}.primary{background:#087d67;color:#fff}`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConfirmDialogComponent {
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();
}
