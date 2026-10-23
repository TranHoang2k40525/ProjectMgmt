import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { ReleaseApi, isReleaseLookupUnavailable } from './release.api';

describe('ReleaseApi', () => {
  it('thêm/gỡ version kèm loại liên kết', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = TestBed.inject(ReleaseApi);
    const http = TestBed.inject(HttpTestingController);
    const base = `${environment.apiBaseUrl}/issues/i1`;

    api.addVersion('i1', 'v1', 'FixVersion').subscribe();
    expect(http.expectOne(`${base}/versions`).request.body).toEqual({ versionId: 'v1', linkType: 'FixVersion' });
    api.removeVersion('i1', 'v1', 'AffectsVersion').subscribe();
    const remove = http.expectOne(r => r.url === `${base}/versions/v1`);
    expect(remove.request.params.get('linkType')).toBe('AffectsVersion');
  });

  it('nhận biết Planning chưa cung cấp Component/Version (501)', () => {
    expect(isReleaseLookupUnavailable(new HttpErrorResponse({ status: 501 }))).toBe(true);
    expect(isReleaseLookupUnavailable(new HttpErrorResponse({ status: 403 }))).toBe(false);
  });
});
