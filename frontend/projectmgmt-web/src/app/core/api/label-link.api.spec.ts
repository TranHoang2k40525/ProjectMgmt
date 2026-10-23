import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { LabelLinkApi } from './label-link.api';

describe('LabelLinkApi', () => {
  let api: LabelLinkApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(LabelLinkApi);
    http = TestBed.inject(HttpTestingController);
  });

  it('tạo nhãn theo Project và gắn/gỡ khỏi Issue', () => {
    api.createLabel('p1', 'backend', '#0052CC').subscribe();
    const create = http.expectOne(`${baseUrl}/projects/p1/labels`);
    expect(create.request.body).toEqual({ name: 'backend', colorHex: '#0052CC' });
    create.flush({ id: 'l1' });

    api.addLabel('i1', 'l1').subscribe();
    expect(http.expectOne(`${baseUrl}/issues/i1/labels/l1`).request.method).toBe('POST');
    api.removeLabel('i1', 'l1').subscribe();
    expect(http.expectOne(`${baseUrl}/issues/i1/labels/l1`).request.method).toBe('DELETE');
  });

  it('tạo và gỡ liên kết Issue', () => {
    api.createLink('i1', 'i2', 'Blocks').subscribe();
    const create = http.expectOne(`${baseUrl}/issues/i1/links`);
    expect(create.request.body).toEqual({ targetIssueId: 'i2', linkType: 'Blocks' });
    create.flush({ id: 'k1' });

    api.deleteLink('k1').subscribe();
    expect(http.expectOne(`${baseUrl}/issue-links/k1`).request.method).toBe('DELETE');
    http.verify();
  });
});
