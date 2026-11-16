import { ChangeDetectionStrategy, Component, OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { IdentityApi } from '../../../core/api/identity.api';
import { RequiredSkillDto, RequirementApi } from '../../../core/api/requirement.api';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ISSUE_SECTION_STYLES } from './issue-section.styles';

interface CatalogSkill { id: string; name: string; category?: string | null }

/** Kỹ năng yêu cầu (đầu vào AI phân công): chọn kỹ năng, mức tối thiểu 1–5, trọng số; nguồn chỉ đọc. */
@Component({
  selector: 'app-issue-required-skills',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [ISSUE_SECTION_STYLES, `
    ul { list-style: none; padding: 0; margin: 0 0 .5rem; }
    li { display: flex; flex-wrap: wrap; gap: .4rem; align-items: center; padding: .35rem 0; border-bottom: 1px solid #eef1f5; font-size: .9rem; }
    li strong { flex: 1 1 100%; }
    input[type=number], select.level { width: 4.5rem; }
    .src { font-size: .75rem; border-radius: 4px; padding: 0 .35rem; background: #eef1f5; color: #42526e; }
    .src.ai { background: #eae6ff; color: #403294; }
  `],
  template: `
    <h2>Kỹ năng yêu cầu</h2>
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
    <ul>
      @for (s of skills(); track s.id) {
        <li>
          <strong>{{ s.skillName || 'Kỹ năng đã xóa' }} <span class="src" [class.ai]="s.source === 'AI'">{{ sourceLabel(s.source) }}</span></strong>
          @if (canWrite()) {
            <label class="muted">Mức <select class="level" [ngModel]="s.minLevel" (ngModelChange)="update(s, $event, s.weight)" [disabled]="busy()">
              @for (l of levels; track l) { <option [ngValue]="l">{{ l }}</option> }</select></label>
            <label class="muted">Trọng số <input type="number" min="0.1" max="9.999" step="0.1" [ngModel]="s.weight" (change)="updateWeight(s, $event)" [disabled]="busy()" /></label>
            <button type="button" class="danger" (click)="remove(s)" [disabled]="busy()" [attr.aria-label]="'Bỏ kỹ năng ' + s.skillName">Bỏ</button>
          } @else {
            <span class="muted">Mức ≥ {{ s.minLevel }} · trọng số {{ s.weight }}</span>
          }
        </li>
      } @empty { <li class="muted">Chưa khai báo kỹ năng.</li> }
    </ul>
    @if (canWrite()) {
      <div class="row">
        <select [(ngModel)]="newSkillId" aria-label="Kỹ năng" [disabled]="busy()">
          <option value="">Chọn kỹ năng…</option>
          @for (c of available(); track c.id) { <option [value]="c.id">{{ c.name }}{{ c.category ? ' · ' + c.category : '' }}</option> }
        </select>
        <select class="level" [(ngModel)]="newLevel" aria-label="Mức tối thiểu">@for (l of levels; track l) { <option [ngValue]="l">{{ l }}</option> }</select>
        <button type="button" (click)="add()" [disabled]="busy() || !newSkillId">Thêm</button>
      </div>
    }
  `
})
export class IssueRequiredSkillsComponent implements OnChanges {
  readonly issueId = input.required<string>();
  readonly canWrite = input(true);
  readonly changed = output<void>();

  private readonly api = inject(RequirementApi);
  private readonly identity = inject(IdentityApi);
  readonly levels = [1, 2, 3, 4, 5];
  readonly skills = signal<RequiredSkillDto[]>([]);
  readonly catalog = signal<CatalogSkill[]>([]);
  readonly available = computed(() => this.catalog().filter(c => !this.skills().some(s => s.skillId === c.id)));
  readonly busy = signal(false);
  readonly error = signal('');
  newSkillId = '';
  newLevel = 1;

  ngOnChanges(): void {
    this.load();
    if (!this.catalog().length) {
      this.identity.getSkillCatalog().subscribe({
        next: result => this.catalog.set((result.items ?? [])
          .filter(item => item.skillId && item.isActive !== false)
          .map(item => ({ id: item.skillId!, name: item.name ?? '', category: item.category }))),
        error: e => this.fail(e)
      });
    }
  }

  load(): void { this.api.requiredSkills(this.issueId()).subscribe({ next: list => this.skills.set(list), error: e => this.fail(e) }); }

  sourceLabel(source: string): string { return source === 'AI' ? 'AI' : source === 'DerivedFromComponent' ? 'Từ Component' : 'Thủ công'; }

  add(): void {
    this.run(this.api.addRequiredSkill(this.issueId(), { skillId: this.newSkillId, minLevel: this.newLevel, weight: 1 }), () => { this.newSkillId = ''; this.newLevel = 1; });
  }

  update(skill: RequiredSkillDto, minLevel: number, weight: number): void {
    this.run(this.api.updateRequiredSkill(skill.id, { skillId: skill.skillId, minLevel, weight }));
  }

  updateWeight(skill: RequiredSkillDto, event: Event): void {
    const weight = Number((event.target as HTMLInputElement).value);
    if (!(weight > 0 && weight <= 9.999)) { this.error.set('Trọng số phải lớn hơn 0 và nhỏ hơn 10.'); this.load(); return; }
    this.update(skill, skill.minLevel, Math.round(weight * 1000) / 1000);
  }

  remove(skill: RequiredSkillDto): void { this.run(this.api.deleteRequiredSkill(skill.id)); }

  private run(request: Observable<unknown>, done?: () => void): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: () => { this.busy.set(false); done?.(); this.load(); this.changed.emit(); },
      error: e => { this.fail(e); this.load(); }
    });
  }

  private fail(error: unknown): void { this.busy.set(false); this.error.set(apiErrorMessage(error, 'Không lưu được kỹ năng yêu cầu.')); }
}
