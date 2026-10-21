import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AcceptanceCriterionDto {
  id: string;
  issueId: string;
  content: string;
  isMet: boolean;
  metBy?: string | null;
  metByName?: string | null;
  metAt?: string | null;
  orderIndex: number;
  source: 'AI' | 'Manual' | string;
  aiGenerationLogId?: string | null;
  wasEditedAfterAi: boolean;
  createdAt: string;
}

export interface RequiredSkillDto {
  id: string;
  issueId: string;
  skillId: string;
  skillName: string;
  minLevel: number;
  weight: number;
  source: 'Manual' | 'AI' | 'DerivedFromComponent' | string;
}

export interface SaveRequiredSkillRequest {
  skillId: string;
  minLevel: number;
  weight: number;
}

/** Đổi chỗ hai phần tử kề nhau (nút lên/xuống thay cho kéo thả, dùng được bằng bàn phím). */
export function moveItem<T>(items: readonly T[], index: number, offset: -1 | 1): T[] {
  const target = index + offset;
  if (index < 0 || index >= items.length || target < 0 || target >= items.length) return [...items];
  const copy = [...items];
  [copy[index], copy[target]] = [copy[target], copy[index]];
  return copy;
}

/** Acceptance Criteria và kỹ năng yêu cầu của module DeliveryIntelligence (C). */
@Injectable({ providedIn: 'root' })
export class RequirementApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  criteria(issueId: string): Observable<AcceptanceCriterionDto[]> {
    return this.http.get<AcceptanceCriterionDto[]>(`${this.baseUrl}/issues/${issueId}/acceptance-criteria`);
  }

  addCriterion(issueId: string, content: string): Observable<AcceptanceCriterionDto> {
    return this.http.post<AcceptanceCriterionDto>(`${this.baseUrl}/issues/${issueId}/acceptance-criteria`, { content, isMet: false });
  }

  updateCriterion(criterionId: string, content: string, isMet: boolean): Observable<AcceptanceCriterionDto> {
    return this.http.put<AcceptanceCriterionDto>(`${this.baseUrl}/acceptance-criteria/${criterionId}`, { content, isMet });
  }

  deleteCriterion(criterionId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/acceptance-criteria/${criterionId}`);
  }

  reorderCriteria(issueId: string, orderedIds: string[]): Observable<AcceptanceCriterionDto[]> {
    return this.http.put<AcceptanceCriterionDto[]>(`${this.baseUrl}/issues/${issueId}/acceptance-criteria/order`, { orderedIds });
  }

  requiredSkills(issueId: string): Observable<RequiredSkillDto[]> {
    return this.http.get<RequiredSkillDto[]>(`${this.baseUrl}/issues/${issueId}/required-skills`);
  }

  addRequiredSkill(issueId: string, request: SaveRequiredSkillRequest): Observable<RequiredSkillDto> {
    return this.http.post<RequiredSkillDto>(`${this.baseUrl}/issues/${issueId}/required-skills`, request);
  }

  updateRequiredSkill(requiredSkillId: string, request: SaveRequiredSkillRequest): Observable<RequiredSkillDto> {
    return this.http.put<RequiredSkillDto>(`${this.baseUrl}/issue-required-skills/${requiredSkillId}`, request);
  }

  deleteRequiredSkill(requiredSkillId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/issue-required-skills/${requiredSkillId}`);
  }
}
