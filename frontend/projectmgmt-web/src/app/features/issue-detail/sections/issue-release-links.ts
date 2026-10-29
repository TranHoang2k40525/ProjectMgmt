import { ChangeDetectionStrategy, Component, OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { IssueReleaseLinksDto, ReleaseApi, VersionLinkType, isReleaseLookupUnavailable } from '../../../core/api/release.api';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ISSUE_SECTION_STYLES } from './issue-section.styles';

/** Components + Fix Versions + Affects Versions (tách riêng) của Issue. */
@Component({
  selector: 'app-issue-release-links',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [ISSUE_SECTION_STYLES, `
    h3 { font-size: .85rem; font-weight: 600; color: #5e6c84; margin: .6rem 0 .3rem; }
    .chip { display: inline-flex; align-items: center; gap: .3rem; border-radius: 999px; padding: .1rem .55rem; font-size: .85rem; border: 1px solid #c1c7d0; margin: 0 .3rem .3rem 0; }
    .chip button { background: none; color: inherit; padding: 0 .2rem; font-weight: 700; }
    .released { color: #175f47; font-size: .75rem; }
    select { max-width: 100%; }
  `],
  template: `
    <h2>Component &amp; Version</h2>
    @if (unavailable()) {
      <p class="muted">Chưa dùng được: Planning chưa cung cấp danh sách Component/Version của Project.</p>
    } @else if (links(); as l) {
      @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
      @for (group of groups; track group.key) {
        <h3 [id]="'release-' + group.key">{{ group.label }}</h3>
        <div role="list" [attr.aria-labelledby]="'release-' + group.key">
          @for (item of itemsOf(group.key); track item.id) {
            <span class="chip" role="listitem">{{ item.name }}
              @if (item.isReleased) { <span class="released">· đã phát hành</span> }
              @if (canWrite()) { <button type="button" (click)="remove(group.key, item.id)" [disabled]="busy()" [attr.aria-label]="'Gỡ ' + item.name">×</button> }
            </span>
          } @empty { <span class="muted">Không có.</span> }
        </div>
        @if (canWrite() && optionsOf(group.key).length) {
          <select #picker (change)="add(group.key, picker.value); picker.value = ''" [disabled]="busy()" [attr.aria-label]="'Thêm ' + group.label">
            <option value="">+ Thêm…</option>
            @for (o of optionsOf(group.key); track o.id) { <option [value]="o.id">{{ o.name }}</option> }
          </select>
        }
      }
      @if (!l.availableComponents.length && !l.availableVersions.length) { <p class="muted">Project chưa có Component/Version.</p> }
    }
  `
})
export class IssueReleaseLinksComponent implements OnChanges {
  readonly issueId = input.required<string>();
  readonly canWrite = input(true);
  readonly changed = output<void>();

  private readonly api = inject(ReleaseApi);
  readonly groups: readonly { key: 'component' | VersionLinkType; label: string }[] = [
    { key: 'component', label: 'Components' }, { key: 'FixVersion', label: 'Fix versions' }, { key: 'AffectsVersion', label: 'Affects versions' }
  ];
  readonly links = signal<IssueReleaseLinksDto | null>(null);
  readonly unavailable = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');

  private readonly selected = computed(() => {
    const l = this.links();
    return {
      component: (l?.components ?? []).map(c => ({ id: c.componentId, name: c.name, isReleased: false })),
      FixVersion: (l?.fixVersions ?? []).map(v => ({ id: v.versionId, name: v.name, isReleased: v.isReleased })),
      AffectsVersion: (l?.affectsVersions ?? []).map(v => ({ id: v.versionId, name: v.name, isReleased: v.isReleased }))
    };
  });

  ngOnChanges(): void { this.run(this.api.links(this.issueId()), false); }

  itemsOf(key: 'component' | VersionLinkType) { return this.selected()[key]; }

  optionsOf(key: 'component' | VersionLinkType) {
    const l = this.links();
    const taken = new Set(this.selected()[key].map(item => item.id));
    const source = key === 'component' ? l?.availableComponents ?? [] : l?.availableVersions ?? [];
    return source.filter(option => !taken.has(option.id));
  }

  add(key: 'component' | VersionLinkType, id: string): void {
    if (!id) return;
    this.run(key === 'component' ? this.api.addComponent(this.issueId(), id) : this.api.addVersion(this.issueId(), id, key), true);
  }

  remove(key: 'component' | VersionLinkType, id: string): void {
    this.run(key === 'component' ? this.api.removeComponent(this.issueId(), id) : this.api.removeVersion(this.issueId(), id, key), true);
  }

  private run(request: Observable<IssueReleaseLinksDto>, notify: boolean): void {
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: links => { this.busy.set(false); this.unavailable.set(false); this.links.set(links); if (notify) this.changed.emit(); },
      error: e => {
        this.busy.set(false);
        if (isReleaseLookupUnavailable(e)) { this.unavailable.set(true); return; }
        this.error.set(apiErrorMessage(e, 'Không lưu được Component/Version.'));
      }
    });
  }
}
