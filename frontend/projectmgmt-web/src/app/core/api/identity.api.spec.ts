import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { IdentityApi } from './identity.api';

describe('IdentityApi', () => {
  let api: IdentityApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/v1`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    api = TestBed.inject(IdentityApi);
    http = TestBed.inject(HttpTestingController);
  });

  it('đọc và cập nhật hồ sơ đúng route users/me', () => {
    api.getMyProfile().subscribe();
    const getRequest = http.expectOne(`${baseUrl}/users/me`);
    expect(getRequest.request.method).toBe('GET');
    getRequest.flush({ success: true, userId: 'user-1' });

    api.updateMyProfile({ fullName: 'Trần Văn Hoàng', timezone: 'Asia/Ho_Chi_Minh' }).subscribe();
    const updateRequest = http.expectOne(`${baseUrl}/users/me/profile`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body.fullName).toBe('Trần Văn Hoàng');
    updateRequest.flush({ success: true, userId: 'user-1' });
    http.verify();
  });

  it('upload avatar bằng multipart field avatar', () => {
    const file = new File(['image-bytes'], 'avatar.png', { type: 'image/png' });
    api.uploadAvatar(file).subscribe();

    const request = http.expectOne(`${baseUrl}/users/me/avatar`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeInstanceOf(FormData);
    const avatar = (request.request.body as FormData).get('avatar') as File;
    expect(avatar.name).toBe('avatar.png');
    expect(avatar.type).toBe('image/png');
    request.flush({ success: true, avatarUrl: '/assets/avatars/avatar.png' });
    http.verify();
  });

  it('thay toàn bộ kỹ năng qua một request nguyên tử', () => {
    api.updateMySkills([{ skillId: 'skill-1', proficiencyLevel: 5 }]).subscribe();

    const request = http.expectOne(`${baseUrl}/users/me/skills`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      skills: [{ skillId: 'skill-1', proficiencyLevel: 5 }]
    });
    request.flush({ success: true, updatedCount: 1 });
    http.verify();
  });

  it('đọc trang Dành cho bạn từ users/for-you', () => {
    api.getForYou().subscribe();

    const request = http.expectOne(`${baseUrl}/users/for-you`);
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, assignedIssues: [], recentIssues: [], attentionFocus: [] });
    http.verify();
  });
});
