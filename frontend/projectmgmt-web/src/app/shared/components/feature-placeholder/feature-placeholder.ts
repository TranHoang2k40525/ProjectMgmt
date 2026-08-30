import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-feature-placeholder',
  template: `
    <section class="card">
      <span class="eyebrow">Sprint 1 · Foundation</span>
      <h1>{{ title() }}</h1>
      <p>{{ description() }}</p>
      <div class="notice">Khung route đã sẵn sàng. Nghiệp vụ sẽ được triển khai ở sprint sở hữu module.</div>
    </section>
  `,
  styles: `
    .card { background: #fff; border: 1px solid #e5eaf0; border-radius: 16px; box-shadow: 0 12px 32px rgba(16,35,63,.06); max-width: 820px; padding: 42px; }
    .eyebrow { color: #087d67; font-size: .76rem; font-weight: 750; letter-spacing: .08em; text-transform: uppercase; }
    h1 { font-size: clamp(1.8rem, 4vw, 2.7rem); margin: 14px 0 12px; }
    p { color: #64748b; font-size: 1.05rem; line-height: 1.65; }
    .notice { background: #f1faf8; border-left: 3px solid #48c6a8; border-radius: 6px; color: #285c51; margin-top: 28px; padding: 14px 16px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeaturePlaceholderComponent {
  readonly title = input.required<string>();
  readonly description = input.required<string>();
}
