import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, concat, concatMap, defer, forkJoin, from, map, of, shareReplay, switchMap, throwError, toArray } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { IdentityApi } from '../../../core/api/identity.api';
import { IdentityService } from '../../../core/services/identity.service';
import { Project, ProjectManagementService, ProjectStatus } from '../../../core/services/project-management.service';
import { guessStatusCategory } from '../work-display';
import { presetWorkflowTemplates } from './workflow-presets';
import { WorkspaceDataSource } from './workspace-data-source';
import {
  CreateOrganizationRequest,
  CreateProjectRequest,
  DEFAULT_ISSUE_TYPES,
  InviteEmailResult,
  InviteLink,
  InviteLinkPreview,
  IssueTypeNameSetting,
  ManagementType,
  MyInvitation,
  OrgInvitation,
  OrgMember,
  OrgMembership,
  OrgRoleCode,
  OrgRoleOption,
  OrganizationSummary,
  ProjectMemberSelection,
  RecentItem,
  RecordVisitRequest,
  SaveWorkflowTemplateRequest,
  WorkItemSummary,
  WorkflowStatusDef,
  WorkflowTemplate,
  WorkspaceProject
} from './workspace.models';

/** Id mẫu có sẵn → templateId của `POST /projects/{id}/workflow/template` hiện có ở backend. */
const PRESET_BACKEND_IDS: Readonly<Record<string, string>> = {
  'preset-scrum-std': 'scrum-std',
  'preset-agile-qa': 'agile-qa',
  'preset-kanban-simple': 'kanban-simple',
  'preset-enterprise': 'enterprise'
};

const STATUS_COLORS = { ToDo: '#64748B', InProgress: '#2563EB', Done: '#16A34A' } as const;

interface UserInfo {
  id: string;
  displayName: string;
}

function notFound(title: string): never {
  throw { status: 404, code: 'ORG_NOT_FOUND', title, detail: title };
}

/**
 * Nguồn dữ liệu duy nhất của không gian Tổ chức → Dự án, gọi HTTP:
 * - API backend đã có cho tổ chức, dự án, tạo dự án (kèm áp workflow, đổi tên kiểu công việc, vai trò), việc được giao, người dùng.
 * - Endpoint trong "Workspace API contract" cho phần backend chưa có (thành viên & vai trò tổ chức, lời mời, link mời,
 *   mẫu workflow của tổ chức, Gần đây). Khi `environment.mockMissingApis` bật, MSW trả lời các endpoint này (src/mocks).
 */
@Injectable()
export class WorkspaceApiDataSource extends WorkspaceDataSource {
  private readonly http = inject(HttpClient);
  private readonly projects = inject(ProjectManagementService);
  private readonly identityApi = inject(IdentityApi);
  private readonly identity = inject(IdentityService);
  private readonly base = `${environment.apiBaseUrl}/v1`;
  private users$: Observable<UserInfo[]> | null = null;

  private org(orgId: string): string {
    return `${this.base}/organizations/${encodeURIComponent(orgId)}`;
  }

  private get meId(): string {
    return this.identity.authState().currentUser?.id ?? '';
  }

  // --- Tổ chức ---------------------------------------------------------------

  /** Tổ chức thật người dùng thấy, cộng tổ chức họ được mời vào; vai trò & số thành viên từ endpoint thành viên tổ chức. */
  listOrganizations(): Observable<OrganizationSummary[]> {
    return forkJoin({
      orgs: this.projects.getOrganizations(),
      projects: this.projects.getProjects(),
      users: this.users(),
      // Chỉ bổ sung vai trò & số thành viên: lỗi thì vẫn hiện tổ chức thật, vai trò suy từ chủ tổ chức.
      memberships: this.http.get<OrgMembership[]>(`${this.base}/users/me/organization-memberships`).pipe(catchError(() => of([])))
    }).pipe(map(({ orgs, projects, users, memberships }) => {
      const byId = new Map<string, OrganizationSummary>();
      const name = (userId: string) => users.find(user => user.id === userId)?.displayName ?? 'Người dùng';
      const projectCount = (orgId: string) => projects.filter(project => project.orgId === orgId && !project.isDeleted).length;
      for (const org of orgs) {
        const membership = memberships.find(item => item.orgId === org.id);
        byId.set(org.id, {
          id: org.id,
          name: org.name,
          slug: org.slug,
          ownerId: org.ownerId,
          ownerName: name(org.ownerId),
          myRole: membership?.role ?? (org.ownerId === this.meId ? 'Owner' : 'Member'),
          projectCount: projectCount(org.id),
          memberCount: membership?.memberCount ?? 1,
          createdAt: org.createdAt
        });
      }
      for (const membership of memberships) {
        if (byId.has(membership.orgId)) continue;
        byId.set(membership.orgId, {
          id: membership.orgId,
          name: membership.orgName,
          slug: '',
          ownerId: membership.ownerId,
          ownerName: name(membership.ownerId),
          myRole: membership.role,
          projectCount: projectCount(membership.orgId),
          memberCount: membership.memberCount,
          createdAt: membership.joinedAt
        });
      }
      return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name, 'vi'));
    }));
  }

  getOrganization(orgId: string): Observable<OrganizationSummary> {
    return this.listOrganizations().pipe(map(orgs => orgs.find(org => org.id === orgId) ?? notFound('Không tìm thấy tổ chức.')));
  }

  createOrganization(request: CreateOrganizationRequest): Observable<OrganizationSummary> {
    return this.projects.createOrganization({ name: request.name.trim(), slug: request.slug.trim() }).pipe(
      switchMap(created => this.getOrganization(created.id))
    );
  }

  // --- Thành viên, lời mời, link mời (endpoint hợp đồng) --------------------------

  /** Lỗi thì dựng từ dữ liệu thật: chủ tổ chức và thành viên các dự án của tổ chức, để vẫn chọn được người khi tạo dự án. */
  listMembers(orgId: string): Observable<OrgMember[]> {
    return this.http.get<OrgMember[]>(`${this.org(orgId)}/members`).pipe(catchError(() => this.membersFromProjects(orgId)));
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
    return this.http.post<OrgMembership | null>(
      `${this.base}/users/me/organization-invitations/${encodeURIComponent(invitationId)}/respond`, { accept }
    ).pipe(switchMap(membership => membership ? this.getOrganization(membership.orgId) : of(null)));
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
    return this.http.post<OrgMembership>(`${this.base}/invite-links/${encodeURIComponent(token)}/accept`, {}).pipe(
      switchMap(membership => this.getOrganization(membership.orgId))
    );
  }

  // --- Mẫu workflow (endpoint hợp đồng) ---------------------------------------------

  /** Lỗi thì dùng các mẫu có sẵn (backend áp được trực tiếp qua `workflow/template`). */
  listWorkflowTemplates(orgId: string): Observable<WorkflowTemplate[]> {
    return this.http.get<WorkflowTemplate[]>(`${this.org(orgId)}/workflow-templates`).pipe(catchError(() => of(presetWorkflowTemplates())));
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

  // --- Dự án (API backend đã có) ------------------------------------------------

  listProjects(orgId: string): Observable<WorkspaceProject[]> {
    return forkJoin({ list: this.projects.getProjects(), users: this.users() }).pipe(
      switchMap(({ list, users }) => {
        const mine = list.filter(project => project.orgId === orgId && !project.isDeleted)
          .sort((a, b) => b.createdAt.localeCompare(a.createdAt));
        if (mine.length === 0) return of([] as WorkspaceProject[]);
        return forkJoin(mine.map(project => this.projects.loadProjectMembers(project.id).pipe(
          map(members => this.toProject(project, users, members.length)),
          catchError(() => of(this.toProject(project, users, 0)))
        )));
      })
    );
  }

  isProjectKeyAvailable(orgId: string, projectKey: string): Observable<boolean> {
    const key = projectKey.trim().toUpperCase();
    return this.projects.getProjects().pipe(
      map(list => !list.some(project => project.orgId === orgId && project.projectKey.toUpperCase() === key))
    );
  }

  /**
   * Tạo dự án thật rồi lần lượt áp mẫu workflow, đổi tên kiểu công việc, đặt vai trò thành viên bằng các API đang có.
   * Bước sau lỗi không hủy dự án: tên bước được trả trong `setupWarnings` để wizard báo người dùng.
   */
  createProject(orgId: string, request: CreateProjectRequest): Observable<WorkspaceProject> {
    return forkJoin({ templates: this.listWorkflowTemplates(orgId), users: this.users() }).pipe(
      switchMap(({ templates, users }) => {
        const template = templates.find(item => item.id === request.workflowTemplateId);
        if (!template) return throwError(() => ({ status: 400, code: 'WORKFLOW_TEMPLATE_NOT_FOUND', title: 'Mẫu workflow không còn tồn tại.', detail: 'Mẫu workflow không còn tồn tại.' }));
        return this.projects.createProject({
          orgId,
          projectKey: request.projectKey.trim().toUpperCase(),
          name: request.name.trim(),
          description: request.description,
          leadUserId: request.leadUserId,
          memberUserIds: request.members.map(member => member.userId)
        }).pipe(switchMap(created => {
          const warnings: string[] = [];
          const step = (label: string, work: () => Observable<unknown>) => defer(work).pipe(
            toArray(),
            catchError(() => {
              warnings.push(label);
              return of(undefined);
            })
          );
          return concat(
            step('Áp dụng mẫu workflow', () => this.applyTemplate(created.id, template.id, template.statuses)),
            step('Đổi tên kiểu công việc', () => this.renameIssueTypes(created.id, request.issueTypes)),
            step('Đặt vai trò thành viên', () => this.applyMemberRoles(created.id, request.members))
          ).pipe(toArray(), map(() => {
            const memberCount = new Set([this.meId, request.leadUserId, ...request.members.map(member => member.userId)].filter(Boolean)).size;
            return {
              ...this.toProject(created, users, memberCount, template.name, template.managementType),
              setupWarnings: warnings
            };
          }));
        }));
      })
    );
  }

  // --- Dành cho bạn & Gần đây ---------------------------------------------------

  listMyWork(orgId?: string): Observable<WorkItemSummary[]> {
    return forkJoin({ forYou: this.identityApi.getForYou(), orgs: this.listOrganizations() }).pipe(map(({ forYou, orgs }) => {
      const projects = this.projects.allProjectsList();
      return (forYou.assignedIssues ?? [])
        .filter(item => item.issueId && item.projectId)
        .map(item => ({ item, project: projects.find(project => project.id === item.projectId) }))
        .filter((entry): entry is { item: typeof entry.item; project: Project } => !!entry.project && (!orgId || entry.project.orgId === orgId))
        .map(({ item, project }) => ({
          id: item.issueId!,
          issueKey: item.issueKey ?? '',
          title: item.title ?? '',
          projectId: project.id,
          projectName: project.name,
          orgId: project.orgId,
          orgName: orgs.find(org => org.id === project.orgId)?.name ?? '',
          statusName: item.statusName ?? '',
          statusCategory: guessStatusCategory(item.statusName),
          priority: 'Medium' as const,
          dueDate: item.dueDate ?? null
        }))
        .sort((a, b) => (a.dueDate ?? '9999').localeCompare(b.dueDate ?? '9999'));
    }));
  }

  listRecent(orgId?: string): Observable<RecentItem[]> {
    const params = orgId ? new HttpParams().set('orgId', orgId) : new HttpParams();
    return this.http.get<RecentItem[]>(`${this.base}/users/me/recent`, { params }).pipe(catchError(() => of([])));
  }

  recordVisit(request: RecordVisitRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/users/me/recent`, request);
  }

  // --- Hỗ trợ ---------------------------------------------------------------------

  private membersFromProjects(orgId: string): Observable<OrgMember[]> {
    return forkJoin({ orgs: this.projects.getOrganizations(), list: this.projects.getProjects() }).pipe(
      switchMap(({ orgs, list }) => {
        const ownerId = orgs.find(org => org.id === orgId)?.ownerId ?? '';
        const projectIds = list.filter(project => project.orgId === orgId && !project.isDeleted).map(project => project.id);
        const memberLists = projectIds.map(id => this.projects.loadProjectMembers(id).pipe(catchError(() => of([]))));
        return forkJoin({ users: this.users(), lists: memberLists.length ? forkJoin(memberLists) : of([]) }).pipe(map(({ users, lists }) => {
          const byId = new Map<string, OrgMember>();
          const add = (userId: string, displayName: string, email: string, avatarUrl: string | null) => {
            if (!userId || byId.has(userId)) return;
            byId.set(userId, { userId, displayName, email, avatarUrl, role: userId === ownerId ? 'Owner' : 'Member', joinedAt: '' });
          };
          const owner = users.find(user => user.id === ownerId);
          if (owner) add(owner.id, owner.displayName, '', null);
          for (const member of lists.flat()) add(member.userId, member.displayName, member.email, member.avatarUrl ?? null);
          return [...byId.values()];
        }));
      })
    );
  }

  private users(): Observable<UserInfo[]> {
    this.users$ ??= this.projects.getUsers().pipe(
      map(users => users.map(user => ({ id: user.userId, displayName: user.displayName }))),
      shareReplay(1)
    );
    return this.users$;
  }

  private toProject(project: Project, users: UserInfo[], memberCount: number, templateName = '', managementType?: ManagementType): WorkspaceProject {
    return {
      id: project.id,
      orgId: project.orgId,
      projectKey: project.projectKey,
      name: project.name,
      description: project.description ?? null,
      leadUserId: project.leadUserId,
      leadName: project.leadName ?? users.find(user => user.id === project.leadUserId)?.displayName ?? '',
      workflowTemplateId: '',
      workflowTemplateName: templateName,
      managementType: managementType ?? (project.type?.toLowerCase().includes('kanban') ? 'Kanban' : 'Scrum'),
      memberCount,
      createdAt: project.createdAt
    };
  }

  /**
   * Áp mẫu cho dự án vừa tạo: backend chỉ thêm trạng thái của mẫu và giữ bộ mặc định (Backlog, To Do, In Review…),
   * nên sau đó bỏ các trạng thái không thuộc mẫu để mỗi loại hình có đúng các giai đoạn của nó. Dự án mới chưa có công việc
   * nên xóa an toàn (backend vẫn từ chối xóa trạng thái đang có công việc).
   */
  private applyTemplate(projectId: string, templateId: string, statuses: WorkflowStatusDef[]): Observable<unknown> {
    const presetId = PRESET_BACKEND_IDS[templateId];
    // Mẫu có sẵn: backend trả luôn danh sách trạng thái sau khi áp, không cần tải lại.
    const applied$ = presetId
      ? this.projects.applyWorkflowTemplateToProject(projectId, presetId)
      : this.applyCustomStatuses(projectId, statuses).pipe(toArray(), switchMap(() => this.projects.getProjectStatuses(projectId)));
    return applied$.pipe(switchMap(existing => this.keepOnlyTemplateStatuses(projectId, existing, statuses)));
  }

  private keepOnlyTemplateStatuses(projectId: string, existing: ProjectStatus[], statuses: WorkflowStatusDef[]): Observable<unknown> {
    const wanted = statuses.map(status => status.name.toLowerCase());
    if (wanted.length === 0) return of(undefined);
    const first = existing.find(status => status.name.toLowerCase() === wanted[0]);
    const setInitial$ = first && !first.isInitial
      ? this.projects.updateWorkflowStatus(projectId, first.id, {
        name: first.name, colorHex: first.colorHex, category: first.category, isInitial: true,
        isDone: first.isDone, isActiveWork: first.isActiveWork, isCanceled: first.isCanceled
      })
      : of(undefined);
    const extra = existing.filter(status => !wanted.includes(status.name.toLowerCase()));
    return concat(setInitial$, from(extra).pipe(concatMap(status => this.projects.deleteWorkflowStatus(projectId, status.id))));
  }

  /** Mẫu riêng của tổ chức: thêm trạng thái còn thiếu rồi sắp theo thứ tự của mẫu (trạng thái cũ giữ ở cuối). */
  private applyCustomStatuses(projectId: string, statuses: WorkflowStatusDef[]): Observable<unknown> {
    return this.projects.getProjectStatuses(projectId).pipe(
      switchMap(existing => {
        const byName = new Map(existing.map(status => [status.name.toLowerCase(), status]));
        const missing = statuses.filter(status => !byName.has(status.name.toLowerCase()));
        const created$ = missing.length === 0
          ? of([] as ProjectStatus[])
          : from(missing).pipe(
            concatMap(status => this.projects.createWorkflowStatus(projectId, {
              name: status.name,
              category: status.category,
              colorHex: STATUS_COLORS[status.category],
              isInitial: false,
              isDone: status.category === 'Done',
              isActiveWork: status.category === 'InProgress'
            })),
            toArray()
          );
        return created$.pipe(switchMap(created => {
          created.forEach(status => byName.set(status.name.toLowerCase(), status));
          const ordered = statuses.map(status => byName.get(status.name.toLowerCase())?.id).filter((id): id is string => !!id);
          const rest = existing.filter(status => !ordered.includes(status.id)).sort((a, b) => a.orderIndex - b.orderIndex).map(status => status.id);
          return this.projects.reorderWorkflowStatuses(projectId, [...ordered, ...rest]);
        }));
      })
    );
  }

  private renameIssueTypes(projectId: string, settings: IssueTypeNameSetting[]): Observable<unknown> {
    const changes = settings.filter(setting => setting.name.trim() !== DEFAULT_ISSUE_TYPES.find(type => type.code === setting.code)?.name);
    if (changes.length === 0) return of(undefined);
    return this.projects.getIssueTypes(projectId).pipe(
      switchMap(types => from(changes).pipe(concatMap(change => {
        const type = change.code === 'Subtask'
          ? types.find(item => item.isSubtask)
          : change.code === 'Epic'
            ? types.find(item => item.hierarchyLevel === 3 || item.name.toLowerCase() === 'epic')
            : types.find(item => !item.isSubtask && item.name.toLowerCase() === change.code.toLowerCase());
        if (!type) return throwError(() => new Error(`Không tìm thấy kiểu ${change.code}.`));
        return this.projects.updateCustomIssueType(projectId, type.id, change.name.trim(), type.icon, type.color, !!type.isSubtask, type.hierarchyLevel ?? 2);
      }), toArray()))
    );
  }

  /** `memberUserIds` khi tạo dự án gán vai trò mặc định; ai được chọn vai trò khác thì đổi lại. */
  private applyMemberRoles(projectId: string, members: ProjectMemberSelection[]): Observable<unknown> {
    const changes = members.filter(member => member.role !== 'Developer');
    if (changes.length === 0) return of(undefined);
    const normalize = (value: string) => value.toLowerCase().replace(/[\s_-]/g, '');
    return this.projects.getRoles('Project').pipe(
      switchMap(roles => from(changes).pipe(concatMap(member => {
        const role = roles.find(item => normalize(item.name) === normalize(member.role));
        if (!role?.id) return throwError(() => new Error(`Không tìm thấy vai trò ${member.role}.`));
        return this.projects.changeProjectMemberRole(projectId, member.userId, role.id);
      }), toArray()))
    );
  }
}
