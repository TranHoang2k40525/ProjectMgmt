import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AccountDto, ApiResult } from './account-api.models';
import { AvatarResult, ForYouDto, ProfileDto, SkillDto } from './identity-api.models';

@Injectable({ providedIn: 'root' })
export class IdentityApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  getMyProfile(): Observable<ProfileDto> {
    return this.http.get<ProfileDto>(`${this.baseUrl}/users/me`);
  }

  updateMyProfile(request: Partial<ProfileDto>): Observable<ProfileDto> {
    return this.http.put<ProfileDto>(`${this.baseUrl}/users/me/profile`, request);
  }

  uploadAvatar(file: File): Observable<AvatarResult> {
    const form = new FormData();
    form.append('avatar', file, file.name);
    return this.http.post<AvatarResult>(`${this.baseUrl}/users/me/avatar`, form);
  }

  changePassword(request: AccountDto): Observable<ApiResult> {
    return this.http.put<ApiResult>(`${this.baseUrl}/users/me/password`, request);
  }

  getSkillCatalog(category?: string, searchQuery?: string): Observable<SkillDto> {
    let params = new HttpParams();
    if (category?.trim()) params = params.set('category', category.trim());
    if (searchQuery?.trim()) params = params.set('searchQuery', searchQuery.trim());
    return this.http.get<SkillDto>(`${this.baseUrl}/skills`, { params });
  }

  createCatalogSkill(request: Partial<SkillDto>): Observable<SkillDto> {
    return this.http.post<SkillDto>(`${this.baseUrl}/skills`, request);
  }

  getUserSkills(userId: string, projectId?: string): Observable<SkillDto> {
    let params = new HttpParams();
    if (projectId) params = params.set('projectId', projectId);
    return this.http.get<SkillDto>(`${this.baseUrl}/users/${userId}/skills`, { params });
  }

  updateMySkills(skills: Array<Partial<SkillDto>>): Observable<SkillDto> {
    return this.http.put<SkillDto>(`${this.baseUrl}/users/me/skills`, { skills });
  }

  verifyUserSkill(
    userId: string,
    skillId: string,
    verified: boolean,
    level?: number,
    projectId?: string
  ): Observable<SkillDto> {
    let params = new HttpParams();
    if (projectId) params = params.set('projectId', projectId);
    return this.http.put<SkillDto>(
      `${this.baseUrl}/users/${userId}/skills/${skillId}/verify`,
      { verified, level },
      { params }
    );
  }

  getForYou(): Observable<ForYouDto> {
    return this.http.get<ForYouDto>(`${this.baseUrl}/users/for-you`);
  }
}
