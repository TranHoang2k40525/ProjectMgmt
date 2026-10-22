import { ChangeDetectionStrategy, Component, OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ISSUE_LINK_TYPES, IssueLinkDto, IssueLinkType, LabelLinkApi } from '../../../core/api/label-link.api';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { ProjectManagementService } from '../../../core/services/project-management.service';
import { ISSUE_SECTION_STYLES } from './issue-section.styles';

/** "Linked work items": chọn loại liên kết, tìm Issue đích trong cùng Project, hiển thị các liên kết hiện có. */
@Component({
  selector: 'app-issue-links',
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [ISSUE_SECTION_STYLES, `ul { list-style: none; padding: 0; } li { display: flex; gap: .6rem; align-items: center; padding: .35rem 0; border-bottom: 1px solid #eef1f5; flex-wrap: wrap; } a { color: #1559a1; text-decoration: underline; } .type { min-width: 9rem; color: #5e6c84; }`],
  template: `
    <h2>Liên kết công việc</h2>
    <ul>
      @for (link of links(); track link.id) {
        <li>
          <span class="type">{{ typeLabel(link.linkType) }}</span>
          <a [routerLink]="['/issues', link.targetIssueId]">{{ key(link.targetIssueNumber) }} · {{ link.targetTitle }}</a>
          <span class="muted">{{ statusName(link.targetStatusId) }}</span>
          @if (canWrite()) { <button type="button" class="danger" (click)="remove(link)" [disabled]="busy()">Gỡ</button> }
        </li>
      } @empty { <li class="muted">Chưa có liên kết.</li> }
    </ul>
    @if (canWrite()) {
      <div class="row">
        <select [(ngModel)]="type" aria-label="Loại liên kết">@for (t of types; track t.value) { <option [value]="t.value">{{ t.label }}</option> }</select>
        <input [(ngModel)]="search" placeholder="Tìm theo mã hoặc tiêu đề…" aria-label="Tìm Issue đích" />
        <select [(ngModel)]="targetId" aria-label="Issue đích">
          <option value="">Chọn Issue…</option>
          @for (item of candidates(); track item.id) { <option [value]="item.id">{{ item.issueKey }} · {{ item.title }}</option> }
        </select>
        <button type="button" (click)="add()" [disabled]="busy() || !targetId">Liên kết</button>
      </div>
    }
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
  `
})
export class IssueLinksComponent implements OnChanges {
  readonly issueId = input.required<string>();
  readonly canWrite = input(true);
  readonly changed = output<void>();

  private readonly api = inject(LabelLinkApi);
  private readonly projects = inject(ProjectManagementService);
  private readonly confirm = inject(ConfirmDialogService);
  readonly types = ISSUE_LINK_TYPES;
  readonly links = signal<IssueLinkDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly searchValue = signal('');
  type: IssueLinkType = 'Relates';
  targetId = '';

  get search(): string { return this.searchValue(); }
  set search(value: string) { this.searchValue.set(value); }

  readonly candidates = computed(() => {
    const text = this.searchValue().trim().toLowerCase();
    const linked = new Set(this.links().map(link => link.targetIssueId));
    return this.projects.workItems()
      .filter(item => item.id !== this.issueId() && !linked.has(item.id))
      .filter(item => !text || item.issueKey.toLowerCase().includes(text) || item.title.toLowerCase().includes(text))
      .slice(0, 50);
  });

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.links(this.issueId()).subscribe({ next: links => this.links.set(links), error: e => this.fail(e) });
  }

  key(issueNumber: number): string { return `${this.projects.currentProject().projectKey}-${issueNumber}`; }
  typeLabel(type: IssueLinkType): string { return ISSUE_LINK_TYPES.find(t => t.value === type)?.label ?? type; }
  statusName(statusId: string): string { return this.projects.projectStatuses().find(s => s.id === statusId)?.name ?? ''; }

  add(): void {
    this.busy.set(true);
    this.error.set('');
    this.api.createLink(this.issueId(), this.targetId, this.type).subscribe({
      next: () => { this.busy.set(false); this.targetId = ''; this.load(); this.changed.emit(); },
      error: e => this.fail(e)
    });
  }

  remove(link: IssueLinkDto): void {
    this.confirm.confirm({
      title: 'Gỡ liên kết', message: `Gỡ liên kết với ${this.key(link.targetIssueNumber)}? Liên kết ngược chiều cũng bị gỡ.`, type: 'warning', confirmText: 'Gỡ',
      onConfirm: () => this.api.deleteLink(link.id).subscribe({ next: () => { this.load(); this.changed.emit(); }, error: e => this.fail(e) })
    });
  }

  private fail(error: unknown): void { this.busy.set(false); this.error.set(apiErrorMessage(error, 'Không lưu được liên kết.')); }
}
