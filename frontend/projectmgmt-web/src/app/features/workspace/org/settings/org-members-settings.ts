import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { apiErrorMessage } from '../../../../core/http/api-error-message';
import { ConfirmDialogService } from '../../../../core/services/confirm-dialog.service';
import { ToastService } from '../../../../core/services/toast.service';
import { SectionStateComponent } from '../../components/section-state';
import { UI } from '../../components/ui-classes';
import { WorkspaceDataSource } from '../../data/workspace-data-source';
import {
  InviteEmailResult,
  InviteLink,
  ORG_ROLE_LABELS,
  OrgInvitation,
  OrgMember,
  OrgRoleCode,
  OrgRoleOption
} from '../../data/workspace.models';
import { initials, timeAgo } from '../../work-display';
import { WorkspaceContextService } from '../../workspace-context.service';
import { INVITE_OUTCOME_LABELS, MAX_INVITE_EMAILS, parseInviteEmails } from './invite-emails';

/** Cài đặt · Thành viên: đổi vai trò, gỡ thành viên, mời qua email, link mời. */
@Component({
  selector: 'app-org-members-settings',
  imports: [FormsModule, SectionStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './org-members-settings.html'
})
export class OrgMembersSettingsComponent implements OnInit {
  private readonly data = inject(WorkspaceDataSource);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly document = inject(DOCUMENT);
  private copiedTimer: ReturnType<typeof setTimeout> | undefined;
  protected readonly context = inject(WorkspaceContextService);
  protected readonly ui = UI;
  protected readonly roleLabels = ORG_ROLE_LABELS;
  protected readonly outcomes = INVITE_OUTCOME_LABELS;

  readonly loading = signal(true);
  readonly error = signal('');
  readonly members = signal<OrgMember[]>([]);
  readonly roles = signal<OrgRoleOption[]>([]);
  readonly invitations = signal<OrgInvitation[]>([]);
  readonly inviteLink = signal<InviteLink | null>(null);
  readonly savingRoleFor = signal<string | null>(null);

  readonly emailText = signal('');
  readonly inviteRole = signal<OrgRoleCode>('Member');
  readonly inviteError = signal('');
  readonly inviting = signal(false);
  readonly inviteResults = signal<InviteEmailResult[]>([]);

  readonly linkRole = signal<OrgRoleCode>('Member');
  readonly linkBusy = signal(false);
  readonly copied = signal(false);

  readonly assignableRoles = computed(() => this.roles().filter(role => role.assignable));
  readonly inviteUrl = computed(() => {
    const link = this.inviteLink();
    return link ? `${this.document.location.origin}/invite/${link.token}` : '';
  });
  readonly emailCount = computed(() => parseInviteEmails(this.emailText()).valid.length);

  private get orgId(): string {
    return this.context.currentOrg()?.id ?? '';
  }

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.copiedTimer));
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      members: this.data.listMembers(this.orgId),
      roles: this.data.listRoles(),
      invitations: this.data.listInvitations(this.orgId),
      link: this.data.getInviteLink(this.orgId)
    }).subscribe({
      next: ({ members, roles, invitations, link }) => {
        this.members.set(members);
        this.roles.set(roles);
        this.invitations.set(invitations);
        this.inviteLink.set(link);
        if (link) this.linkRole.set(link.role);
        this.loading.set(false);
      },
      error: error => {
        this.error.set(apiErrorMessage(error, 'Không tải được thành viên của tổ chức.'));
        this.loading.set(false);
      }
    });
  }

  isOwnerRow(member: OrgMember): boolean {
    return member.userId === this.context.currentOrg()?.ownerId;
  }

  changeRole(member: OrgMember, select: HTMLSelectElement): void {
    const role = select.value as OrgRoleCode;
    if (role === member.role) return;
    this.savingRoleFor.set(member.userId);
    this.data.updateMemberRole(this.orgId, member.userId, role).subscribe({
      next: updated => {
        this.savingRoleFor.set(null);
        this.members.update(list => list.map(item => item.userId === updated.userId ? updated : item));
        this.toast.success('Đã đổi vai trò', `${updated.displayName}: ${this.roleLabels[updated.role]}`);
      },
      error: error => {
        this.savingRoleFor.set(null);
        select.value = member.role;
        this.toast.error('Không đổi được vai trò', apiErrorMessage(error, 'Thử lại sau.'));
      }
    });
  }

  removeMember(member: OrgMember): void {
    this.confirm.confirm({
      title: `Gỡ ${member.displayName} khỏi tổ chức?`,
      message: 'Người này sẽ mất quyền truy cập mọi dự án của tổ chức. Bạn có thể mời lại sau.',
      type: 'danger',
      confirmText: 'Gỡ khỏi tổ chức',
      cancelText: 'Hủy',
      onConfirm: () => this.data.removeMember(this.orgId, member.userId).subscribe({
        next: () => {
          this.members.update(list => list.filter(item => item.userId !== member.userId));
          this.context.reloadCurrent();
          this.toast.success('Đã gỡ thành viên', member.displayName);
        },
        error: error => this.toast.error('Không gỡ được thành viên', apiErrorMessage(error, 'Thử lại sau.'))
      })
    });
  }

  sendInvites(): void {
    if (this.inviting()) return;
    const { valid, invalid } = parseInviteEmails(this.emailText());
    this.inviteResults.set([]);
    if (invalid.length > 0) {
      this.inviteError.set(`Email không hợp lệ: ${invalid.join(', ')}`);
      return;
    }
    if (valid.length === 0) {
      this.inviteError.set('Nhập ít nhất một email.');
      return;
    }
    if (valid.length > MAX_INVITE_EMAILS) {
      this.inviteError.set(`Mỗi lần mời tối đa ${MAX_INVITE_EMAILS} email.`);
      return;
    }
    this.inviteError.set('');
    this.inviting.set(true);
    this.data.inviteByEmail(this.orgId, valid, this.inviteRole()).subscribe({
      next: results => {
        this.inviting.set(false);
        this.inviteResults.set(results);
        // Chỉ giữ lại email cần sửa (chưa có tài khoản); email đã là thành viên hoặc đã được mời thì không cần gửi lại.
        this.emailText.set(results.filter(result => result.outcome === 'NoAccount').map(result => result.email).join('\n'));
        if (results.some(result => result.outcome === 'Sent')) {
          this.data.listInvitations(this.orgId).subscribe({ next: list => this.invitations.set(list), error: () => undefined });
        }
      },
      error: error => {
        this.inviting.set(false);
        this.inviteError.set(apiErrorMessage(error, 'Không gửi được lời mời. Thử lại sau.'));
      }
    });
  }

  cancelInvitation(invitation: OrgInvitation): void {
    this.data.cancelInvitation(this.orgId, invitation.id).subscribe({
      next: () => {
        this.invitations.update(list => list.filter(item => item.id !== invitation.id));
        this.toast.info('Đã hủy lời mời', invitation.email);
      },
      error: error => this.toast.error('Không hủy được lời mời', apiErrorMessage(error, 'Thử lại sau.'))
    });
  }

  createLink(): void {
    if (!this.inviteLink()) {
      this.regenerate();
      return;
    }
    this.confirm.confirm({
      title: 'Tạo link mời mới?',
      message: 'Link hiện tại sẽ ngừng hoạt động; ai chưa dùng sẽ cần link mới.',
      type: 'warning',
      confirmText: 'Tạo link mới',
      cancelText: 'Hủy',
      onConfirm: () => this.regenerate()
    });
  }

  disableLink(): void {
    this.confirm.confirm({
      title: 'Tắt link mời?',
      message: 'Không ai tham gia được bằng link này nữa. Bạn có thể tạo link mới bất cứ lúc nào.',
      type: 'warning',
      confirmText: 'Tắt link',
      cancelText: 'Hủy',
      onConfirm: () => {
        this.linkBusy.set(true);
        this.data.disableInviteLink(this.orgId).subscribe({
          next: () => {
            this.linkBusy.set(false);
            this.inviteLink.set(null);
          },
          error: error => {
            this.linkBusy.set(false);
            this.toast.error('Không tắt được link mời', apiErrorMessage(error, 'Thử lại sau.'));
          }
        });
      }
    });
  }

  async copyLink(input: HTMLInputElement): Promise<void> {
    const url = this.inviteUrl();
    if (!url) return;
    try {
      await navigator.clipboard.writeText(url);
      this.markCopied();
    } catch {
      // Trình duyệt chặn Clipboard API (http, iframe…): chọn sẵn để người dùng tự sao chép.
      input.focus();
      input.select();
      this.toast.info('Đã chọn link', 'Nhấn Ctrl+C để sao chép.');
    }
  }

  joinedAgo(member: OrgMember): string {
    return timeAgo(member.joinedAt);
  }

  sentAgo(invitation: OrgInvitation): string {
    return timeAgo(invitation.createdAt);
  }

  avatar(member: OrgMember): string {
    return initials(member.displayName);
  }

  private regenerate(): void {
    this.linkBusy.set(true);
    this.data.regenerateInviteLink(this.orgId, this.linkRole()).subscribe({
      next: link => {
        this.linkBusy.set(false);
        this.inviteLink.set(link);
        this.copied.set(false);
        this.toast.success('Đã tạo link mời', 'Gửi link cho người bạn muốn mời.');
      },
      error: error => {
        this.linkBusy.set(false);
        this.toast.error('Không tạo được link mời', apiErrorMessage(error, 'Thử lại sau.'));
      }
    });
  }

  private markCopied(): void {
    this.copied.set(true);
    clearTimeout(this.copiedTimer);
    this.copiedTimer = setTimeout(() => this.copied.set(false), 2000);
  }
}
