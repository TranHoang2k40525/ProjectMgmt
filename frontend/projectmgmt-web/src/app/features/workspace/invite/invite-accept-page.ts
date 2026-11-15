import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { workspaceErrorMessage } from '../workspace-error';
import { ToastService } from '../../../core/services/toast.service';
import { UI } from '../components/ui-classes';
import { WorkspaceDataSource } from '../data/workspace-data-source';
import { InviteLinkPreview, ORG_ROLE_LABELS } from '../data/workspace.models';
import { WorkspaceContextService } from '../workspace-context.service';

/** Trang mở từ link mời: xem tổ chức và vai trò, rồi tham gia hoặc bỏ qua. Cần đăng nhập (authGuard giữ returnUrl). */
@Component({
  selector: 'app-invite-accept-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto flex w-full max-w-lg flex-col px-4 py-10 sm:py-16">
      <section [class]="ui.card" class="text-center" aria-labelledby="invite-title" [attr.aria-busy]="loading()">
        @if (loading()) {
          <div class="mx-auto mb-4 size-14 rounded-2xl bg-slate-100 dark:bg-slate-800 motion-safe:animate-pulse"></div>
          <div class="mx-auto h-6 w-2/3 rounded bg-slate-100 dark:bg-slate-800 motion-safe:animate-pulse"></div>
          <h1 id="invite-title" class="sr-only">Đang mở lời mời…</h1>
        } @else if (preview(); as invite) {
          <span class="mx-auto mb-4 flex size-14 items-center justify-center rounded-2xl bg-primary-container text-2xl font-bold text-white" aria-hidden="true">
            {{ invite.orgName.charAt(0).toUpperCase() }}
          </span>
          <h1 id="invite-title" class="text-xl font-bold text-slate-900 dark:text-white text-balance">
            {{ invite.alreadyMember ? 'Bạn đã ở trong ' + invite.orgName : 'Tham gia ' + invite.orgName + '?' }}
          </h1>
          <p class="mt-2 text-sm text-slate-600 dark:text-slate-300 text-pretty">
            @if (invite.alreadyMember) {
              Bạn đã là thành viên của tổ chức này.
            } @else {
              {{ invite.invitedByName }} mời bạn tham gia với vai trò <strong>{{ roleLabels[invite.role] }}</strong>.
            }
          </p>
          @if (actionError()) {
            <p role="alert" class="mt-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800 dark:bg-red-950/40 dark:text-red-300">{{ actionError() }}</p>
          }
          <div class="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-center">
            @if (invite.alreadyMember) {
              <button type="button" [class]="ui.primaryButton" (click)="goToOrg(invite.orgId)">Vào tổ chức</button>
            } @else {
              <a routerLink="/for-you" [class]="ui.secondaryButton">Không tham gia</a>
              <button type="button" [class]="ui.primaryButton" [disabled]="joining()" (click)="join(invite)">
                {{ joining() ? 'Đang tham gia…' : 'Tham gia tổ chức' }}
              </button>
            }
          </div>
        } @else {
          <span class="material-symbols-outlined mx-auto mb-3 block text-[40px] text-slate-400" aria-hidden="true">link_off</span>
          <h1 id="invite-title" class="text-xl font-bold text-slate-900 dark:text-white">Link mời không còn hiệu lực</h1>
          <p class="mt-2 text-sm text-slate-600 dark:text-slate-300 text-pretty">{{ loadError() }}</p>
          <a routerLink="/for-you" [class]="ui.primaryButton" class="mt-6">Về trang chủ</a>
        }
      </section>
    </div>
  `
})
export class InviteAcceptPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly data = inject(WorkspaceDataSource);
  private readonly context = inject(WorkspaceContextService);
  private readonly toast = inject(ToastService);
  protected readonly ui = UI;
  protected readonly roleLabels = ORG_ROLE_LABELS;

  readonly loading = signal(true);
  readonly preview = signal<InviteLinkPreview | null>(null);
  readonly loadError = signal('');
  readonly joining = signal(false);
  readonly actionError = signal('');

  ngOnInit(): void {
    const token = this.route.snapshot.paramMap.get('token') ?? '';
    this.data.getInviteByToken(token).subscribe({
      next: preview => {
        this.preview.set(preview);
        this.loading.set(false);
      },
      error: (error: { status?: number }) => {
        this.loadError.set(error?.status === 404
          ? 'Link có thể đã bị thu hồi hoặc tắt. Hãy xin người mời gửi link mới.'
          : workspaceErrorMessage(error, 'Không mở được lời mời. Thử lại sau.'));
        this.loading.set(false);
      }
    });
  }

  join(invite: InviteLinkPreview): void {
    this.joining.set(true);
    this.actionError.set('');
    this.data.acceptInviteLink(invite.token).subscribe({
      next: org => {
        this.joining.set(false);
        this.context.upsert(org);
        this.toast.success('Đã tham gia tổ chức', org.name);
        this.goToOrg(org.id);
      },
      error: error => {
        this.joining.set(false);
        this.actionError.set(workspaceErrorMessage(error, 'Không tham gia được. Thử lại sau.'));
      }
    });
  }

  goToOrg(orgId: string): void {
    void this.router.navigate(['/orgs', orgId, 'overview']);
  }
}
