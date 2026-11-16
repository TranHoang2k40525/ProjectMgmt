import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { ActivityDto, HistoryApi, describeActivity, formatDuration } from './history.api';

const activity = (patch: Partial<ActivityDto>): ActivityDto => ({ id: 'a', action: 'Updated', actorName: 'An', source: 'User', createdAt: '', ...patch });

describe('history helpers', () => {
  it('diễn đạt đổi trạng thái, đổi trường và hành động khác', () => {
    expect(describeActivity(activity({ action: 'StatusChanged', oldValue: 'To Do', newValue: 'In Progress' }))).toBe('đổi trạng thái To Do → In Progress');
    expect(describeActivity(activity({ fieldName: 'AssigneeId', oldValue: null, newValue: 'Bình' }))).toBe('đổi người xử lý: trống → Bình');
    expect(describeActivity(activity({ fieldName: 'PriorityId', oldValue: '33333333-0000-0000-0000-000000000003', newValue: '33333333-0000-0000-0000-000000000001' })))
      .toBe('đổi độ ưu tiên: Medium → Highest');
    expect(describeActivity(activity({ fieldName: 'Description', newValue: 'x'.repeat(500) }))).toBe('đã cập nhật mô tả');
    expect(describeActivity(activity({ action: 'AttachmentAdded', newValue: 'spec.pdf' }))).toBe('đã đính kèm "spec.pdf"');
    expect(describeActivity(activity({ action: 'Created' }))).toBe('đã tạo công việc');
  });

  it('định dạng thời lượng ở trạng thái trước', () => {
    expect(formatDuration(45)).toBe('45 giây');
    expect(formatDuration(3 * 3600 + 120)).toBe('3 giờ 2 phút');
    expect(formatDuration(3 * 86400 + 5 * 3600)).toBe('3 ngày 5 giờ');
    expect(formatDuration(null)).toBe('');
  });
});

describe('HistoryApi', () => {
  it('gọi đúng route lịch sử có phân trang', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = TestBed.inject(HistoryApi);
    const http = TestBed.inject(HttpTestingController);
    const base = `${environment.apiBaseUrl}/issues/i1`;

    api.activity('i1', 2, 10).subscribe();
    const req = http.expectOne(r => r.url === `${base}/history`);
    expect(req.request.params.get('page')).toBe('2');
    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 10 });
    api.statuses('i1').subscribe();
    http.expectOne(`${base}/status-history`).flush([]);
    api.assignments('i1').subscribe();
    http.expectOne(`${base}/assignment-history`).flush([]);
    http.verify();
  });
});
