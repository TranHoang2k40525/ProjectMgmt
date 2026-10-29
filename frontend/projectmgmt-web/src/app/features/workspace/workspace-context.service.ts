import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { IdentityService } from '../../core/services/identity.service';
import { WorkspaceDataSource } from './data/workspace-data-source';
import { OrganizationSummary, RecentKind } from './data/workspace.models';

const LAST_ORG_KEY = 'pm-workspace-last-org';

/** Tổ chức đang mở và danh sách tổ chức của người dùng, dùng chung cho trang chủ, màn tổ chức và app shell. */
@Injectable({ providedIn: 'root' })
export class WorkspaceContextService {
  private readonly data = inject(WorkspaceDataSource);
  private readonly identity = inject(IdentityService);

  readonly organizations = signal<OrganizationSummary[]>([]);
  readonly currentOrg = signal<OrganizationSummary | null>(null);
  readonly currentUserId = computed(() => this.identity.authState().currentUser?.id ?? null);
  readonly isOwner = computed(() => {
    const org = this.currentOrg();
    return !!org && org.ownerId === this.currentUserId();
  });

  refreshOrganizations(): Observable<OrganizationSummary[]> {
    return this.data.listOrganizations().pipe(tap(orgs => this.organizations.set(orgs)));
  }

  /** Mở tổ chức: đặt làm tổ chức hiện tại, nhớ cho lần sau và ghi vào "Gần đây". */
  openOrganization(orgId: string): Observable<OrganizationSummary> {
    return this.data.getOrganization(orgId).pipe(tap(org => {
      this.currentOrg.set(org);
      this.upsert(org);
      this.writeLastOrg(org.id);
      this.recordVisit('organization', org.id);
    }));
  }

  /** Cập nhật tổ chức trong danh sách sau khi tạo mới, nhận lời mời hoặc số liệu thay đổi. */
  upsert(org: OrganizationSummary): void {
    this.organizations.update(orgs => {
      const index = orgs.findIndex(item => item.id === org.id);
      if (index < 0) return [...orgs, org].sort((a, b) => a.name.localeCompare(b.name, 'vi'));
      const next = [...orgs];
      next[index] = org;
      return next;
    });
    if (this.currentOrg()?.id === org.id) this.currentOrg.set(org);
  }

  recordVisit(kind: RecentKind, id: string): void {
    this.data.recordVisit({ kind, id }).subscribe({ error: () => undefined });
  }

  lastOrgId(): string | null {
    try {
      return typeof localStorage === 'undefined' ? null : localStorage.getItem(LAST_ORG_KEY);
    } catch {
      return null;
    }
  }

  private writeLastOrg(orgId: string): void {
    try {
      if (typeof localStorage !== 'undefined') localStorage.setItem(LAST_ORG_KEY, orgId);
    } catch {
      /* Không lưu được thì lần sau chỉ không tự mở lại tổ chức cũ. */
    }
  }
}
