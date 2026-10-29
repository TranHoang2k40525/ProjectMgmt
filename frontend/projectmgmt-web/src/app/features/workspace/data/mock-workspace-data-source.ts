import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable, defer, of, switchMap, throwError, timer } from 'rxjs';
import { IdentityService } from '../../../core/services/identity.service';
import { EMAIL_PATTERN, isValidProjectKey } from '../project-key';
import { MockWorkspaceDb, PRESET_WORKFLOW_TEMPLATES, createWorkspaceSeed } from './mock-workspace-seed';
import { WorkspaceDataSource } from './workspace-data-source';
import {
  CreateOrganizationRequest,
  CreateProjectRequest,
  DEFAULT_ISSUE_TYPES,
  InviteEmailResult,
  InviteLink,
  InviteLinkPreview,
  MyInvitation,
  OrgInvitation,
  OrgMember,
  OrgRoleCode,
  OrgRoleOption,
  OrganizationSummary,
  PROJECT_ROLE_OPTIONS,
  ProjectRoleCode,
  RecentItem,
  RecordVisitRequest,
  SaveWorkflowTemplateRequest,
  WorkItemSummary,
  WorkflowTemplate,
  WorkspaceProject,
  WorkspaceUser
} from './workspace.models';

/** Độ trễ giả lập mỗi lời gọi (ms); test đặt 0 để chạy đồng bộ. */
export const MOCK_WORKSPACE_LATENCY = new InjectionToken<number>('MOCK_WORKSPACE_LATENCY', {
  providedIn: 'root',
  factory: () => 180
});

/**
 * `seed`: tạo dữ liệu mẫu cho người dùng mới (chế độ demo 'mock').
 * Chế độ 'hybrid' tắt seed và dùng khóa lưu riêng: kho này chỉ giữ phần backend chưa có,
 * còn tổ chức, dự án, người dùng được đồng bộ từ API thật qua {@link MockWorkspaceDataSource.syncReal}.
 */
export interface MockWorkspaceOptions {
  seed: boolean;
  storagePrefix: string;
}

export const MOCK_WORKSPACE_OPTIONS = new InjectionToken<MockWorkspaceOptions>('MOCK_WORKSPACE_OPTIONS', {
  providedIn: 'root',
  factory: () => ({ seed: true, storagePrefix: 'pm-workspace-db-v1:' })
});

/** Dữ liệu thật đưa vào kho phụ ở chế độ hybrid. */
export interface RealWorkspaceSnapshot {
  users: { id: string; displayName: string; email: string; avatarUrl: string | null }[];
  organizations: { id: string; name: string; slug: string; ownerId: string; createdAt: string }[];
  /** Dự án người dùng hiện tại truy cập được (API thật đã lọc theo quyền). */
  projects: { id: string; orgId: string; projectKey: string; name: string; description: string | null; leadUserId: string; type?: string; createdAt: string }[];
  workItems?: { id: string; projectId: string; issueKey: string; title: string; statusName: string; statusCategory: Db['workItems'][number]['statusCategory']; dueDate: string | null }[];
}
const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const MAX_INVITE_EMAILS = 20;
const MAX_RECENT = 8;

type Db = MockWorkspaceDb;
type OrgRow = Db['organizations'][number];
type ProjectRow = Db['projects'][number];

function fail(status: number, code: string, title: string): never {
  throw new HttpErrorResponse({ status, statusText: title, error: { status, title, detail: title, code } });
}

function newId(prefix: string): string {
  const random = typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
  return `${prefix}-${random}`;
}

function newToken(): string {
  const alphabet = 'abcdefghijkmnpqrstuvwxyz23456789';
  let token = '';
  for (let i = 0; i < 20; i++) token += alphabet[Math.floor(Math.random() * alphabet.length)];
  return token;
}

function emptyDb(me: WorkspaceUser): Db {
  return {
    version: 1, users: [me], organizations: [], orgMembers: [], invitations: [], inviteLinks: [], workflowTemplates: [],
    projects: [], projectMembers: [], issueTypes: [], workItems: [], recentVisits: []
  };
}

function clone<T>(value: T): T {
  return value === undefined ? value : JSON.parse(JSON.stringify(value)) as T;
}

/**
 * Giả lập cơ sở dữ liệu của không gian Tổ chức → Dự án trong trình duyệt: các bảng nằm trong localStorage
 * (mỗi người dùng một khối), ràng buộc và mã lỗi giống API thật để màn hình xử lý như khi gọi server.
 */
@Injectable()
export class MockWorkspaceDataSource extends WorkspaceDataSource {
  private readonly identity = inject(IdentityService);
  private readonly latency = inject(MOCK_WORKSPACE_LATENCY);
  private readonly options = inject(MOCK_WORKSPACE_OPTIONS);
  private db: Db | null = null;
  private dbOwnerId = '';

  listOrganizations(): Observable<OrganizationSummary[]> {
    return this.read(db => {
      const me = this.me();
      return db.organizations
        .filter(org => this.memberRow(db, org.id, me.id))
        .map(org => this.toOrgSummary(db, org))
        .sort((a, b) => a.name.localeCompare(b.name, 'vi'));
    });
  }

  getOrganization(orgId: string): Observable<OrganizationSummary> {
    return this.read(db => this.toOrgSummary(db, this.requireMemberOrg(db, orgId)));
  }

  createOrganization(request: CreateOrganizationRequest): Observable<OrganizationSummary> {
    return this.run(db => {
      const me = this.me();
      const name = request.name.trim();
      const slug = request.slug.trim().toLowerCase();
      if (name.length < 2 || name.length > 100) fail(400, 'ORG_NAME_INVALID', 'Tên tổ chức từ 2 đến 100 ký tự.');
      if (slug.length < 2 || slug.length > 60 || !SLUG_PATTERN.test(slug)) {
        fail(400, 'ORG_SLUG_INVALID', 'Đường dẫn chỉ gồm chữ thường, số và dấu gạch nối (2–60 ký tự).');
      }
      if (db.organizations.some(org => org.slug === slug)) fail(409, 'ORG_SLUG_DUPLICATE', 'Đường dẫn tổ chức đã được dùng.');
      const now = new Date().toISOString();
      const org: OrgRow = { id: newId('org'), name, slug, ownerId: me.id, defaultTemplateId: PRESET_WORKFLOW_TEMPLATES[0].id, createdAt: now };
      db.organizations.push(org);
      db.orgMembers.push({ orgId: org.id, userId: me.id, role: 'Owner', joinedAt: now });
      return this.toOrgSummary(db, org);
    });
  }

  listMembers(orgId: string): Observable<OrgMember[]> {
    return this.read(db => {
      this.requireMemberOrg(db, orgId);
      const rank: Record<OrgRoleCode, number> = { Owner: 0, Admin: 1, Member: 2 };
      return db.orgMembers
        .filter(member => member.orgId === orgId)
        .map(member => this.toMember(db, member))
        .sort((a, b) => rank[a.role] - rank[b.role] || a.displayName.localeCompare(b.displayName, 'vi'));
    });
  }

  updateMemberRole(orgId: string, userId: string, role: OrgRoleCode): Observable<OrgMember> {
    return this.run(db => {
      const org = this.requireOwnerOrg(db, orgId);
      if (role === 'Owner') fail(400, 'ORG_ROLE_INVALID', 'Không thể gán vai trò Chủ tổ chức.');
      if (userId === org.ownerId) fail(409, 'ORG_OWNER_ROLE_LOCKED', 'Không thể đổi vai trò của chủ tổ chức.');
      const member = this.memberRow(db, orgId, userId) ?? fail(404, 'ORG_MEMBER_NOT_FOUND', 'Người này không còn trong tổ chức.');
      member.role = role;
      return this.toMember(db, member);
    });
  }

  removeMember(orgId: string, userId: string): Observable<void> {
    return this.run(db => {
      const org = this.requireOwnerOrg(db, orgId);
      if (userId === org.ownerId) fail(409, 'ORG_OWNER_ROLE_LOCKED', 'Không thể gỡ chủ tổ chức.');
      if (!this.memberRow(db, orgId, userId)) fail(404, 'ORG_MEMBER_NOT_FOUND', 'Người này không còn trong tổ chức.');
      const orgProjectIds = new Set(db.projects.filter(project => project.orgId === orgId).map(project => project.id));
      db.orgMembers = db.orgMembers.filter(member => !(member.orgId === orgId && member.userId === userId));
      db.projectMembers = db.projectMembers.filter(member => !(member.userId === userId && orgProjectIds.has(member.projectId)));
    });
  }

  listRoles(): Observable<OrgRoleOption[]> {
    return this.read(() => [
      { code: 'Owner', name: 'Chủ tổ chức', description: 'Người tạo tổ chức, toàn quyền cài đặt.', assignable: false },
      { code: 'Admin', name: 'Quản trị', description: 'Quản lý dự án và hỗ trợ thành viên.', assignable: true },
      { code: 'Member', name: 'Thành viên', description: 'Tham gia các dự án được thêm vào.', assignable: true }
    ]);
  }

  inviteByEmail(orgId: string, emails: string[], role: OrgRoleCode): Observable<InviteEmailResult[]> {
    return this.run(db => {
      this.requireOwnerOrg(db, orgId);
      if (role === 'Owner') fail(400, 'ORG_ROLE_INVALID', 'Không thể mời với vai trò Chủ tổ chức.');
      const unique = [...new Set(emails.map(email => email.trim().toLowerCase()).filter(email => email.length > 0))];
      if (unique.length === 0) fail(400, 'INVITE_EMAILS_REQUIRED', 'Nhập ít nhất một email.');
      if (unique.length > MAX_INVITE_EMAILS) fail(400, 'INVITE_EMAILS_TOO_MANY', `Mỗi lần mời tối đa ${MAX_INVITE_EMAILS} email.`);
      const invalid = unique.find(email => !EMAIL_PATTERN.test(email));
      if (invalid) fail(400, 'INVITE_EMAIL_INVALID', `Email không hợp lệ: ${invalid}`);

      const me = this.me();
      return unique.map((email): InviteEmailResult => {
        const user = db.users.find(candidate => candidate.email.toLowerCase() === email);
        if (!user) return { email, outcome: 'NoAccount' };
        if (this.memberRow(db, orgId, user.id)) return { email, outcome: 'AlreadyMember' };
        if (db.invitations.some(invite => invite.orgId === orgId && invite.email === email && invite.status === 'Pending')) {
          return { email, outcome: 'AlreadyInvited' };
        }
        db.invitations.push({ id: newId('invite'), orgId, email, role, invitedBy: me.id, createdAt: new Date().toISOString(), status: 'Pending' });
        return { email, outcome: 'Sent' };
      });
    });
  }

  listInvitations(orgId: string): Observable<OrgInvitation[]> {
    return this.read(db => {
      this.requireOwnerOrg(db, orgId);
      return db.invitations
        .filter(invite => invite.orgId === orgId && invite.status === 'Pending')
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
        .map(invite => ({
          id: invite.id,
          orgId: invite.orgId,
          email: invite.email,
          role: invite.role,
          invitedByName: this.userName(db, invite.invitedBy),
          createdAt: invite.createdAt
        }));
    });
  }

  cancelInvitation(orgId: string, invitationId: string): Observable<void> {
    return this.run(db => {
      this.requireOwnerOrg(db, orgId);
      const invite = db.invitations.find(item => item.id === invitationId && item.orgId === orgId && item.status === 'Pending')
        ?? fail(404, 'INVITATION_NOT_FOUND', 'Lời mời không còn hiệu lực.');
      invite.status = 'Cancelled';
    });
  }

  listMyInvitations(): Observable<MyInvitation[]> {
    return this.read(db => {
      const email = this.me().email.toLowerCase();
      return db.invitations
        .filter(invite => invite.email === email && invite.status === 'Pending')
        .map(invite => ({
          id: invite.id,
          orgId: invite.orgId,
          orgName: db.organizations.find(org => org.id === invite.orgId)?.name ?? '',
          role: invite.role,
          invitedByName: this.userName(db, invite.invitedBy),
          createdAt: invite.createdAt
        }));
    });
  }

  respondInvitation(invitationId: string, accept: boolean): Observable<OrganizationSummary | null> {
    return this.run(db => {
      const me = this.me();
      const invite = db.invitations.find(item => item.id === invitationId && item.status === 'Pending' && item.email === me.email.toLowerCase())
        ?? fail(404, 'INVITATION_NOT_FOUND', 'Lời mời không còn hiệu lực.');
      invite.status = accept ? 'Accepted' : 'Declined';
      if (!accept) return null;
      this.addMember(db, invite.orgId, me.id, invite.role);
      return this.toOrgSummary(db, this.orgRow(db, invite.orgId));
    });
  }

  getInviteLink(orgId: string): Observable<InviteLink | null> {
    return this.read(db => {
      this.requireOwnerOrg(db, orgId);
      const link = db.inviteLinks.find(item => item.orgId === orgId && item.isActive);
      return link ? { token: link.token, orgId, role: link.role, createdAt: link.createdAt } : null;
    });
  }

  regenerateInviteLink(orgId: string, role: OrgRoleCode): Observable<InviteLink> {
    return this.run(db => {
      this.requireOwnerOrg(db, orgId);
      if (role === 'Owner') fail(400, 'ORG_ROLE_INVALID', 'Không thể tạo link với vai trò Chủ tổ chức.');
      db.inviteLinks.filter(item => item.orgId === orgId).forEach(item => (item.isActive = false));
      const link = { token: newToken(), orgId, role, createdBy: this.me().id, createdAt: new Date().toISOString(), isActive: true };
      db.inviteLinks.push(link);
      return { token: link.token, orgId, role, createdAt: link.createdAt };
    });
  }

  disableInviteLink(orgId: string): Observable<void> {
    return this.run(db => {
      this.requireOwnerOrg(db, orgId);
      db.inviteLinks.filter(item => item.orgId === orgId).forEach(item => (item.isActive = false));
    });
  }

  getInviteByToken(token: string): Observable<InviteLinkPreview> {
    return this.read(db => {
      const link = this.activeLink(db, token);
      const org = this.orgRow(db, link.orgId);
      return {
        token,
        orgId: org.id,
        orgName: org.name,
        invitedByName: this.userName(db, link.createdBy),
        role: link.role,
        alreadyMember: !!this.memberRow(db, org.id, this.me().id)
      };
    });
  }

  acceptInviteLink(token: string): Observable<OrganizationSummary> {
    return this.run(db => {
      const link = this.activeLink(db, token);
      this.addMember(db, link.orgId, this.me().id, link.role);
      return this.toOrgSummary(db, this.orgRow(db, link.orgId));
    });
  }

  listWorkflowTemplates(orgId: string): Observable<WorkflowTemplate[]> {
    return this.read(db => {
      const org = this.requireMemberOrg(db, orgId);
      const presets: WorkflowTemplate[] = PRESET_WORKFLOW_TEMPLATES.map(preset => ({
        ...clone(preset), orgId: null, isPreset: true, isDefault: preset.id === org.defaultTemplateId
      }));
      const custom = db.workflowTemplates
        .filter(template => template.orgId === orgId)
        .map(template => ({ ...clone(template), isPreset: false, isDefault: template.id === org.defaultTemplateId }));
      return [...presets, ...custom];
    });
  }

  createWorkflowTemplate(orgId: string, request: SaveWorkflowTemplateRequest): Observable<WorkflowTemplate> {
    return this.run(db => {
      const org = this.requireOwnerOrg(db, orgId);
      const valid = this.validateTemplate(db, orgId, request, null);
      const row = { id: newId('template'), orgId, ...valid };
      db.workflowTemplates.push(row);
      return { ...clone(row), isPreset: false, isDefault: org.defaultTemplateId === row.id };
    });
  }

  updateWorkflowTemplate(orgId: string, templateId: string, request: SaveWorkflowTemplateRequest): Observable<WorkflowTemplate> {
    return this.run(db => {
      const org = this.requireOwnerOrg(db, orgId);
      const row = this.customTemplate(db, orgId, templateId);
      Object.assign(row, this.validateTemplate(db, orgId, request, templateId));
      return { ...clone(row), isPreset: false, isDefault: org.defaultTemplateId === row.id };
    });
  }

  deleteWorkflowTemplate(orgId: string, templateId: string): Observable<void> {
    return this.run(db => {
      const org = this.requireOwnerOrg(db, orgId);
      this.customTemplate(db, orgId, templateId);
      if (org.defaultTemplateId === templateId) {
        fail(409, 'WORKFLOW_TEMPLATE_IN_USE_AS_DEFAULT', 'Đang là mẫu mặc định; hãy chọn mẫu mặc định khác trước khi xóa.');
      }
      db.workflowTemplates = db.workflowTemplates.filter(template => template.id !== templateId);
    });
  }

  setDefaultWorkflowTemplate(orgId: string, templateId: string): Observable<WorkflowTemplate> {
    return this.run(db => {
      const org = this.requireOwnerOrg(db, orgId);
      const template = this.findTemplate(db, orgId, templateId) ?? fail(404, 'WORKFLOW_TEMPLATE_NOT_FOUND', 'Không tìm thấy mẫu workflow.');
      org.defaultTemplateId = templateId;
      return { ...clone(template), orgId: template.isPreset ? null : orgId, isDefault: true };
    });
  }

  listProjects(orgId: string): Observable<WorkspaceProject[]> {
    return this.read(db => {
      this.requireMemberOrg(db, orgId);
      const me = this.me();
      return db.projects
        .filter(project => project.orgId === orgId && db.projectMembers.some(member => member.projectId === project.id && member.userId === me.id))
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
        .map(project => this.toProject(db, project));
    });
  }

  isProjectKeyAvailable(orgId: string, projectKey: string): Observable<boolean> {
    return this.read(db => {
      this.requireMemberOrg(db, orgId);
      return !this.keyTaken(db, orgId, projectKey);
    });
  }

  createProject(orgId: string, request: CreateProjectRequest): Observable<WorkspaceProject> {
    return this.run(db => {
      this.requireMemberOrg(db, orgId);
      const me = this.me();
      const name = request.name.trim();
      const key = request.projectKey.trim().toUpperCase();
      const description = request.description?.trim() || null;
      if (name.length < 2 || name.length > 100) fail(400, 'PROJECT_NAME_INVALID', 'Tên dự án từ 2 đến 100 ký tự.');
      if (!isValidProjectKey(key)) fail(400, 'PROJECT_KEY_INVALID', 'Mã dự án gồm 2–10 chữ in hoa hoặc số, bắt đầu bằng chữ.');
      if (this.keyTaken(db, orgId, key)) fail(409, 'PROJECT_KEY_DUPLICATE', `Mã ${key} đã được dùng trong tổ chức.`);
      if (description && description.length > 1000) fail(400, 'PROJECT_DESCRIPTION_TOO_LONG', 'Mô tả tối đa 1000 ký tự.');
      if (!this.memberRow(db, orgId, request.leadUserId)) fail(400, 'PROJECT_LEAD_INVALID', 'Người phụ trách phải là thành viên tổ chức.');
      const template = this.findTemplate(db, orgId, request.workflowTemplateId)
        ?? fail(400, 'WORKFLOW_TEMPLATE_NOT_FOUND', 'Mẫu workflow không còn tồn tại.');
      const issueTypes = this.validateIssueTypes(request);
      const validRoles = new Set(PROJECT_ROLE_OPTIONS.map(option => option.code));
      for (const member of request.members) {
        if (!this.memberRow(db, orgId, member.userId)) fail(400, 'PROJECT_MEMBER_INVALID', 'Thành viên dự án phải thuộc tổ chức.');
        if (!validRoles.has(member.role)) fail(400, 'PROJECT_ROLE_INVALID', 'Vai trò dự án không hợp lệ.');
      }

      const project: ProjectRow = {
        id: newId('project'),
        orgId,
        projectKey: key,
        name,
        description,
        leadUserId: request.leadUserId,
        workflowTemplateId: template.id,
        workflowTemplateName: template.name,
        managementType: template.managementType,
        statuses: clone(template.statuses),
        issueCounter: 0,
        createdAt: new Date().toISOString()
      };
      db.projects.push(project);
      // Người tạo và người phụ trách luôn là ProjectManager; người được chọn thêm giữ vai trò đã chọn.
      const added = new Set<string>();
      const join = (userId: string, role: ProjectRoleCode) => {
        if (added.has(userId)) return;
        added.add(userId);
        db.projectMembers.push({ projectId: project.id, userId, role });
      };
      join(me.id, 'ProjectManager');
      join(request.leadUserId, 'ProjectManager');
      request.members.forEach(member => join(member.userId, member.role));
      db.issueTypes.push(...issueTypes.map(type => ({ projectId: project.id, code: type.code, name: type.name })));
      this.touch(db, 'project', project.id);
      return this.toProject(db, project);
    });
  }

  listMyWork(orgId?: string): Observable<WorkItemSummary[]> {
    return this.read(db => {
      const me = this.me();
      const visibleOrgIds = new Set(db.orgMembers.filter(member => member.userId === me.id).map(member => member.orgId));
      return db.workItems
        .filter(item => item.assigneeId === me.id)
        .map(item => ({ item, project: db.projects.find(project => project.id === item.projectId) }))
        .filter((entry): entry is { item: Db['workItems'][number]; project: ProjectRow } =>
          !!entry.project && visibleOrgIds.has(entry.project.orgId) && (!orgId || entry.project.orgId === orgId))
        .map(({ item, project }) => ({
          id: item.id,
          issueKey: `${project.projectKey}-${item.number}`,
          title: item.title,
          projectId: project.id,
          projectName: project.name,
          orgId: project.orgId,
          orgName: this.orgRow(db, project.orgId).name,
          statusName: item.statusName,
          statusCategory: item.statusCategory,
          priority: item.priority,
          dueDate: item.dueDate
        }))
        .sort((a, b) => (a.dueDate ?? '9999').localeCompare(b.dueDate ?? '9999'));
    });
  }

  listRecent(orgId?: string): Observable<RecentItem[]> {
    return this.read(db => {
      const me = this.me();
      const isMember = (id: string) => !!this.memberRow(db, id, me.id);
      const items: RecentItem[] = [];
      for (const visit of [...db.recentVisits].sort((a, b) => b.visitedAt.localeCompare(a.visitedAt))) {
        const item = this.describeVisit(db, visit);
        if (!item || !isMember(item.orgId) || (orgId && item.orgId !== orgId)) continue;
        items.push(item);
        if (items.length === MAX_RECENT) break;
      }
      return items;
    });
  }

  recordVisit(request: RecordVisitRequest): Observable<void> {
    return this.run(db => this.touch(db, request.kind, request.id));
  }

  /**
   * Chế độ hybrid: thay tổ chức/dự án/người dùng trong kho phụ bằng dữ liệu thật, giữ phần chỉ kho phụ có
   * (thành viên, lời mời, link mời, mẫu workflow, mẫu đã dùng của dự án, Gần đây).
   */
  syncReal(snapshot: RealWorkspaceSnapshot): void {
    const db = this.load();
    const me = this.me();
    for (const user of snapshot.users) {
      const existing = db.users.find(item => item.id === user.id);
      if (existing) Object.assign(existing, user); else db.users.push({ ...user });
    }

    const realOrgIds = new Set(snapshot.organizations.map(org => org.id));
    db.organizations = snapshot.organizations.map(org => {
      const previous = db.organizations.find(item => item.id === org.id);
      return { ...org, defaultTemplateId: previous?.defaultTemplateId ?? PRESET_WORKFLOW_TEMPLATES[0].id };
    });
    db.orgMembers = db.orgMembers.filter(member => realOrgIds.has(member.orgId));
    for (const org of snapshot.organizations) {
      this.ensureMember(db, org.id, org.ownerId, 'Owner');
      this.ensureMember(db, org.id, me.id, 'Member');
    }

    const previousProjects = new Map(db.projects.map(project => [project.id, project]));
    db.projects = snapshot.projects.filter(project => realOrgIds.has(project.orgId)).map(project => {
      const previous = previousProjects.get(project.id);
      const managementType = previous?.managementType ?? (project.type?.toLowerCase().includes('kanban') ? 'Kanban' : 'Scrum');
      return {
        id: project.id,
        orgId: project.orgId,
        projectKey: project.projectKey,
        name: project.name,
        description: project.description,
        leadUserId: project.leadUserId,
        workflowTemplateId: previous?.workflowTemplateId ?? '',
        workflowTemplateName: previous?.workflowTemplateName ?? '',
        managementType,
        statuses: previous?.statuses ?? [],
        issueCounter: previous?.issueCounter ?? 0,
        createdAt: project.createdAt
      };
    });
    const projectIds = new Set(db.projects.map(project => project.id));
    db.projectMembers = db.projectMembers.filter(member => projectIds.has(member.projectId));
    for (const project of db.projects) {
      if (!db.projectMembers.some(member => member.projectId === project.id && member.userId === me.id)) {
        db.projectMembers.push({ projectId: project.id, userId: me.id, role: 'Developer' });
      }
    }

    if (snapshot.workItems) {
      db.workItems = snapshot.workItems.filter(item => projectIds.has(item.projectId)).map(item => ({
        id: item.id,
        projectId: item.projectId,
        number: Number(item.issueKey.split('-').pop()) || 0,
        title: item.title,
        assigneeId: me.id,
        statusName: item.statusName,
        statusCategory: item.statusCategory,
        priority: 'Medium',
        dueDate: item.dueDate
      }));
    }
    this.persist(db);
  }

  /** Ghi thành viên dự án thật vào kho phụ để tổ chức có danh sách thành viên khi backend chưa có API. */
  addKnownMembers(orgId: string, projectId: string, userIds: string[]): void {
    const db = this.load();
    if (!db.organizations.some(org => org.id === orgId)) return;
    for (const userId of userIds) {
      this.ensureMember(db, orgId, userId, 'Member');
      if (!db.projectMembers.some(member => member.projectId === projectId && member.userId === userId)) {
        db.projectMembers.push({ projectId, userId, role: 'Developer' });
      }
    }
    this.persist(db);
  }

  /** Lưu mẫu workflow mà wizard đã dùng cho dự án thật vừa tạo (backend chưa lưu thông tin này). */
  rememberProjectTemplate(projectId: string, templateId: string, orgId: string): void {
    const db = this.load();
    const project = db.projects.find(item => item.id === projectId);
    const template = this.findTemplate(db, orgId, templateId);
    if (!project || !template) return;
    project.workflowTemplateId = template.id;
    project.workflowTemplateName = template.name;
    project.managementType = template.managementType;
    project.statuses = clone(template.statuses);
    this.persist(db);
  }

  // --- hạ tầng -------------------------------------------------------------

  private ensureMember(db: Db, orgId: string, userId: string, role: OrgRoleCode): void {
    const existing = this.memberRow(db, orgId, userId);
    if (existing) {
      if (role === 'Owner') existing.role = 'Owner';
      return;
    }
    db.orgMembers.push({ orgId, userId, role, joinedAt: new Date().toISOString() });
  }

  /** Lời gọi chỉ đọc: không ghi lại localStorage. */
  private read<T>(work: (db: Db) => T): Observable<T> {
    return this.run(work, false);
  }

  private run<T>(work: (db: Db) => T, mutates = true): Observable<T> {
    const result$ = defer(() => {
      try {
        const db = this.load();
        const value = work(db);
        if (mutates) this.persist(db);
        return of(clone(value));
      } catch (error) {
        return throwError(() => error);
      }
    });
    return this.latency > 0 ? timer(this.latency).pipe(switchMap(() => result$)) : result$;
  }

  private me(): WorkspaceUser {
    const user = this.identity.authState().currentUser;
    if (!user) fail(401, 'AUTH_UNAUTHENTICATED', 'Phiên đăng nhập đã hết hạn.');
    return { id: user.id, displayName: user.displayName || user.email, email: user.email, avatarUrl: user.avatarUrl ?? null };
  }

  private load(): Db {
    const me = this.me();
    if (this.db && this.dbOwnerId === me.id) return this.db;
    const db = this.readStored(me.id) ?? (this.options.seed ? createWorkspaceSeed(me) : emptyDb(me));
    const self = db.users.find(user => user.id === me.id);
    if (self) Object.assign(self, me); else db.users.unshift(me);
    this.db = db;
    this.dbOwnerId = me.id;
    return db;
  }

  private readStored(userId: string): Db | null {
    try {
      const raw = typeof localStorage === 'undefined' ? null : localStorage.getItem(this.options.storagePrefix + userId);
      const parsed = raw ? JSON.parse(raw) as Db : null;
      return parsed?.version === 1 ? parsed : null;
    } catch {
      return null;
    }
  }

  private persist(db: Db): void {
    try {
      if (typeof localStorage !== 'undefined') localStorage.setItem(this.options.storagePrefix + this.dbOwnerId, JSON.stringify(db));
    } catch {
      /* Bộ nhớ trình duyệt bị chặn hoặc đầy: dữ liệu vẫn dùng được trong phiên hiện tại. */
    }
  }

  private orgRow(db: Db, orgId: string): OrgRow {
    return db.organizations.find(org => org.id === orgId) ?? fail(404, 'ORG_NOT_FOUND', 'Không tìm thấy tổ chức.');
  }

  private memberRow(db: Db, orgId: string, userId: string) {
    return db.orgMembers.find(member => member.orgId === orgId && member.userId === userId);
  }

  /** Không phải thành viên thì trả 404 như tổ chức không tồn tại, tránh lộ thông tin. */
  private requireMemberOrg(db: Db, orgId: string): OrgRow {
    const org = this.orgRow(db, orgId);
    if (!this.memberRow(db, orgId, this.me().id)) fail(404, 'ORG_NOT_FOUND', 'Không tìm thấy tổ chức.');
    return org;
  }

  private requireOwnerOrg(db: Db, orgId: string): OrgRow {
    const org = this.requireMemberOrg(db, orgId);
    if (org.ownerId !== this.me().id) fail(403, 'ORG_FORBIDDEN', 'Chỉ chủ tổ chức được thay đổi cài đặt.');
    return org;
  }

  private addMember(db: Db, orgId: string, userId: string, role: OrgRoleCode): void {
    if (this.memberRow(db, orgId, userId)) return;
    db.orgMembers.push({ orgId, userId, role: role === 'Owner' ? 'Member' : role, joinedAt: new Date().toISOString() });
  }

  private activeLink(db: Db, token: string) {
    return db.inviteLinks.find(link => link.token === token && link.isActive)
      ?? fail(404, 'INVITE_LINK_INVALID', 'Link mời không còn hiệu lực.');
  }

  private userName(db: Db, userId: string): string {
    return db.users.find(user => user.id === userId)?.displayName ?? 'Người dùng';
  }

  private toOrgSummary(db: Db, org: OrgRow): OrganizationSummary {
    const me = this.me();
    return {
      id: org.id,
      name: org.name,
      slug: org.slug,
      ownerId: org.ownerId,
      ownerName: this.userName(db, org.ownerId),
      myRole: this.memberRow(db, org.id, me.id)?.role ?? 'Member',
      projectCount: db.projects.filter(project => project.orgId === org.id
        && db.projectMembers.some(member => member.projectId === project.id && member.userId === me.id)).length,
      memberCount: db.orgMembers.filter(member => member.orgId === org.id).length,
      createdAt: org.createdAt
    };
  }

  private toMember(db: Db, member: Db['orgMembers'][number]): OrgMember {
    const user = db.users.find(candidate => candidate.id === member.userId);
    return {
      userId: member.userId,
      displayName: user?.displayName ?? 'Người dùng',
      email: user?.email ?? '',
      avatarUrl: user?.avatarUrl ?? null,
      role: member.role,
      joinedAt: member.joinedAt
    };
  }

  private toProject(db: Db, project: ProjectRow): WorkspaceProject {
    return {
      id: project.id,
      orgId: project.orgId,
      projectKey: project.projectKey,
      name: project.name,
      description: project.description,
      leadUserId: project.leadUserId,
      leadName: this.userName(db, project.leadUserId),
      workflowTemplateId: project.workflowTemplateId,
      workflowTemplateName: project.workflowTemplateName,
      managementType: project.managementType,
      memberCount: db.projectMembers.filter(member => member.projectId === project.id).length,
      createdAt: project.createdAt
    };
  }

  private findTemplate(db: Db, orgId: string, templateId: string) {
    const preset = PRESET_WORKFLOW_TEMPLATES.find(item => item.id === templateId);
    if (preset) return { ...clone(preset), isPreset: true };
    const custom = db.workflowTemplates.find(item => item.id === templateId && item.orgId === orgId);
    return custom ? { ...clone(custom), isPreset: false } : null;
  }

  private customTemplate(db: Db, orgId: string, templateId: string) {
    if (PRESET_WORKFLOW_TEMPLATES.some(item => item.id === templateId)) {
      fail(409, 'WORKFLOW_TEMPLATE_PRESET_READONLY', 'Mẫu có sẵn không sửa hoặc xóa được; hãy tạo bản sao.');
    }
    return db.workflowTemplates.find(item => item.id === templateId && item.orgId === orgId)
      ?? fail(404, 'WORKFLOW_TEMPLATE_NOT_FOUND', 'Không tìm thấy mẫu workflow.');
  }

  private validateTemplate(db: Db, orgId: string, request: SaveWorkflowTemplateRequest, selfId: string | null) {
    const name = request.name.trim();
    if (name.length < 2 || name.length > 60) fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Tên mẫu từ 2 đến 60 ký tự.');
    if (request.managementType !== 'Scrum' && request.managementType !== 'Kanban') {
      fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Loại hình quản lý không hợp lệ.');
    }
    const lowerName = name.toLowerCase();
    if (db.workflowTemplates.some(item => item.orgId === orgId && item.id !== selfId && item.name.toLowerCase() === lowerName)) {
      fail(409, 'WORKFLOW_TEMPLATE_NAME_DUPLICATE', 'Tổ chức đã có mẫu cùng tên.');
    }
    const statuses = request.statuses.map(status => ({ name: status.name.trim(), category: status.category }));
    if (statuses.length < 2 || statuses.length > 20) fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Mẫu cần từ 2 đến 20 trạng thái.');
    if (statuses.some(status => status.name.length === 0 || status.name.length > 40)) {
      fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Tên trạng thái không được trống và tối đa 40 ký tự.');
    }
    if (new Set(statuses.map(status => status.name.toLowerCase())).size !== statuses.length) {
      fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Tên trạng thái không được trùng nhau.');
    }
    if (!statuses.some(status => status.category === 'ToDo') || !statuses.some(status => status.category === 'Done')) {
      fail(400, 'WORKFLOW_TEMPLATE_INVALID', 'Cần ít nhất một trạng thái "Cần làm" và một trạng thái "Hoàn thành".');
    }
    return { name, managementType: request.managementType, statuses };
  }

  private validateIssueTypes(request: CreateProjectRequest) {
    const byCode = new Map(request.issueTypes.map(type => [type.code, type.name.trim()]));
    const result = DEFAULT_ISSUE_TYPES.map(type => ({ code: type.code, name: byCode.get(type.code) ?? type.name }));
    if (result.some(type => type.name.length === 0 || type.name.length > 60)) {
      fail(400, 'ISSUE_TYPE_NAME_INVALID', 'Tên kiểu công việc không được trống và tối đa 60 ký tự.');
    }
    if (new Set(result.map(type => type.name.toLowerCase())).size !== result.length) {
      fail(400, 'ISSUE_TYPE_NAME_INVALID', 'Tên các kiểu công việc không được trùng nhau.');
    }
    return result;
  }

  private keyTaken(db: Db, orgId: string, key: string): boolean {
    const upper = key.trim().toUpperCase();
    return db.projects.some(project => project.orgId === orgId && project.projectKey === upper);
  }

  private touch(db: Db, kind: RecordVisitRequest['kind'], id: string): void {
    db.recentVisits = [
      { kind, id, visitedAt: new Date().toISOString() },
      ...db.recentVisits.filter(visit => !(visit.kind === kind && visit.id === id))
    ].slice(0, 30);
  }

  private describeVisit(db: Db, visit: Db['recentVisits'][number]): RecentItem | null {
    if (visit.kind === 'organization') {
      const org = db.organizations.find(item => item.id === visit.id);
      return org ? { kind: 'organization', id: org.id, title: org.name, subtitle: 'Tổ chức', orgId: org.id, visitedAt: visit.visitedAt } : null;
    }
    if (visit.kind === 'project') {
      const project = db.projects.find(item => item.id === visit.id);
      if (!project) return null;
      const orgName = db.organizations.find(org => org.id === project.orgId)?.name ?? '';
      return { kind: 'project', id: project.id, title: project.name, subtitle: `${project.projectKey} · ${orgName}`, orgId: project.orgId, visitedAt: visit.visitedAt };
    }
    const item = db.workItems.find(work => work.id === visit.id);
    const project = item ? db.projects.find(candidate => candidate.id === item.projectId) : undefined;
    if (!item || !project) return null;
    return {
      kind: 'issue',
      id: item.id,
      title: item.title,
      subtitle: `${project.projectKey}-${item.number} · ${project.name}`,
      orgId: project.orgId,
      visitedAt: visit.visitedAt
    };
  }
}
