import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { RequirementApi, moveItem } from './requirement.api';

describe('moveItem', () => {
  it('đổi chỗ phần tử kề và giữ nguyên khi vượt biên', () => {
    expect(moveItem(['a', 'b', 'c'], 1, -1)).toEqual(['b', 'a', 'c']);
    expect(moveItem(['a', 'b', 'c'], 1, 1)).toEqual(['a', 'c', 'b']);
    expect(moveItem(['a', 'b'], 0, -1)).toEqual(['a', 'b']);
    expect(moveItem(['a', 'b'], 1, 1)).toEqual(['a', 'b']);
  });
});

describe('RequirementApi', () => {
  it('gọi đúng route tiêu chí và kỹ năng yêu cầu', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = TestBed.inject(RequirementApi);
    const http = TestBed.inject(HttpTestingController);
    const base = `${environment.apiBaseUrl}`;

    api.addCriterion('i1', 'Đạt').subscribe();
    const add = http.expectOne(`${base}/issues/i1/acceptance-criteria`);
    expect(add.request.body).toEqual({ content: 'Đạt', isMet: false });
    add.flush({});
    api.reorderCriteria('i1', ['c2', 'c1']).subscribe();
    expect(http.expectOne(`${base}/issues/i1/acceptance-criteria/order`).request.body).toEqual({ orderedIds: ['c2', 'c1'] });
    api.updateRequiredSkill('r1', { skillId: 's1', minLevel: 3, weight: 1 }).subscribe();
    expect(http.expectOne(`${base}/issue-required-skills/r1`).request.method).toBe('PUT');
  });
});
