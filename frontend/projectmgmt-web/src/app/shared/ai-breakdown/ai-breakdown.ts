import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'app-ai-breakdown',
  template: '<button type="button" [disabled]="disabled()" (click)="requested.emit()">AI Breakdown</button>',
  styles: `button{background:#5e63e8;border:0;border-radius:8px;color:#fff;font-weight:700;padding:9px 14px}button:disabled{opacity:.5}`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AiBreakdownComponent {
  readonly disabled = input(false);
  readonly requested = output<void>();
}
