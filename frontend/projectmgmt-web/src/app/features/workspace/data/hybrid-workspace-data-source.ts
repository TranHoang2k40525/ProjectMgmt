import { Injectable, inject } from '@angular/core';
import { Observable, catchError, concat, concatMap, defer, forkJoin, from, map, of, shareReplay, switchMap, tap, throwError, toArray } from 'rxjs';
import { IdentityApi } from '../../../core/api/identity.api';
import { ProjectManagementService, ProjectStatus } from '../../../core/services/project-management.service';
import { guessStatusCategory } from '../work-display';
import { MockWorkspaceDataSource, RealWorkspaceSnapshot } from './mock-workspace-data-source';
import { PRESET_WORKFLOW_TEMPLATES } from './mock-workspace-seed';
import { WorkspaceDataSource } from './workspace-data-source';
import {
  CreateOrganizationRequest,
  CreateProjectRequest,
  DEFAULT_ISSUE_TYPES,
  InviteEmailResult,
  InviteLink,
  InviteLinkPreview,
  IssueTypeNameSetting,
  MyInvitation,
  OrgInvitation,
  OrgMember,
  OrgRoleCode,
  OrgRoleOption,
  OrganizationSummary,
  ProjectMemberSelection,
  RecentItem,
  RecordVisitRequest,
  SaveWorkflowTemplateRequest,
  WorkItemSummary,
  WorkflowTemplate,
  WorkspaceProject
} from './workspace.models';

/** Id mẫu có sẵn phía FE → templateId của `POST /projects/{id}/workflow/template` hiện có. */
const PRESET_BACKEND_IDS: Readonly<Record<string, string>> = {
  'preset-scrum-std': 'scrum-std',
  'preset-agile-qa': 'agile-qa',
  'preset-kanban-simple': 'kanban-simple',
  'preset-enterprise': 'enterprise'
};

const STATUS_COLORS = { ToDo: '#64748B', InProgress: '#2563EB', Done: '#16A34A' } as const;

/**
 * Nguồn dữ liệu mặc định: tổ chức, dự án, tạo dự án, việc được giao và người dùng đi qua API thật đang có,
 * nên mở dự án là vào được board/backlog thật. Phần backend chưa có (thành viên & vai trò tổ chức, lời mời,
 * link mời, mẫu workflow riêng của tổ chức, Gần đây) lưu ở kho phụ {@link MockWorkspaceDataSource}
 * — dữ liệu thật được đồng bộ vào đó trước mỗi thao tác để ràng buộc vẫn đúng.
 */
@Injectable()
export class HybridWorkspaceDataSource extends WorkspaceDataSource {
  private readonly projects = inject(ProjectManagementService);
  private readonly identityApi = inject(IdentityApi);
  private readonly store = inject(MockWorkspaceDataSource);

  private users$: Observable<RealWorkspaceSnapshot['users']> | null = null;
  private synced = false;
  private readonly enrichedOrgs = new Set<string>();

  // --- Tổ chức ---------------------------------------------------------------

  listOrganizations(): Observable<OrganizationSummary[]> {
    return this.sync().pipe(switchMap(() => this.store.listOrganizations()));
  }

  getOrganization(orgId: string): Observable<OrganizationSummary> {
    return this.sync().pipe(switchMap(() => this.store.getOrganization(orgId)));
  }

  createOrganization(request: CreateOrganizationRequest): Observable<OrganizationSummary> {
    return this.projects.createOrganization({ name: request.name.trim(), slug: request.slug.trim() }).pipe(
      switchMap(org => this.sync().pipe(switchMap(() => this.store.getOrganization(org.id))))
    );
  }

  listMembers(orgId: string): Observable<OrgMember[]> {
    return this.sync().pipe(
      switchMap(() => this.enrichMembers(orgId)),
      switchMap(() => this.store.listMembers(orgId))
    );
  }

  updateMemberRole(orgId: string, userId: string, role: OrgRoleCode): Observable<OrgMember> {
    return this.ensureSynced().pipe(switchMap(() => this.store.updateMemberRole(orgId, userId, role)));
  }

  removeMember(orgId: string, userId: string): Observable<void> {
    return this.ensureSynced().pipe(switchMap(() => this.store.removeMember(orgId, userId)));
  }

  listRoles(): Observable<OrgRoleOption[]> {
    return this.store.listRoles();
  }

  // --- Lời mời & link mời (kho phụ) -------------------------------------------

  inviteByEmail(orgId: string, emails: string[], role: OrgRoleCode): Observable<InviteEmailResult[]> {
    return this.ensureSynced().pipe(switchMap(() => this.store.inviteByEmail(orgId, emails, role)));
  }

  listInvitations(orgId: string): Observable<OrgInvitation[]> {
    return this.ensureSynced().pipe(switchMap(() => this.store.listInvitations(orgId)));
  }

  cancelInvitation(orgId: string, invitationId: string): Observable<void> {
    return this.ensureSynced().pipe(switchMap(() => this.store.cancelInvitation(orgId, invitationId)));
  }

  listMyInvitations(): Observable<MyInvitation[]> {
    return this.ensureSynced().pipe(switchMap(() => this.store.listMyInvitations()));
  }

  respondInvitation(invitationId: string, accept: boolean): Observable<OrganizationSummary | null> {
    return this.ensureSynced().pipe(switchMap(() => this.store.respondInvitation(invitationId, accept)));
  }

  getInviteLink(orgId: string): Observable<InviteLink | null> {
    return this.ensureSynced().pipe(switchMap(() => this.store.getInviteLink(orgId)));
  }

  regenerateInviteLink(orgId: string, role: OrgRoleCode): Observable<InviteLink> {
    return this.ensureSynced().pipe(switchMap(() => this.store.regenerateInviteLink(orgId, role)));
  }

  disableInviteLink(orgId: string): Observable<void> {
    return this.ensureSynced().pipe(switchMap(() => this.store.disableInviteLink(orgId)));
  }

  getInviteByToken(token: string): Observable<InviteLinkPreview> {
    return this.ensureSynced().pipe(switchMap(() => this.store.getInviteByToken(token)));
  }

  acceptInviteLink(token: string): Observable<OrganizationSummary> {
    return this.ensureSynced().pipe(switchMap(() => this.store.acceptInviteLink(token)));
  }

  // --- Mẫu workflow (kho phụ) --------------------------------------------------

  listWorkflowTemplates(orgId: string): Observable<WorkflowTemplate[]> {
    return this.ensureSynced().pipe(switchMap(() => this.store.listWorkflowTemplates(orgId)));
  }

  createWorkflowTemplate(orgId: string, request: SaveWorkflowTemplateRequest): Observable<WorkflowTemplate> {
    return this.ensureSynced().pipe(switchMap(() => this.store.createWorkflowTemplate(orgId, request)));
  }

  updateWorkflowTemplate(orgId: string, templateId: string, request: SaveWorkflowTemplateRequest): Observable<WorkflowTemplate> {
    return this.ensureSynced().pipe(switchMap(() => this.store.updateWorkflowTemplate(orgId, templateId, request)));
  }

  deleteWorkflowTemplate(orgId: string, templateId: string): Observable<void> {
    return this.ensureSynced().pipe(switchMap(() => this.store.deleteWorkflowTemplate(orgId, templateId)));
  }

  setDefaultWorkflowTemplate(orgId: string, templateId: string): Observable<WorkflowTemplate> {
    return this.ensureSynced().pipe(switchMap(() => this.store.setDefaultWorkflowTemplate(orgId, templateId)));
  }

  // --- Dự án (API thật) ---------------------------------------------------------

  listProjects(orgId: string): Observable<WorkspaceProject[]> {
    return this.sync().pipe(switchMap(() => this.store.listProjects(orgId)));
  }

  isProjectKeyAvailable(orgId: string, projectKey: string): Observable<boolean> {
    const key = projectKey.trim().toUpperCase();
    return this.projects.getProjects().pipe(
      map(list => !list.some(project => project.orgId === orgId && project.projectKey.toUpperCase() === key))
    );
  }

  /**
   * Tạo dự án thật rồi lần lượt áp mẫu workflow, đổi tên kiểu công việc, đặt vai trò thành viên bằng các API
   * đang có. Bước sau lỗi không hủy dự án: tên bước được trả trong `setupWarnings` để wizard báo người dùng.
   */
  createProject(orgId: string, request: CreateProjectRequest): Observable<WorkspaceProject> {
    return this.projects.createProject({
      orgId,
      projectKey: request.projectKey.trim().toUpperCase(),
      name: request.name.trim(),
      description: request.description,
      leadUserId: request.leadUserId,
      memberUserIds: request.members.map(member => member.userId)
    }).pipe(
      switchMap(created => {
        const warnings: string[] = [];
        const step = (label: string, work: () => Observable<unknown>) => defer(work).pipe(
          toArray(),
          catchError(() => {
            warnings.push(label);
            return of(undefined);
          })
        );
        return concat(
          step('Áp dụng mẫu workflow', () => this.applyTemplate(orgId, created.id, request.workflowTemplateId)),
          step('Đổi tên kiểu công việc', () => this.renameIssueTypes(created.id, request.issueTypes)),
          step('Đặt vai trò thành viên', () => this.applyMemberRoles(created.id, request.members))
        ).pipe(
          toArray(),
          switchMap(() => this.sync()),
          tap(() => {
            this.store.addKnownMembers(orgId, created.id, [request.leadUserId, ...request.members.map(member => member.userId)]);
            this.store.rememberProjectTemplate(created.id, request.workflowTemplateId, orgId);
          }),
          switchMap(() => this.store.listProjects(orgId)),
          map(list => {
            const project = list.find(item => item.id === created.id);
            if (!project) throw new Error('Dự án vừa tạo không có trong danh sách.');
            return { ...project, setupWarnings: warnings };
          })
        );
      })
    );
  }

  // --- Dành cho bạn & Gần đây ---------------------------------------------------

  listMyWork(orgId?: string): Observable<WorkItemSummary[]> {
    return this.sync(true).pipe(switchMap(() => this.store.listMyWork(orgId)));
  }

  listRecent(orgId?: string): Observable<RecentItem[]> {
    return this.ensureSynced().pipe(switchMap(() => this.store.listRecent(orgId)));
  }

  recordVisit(request: RecordVisitRequest): Observable<void> {
    return this.store.recordVisit(request);
  }

  // --- Đồng bộ dữ liệu thật vào kho phụ ---------------------------------------------

  /** Lấy tổ chức, dự án, người dùng (và việc được giao) từ API thật rồi đồng bộ vào kho phụ. */
  private sync(withWork = false): Observable<void> {
    return forkJoin({
      organizations: this.projects.getOrganizations(),
      projects: this.projects.getProjects(),
      users: this.users(),
      forYou: withWork ? this.identityApi.getForYou() : of(null)
    }).pipe(map(({ organizations, projects, users, forYou }) => {
      this.store.syncReal({
        users,
        organizations: organizations.map(org => ({ id: org.id, name: org.name, slug: org.slug, ownerId: org.ownerId, createdAt: org.createdAt })),
        projects: projects.filter(project => !project.isDeleted).map(project => ({
          id: project.id,
          orgId: project.orgId,
          projectKey: project.projectKey,
          name: project.name,
          description: project.description ?? null,
          leadUserId: project.leadUserId,
          type: project.type,
          createdAt: project.createdAt
        })),
        workItems: forYou
          ? (forYou.assignedIssues ?? [])
            .filter(item => item.issueId && item.projectId)
            .map(item => ({
              id: item.issueId!,
              projectId: item.projectId!,
              issueKey: item.issueKey ?? '',
              title: item.title ?? '',
              statusName: item.statusName ?? '',
              statusCategory: guessStatusCategory(item.statusName),
              dueDate: item.dueDate ?? null
            }))
          : undefined
      });
      this.synced = true;
    }));
  }

  private ensureSynced(): Observable<void> {
    return this.synced ? of(undefined) : this.sync();
  }

  private users(): Observable<RealWorkspaceSnapshot['users']> {
    this.users$ ??= this.projects.getUsers().pipe(
      map(users => users.map(user => ({ id: user.userId, displayName: user.displayName, email: user.email ?? '', avatarUrl: user.avatarUrl ?? null }))),
      shareReplay(1)
    );
    return this.users$;
  }

  /** Backend chưa có thành viên tổ chức: coi người trong các dự án của tổ chức là thành viên. */
  private enrichMembers(orgId: string): Observable<void> {
    if (this.enrichedOrgs.has(orgId)) return of(undefined);
    const orgProjects = this.projects.allProjectsList().filter(project => project.orgId === orgId);
    if (orgProjects.length === 0) return of(undefined);
    return forkJoin(orgProjects.map(project => this.projects.loadProjectMembers(project.id).pipe(
      tap(members => this.store.addKnownMembers(orgId, project.id, members.map(member => member.userId).filter(Boolean))),
      catchError(() => of([]))
    ))).pipe(map(() => {
      this.enrichedOrgs.add(orgId);
    }));
  }

  // --- Các bước sau khi tạo dự án -------------------------------------------------

  private applyTemplate(orgId: string, projectId: string, templateId: string): Observable<unknown> {
    const presetId = PRESET_BACKEND_IDS[templateId];
    if (presetId) return this.projects.applyWorkflowTemplateToProject(projectId, presetId);
    return this.store.listWorkflowTemplates(orgId).pipe(
      map(templates => templates.find(template => template.id === templateId)
        ?? PRESET_WORKFLOW_TEMPLATES.find(template => template.id === templateId)),
      switchMap(template => template ? this.applyCustomStatuses(projectId, template.statuses) : throwError(() => new Error('Không tìm thấy mẫu.')))
    );
  }

  /** Mẫu riêng của tổ chức: thêm trạng thái còn thiếu rồi sắp theo thứ tự của mẫu (trạng thái cũ giữ ở cuối). */
  private applyCustomStatuses(projectId: string, statuses: WorkflowTemplate['statuses']): Observable<unknown> {
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
