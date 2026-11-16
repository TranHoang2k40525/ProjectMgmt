import { ChangeDetectionStrategy, Component, OnChanges, computed, inject, input, signal } from '@angular/core';
import { WatcherDto } from '../../../core/api/collaboration-api.models';
import { CollaborationApi } from '../../../core/api/collaboration.api';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { IdentityService } from '../../../core/services/identity.service';
import { ISSUE_SECTION_STYLES } from './issue-section.styles';

@Component({
  selector: 'app-issue-watch-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [ISSUE_SECTION_STYLES, `:host { display: inline-flex; } button.watching { background: #e3fcef; color: #175f47; border: 1px solid #175f47; }`],
  template: `
    <button type="button" [class.watching]="isWatching()" (click)="toggle()" [disabled]="busy()"
      [attr.aria-pressed]="isWatching()" [title]="names()">
      {{ isWatching() ? 'Đang theo dõi' : 'Theo dõi' }} · {{ watchers().length }}
    </button>
    @if (error()) { <span role="alert" class="error">{{ error() }}</span> }
  `
})
export class IssueWatchButtonComponent implements OnChanges {
  readonly issueId = input.required<string>();
  private readonly api = inject(CollaborationApi);
  private readonly identity = inject(IdentityService);
  readonly watchers = signal<WatcherDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly isWatching = computed(() => this.watchers().some(w => w.userId === this.identity.authState().currentUser?.id));
  readonly names = computed(() => this.watchers().map(w => w.displayName).join(', ') || 'Chưa có người theo dõi');

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.getWatchers(this.issueId()).subscribe({
      next: watchers => this.watchers.set(watchers),
      error: e => this.error.set(apiErrorMessage(e, 'Không tải được người theo dõi.'))
    });
  }

  toggle(): void {
    this.busy.set(true);
    const request = this.isWatching() ? this.api.unwatch(this.issueId()) : this.api.watch(this.issueId());
    request.subscribe({
      next: () => { this.busy.set(false); this.load(); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e, 'Không đổi được trạng thái theo dõi.')); }
    });
  }
}
