import { ChangeDetectionStrategy, Component, OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { AcceptanceCriterionDto, RequirementApi, moveItem } from '../../../core/api/requirement.api';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { ISSUE_SECTION_STYLES } from './issue-section.styles';

/** Acceptance Criteria: thêm, sửa, tick đạt (ghi người/thời điểm), xóa có xác nhận, sắp xếp bằng nút lên/xuống. */
@Component({
  selector: 'app-issue-criteria',
  imports: [FormsModule, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [ISSUE_SECTION_STYLES, `
    ol { list-style: none; padding: 0; margin: 0 0 .5rem; }
    li { display: grid; grid-template-columns: auto 1fr auto; gap: .6rem; align-items: start; padding: .45rem 0; border-bottom: 1px solid #eef1f5; }
    .met { text-decoration: line-through; color: #5e6c84; }
    .ai { background: #eae6ff; color: #403294; border-radius: 4px; padding: 0 .35rem; font-size: .75rem; font-weight: 600; margin-left: .35rem; }
    .tools { display: flex; gap: .15rem; flex-wrap: wrap; }
    .progress { height: .4rem; background: #eef1f5; border-radius: 999px; overflow: hidden; margin-bottom: .6rem; }
    .progress span { display: block; height: 100%; background: #36b37e; }
  `],
  template: `
    <h2>Tiêu chí nghiệm thu <span class="muted">({{ metCount() }}/{{ criteria().length }})</span></h2>
    @if (criteria().length) {
      <div class="progress" role="progressbar" aria-label="Tỉ lệ tiêu chí đã đạt" [attr.aria-valuenow]="percent()" aria-valuemin="0" aria-valuemax="100"><span [style.width.%]="percent()"></span></div>
    }
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
    <ol>
      @for (c of criteria(); track c.id; let i = $index; let last = $last) {
        <li>
          <input type="checkbox" [checked]="c.isMet" (change)="toggle(c)" [disabled]="busy() || !canWrite()" [attr.aria-label]="'Đánh dấu đạt: ' + c.content" />
          <div>
            @if (editingId() === c.id) {
              <textarea [(ngModel)]="editDraft" rows="2" maxlength="4000" aria-label="Sửa tiêu chí"></textarea>
              <div class="row"><button type="button" (click)="saveEdit(c)" [disabled]="busy() || !editDraft.trim()">Lưu</button><button type="button" class="link" (click)="editingId.set(null)">Hủy</button></div>
            } @else {
              <span class="preserve" [class.met]="c.isMet">{{ c.content }}</span>
              @if (c.source === 'AI') { <span class="ai" [title]="c.wasEditedAfterAi ? 'Do AI gợi ý, đã được chỉnh sửa' : 'Do AI gợi ý'">AI{{ c.wasEditedAfterAi ? ' · đã sửa' : '' }}</span> }
              @if (c.isMet && c.metAt) { <br /><span class="muted">Đạt bởi {{ c.metByName || 'không rõ' }} · {{ c.metAt | date:'dd/MM/yyyy HH:mm' }}</span> }
            }
          </div>
          @if (canWrite() && editingId() !== c.id) {
            <div class="tools">
              <button type="button" class="link" (click)="move(i, -1)" [disabled]="busy() || i === 0" [attr.aria-label]="'Đưa lên: ' + c.content">↑</button>
              <button type="button" class="link" (click)="move(i, 1)" [disabled]="busy() || last" [attr.aria-label]="'Đưa xuống: ' + c.content">↓</button>
              <button type="button" class="link" (click)="startEdit(c)">Sửa</button>
              <button type="button" class="danger" (click)="remove(c)" [disabled]="busy()">Xóa</button>
            </div>
          }
        </li>
      } @empty { <li class="muted" style="display:block">Chưa có tiêu chí nghiệm thu.</li> }
    </ol>
    @if (canWrite()) {
      <div class="row"><input [(ngModel)]="draft" maxlength="4000" placeholder="Thêm tiêu chí…" aria-label="Tiêu chí mới" (keydown.enter)="add()" style="flex:1" />
        <button type="button" (click)="add()" [disabled]="busy() || !draft.trim()">Thêm</button></div>
    }
  `
})
export class IssueCriteriaComponent implements OnChanges {
  readonly issueId = input.required<string>();
  readonly canWrite = input(true);
  readonly changed = output<void>();

  private readonly api = inject(RequirementApi);
  private readonly confirm = inject(ConfirmDialogService);
  readonly criteria = signal<AcceptanceCriterionDto[]>([]);
  readonly metCount = computed(() => this.criteria().filter(c => c.isMet).length);
  readonly percent = computed(() => this.criteria().length ? Math.round(this.metCount() * 100 / this.criteria().length) : 0);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly editingId = signal<string | null>(null);
  draft = '';
  editDraft = '';

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.criteria(this.issueId()).subscribe({ next: list => this.criteria.set(list), error: e => this.fail(e) });
  }

  add(): void { if (this.draft.trim()) this.run(this.api.addCriterion(this.issueId(), this.draft.trim()), () => this.draft = ''); }
  toggle(c: AcceptanceCriterionDto): void { this.run(this.api.updateCriterion(c.id, c.content, !c.isMet)); }
  startEdit(c: AcceptanceCriterionDto): void { this.editingId.set(c.id); this.editDraft = c.content; }
  saveEdit(c: AcceptanceCriterionDto): void { this.run(this.api.updateCriterion(c.id, this.editDraft.trim(), c.isMet), () => this.editingId.set(null)); }

  move(index: number, offset: -1 | 1): void {
    const ordered = moveItem(this.criteria(), index, offset);
    this.run(this.api.reorderCriteria(this.issueId(), ordered.map(c => c.id)));
  }

  remove(c: AcceptanceCriterionDto): void {
    this.confirm.confirm({
      title: 'Xóa tiêu chí', message: `Xóa tiêu chí "${c.content.slice(0, 80)}"?`, type: 'danger', confirmText: 'Xóa',
      onConfirm: () => this.run(this.api.deleteCriterion(c.id))
    });
  }

  private run(request: Observable<unknown>, done?: () => void): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: () => { this.busy.set(false); done?.(); this.load(); this.changed.emit(); },
      error: e => this.fail(e)
    });
  }

  private fail(error: unknown): void { this.busy.set(false); this.error.set(apiErrorMessage(error, 'Không lưu được tiêu chí.')); }
}
