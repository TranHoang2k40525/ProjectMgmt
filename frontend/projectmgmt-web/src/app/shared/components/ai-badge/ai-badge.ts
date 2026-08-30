import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-ai-badge',
  template: '<span title="Nội dung có AI hỗ trợ">AI</span>',
  styles: `span{background:linear-gradient(135deg,#6b63ff,#3f9de8);border-radius:5px;color:#fff;font-size:.65rem;font-weight:800;letter-spacing:.06em;padding:3px 6px}`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AiBadgeComponent {}
