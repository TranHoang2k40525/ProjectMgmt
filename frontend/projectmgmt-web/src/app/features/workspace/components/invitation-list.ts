import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MyInvitation, ORG_ROLE_LABELS } from '../data/workspace.models';
import { timeAgo } from '../work-display';
import { UI } from './ui-classes';

export interface InvitationResponse {
  invitation: MyInvitation;
  accept: boolean;
}

/** Lời mời vào tổ chức đang chờ người dùng hiện tại trả lời. */
@Component({
  selector: 'app-invitation-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="space-y-2" aria-label="Lời mời vào tổ chức">
      @for (invitation of invitations(); track invitation.id) {
        <li class="flex flex-col sm:flex-row sm:items-center gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 dark:border-amber-900 dark:bg-amber-950/30">
          <span class="material-symbols-outlined text-amber-700 dark:text-amber-300" aria-hidden="true">mail</span>
          <p class="flex-1 min-w-0 break-words text-sm text-slate-800 dark:text-slate-100 text-pretty">
            <strong>{{ invitation.invitedByName }}</strong> mời bạn vào <strong>{{ invitation.orgName }}</strong>
            với vai trò {{ roleLabels[invitation.role] }}
            <span class="text-slate-500"> · {{ ago(invitation) }}</span>
          </p>
          <span class="flex gap-2 shrink-0">
            <button type="button" [class]="ui.secondaryButton" [disabled]="busyId() === invitation.id"
                    (click)="responded.emit({ invitation, accept: false })">Từ chối</button>
            <button type="button" [class]="ui.primaryButton" [disabled]="busyId() === invitation.id"
                    (click)="responded.emit({ invitation, accept: true })">Tham gia</button>
          </span>
        </li>
      }
    </ul>
  `
})
export class InvitationListComponent {
  readonly invitations = input.required<MyInvitation[]>();
  readonly busyId = input<string | null>(null);
  readonly responded = output<InvitationResponse>();

  protected readonly ui = UI;
  protected readonly roleLabels = ORG_ROLE_LABELS;

  ago(invitation: MyInvitation): string {
    return timeAgo(invitation.createdAt);
  }
}
