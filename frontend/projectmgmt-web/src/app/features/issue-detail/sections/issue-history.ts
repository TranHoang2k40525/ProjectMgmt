import { ChangeDetectionStrategy, Component, OnChanges, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  ASSIGNMENT_SOURCE_LABELS, ActivityDto, AssignmentChangeDto, HistoryApi, StatusChangeDto, describeActivity, formatDuration
} from '../../../core/api/history.api';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ISSUE_SECTION_STYLES } from './issue-section.styles';

type HistoryTab = 'all' | 'status' | 'assignee';

/** Tab Lịch sử: nhật ký đầy đủ (phân trang "Xem thêm"), lịch sử trạng thái kèm thời gian ở trạng thái trước, lịch sử giao việc. */
@Component({
  selector: 'app-issue-history',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [ISSUE_SECTION_STYLES, `
    [role=tablist] { display: flex; gap: .25rem; border-bottom: 1px solid #dce3ed; margin-bottom: .75rem; }
    [role=tab] { background: none; color: #42526e; border-radius: 6px 6px 0 0; padding: .4rem .8rem; font-weight: 600; border-bottom: 2px solid transparent; }
    [role=tab][aria-selected=true] { color: #175f47; border-bottom-color: #175f47; }
    ol { list-style: none; padding: 0; margin: 0; }
    li { display: grid; grid-template-columns: 8.5rem 1fr; gap: .75rem; padding: .45rem 0; border-bottom: 1px solid #eef1f5; }
    @media (max-width: 600px) { li { grid-template-columns: 1fr; gap: .15rem; } }
    time { color: #5e6c84; font-size: .85rem; }
    .ai { background: #eae6ff; color: #403294; border-radius: 4px; padding: 0 .35rem; font-size: .75rem; font-weight: 600; margin-left: .35rem; }
  `],
  template: `
    <h2>Lịch sử thay đổi</h2>
    <div role="tablist" aria-label="Loại lịch sử">
      @for (t of tabs; track t.id) {
        <button type="button" role="tab" [id]="'history-tab-' + t.id" aria-controls="history-panel"
          [attr.aria-selected]="tab() === t.id" [attr.tabindex]="tab() === t.id ? 0 : -1" (click)="select(t.id)" (keydown)="onTabKey($event)">{{ t.label }}</button>
      }
    </div>
    <div role="tabpanel" id="history-panel" [attr.aria-labelledby]="'history-tab-' + tab()" tabindex="0">
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
    @if (loading()) { <p class="muted" role="status">Đang tải lịch sử…</p> }

    @switch (tab()) {
      @case ('all') {
        <ol>
          @for (a of activities(); track a.id) {
            <li><time [attr.datetime]="a.createdAt">{{ a.createdAt | date:'dd/MM/yyyy HH:mm' }}</time>
              <span><strong>{{ a.actorName || 'Hệ thống' }}</strong> {{ describe(a) }}@if (a.source === 'AI') {<span class="ai">AI</span>}</span></li>
          } @empty { @if (!loading()) { <li class="muted">Chưa có thay đổi.</li> } }
        </ol>
        @if (activities().length < total()) {
          <div class="row" style="margin-top:.5rem"><button type="button" class="link" (click)="more()" [disabled]="loading()">Xem thêm ({{ total() - activities().length }})</button></div>
        }
      }
      @case ('status') {
        <ol>
          @for (s of statuses(); track s.id) {
            <li><time [attr.datetime]="s.changedAt">{{ s.changedAt | date:'dd/MM/yyyy HH:mm' }}</time>
              <span><strong>{{ s.changedByName || 'Hệ thống' }}</strong>
                @if (s.fromStatusName) { đổi trạng thái {{ s.fromStatusName }} → {{ s.toStatusName }}
                  @if ((s.durationSeconds ?? null) !== null) { <span class="muted">(ở {{ s.fromStatusName }} {{ duration(s.durationSeconds!) }})</span> }
                } @else { tạo ở trạng thái {{ s.toStatusName }} }
              </span></li>
          } @empty { @if (!loading()) { <li class="muted">Chưa có lịch sử trạng thái.</li> } }
        </ol>
      }
      @case ('assignee') {
        <ol>
          @for (h of assignments(); track h.id) {
            <li><time [attr.datetime]="h.assignedAt">{{ h.assignedAt | date:'dd/MM/yyyy HH:mm' }}</time>
              <span><strong>{{ h.assignedByName || 'Hệ thống' }}</strong> {{ h.fromAssigneeName || 'Chưa phân công' }} → {{ h.toAssigneeName || 'Chưa phân công' }}
                <span class="muted">· {{ sourceLabel(h.assignmentSource) }}@if ((h.storyPointsAtTime ?? null) !== null) { · {{ h.storyPointsAtTime }} điểm }</span>
                @if (h.assignmentSource === 'AiSuggested' || h.assignmentSource === 'AiAuto') {<span class="ai">AI</span>}
                @if (h.reason) { <br /><span class="muted">{{ h.reason }}</span> }
              </span></li>
          } @empty { @if (!loading()) { <li class="muted">Chưa có lịch sử giao việc.</li> } }
        </ol>
      }
    }
    </div>
  `
})
export class IssueHistoryComponent implements OnChanges {
  readonly issueId = input.required<string>();
  /** Tăng giá trị này để tải lại sau khi trang cha lưu thay đổi. */
  readonly version = input(0);

  private readonly api = inject(HistoryApi);
  private readonly pageSize = 20;
  readonly tabs: readonly { id: HistoryTab; label: string }[] = [
    { id: 'all', label: 'Tất cả' }, { id: 'status', label: 'Trạng thái' }, { id: 'assignee', label: 'Người xử lý' }
  ];
  readonly tab = signal<HistoryTab>('all');
  readonly activities = signal<ActivityDto[]>([]);
  readonly statuses = signal<StatusChangeDto[]>([]);
  readonly assignments = signal<AssignmentChangeDto[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly error = signal('');
  private page = 1;

  ngOnChanges(): void { this.reload(); }

  describe(activity: ActivityDto): string { return describeActivity(activity); }
  duration(seconds: number): string { return formatDuration(seconds); }
  sourceLabel(source: string): string { return ASSIGNMENT_SOURCE_LABELS[source] ?? source; }

  select(tab: HistoryTab): void {
    this.tab.set(tab);
    this.reload();
  }

  /** Mũi tên trái/phải, Home/End chuyển tab và đặt focus theo mẫu ARIA Tabs. */
  onTabKey(event: KeyboardEvent): void {
    const index = this.tabs.findIndex(t => t.id === this.tab());
    const next = event.key === 'ArrowRight' ? (index + 1) % this.tabs.length
      : event.key === 'ArrowLeft' ? (index - 1 + this.tabs.length) % this.tabs.length
      : event.key === 'Home' ? 0 : event.key === 'End' ? this.tabs.length - 1 : -1;
    if (next < 0) return;
    event.preventDefault();
    this.select(this.tabs[next].id);
    (event.currentTarget as HTMLElement).parentElement?.querySelector<HTMLElement>(`#history-tab-${this.tabs[next].id}`)?.focus();
  }

  more(): void {
    this.page++;
    this.loadActivity(true);
  }

  private reload(): void {
    this.error.set('');
    if (this.tab() === 'all') { this.page = 1; this.loadActivity(false); return; }
    this.loading.set(true);
    if (this.tab() === 'status') {
      this.api.statuses(this.issueId()).subscribe({ next: rows => { this.loading.set(false); this.statuses.set(rows); }, error: e => this.fail(e) });
    } else {
      this.api.assignments(this.issueId()).subscribe({ next: rows => { this.loading.set(false); this.assignments.set(rows); }, error: e => this.fail(e) });
    }
  }

  private loadActivity(append: boolean): void {
    this.loading.set(true);
    this.api.activity(this.issueId(), this.page, this.pageSize).subscribe({
      next: result => {
        this.loading.set(false);
        this.total.set(result.totalCount);
        this.activities.update(current => append ? [...current, ...result.items] : result.items);
      },
      error: e => this.fail(e)
    });
  }

  private fail(error: unknown): void { this.loading.set(false); this.error.set(apiErrorMessage(error, 'Không tải được lịch sử.')); }
}
