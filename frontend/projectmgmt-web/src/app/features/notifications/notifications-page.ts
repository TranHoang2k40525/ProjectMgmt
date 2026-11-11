import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { IdentityService } from '../../core/services/identity.service';
import { ProjectManagementService } from '../../core/services/project-management.service';
import { NotificationModel } from '../../core/models/identity.models';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { ToastService } from '../../core/services/toast.service';
import { WorkspaceDataSource } from '../workspace/data/workspace-data-source';
import { WorkspaceContextService } from '../workspace/workspace-context.service';

@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './notifications-page.html',
  styleUrls: ['./notifications-page.scss']
})
export class NotificationsPageComponent implements OnInit {
  private router = inject(Router);
  private identityService = inject(IdentityService);
  private projectService = inject(ProjectManagementService);
  private toastService = inject(ToastService, { optional: true });
  private realtime = inject(RealtimeService, { optional: true });
  private workspaceData = inject(WorkspaceDataSource, { optional: true });
  private workspaceContext = inject(WorkspaceContextService, { optional: true });

  readonly notifications = this.identityService.notifications;
  readonly unreadCount = this.identityService.unreadCount;
  readonly filterUnreadOnly = signal<boolean>(false);
  readonly currentProject = this.projectService.currentProject;
  readonly onlineMembers = computed(() => this.projectService.currentProject()?.members ?? []);

  readonly isRealtimeConnected = computed(() => {
    return this.realtime?.state() === 'Connected';
  });

  readonly realtimeStateLabel = computed(() => {
    const s = this.realtime?.state();
    if (s === 'Connected') return 'Đang kết nối';
    if (s === 'Connecting' || s === 'Reconnecting') return 'Đang thiết lập';
    return 'Sẵn sàng';
  });

  ngOnInit(): void {
    // Sync any pending organization invitations so their notifications are backfilled
    if (this.workspaceData) {
      this.workspaceData.listMyInvitations().subscribe({
        next: () => {
          this.identityService.getNotifications().subscribe();
          this.identityService.getUnreadCount().subscribe();
        },
        error: () => {
          this.identityService.getNotifications().subscribe();
          this.identityService.getUnreadCount().subscribe();
        }
      });
    } else {
      this.identityService.getNotifications().subscribe();
      this.identityService.getUnreadCount().subscribe();
    }
  }

  toggleFilter(unreadOnly: boolean): void {
    this.filterUnreadOnly.set(unreadOnly);
  }

  markAsRead(itemOrId: NotificationModel | string): void {
    const id = typeof itemOrId === 'string' ? itemOrId : itemOrId.id;
    const item = typeof itemOrId === 'string' ? undefined : itemOrId;
    const actionUrl = item?.actionUrl;

    this.identityService.markNotificationAsRead(id).subscribe({
      next: () => {
        if (!item || !actionUrl) return;
        // Do not auto-navigate away if it is an unanswered invitation card so user can click Accept/Decline
        if ((item.type === 'ORGANIZATION_INVITATION' || item.type === 'PROJECT_INVITATION') && !item.responseStatus) {
          return;
        }
        if (item.projectId) {
          this.projectService.loadWorkspace(item.projectId);
        }
        void this.router.navigateByUrl(actionUrl);
      }
    });
  }

  markAllAsRead(): void {
    this.identityService.markAllNotificationsAsRead().subscribe();
  }

  get displayedNotifications(): NotificationModel[] {
    if (this.filterUnreadOnly()) {
      return this.notifications().filter(n => !n.isRead);
    }
    return this.notifications();
  }

  respondToOrgInvitation(item: NotificationModel, accept: boolean, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    const invitationId = item.entityId;
    if (!invitationId || !this.workspaceData) {
      this.toastService?.error('Lỗi', 'Không tìm thấy mã lời mời tổ chức.');
      return;
    }

    this.workspaceData.respondInvitation(invitationId, accept).subscribe({
      next: (org) => {
        item.responseStatus = accept ? 'ACCEPTED' : 'DECLINED';
        item.isRead = true;
        this.identityService.markNotificationAsRead(item.id).subscribe();
        if (accept && org) {
          this.workspaceContext?.upsert(org);
          this.toastService?.success('Chấp nhận lời mời', `Bạn đã tham gia tổ chức ${org.name} thành công.`);
          void this.router.navigate(['/orgs', org.id, 'overview']);
        } else if (accept) {
          this.workspaceContext?.refreshOrganizations().subscribe();
          this.toastService?.success('Chấp nhận lời mời', 'Bạn đã tham gia tổ chức thành công.');
          void this.router.navigate(['/for-you']);
        } else {
          this.toastService?.info('Từ chối lời mời', 'Bạn đã từ chối lời mời tham gia tổ chức.');
        }
      },
      error: (err) => {
        const msg = err?.error?.message || 'Lời mời không còn hiệu lực hoặc bạn đã tham gia tổ chức này.';
        this.toastService?.error('Không thể phản hồi lời mời', msg);
      }
    });
  }

  respondToInvitation(item: NotificationModel, accept: boolean, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    if (!item.projectId) {
      this.toastService?.error('Lỗi', 'Thông báo không chứa mã dự án.');
      return;
    }

    this.identityService.respondProjectInvitation(item.projectId, item.id, accept).subscribe({
      next: () => {
        if (accept) {
          this.toastService?.success('Chấp nhận lời mời', 'Bạn đã tham gia dự án thành công.');
          this.workspaceContext?.refreshOrganizations().subscribe();
          this.projectService.getProjects().subscribe({
            next: () => {
              if (item.projectId) {
                this.projectService.loadWorkspace(item.projectId);
                void this.router.navigate(['/projects', item.projectId, 'summary']);
              }
            },
            error: () => {
              if (item.projectId) {
                this.projectService.loadWorkspace(item.projectId);
                void this.router.navigate(['/projects', item.projectId, 'summary']);
              }
            }
          });
        } else {
          this.toastService?.info('Từ chối lời mời', 'Bạn đã từ chối tham gia dự án.');
        }
      },
      error: (err) => {
        const msg = err?.error?.message || 'Không thể phản hồi lời mời. Vui lòng thử lại sau.';
        this.toastService?.error('Lỗi', msg);
      }
    });
  }
}

