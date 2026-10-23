import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type VersionLinkType = 'FixVersion' | 'AffectsVersion';

export interface IssueVersionDto {
  versionId: string;
  name: string;
  linkType: VersionLinkType;
  releaseDate?: string | null;
  isReleased: boolean;
}

export interface ReleaseOptionDto {
  id: string;
  name: string;
  releaseDate?: string | null;
  isReleased: boolean;
}

export interface IssueReleaseLinksDto {
  components: { componentId: string; name: string }[];
  fixVersions: IssueVersionDto[];
  affectsVersions: IssueVersionDto[];
  availableComponents: ReleaseOptionDto[];
  availableVersions: ReleaseOptionDto[];
}

/** 501 = Planning chưa cung cấp Component/Version (contract chưa có bản thật). */
export function isReleaseLookupUnavailable(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 501;
}

/** Component / Fix & Affects Version của Issue (module DeliveryIntelligence). */
@Injectable({ providedIn: 'root' })
export class ReleaseApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/issues`;

  links(issueId: string): Observable<IssueReleaseLinksDto> {
    return this.http.get<IssueReleaseLinksDto>(`${this.baseUrl}/${issueId}/release-links`);
  }

  addComponent(issueId: string, componentId: string): Observable<IssueReleaseLinksDto> {
    return this.http.post<IssueReleaseLinksDto>(`${this.baseUrl}/${issueId}/components`, { componentId });
  }

  removeComponent(issueId: string, componentId: string): Observable<IssueReleaseLinksDto> {
    return this.http.delete<IssueReleaseLinksDto>(`${this.baseUrl}/${issueId}/components/${componentId}`);
  }

  addVersion(issueId: string, versionId: string, linkType: VersionLinkType): Observable<IssueReleaseLinksDto> {
    return this.http.post<IssueReleaseLinksDto>(`${this.baseUrl}/${issueId}/versions`, { versionId, linkType });
  }

  removeVersion(issueId: string, versionId: string, linkType: VersionLinkType): Observable<IssueReleaseLinksDto> {
    return this.http.delete<IssueReleaseLinksDto>(`${this.baseUrl}/${issueId}/versions/${versionId}`, { params: { linkType } });
  }
}
