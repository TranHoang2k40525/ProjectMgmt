import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IssueCoreDto, IssueListItemDto, IssueListQuery, IssueRankDto, PagedResult, UpdateIssueRequest } from './issue-api.models';

/** API Issue của module DeliveryIntelligence (C). Tạo Issue vẫn đi qua ProjectManagementService cho tới khi Planning có IIssueNumberGenerator. */
@Injectable({ providedIn: 'root' })
export class IssueApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  list(projectId: string, query: IssueListQuery = {}): Observable<PagedResult<IssueListItemDto>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') params = params.set(key, String(value));
    }
    return this.http.get<PagedResult<IssueListItemDto>>(`${this.baseUrl}/projects/${projectId}/issues`, { params });
  }

  getCore(issueId: string): Observable<IssueCoreDto> {
    return this.http.get<IssueCoreDto>(`${this.baseUrl}/issues/${issueId}/core`);
  }

  update(issueId: string, request: UpdateIssueRequest): Observable<IssueCoreDto> {
    return this.http.patch<IssueCoreDto>(`${this.baseUrl}/issues/${issueId}`, request);
  }

  changeParent(issueId: string, parentId: string | null): Observable<IssueCoreDto> {
    return this.http.put<IssueCoreDto>(`${this.baseUrl}/issues/${issueId}/parent`, { parentId });
  }

  rank(issueId: string, previousIssueId: string | null, nextIssueId: string | null): Observable<IssueRankDto> {
    return this.http.put<IssueRankDto>(`${this.baseUrl}/issues/${issueId}/rank`, { previousIssueId, nextIssueId });
  }
}
