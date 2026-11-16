import { ChangeDetectionStrategy, Component, OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { LabelDto, LabelLinkApi } from '../../../core/api/label-link.api';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ISSUE_SECTION_STYLES } from './issue-section.styles';

/** Chọn nhiều nhãn của Project cho Issue; gõ tên chưa có để tạo nhãn mới. */
@Component({
  selector: 'app-issue-labels',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [ISSUE_SECTION_STYLES, `
    .chip { display: inline-flex; align-items: center; gap: .3rem; border-radius: 999px; padding: .15rem .6rem; font-size: .85rem; border: 1px solid #c1c7d0; }
    .chip button { background: none; color: inherit; padding: 0 .2rem; font-weight: 700; }
    .dot { width: .6rem; height: .6rem; border-radius: 50%; display: inline-block; }
  `],
  template: `
    <h2>Nhãn</h2>
    <div class="row">
      @for (label of labels(); track label.id) {
        <span class="chip"><span class="dot" [style.background]="label.colorHex"></span>{{ label.name }}
          @if (canWrite()) { <button type="button" (click)="remove(label)" [disabled]="busy()" [attr.aria-label]="'Gỡ nhãn ' + label.name">×</button> }
        </span>
      } @empty { <span class="muted">Chưa gắn nhãn.</span> }
    </div>
    @if (canWrite()) {
      <div class="row" style="margin-top:.5rem">
        <input list="project-labels" [(ngModel)]="draft" maxlength="60" placeholder="Thêm nhãn…" aria-label="Tên nhãn" (keydown.enter)="add()" />
        <datalist id="project-labels">@for (label of available(); track label.id) { <option [value]="label.name"></option> }</datalist>
        <button type="button" (click)="add()" [disabled]="busy() || !draft.trim()">{{ isNew() ? 'Tạo & gắn' : 'Gắn nhãn' }}</button>
      </div>
    }
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
  `
})
export class IssueLabelsComponent implements OnChanges {
  readonly issueId = input.required<string>();
  readonly projectId = input.required<string>();
  readonly canWrite = input(true);
  readonly changed = output<void>();

  private readonly api = inject(LabelLinkApi);
  readonly labels = signal<LabelDto[]>([]);
  readonly projectLabels = signal<LabelDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly draftValue = signal('');
  readonly available = computed(() => this.projectLabels().filter(p => !this.labels().some(l => l.id === p.id)));
  readonly isNew = computed(() => {
    const name = this.draftValue().trim().toLowerCase();
    return !!name && !this.projectLabels().some(label => label.name.toLowerCase() === name);
  });

  get draft(): string { return this.draftValue(); }
  set draft(value: string) { this.draftValue.set(value); }

  ngOnChanges(): void {
    this.api.issueLabels(this.issueId()).subscribe({ next: labels => this.labels.set(labels), error: e => this.fail(e) });
    this.api.projectLabels(this.projectId()).subscribe({ next: labels => this.projectLabels.set(labels), error: e => this.fail(e) });
  }

  add(): void {
    const name = this.draft.trim();
    if (!name || this.busy()) return;
    const existing = this.projectLabels().find(label => label.name.toLowerCase() === name.toLowerCase());
    if (existing) { this.attach(existing.id); return; }
    this.busy.set(true);
    this.api.createLabel(this.projectId(), name).subscribe({
      next: label => { this.projectLabels.update(all => [...all, label]); this.busy.set(false); this.attach(label.id); },
      error: e => this.fail(e)
    });
  }

  remove(label: LabelDto): void { this.run(this.api.removeLabel(this.issueId(), label.id)); }

  private attach(labelId: string): void { this.run(this.api.addLabel(this.issueId(), labelId), () => this.draft = ''); }

  private run(request: Observable<LabelDto[]>, done?: () => void): void {
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: labels => { this.labels.set(labels); this.busy.set(false); done?.(); this.changed.emit(); },
      error: e => this.fail(e)
    });
  }

  private fail(error: unknown): void { this.busy.set(false); this.error.set(apiErrorMessage(error, 'Không lưu được nhãn.')); }
}
