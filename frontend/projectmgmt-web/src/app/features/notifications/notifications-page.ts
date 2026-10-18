import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IdentityService } from '../../core/services/identity.service';
import { NotificationModel } from '../../core/mocks/identity-mock-db';

@Component({
  selector: 'app-notifications-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notifications-page.html',
  styleUrls: ['./notifications-page.scss']
})
export class NotificationsPageComponent implements OnInit {
  private identityService = inject(IdentityService);

  readonly notifications = this.identityService.notifications;
  readonly unreadCount = this.identityService.unreadCount;
  readonly filterUnreadOnly = signal<boolean>(false);

  ngOnInit(): void {
    this.identityService.getNotifications().subscribe();
  }

  toggleFilter(unreadOnly: boolean): void {
    this.filterUnreadOnly.set(unreadOnly);
  }

  markAsRead(id: string): void {
    this.identityService.markNotificationAsRead(id).subscribe();
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
