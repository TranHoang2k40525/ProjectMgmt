import { Observable } from 'rxjs';
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

/**
 * Nguồn dữ liệu của không gian Tổ chức → Dự án. Màn hình chỉ phụ thuộc lớp này;
 * bản cài đặt (mock giả lập DB hoặc HTTP) được chọn trong {@link provideWorkspaceData}.
 * Lỗi trả về dạng HttpErrorResponse với body ProblemDetails (`title`, `detail`, `code`).
 */
export abstract class WorkspaceDataSource {
  abstract listOrganizations(): Observable<OrganizationSummary[]>;
  abstract getOrganization(orgId: string): Observable<OrganizationSummary>;
  abstract createOrganization(request: CreateOrganizationRequest): Observable<OrganizationSummary>;

  abstract listMembers(orgId: string): Observable<OrgMember[]>;
  abstract updateMemberRole(orgId: string, userId: string, role: OrgRoleCode): Observable<OrgMember>;
  abstract removeMember(orgId: string, userId: string): Observable<void>;
  abstract listRoles(): Observable<OrgRoleOption[]>;

  abstract inviteByEmail(orgId: string, emails: string[], role: OrgRoleCode): Observable<InviteEmailResult[]>;
  abstract listInvitations(orgId: string): Observable<OrgInvitation[]>;
  abstract cancelInvitation(orgId: string, invitationId: string): Observable<void>;
  abstract listMyInvitations(): Observable<MyInvitation[]>;
  abstract respondInvitation(invitationId: string, accept: boolean): Observable<OrganizationSummary | null>;

  /** null khi tổ chức chưa bật link mời. */
  abstract getInviteLink(orgId: string): Observable<InviteLink | null>;
  abstract regenerateInviteLink(orgId: string, role: OrgRoleCode): Observable<InviteLink>;
  abstract disableInviteLink(orgId: string): Observable<void>;
  abstract getInviteByToken(token: string): Observable<InviteLinkPreview>;
  abstract acceptInviteLink(token: string): Observable<OrganizationSummary>;

  /** Gồm mẫu có sẵn của hệ thống (isPreset) và mẫu riêng của tổ chức. */
  abstract listWorkflowTemplates(orgId: string): Observable<WorkflowTemplate[]>;
  abstract createWorkflowTemplate(orgId: string, request: SaveWorkflowTemplateRequest): Observable<WorkflowTemplate>;
  abstract updateWorkflowTemplate(orgId: string, templateId: string, request: SaveWorkflowTemplateRequest): Observable<WorkflowTemplate>;
  abstract deleteWorkflowTemplate(orgId: string, templateId: string): Observable<void>;
  abstract setDefaultWorkflowTemplate(orgId: string, templateId: string): Observable<WorkflowTemplate>;

  /** Dự án của tổ chức mà người dùng hiện tại là thành viên. */
  abstract listProjects(orgId: string): Observable<WorkspaceProject[]>;
  abstract isProjectKeyAvailable(orgId: string, projectKey: string): Observable<boolean>;
  abstract createProject(orgId: string, request: CreateProjectRequest): Observable<WorkspaceProject>;

  /** Công việc giao cho người dùng hiện tại; orgId để lọc theo tổ chức. */
  abstract listMyWork(orgId?: string): Observable<WorkItemSummary[]>;
  abstract listRecent(orgId?: string): Observable<RecentItem[]>;
  abstract recordVisit(request: RecordVisitRequest): Observable<void>;
}
