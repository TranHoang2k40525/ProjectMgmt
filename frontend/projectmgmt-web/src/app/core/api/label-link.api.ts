import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LabelDto {
  id: string;
  projectId: string;
  name: string;
  colorHex: string;
}

export type IssueLinkType = 'Blocks' | 'IsBlockedBy' | 'Relates' | 'Duplicates' | 'IsDuplicatedBy';

export interface IssueLinkDto {
  id: string;
  linkType: IssueLinkType;
  targetIssueId: string;
  targetIssueNumber: number;
  targetTitle: string;
  targetStatusId: string;
  createdAt: string;
}

export const ISSUE_LINK_TYPES: readonly { value: IssueLinkType; label: string }[] = [
  { value: 'Blocks', label: 'chặn (blocks)' },
  { value: 'IsBlockedBy', label: 'bị chặn bởi (is blocked by)' },
  { value: 'Relates', label: 'liên quan (relates to)' },
  { value: 'Duplicates', label: 'trùng với (duplicates)' },
  { value: 'IsDuplicatedBy', label: 'bị trùng bởi (is duplicated by)' }
];

/** Label và IssueLink của module DeliveryIntelligence (C). */
@Injectable({ providedIn: 'root' })
export class LabelLinkApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  projectLabels(projectId: string): Observable<LabelDto[]> {
    return this.http.get<LabelDto[]>(`${this.baseUrl}/projects/${projectId}/labels`);
  }

  createLabel(projectId: string, name: string, colorHex?: string): Observable<LabelDto> {
    return this.http.post<LabelDto>(`${this.baseUrl}/projects/${projectId}/labels`, { name, colorHex });
  }

  issueLabels(issueId: string): Observable<LabelDto[]> {
    return this.http.get<LabelDto[]>(`${this.baseUrl}/issues/${issueId}/labels`);
  }

  addLabel(issueId: string, labelId: string): Observable<LabelDto[]> {
    return this.http.post<LabelDto[]>(`${this.baseUrl}/issues/${issueId}/labels/${labelId}`, {});
  }

  removeLabel(issueId: string, labelId: string): Observable<LabelDto[]> {
    return this.http.delete<LabelDto[]>(`${this.baseUrl}/issues/${issueId}/labels/${labelId}`);
  }

  links(issueId: string): Observable<IssueLinkDto[]> {
    return this.http.get<IssueLinkDto[]>(`${this.baseUrl}/issues/${issueId}/links`);
  }

  createLink(issueId: string, targetIssueId: string, linkType: IssueLinkType): Observable<IssueLinkDto> {
    return this.http.post<IssueLinkDto>(`${this.baseUrl}/issues/${issueId}/links`, { targetIssueId, linkType });
  }

  deleteLink(linkId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/issue-links/${linkId}`);
  }
}
