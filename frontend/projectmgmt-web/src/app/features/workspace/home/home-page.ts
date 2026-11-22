import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { workspaceErrorMessage } from '../workspace-error';
import { IdentityService } from '../../../core/services/identity.service';
import { ToastService } from '../../../core/services/toast.service';
import { InvitationListComponent, InvitationResponse } from '../components/invitation-list';
import { RecentListComponent } from '../components/recent-list';
import { SectionStateComponent } from '../components/section-state';
import { UI } from '../components/ui-classes';
import { WorkListComponent } from '../components/work-list';
import { CreateOrgModalComponent } from '../create-org/create-org-modal';
import { WorkspaceDataSource } from '../data/workspace-data-source';
import { MyInvitation, ORG_ROLE_LABELS, OrganizationSummary, RecentItem, WorkItemSummary } from '../data/workspace.models';
import { WorkspaceContextService } from '../workspace-context.service';
import { WorkspaceNavigation } from '../workspace-navigation';

/** Trang chủ sau đăng nhập: Dành cho bạn (mọi tổ chức), Gần đây, Tổ chức của bạn. */
@Component({
  selector: 'app-workspace-home-page',
  imports: [RouterLink, WorkListComponent, RecentListComponent, InvitationListComponent, SectionStateComponent, CreateOrgModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home-page.html'
})
export class WorkspaceHomePageComponent implements OnInit {
  private readonly data = inject(WorkspaceDataSource);
  private readonly identity = inject(IdentityService);
  private readonly toast = inject(ToastService);
  protected readonly context = inject(WorkspaceContextService);
  protected readonly nav = inject(WorkspaceNavigation);

  protected readonly ui = UI;
  protected readonly roleLabels = ORG_ROLE_LABELS;

  readonly work = signal<WorkItemSummary[]>([]);
  readonly workLoading = signal(true);
  readonly workError = signal('');
  readonly invitations = signal<MyInvitation[]>([]);
  readonly respondingId = signal<string | null>(null);
  readonly recent = signal<RecentItem[]>([]);
  readonly recentLoading = signal(true);
  readonly recentError = signal('');
  readonly orgsLoading = signal(true);
  readonly orgsError = signal('');
  readonly createOrgOpen = signal(false);

  readonly displayName = computed(() => this.identity.authState().currentUser?.displayName || 'bạn');
  readonly greeting = computed(() => {
    const hour = new Date().getHours();
    return hour < 11 ? 'Chào buổi sáng' : hour < 14 ? 'Chào buổi trưa' : hour < 18 ? 'Chào buổi chiều' : 'Chào buổi tối';
  });
  readonly openWorkCount = computed(() => this.work().filter(item => item.statusCategory !== 'Done').length);

  ngOnInit(): void {
    this.loadWork();
    this.loadRecent();
    this.loadOrganizations();
  }

  loadWork(): void {
    this.workLoading.set(true);
    this.workError.set('');
    this.data.listMyWork().subscribe({
      next: items => {
        this.work.set(items);
        this.workLoading.set(false);
      },
      error: error => {
        this.workError.set(workspaceErrorMessage(error, 'Không tải được công việc của bạn.'));
        this.workLoading.set(false);
      }
    });
    this.data.listMyInvitations().subscribe({ next: items => this.invitations.set(items), error: () => this.invitations.set([]) });
  }

  loadRecent(): void {
    this.recentLoading.set(true);
    this.recentError.set('');
    this.data.listRecent().subscribe({
      next: items => {
        this.recent.set(items);
        this.recentLoading.set(false);
      },
      error: error => {
        this.recentError.set(workspaceErrorMessage(error, 'Không tải được mục gần đây.'));
        this.recentLoading.set(false);
      }
    });
  }

  loadOrganizations(): void {
    this.orgsLoading.set(true);
    this.orgsError.set('');
    this.context.refreshOrganizations().subscribe({
      next: () => this.orgsLoading.set(false),
      error: error => {
        this.orgsError.set(workspaceErrorMessage(error, 'Không tải được danh sách tổ chức.'));
        this.orgsLoading.set(false);
      }
    });
  }

  respond({ invitation, accept }: InvitationResponse): void {
    this.respondingId.set(invitation.id);
    this.data.respondInvitation(invitation.id, accept).subscribe({
      next: org => {
        this.respondingId.set(null);
        this.invitations.update(items => items.filter(item => item.id !== invitation.id));
        if (org) {
          this.context.upsert(org);
          this.toast.success('Đã tham gia tổ chức', org.name);
          this.loadWork();
        } else {
          this.toast.info('Đã từ chối lời mời', invitation.orgName);
        }
      },
      error: error => {
        this.respondingId.set(null);
        this.toast.error('Không trả lời được lời mời', workspaceErrorMessage(error, 'Thử lại sau.'));
      }
    });
  }

  onOrgCreated(org: OrganizationSummary): void {
    this.createOrgOpen.set(false);
    this.context.upsert(org);
    this.toast.success('Đã tạo tổ chức', org.name);
    this.nav.openOrganization(org.id);
  }

  isOwner(org: OrganizationSummary): boolean {
    return org.ownerId === this.context.currentUserId();
  }
}
