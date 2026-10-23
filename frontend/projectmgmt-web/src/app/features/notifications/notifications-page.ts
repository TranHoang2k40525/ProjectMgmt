import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { IdentityService } from '../../core/services/identity.service';
import { ProjectManagementService } from '../../core/services/project-management.service';
import { NotificationModel } from '../../core/models/identity.models';
import { RealtimeService } from '../../core/realtime/realtime.service';

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
  private realtime = inject(RealtimeService, { optional: true });

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
    this.identityService.getNotifications().subscribe();
  }

  toggleFilter(unreadOnly: boolean): void {
    this.filterUnreadOnly.set(unreadOnly);
  }

  markAsRead(itemOrId: NotificationModel | string): void {
    const id = typeof itemOrId === 'string' ? itemOrId : itemOrId.id;
    const actionUrl = typeof itemOrId === 'string' ? undefined : itemOrId.actionUrl;
    this.identityService.markNotificationAsRead(id).subscribe({
      next: () => {
        if (actionUrl) void this.router.navigateByUrl(actionUrl);
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
}

