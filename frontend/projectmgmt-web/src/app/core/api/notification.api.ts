import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NotificationDto } from './notification-api.models';

@Injectable({ providedIn: 'root' })
export class NotificationApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1/notifications`;

  getInbox(isRead?: boolean, page = 1, pageSize = 20): Observable<NotificationDto> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (typeof isRead === 'boolean') {
      params = params.set('isRead', isRead);
    }
    return this.http.get<NotificationDto>(this.baseUrl, { params });
  }

  getUnreadCount(): Observable<NotificationDto> {
    return this.http.get<NotificationDto>(`${this.baseUrl}/unread-count`);
  }

  markRead(notificationId: string): Observable<NotificationDto> {
    return this.http.put<NotificationDto>(`${this.baseUrl}/${notificationId}/read`, {});
  }

  markReadAll(): Observable<NotificationDto> {
    return this.http.put<NotificationDto>(`${this.baseUrl}/read-all`, {});
  }
}
