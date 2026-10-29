import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { IdentityApi } from '../../../core/api/identity.api';
import { IdentityService } from '../../../core/services/identity.service';
import { ProjectManagementService } from '../../../core/services/project-management.service';
import { HybridWorkspaceDataSource } from './hybrid-workspace-data-source';
import { MOCK_WORKSPACE_LATENCY, MOCK_WORKSPACE_OPTIONS, MockWorkspaceDataSource } from './mock-workspace-data-source';
import { CreateProjectRequest, DEFAULT_ISSUE_TYPES } from './workspace.models';

const me = { id: 'me', email: 'me@example.com', displayName: 'Tôi', avatarUrl: null };
const org = { id: 'org-1', name: 'HUCE Lab', slug: 'huce-lab', ownerId: 'me', isActive: true, createdAt: '2026-10-01T00:00:00Z' };
const otherOrg = { id: 'org-2', name: 'Nhóm khác', slug: 'nhom-khac', ownerId: 'u2', isActive: true, createdAt: '2026-10-02T00:00:00Z' };
const project = {
  id: 'p-1', orgId: 'org-1', projectKey: 'LMS', name: 'Học tập', description: null, leadUserId: 'me', type: 'Scrum',
  issueCounter: 3, isArchived: false, isDeleted: false, createdAt: '2026-10-05T00:00:00Z'
};

function value<T>(source: Observable<T>): T {
  let result: T | undefined;
  let failure: unknown;
  source.subscribe({ next: v => (result = v), error: e => (failure = e) });
  if (failure) throw failure;
  return result as T;
}

describe('HybridWorkspaceDataSource', () => {
  let projects: Record<string, ReturnType<typeof vi.fn>> & { allProjectsList: ReturnType<typeof signal> };
  let realProjects: typeof project[];

  beforeEach(() => {
    localStorage.clear();
    realProjects = [project];
    projects = {
      allProjectsList: signal(realProjects),
      getOrganizations: vi.fn(() => of([org, otherOrg])),
      getProjects: vi.fn(() => of(realProjects)),
      getUsers: vi.fn(() => of([
        { userId: 'me', displayName: 'Tôi', email: 'me@example.com' },
        { userId: 'u2', displayName: 'Lê Thu Hà', email: 'ha@example.com' }
      ])),
      createOrganization: vi.fn(),
      createProject: vi.fn(),
      applyWorkflowTemplateToProject: vi.fn(() => of([])),
      getIssueTypes: vi.fn(() => of([
        { id: 't-epic', name: 'Epic', icon: 'bolt', color: '#000', hierarchyLevel: 3, isSubtask: false },
        { id: 't-story', name: 'Story', icon: 'book', color: '#111', hierarchyLevel: 2, isSubtask: false },
        { id: 't-sub', name: 'Subtask', icon: 'sub', color: '#222', hierarchyLevel: 1, isSubtask: true }
      ])),
      updateCustomIssueType: vi.fn(() => of({})),
      getRoles: vi.fn(() => of([{ id: 'r-sm', name: 'ScrumMaster', code: 'SCRUMMASTER' }])),
      changeProjectMemberRole: vi.fn(() => of({})),
      loadProjectMembers: vi.fn(() => of([{ userId: 'u2' }])),
      getProjectStatuses: vi.fn(),
      createWorkflowStatus: vi.fn(),
      reorderWorkflowStatuses: vi.fn()
    } as never;
    TestBed.configureTestingModule({
      providers: [
        HybridWorkspaceDataSource,
        MockWorkspaceDataSource,
        { provide: MOCK_WORKSPACE_LATENCY, useValue: 0 },
        { provide: MOCK_WORKSPACE_OPTIONS, useValue: { seed: false, storagePrefix: 'test-shadow:' } },
        { provide: IdentityService, useValue: { authState: signal({ currentUser: me }) } },
        { provide: ProjectManagementService, useValue: projects },
        {
          provide: IdentityApi,
          useValue: {
            getForYou: () => of({
              assignedIssues: [
                { issueId: 'i-1', projectId: 'p-1', issueKey: 'LMS-1', title: 'Đăng nhập SSO', statusName: 'In Progress', dueDate: '2026-11-01' },
                { issueId: 'i-2', projectId: 'p-1', issueKey: 'LMS-2', title: 'Xong rồi', statusName: 'Done', dueDate: null }
              ]
            })
          }
        }
      ]
    });
  });

  const source = () => TestBed.inject(HybridWorkspaceDataSource);

  it('lists real organizations without demo data and derives roles from the owner', () => {
    const orgs = value(source().listOrganizations());
    expect(orgs.map(item => [item.id, item.myRole, item.ownerName, item.projectCount])).toEqual([
      ['org-1', 'Owner', 'Tôi', 1],
      ['org-2', 'Member', 'Lê Thu Hà', 0]
    ]);
  });

  it('maps assigned issues from the real For You API with guessed status groups', () => {
    const work = value(source().listMyWork('org-1'));
    expect(work.map(item => [item.id, item.issueKey, item.projectName, item.orgName, item.statusCategory])).toEqual([
      ['i-1', 'LMS-1', 'Học tập', 'HUCE Lab', 'InProgress'],
      ['i-2', 'LMS-2', 'Học tập', 'HUCE Lab', 'Done']
    ]);
  });

  it('treats people in the organization projects as organization members', () => {
    const members = value(source().listMembers('org-1'));
    expect(members.map(member => [member.userId, member.role])).toEqual([['me', 'Owner'], ['u2', 'Member']]);
  });

  it('creates a real project, applies the preset, renames types and sets roles', () => {
    const created = { ...project, id: 'p-new', projectKey: 'TV', name: 'Thư viện' };
    projects['createProject'].mockImplementation(() => {
      realProjects = [...realProjects, created];
      return of(created);
    });
    const request: CreateProjectRequest = {
      name: 'Thư viện', projectKey: 'tv', description: null, leadUserId: 'me', workflowTemplateId: 'preset-kanban-simple',
      issueTypes: DEFAULT_ISSUE_TYPES.map(type => type.code === 'Story' ? { ...type, name: 'Use case' } : { ...type }),
      members: [{ userId: 'u2', role: 'ScrumMaster' }]
    };
    value(source().listOrganizations());
    const result = value(source().createProject('org-1', request));

    expect(projects['createProject']).toHaveBeenCalledWith(expect.objectContaining({ orgId: 'org-1', projectKey: 'TV', memberUserIds: ['u2'] }));
    expect(projects['applyWorkflowTemplateToProject']).toHaveBeenCalledWith('p-new', 'kanban-simple');
    expect(projects['updateCustomIssueType']).toHaveBeenCalledWith('p-new', 't-story', 'Use case', 'book', '#111', false, 2);
    expect(projects['changeProjectMemberRole']).toHaveBeenCalledWith('p-new', 'u2', 'r-sm');
    expect(result).toMatchObject({ id: 'p-new', workflowTemplateName: 'Kanban đơn giản', managementType: 'Kanban', setupWarnings: [] });
  });

  it('keeps the created project and reports the steps that failed', () => {
    const created = { ...project, id: 'p-new', projectKey: 'TV' };
    projects['createProject'].mockImplementation(() => {
      realProjects = [...realProjects, created];
      return of(created);
    });
    projects['applyWorkflowTemplateToProject'].mockReturnValue(throwError(() => new Error('500')));
    const result = value(source().createProject('org-1', {
      name: 'Thư viện', projectKey: 'TV', description: null, leadUserId: 'me', workflowTemplateId: 'preset-scrum-std',
      issueTypes: DEFAULT_ISSUE_TYPES.map(type => ({ ...type })), members: []
    }));
    expect(result.id).toBe('p-new');
    expect(result.setupWarnings).toEqual(['Áp dụng mẫu workflow']);
  });

  it('checks key availability against real projects of the organization', () => {
    expect(value(source().isProjectKeyAvailable('org-1', 'lms'))).toBe(false);
    expect(value(source().isProjectKeyAvailable('org-2', 'LMS'))).toBe(true);
  });
});
