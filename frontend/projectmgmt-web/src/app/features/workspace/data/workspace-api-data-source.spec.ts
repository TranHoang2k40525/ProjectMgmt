import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../../../environments/environment';
import { IdentityApi } from '../../../core/api/identity.api';
import { IdentityService } from '../../../core/services/identity.service';
import { ProjectManagementService } from '../../../core/services/project-management.service';
import { WorkspaceApiDataSource } from './workspace-api-data-source';
import { CreateProjectRequest, DEFAULT_ISSUE_TYPES, WorkflowTemplate } from './workspace.models';

const API = `${environment.apiBaseUrl}/v1`;
const org = { id: 'org-1', name: 'HUCE Lab', slug: 'huce-lab', ownerId: 'me', isActive: true, createdAt: '2026-10-01T00:00:00Z' };
const project = {
  id: 'p-1', orgId: 'org-1', projectKey: 'LMS', name: 'Học tập', description: null, leadUserId: 'me', type: 'Scrum',
  issueCounter: 3, isArchived: false, isDeleted: false, createdAt: '2026-10-05T00:00:00Z'
};
const kanban: WorkflowTemplate = {
  id: 'preset-kanban-simple', orgId: null, name: 'Kanban đơn giản', managementType: 'Kanban', isPreset: true, isDefault: false,
  statuses: [{ name: 'To Do', category: 'ToDo' }, { name: 'Done', category: 'Done' }]
};

describe('WorkspaceApiDataSource', () => {
  let http: HttpTestingController;
  let pm: Record<string, ReturnType<typeof vi.fn>> & { allProjectsList: ReturnType<typeof signal> };
  let realProjects: typeof project[];

  beforeEach(() => {
    realProjects = [project];
    pm = {
      allProjectsList: signal(realProjects),
      getOrganizations: vi.fn(() => of([org])),
      getProjects: vi.fn(() => of(realProjects)),
      getUsers: vi.fn(() => of([{ userId: 'me', displayName: 'Tôi' }, { userId: 'u2', displayName: 'Lê Thu Hà' }])),
      createOrganization: vi.fn(),
      createProject: vi.fn(),
      loadProjectMembers: vi.fn(() => of([{ userId: 'me' }, { userId: 'u2' }])),
      // Áp mẫu trả về trạng thái của dự án sau khi áp: bộ mặc định + trạng thái của mẫu (mẫu chỉ thêm, không bỏ).
      applyWorkflowTemplateToProject: vi.fn(() => of([
        { id: 's-backlog', name: 'Backlog', category: 'ToDo', isInitial: true, orderIndex: 1 },
        { id: 's-todo', name: 'To Do', category: 'ToDo', isInitial: false, orderIndex: 2 },
        { id: 's-review', name: 'In Review', category: 'InProgress', isInitial: false, orderIndex: 3 },
        { id: 's-done', name: 'Done', category: 'Done', isInitial: false, isDone: true, orderIndex: 4 }
      ])),
      updateWorkflowStatus: vi.fn(() => of({})),
      deleteWorkflowStatus: vi.fn(() => of(true)),
      getIssueTypes: vi.fn(() => of([{ id: 't-story', name: 'Story', icon: 'book', color: '#111', hierarchyLevel: 2, isSubtask: false }])),
      updateCustomIssueType: vi.fn(() => of({})),
      getRoles: vi.fn(() => of([{ id: 'r-sm', name: 'ScrumMaster', code: 'SCRUMMASTER' }])),
      changeProjectMemberRole: vi.fn(() => of({}))
    } as never;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        WorkspaceApiDataSource,
        { provide: ProjectManagementService, useValue: pm },
        { provide: IdentityService, useValue: { authState: signal({ currentUser: { id: 'me' } }) } },
        {
          provide: IdentityApi,
          useValue: { getForYou: () => of({ assignedIssues: [{ issueId: 'i-1', projectId: 'p-1', issueKey: 'LMS-1', title: 'SSO', statusName: 'In Progress', dueDate: null }] }) }
        }
      ]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function collect<T>(source: Observable<T>): { value?: T; error?: unknown } {
    const result: { value?: T; error?: unknown } = {};
    source.subscribe({ next: value => (result.value = value), error: error => (result.error = error) });
    return result;
  }

  it('merges real organizations with memberships from the organization member API', () => {
    const result = collect(TestBed.inject(WorkspaceApiDataSource).listOrganizations());
    http.expectOne(`${API}/users/me/organization-memberships`).flush([
      { orgId: 'org-1', orgName: 'HUCE Lab', ownerId: 'me', role: 'Owner', memberCount: 3, joinedAt: '' },
      { orgId: 'org-invited', orgName: 'CLB', ownerId: 'u2', role: 'Member', memberCount: 2, joinedAt: '2026-10-20T00:00:00Z' }
    ]);
    expect(result.value?.map(item => [item.id, item.myRole, item.memberCount, item.projectCount, item.ownerName])).toEqual([
      ['org-invited', 'Member', 2, 0, 'Lê Thu Hà'],
      ['org-1', 'Owner', 3, 1, 'Tôi']
    ]);
  });

  it('still lists real organizations and an empty recent list when the contract endpoints fail', () => {
    const source = TestBed.inject(WorkspaceApiDataSource);
    const orgs = collect(source.listOrganizations());
    http.expectOne(`${API}/users/me/organization-memberships`).flush({}, { status: 404, statusText: 'Not Found' });
    expect(orgs.error).toBeUndefined();
    expect(orgs.value?.map(item => [item.id, item.myRole, item.memberCount])).toEqual([['org-1', 'Owner', 1]]);

    const recent = collect(source.listRecent());
    http.expectOne(`${API}/users/me/recent`).flush({}, { status: 404, statusText: 'Not Found' });
    expect(recent.value).toEqual([]);
  });

  it('falls back to project members and preset templates so a project can still be created', () => {
    pm['loadProjectMembers'].mockReturnValue(of([{ userId: 'u2', displayName: 'Lê Thu Hà', email: 'ha@example.com' }]));
    const source = TestBed.inject(WorkspaceApiDataSource);
    const members = collect(source.listMembers('org-1'));
    http.expectOne(`${API}/organizations/org-1/members`).flush({}, { status: 404, statusText: 'Not Found' });
    expect(members.value?.map(item => [item.userId, item.displayName, item.role])).toEqual([['me', 'Tôi', 'Owner'], ['u2', 'Lê Thu Hà', 'Member']]);

    const templates = collect(source.listWorkflowTemplates('org-1'));
    http.expectOne(`${API}/organizations/org-1/workflow-templates`).flush({}, { status: 404, statusText: 'Not Found' });
    expect(templates.value?.map(item => [item.id, item.isDefault])).toEqual([
      ['preset-scrum-std', true], ['preset-agile-qa', false], ['preset-kanban-simple', false], ['preset-enterprise', false]
    ]);
  });

  it('calls the contract endpoints for features the backend does not have yet', () => {
    const source = TestBed.inject(WorkspaceApiDataSource);
    collect(source.inviteByEmail('org-1', ['a@example.com'], 'Member'));
    const invite = http.expectOne(`${API}/organizations/org-1/invitations`);
    expect(invite.request.method).toBe('POST');
    expect(invite.request.body).toEqual({ emails: ['a@example.com'], role: 'Member' });
    invite.flush([]);

    collect(source.setDefaultWorkflowTemplate('org-1', 'tpl 1'));
    expect(http.expectOne(`${API}/organizations/org-1/workflow-templates/tpl%201/default`).request.method).toBe('PUT');

    collect(source.recordVisit({ kind: 'project', id: 'p-1' }));
    expect(http.expectOne(`${API}/users/me/recent`).request.body).toEqual({ kind: 'project', id: 'p-1' });
  });

  it('creates a real project and reports a failed follow-up step', () => {
    const created = { ...project, id: 'p-new', projectKey: 'TV', name: 'Thư viện' };
    pm['createProject'].mockReturnValue(of(created));
    pm['updateCustomIssueType'].mockReturnValue(throwError(() => new Error('500')));
    const request: CreateProjectRequest = {
      name: 'Thư viện', projectKey: 'tv', description: null, leadUserId: 'me', workflowTemplateId: kanban.id,
      issueTypes: DEFAULT_ISSUE_TYPES.map(type => type.code === 'Story' ? { ...type, name: 'Use case' } : { ...type }),
      members: [{ userId: 'u2', role: 'ScrumMaster' }]
    };
    const result = collect(TestBed.inject(WorkspaceApiDataSource).createProject('org-1', request));
    http.expectOne(`${API}/organizations/org-1/workflow-templates`).flush([kanban]);

    expect(pm['createProject']).toHaveBeenCalledWith(expect.objectContaining({ orgId: 'org-1', projectKey: 'TV', memberUserIds: ['u2'] }));
    expect(pm['applyWorkflowTemplateToProject']).toHaveBeenCalledWith('p-new', 'kanban-simple');
    // Chỉ giữ trạng thái của mẫu (To Do, Done) và lấy trạng thái đầu của mẫu làm trạng thái khởi đầu.
    expect(pm['updateWorkflowStatus']).toHaveBeenCalledWith('p-new', 's-todo', expect.objectContaining({ name: 'To Do', isInitial: true }));
    expect(pm['deleteWorkflowStatus'].mock.calls).toEqual([['p-new', 's-backlog'], ['p-new', 's-review']]);
    expect(pm['changeProjectMemberRole']).toHaveBeenCalledWith('p-new', 'u2', 'r-sm');
    expect(result.value).toMatchObject({ id: 'p-new', workflowTemplateName: 'Kanban đơn giản', managementType: 'Kanban', memberCount: 2, setupWarnings: ['Đổi tên kiểu công việc'] });
  });

  it('maps assigned issues from the For You API with organization names', () => {
    const result = collect(TestBed.inject(WorkspaceApiDataSource).listMyWork('org-1'));
    http.expectOne(`${API}/users/me/organization-memberships`).flush([]);
    expect(result.value?.map(item => [item.id, item.projectName, item.orgName, item.statusCategory])).toEqual([['i-1', 'Học tập', 'HUCE Lab', 'InProgress']]);
  });

  it('lists projects of the organization with real member counts', () => {
    const result = collect(TestBed.inject(WorkspaceApiDataSource).listProjects('org-1'));
    expect(result.value?.map(item => [item.projectKey, item.leadName, item.memberCount, item.managementType])).toEqual([['LMS', 'Tôi', 2, 'Scrum']]);
  });
});
