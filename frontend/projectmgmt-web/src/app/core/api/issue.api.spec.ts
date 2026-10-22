import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { IssueApi } from './issue.api';

describe('IssueApi', () => {
  let api: IssueApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(IssueApi);
    http = TestBed.inject(HttpTestingController);
  });

  it('liệt kê Issue có phân trang và chỉ gửi bộ lọc có giá trị', () => {
    api.list('p1', { backlogOnly: true, searchText: '', page: 2, pageSize: 10 }).subscribe();
    const req = http.expectOne(r => r.url === `${baseUrl}/projects/p1/issues`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('backlogOnly')).toBe('true');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.has('searchText')).toBe(false);
    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 10 });
    http.verify();
  });

  it('đọc dữ liệu lõi và cập nhật Issue bằng PATCH', () => {
    api.getCore('i1').subscribe();
    http.expectOne(`${baseUrl}/issues/i1/core`).flush({ id: 'i1' });

    api.update('i1', { title: 'Mới', storyPoints: 3 }).subscribe();
    const req = http.expectOne(`${baseUrl}/issues/i1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ title: 'Mới', storyPoints: 3 });
    req.flush({ id: 'i1' });
    http.verify();
  });

  it('đổi cha và xếp hạng gửi đúng body', () => {
    api.changeParent('i1', null).subscribe();
    const parent = http.expectOne(`${baseUrl}/issues/i1/parent`);
    expect(parent.request.method).toBe('PUT');
    expect(parent.request.body).toEqual({ parentId: null });
    parent.flush({ id: 'i1' });

    api.rank('i1', 'a', null).subscribe();
    const rank = http.expectOne(`${baseUrl}/issues/i1/rank`);
    expect(rank.request.body).toEqual({ previousIssueId: 'a', nextIssueId: null });
    rank.flush({ issueId: 'i1', rankOrder: 2, rebalanced: false });
    http.verify();
  });
});
