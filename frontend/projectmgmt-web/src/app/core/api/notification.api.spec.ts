import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { NotificationApi } from './notification.api';

describe('NotificationApi', () => {
  let api: NotificationApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/v1/notifications`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    api = TestBed.inject(NotificationApi);
    http = TestBed.inject(HttpTestingController);
  });

  it('lấy danh sách thông báo với phân trang và filter', () => {
    api.getInbox(false, 1, 10).subscribe();
    const req = http.expectOne(`${baseUrl}?page=1&pageSize=10&isRead=false`);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, items: [], totalCount: 0, unreadCount: 0 });
    http.verify();
  });

  it('lấy số lượng thông báo chưa đọc', () => {
    api.getUnreadCount().subscribe();
    const req = http.expectOne(`${baseUrl}/unread-count`);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, unreadCount: 5 });
    http.verify();
  });

  it('đánh dấu một thông báo đã đọc', () => {
    const id = 'b59a4bb3-41bb-4592-80ea-316279f9064c';
    api.markRead(id).subscribe();
    const req = http.expectOne(`${baseUrl}/${id}/read`);
    expect(req.request.method).toBe('PUT');
    req.flush({ success: true });
    http.verify();
  });

  it('đánh dấu tất cả thông báo đã đọc', () => {
    api.markReadAll().subscribe();
    const req = http.expectOne(`${baseUrl}/read-all`);
    expect(req.request.method).toBe('PUT');
    req.flush({ success: true });
    http.verify();
  });
});
