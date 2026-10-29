import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { WorkspaceDataSource } from './workspace-data-source';
import {
  CreateOrganizationRequest,
  CreateProjectRequest,
  InviteEmailResult,
  InviteLink,
  InviteLinkPreview,
  MyInvitation,
  OrgInvitation,
  OrgMember,
  OrgRoleCode,
  OrgRoleOption,
  OrganizationSummary,
  RecentItem,
  RecordVisitRequest,
  SaveWorkflowTemplateRequest,
  WorkItemSummary,
  WorkflowTemplate,
  WorkspaceProject
} from './workspace.models';

/** Gọi API theo hợp đồng trong tài liệu "Workspace API contract". */
@Injectable()
export class HttpWorkspaceDataSource extends WorkspaceDataSource {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/v1`;

  private org(orgId: string): string {
    return `${this.base}/organizations/${encodeURIComponent(orgId)}`;
  }

  private orgFilter(orgId?: string): HttpParams {
    return orgId ? new HttpParams().set('orgId', orgId) : new HttpParams();
  }

  listOrganizations(): Observable<OrganizationSummary[]> {
    return this.http.get<OrganizationSummary[]>(`${this.base}/organizations`);
  }

  getOrganization(orgId: string): Observable<OrganizationSummary> {
    return this.http.get<OrganizationSummary>(this.org(orgId));
  }

  createOrganization(request: CreateOrganizationRequest): Observable<OrganizationSummary> {
    return this.http.post<OrganizationSummary>(`${this.base}/organizations`, request);
  }

  listMembers(orgId: string): Observable<OrgMember[]> {
    return this.http.get<OrgMember[]>(`${this.org(orgId)}/members`);
  }

  updateMemberRole(orgId: string, userId: string, role: OrgRoleCode): Observable<OrgMember> {
    return this.http.put<OrgMember>(`${this.org(orgId)}/members/${encodeURIComponent(userId)}`, { role });
  }

  removeMember(orgId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.org(orgId)}/members/${encodeURIComponent(userId)}`);
  }

  listRoles(): Observable<OrgRoleOption[]> {
    return this.http.get<OrgRoleOption[]>(`${this.base}/organizations/roles`);
  }

  inviteByEmail(orgId: string, emails: string[], role: OrgRoleCode): Observable<InviteEmailResult[]> {
    return this.http.post<InviteEmailResult[]>(`${this.org(orgId)}/invitations`, { emails, role });
  }

  listInvitations(orgId: string): Observable<OrgInvitation[]> {
    return this.http.get<OrgInvitation[]>(`${this.org(orgId)}/invitations`, { params: { status: 'Pending' } });
  }

  cancelInvitation(orgId: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(`${this.org(orgId)}/invitations/${encodeURIComponent(invitationId)}`);
  }

  listMyInvitations(): Observable<MyInvitation[]> {
    return this.http.get<MyInvitation[]>(`${this.base}/users/me/organization-invitations`);
  }

  respondInvitation(invitationId: string, accept: boolean): Observable<OrganizationSummary | null> {
    return this.http.post<OrganizationSummary | null>(
      `${this.base}/users/me/organization-invitations/${encodeURIComponent(invitationId)}/respond`, { accept });
  }

  getInviteLink(orgId: string): Observable<InviteLink | null> {
    return this.http.get<InviteLink | null>(`${this.org(orgId)}/invite-link`).pipe(map(link => link ?? null));
  }

  regenerateInviteLink(orgId: string, role: OrgRoleCode): Observable<InviteLink> {
    return this.http.post<InviteLink>(`${this.org(orgId)}/invite-link`, { role });
  }

  disableInviteLink(orgId: string): Observable<void> {
    return this.http.delete<void>(`${this.org(orgId)}/invite-link`);
  }

  getInviteByToken(token: string): Observable<InviteLinkPreview> {
    return this.http.get<InviteLinkPreview>(`${this.base}/invite-links/${encodeURIComponent(token)}`);
  }

  acceptInviteLink(token: string): Observable<OrganizationSummary> {
    return this.http.post<OrganizationSummary>(`${this.base}/invite-links/${encodeURIComponent(token)}/accept`, {});
  }

  listWorkflowTemplates(orgId: string): Observable<WorkflowTemplate[]> {
    return this.http.get<WorkflowTemplate[]>(`${this.org(orgId)}/workflow-templates`);
  }

  createWorkflowTemplate(orgId: string, request: SaveWorkflowTemplateRequest): Observable<WorkflowTemplate> {
    return this.http.post<WorkflowTemplate>(`${this.org(orgId)}/workflow-templates`, request);
  }

  updateWorkflowTemplate(orgId: string, templateId: string, request: SaveWorkflowTemplateRequest): Observable<WorkflowTemplate> {
    return this.http.put<WorkflowTemplate>(`${this.org(orgId)}/workflow-templates/${encodeURIComponent(templateId)}`, request);
  }

  deleteWorkflowTemplate(orgId: string, templateId: string): Observable<void> {
    return this.http.delete<void>(`${this.org(orgId)}/workflow-templates/${encodeURIComponent(templateId)}`);
  }

  setDefaultWorkflowTemplate(orgId: string, templateId: string): Observable<WorkflowTemplate> {
    return this.http.put<WorkflowTemplate>(`${this.org(orgId)}/workflow-templates/${encodeURIComponent(templateId)}/default`, {});
  }

  listProjects(orgId: string): Observable<WorkspaceProject[]> {
    return this.http.get<WorkspaceProject[]>(`${this.org(orgId)}/projects`);
  }

  isProjectKeyAvailable(orgId: string, projectKey: string): Observable<boolean> {
    return this.http
      .get<{ available: boolean }>(`${this.org(orgId)}/projects/key-availability`, { params: { key: projectKey } })
      .pipe(map(result => result.available));
  }

  createProject(orgId: string, request: CreateProjectRequest): Observable<WorkspaceProject> {
    return this.http.post<WorkspaceProject>(`${this.org(orgId)}/projects`, request);
  }

  listMyWork(orgId?: string): Observable<WorkItemSummary[]> {
    return this.http.get<WorkItemSummary[]>(`${this.base}/users/me/work-items`, { params: this.orgFilter(orgId) });
  }

  listRecent(orgId?: string): Observable<RecentItem[]> {
    return this.http.get<RecentItem[]>(`${this.base}/users/me/recent`, { params: this.orgFilter(orgId) });
  }

  recordVisit(request: RecordVisitRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/users/me/recent`, request);
  }
}
