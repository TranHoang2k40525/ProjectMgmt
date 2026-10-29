import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { IdentityService } from '../../../core/services/identity.service';
import { MOCK_WORKSPACE_LATENCY, MockWorkspaceDataSource } from './mock-workspace-data-source';
import { CreateProjectRequest, DEFAULT_ISSUE_TYPES } from './workspace.models';

const me = { id: 'user-me', email: 'me@example.com', displayName: 'Tôi', avatarUrl: null };
const authState = signal<{ currentUser: typeof me | null }>({ currentUser: me });

function value<T>(source: Observable<T>): T {
  let result: T | undefined;
  let failure: unknown;
  source.subscribe({ next: v => (result = v), error: e => (failure = e) });
  if (failure) throw failure;
  return result as T;
}

function errorCode(source: Observable<unknown>): string {
  let code = '';
  source.subscribe({ error: (e: HttpErrorResponse) => (code = e.error.code) });
  return code;
}

function createSource(): MockWorkspaceDataSource {
  return TestBed.inject(MockWorkspaceDataSource);
}

function projectRequest(overrides: Partial<CreateProjectRequest> = {}): CreateProjectRequest {
  return {
    name: 'Hệ thống quản lý thư viện',
    projectKey: 'HTQLTV',
    description: null,
    leadUserId: me.id,
    workflowTemplateId: 'preset-kanban-simple',
    issueTypes: DEFAULT_ISSUE_TYPES.map(type => ({ ...type })),
    members: [],
    ...overrides
  };
}

describe('MockWorkspaceDataSource', () => {
  beforeEach(() => {
    localStorage.clear();
    authState.set({ currentUser: me });
    TestBed.configureTestingModule({
      providers: [
        MockWorkspaceDataSource,
        { provide: MOCK_WORKSPACE_LATENCY, useValue: 0 },
        { provide: IdentityService, useValue: { authState } }
      ]
    });
  });

  it('seeds an owned and a joined organization for a new user and persists changes', () => {
    const orgs = value(createSource().listOrganizations());
    expect(orgs.map(org => org.myRole).sort()).toEqual(['Member', 'Owner']);

    value(createSource().createOrganization({ name: 'Phòng Lab AI', slug: 'phong-lab-ai' }));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [MockWorkspaceDataSource, { provide: MOCK_WORKSPACE_LATENCY, useValue: 0 }, { provide: IdentityService, useValue: { authState } }]
    });
    expect(value(createSource().listOrganizations()).some(org => org.slug === 'phong-lab-ai')).toBe(true);
  });

  it('hides organizations the user does not belong to', () => {
    expect(errorCode(createSource().getOrganization('mock-org-club'))).toBe('ORG_NOT_FOUND');
  });

  it('creates a project with the template, renamed issue types and selected members', () => {
    const source = createSource();
    const project = value(source.createProject('mock-org-lab', projectRequest({
      issueTypes: DEFAULT_ISSUE_TYPES.map(type => type.code === 'Story' ? { ...type, name: 'Use case' } : { ...type }),
      members: [{ userId: 'mock-user-03', role: 'Developer' }]
    })));
    expect(project).toMatchObject({ projectKey: 'HTQLTV', managementType: 'Kanban', workflowTemplateName: 'Kanban đơn giản', memberCount: 2 });
    expect(value(source.listProjects('mock-org-lab')).map(item => item.id)).toContain(project.id);
    expect(value(source.listRecent())[0]).toMatchObject({ kind: 'project', id: project.id });
  });

  it('rejects duplicate keys, leads outside the organization and duplicate issue type names', () => {
    const source = createSource();
    expect(errorCode(source.createProject('mock-org-lab', projectRequest({ projectKey: 'CTSV' })))).toBe('PROJECT_KEY_DUPLICATE');
    expect(value(source.isProjectKeyAvailable('mock-org-lab', 'ctsv'))).toBe(false);
    expect(errorCode(source.createProject('mock-org-lab', projectRequest({ leadUserId: 'mock-user-08' })))).toBe('PROJECT_LEAD_INVALID');
    expect(errorCode(source.createProject('mock-org-lab', projectRequest({
      issueTypes: DEFAULT_ISSUE_TYPES.map(type => ({ ...type, name: 'Việc' }))
    })))).toBe('ISSUE_TYPE_NAME_INVALID');
  });

  it('reports each invited email and blocks settings for non-owners', () => {
    const source = createSource();
    const results = value(source.inviteByEmail('mock-org-lab', [
      'GiaQuan.Hoang@example.com', 'thuha.le@example.com', 'hainam.dang@example.com', 'nobody@example.com'
    ], 'Member'));
    expect(results.map(result => result.outcome)).toEqual(['Sent', 'AlreadyMember', 'AlreadyInvited', 'NoAccount']);
    expect(errorCode(source.inviteByEmail('mock-org-lab', ['not-an-email'], 'Member'))).toBe('INVITE_EMAIL_INVALID');
    expect(errorCode(source.listInvitations('mock-org-thesis'))).toBe('ORG_FORBIDDEN');
  });

  it('changes member roles but never the owner role', () => {
    const source = createSource();
    expect(value(source.updateMemberRole('mock-org-lab', 'mock-user-04', 'Admin')).role).toBe('Admin');
    expect(errorCode(source.updateMemberRole('mock-org-lab', me.id, 'Member'))).toBe('ORG_OWNER_ROLE_LOCKED');
    expect(errorCode(source.updateMemberRole('mock-org-lab', 'mock-user-04', 'Owner'))).toBe('ORG_ROLE_INVALID');
  });

  it('revokes the previous invite link when a new one is generated', () => {
    const source = createSource();
    const oldLink = value(source.getInviteLink('mock-org-lab'))!;
    const newLink = value(source.regenerateInviteLink('mock-org-lab', 'Admin'));
    expect(newLink.token).not.toBe(oldLink.token);
    expect(errorCode(source.getInviteByToken(oldLink.token))).toBe('INVITE_LINK_INVALID');
    expect(value(source.getInviteByToken(newLink.token))).toMatchObject({ orgName: 'HUCE Software Lab', alreadyMember: true });
  });

  it('lets an invitee accept a pending invitation', () => {
    const source = createSource();
    const [invite] = value(source.listMyInvitations());
    const org = value(source.respondInvitation(invite.id, true));
    expect(org).toMatchObject({ id: 'mock-org-club', myRole: 'Member' });
    expect(value(source.listMyInvitations())).toHaveLength(0);
  });

  it('validates workflow templates and protects the default template', () => {
    const source = createSource();
    expect(errorCode(source.createWorkflowTemplate('mock-org-lab', {
      name: 'Thiếu Done', managementType: 'Scrum', statuses: [{ name: 'To Do', category: 'ToDo' }, { name: 'Doing', category: 'InProgress' }]
    }))).toBe('WORKFLOW_TEMPLATE_INVALID');
    const created = value(source.createWorkflowTemplate('mock-org-lab', {
      name: 'Kanban hỗ trợ', managementType: 'Kanban', statuses: [{ name: 'Mới', category: 'ToDo' }, { name: 'Xong', category: 'Done' }]
    }));
    expect(value(source.setDefaultWorkflowTemplate('mock-org-lab', created.id)).isDefault).toBe(true);
    expect(errorCode(source.deleteWorkflowTemplate('mock-org-lab', created.id))).toBe('WORKFLOW_TEMPLATE_IN_USE_AS_DEFAULT');
    expect(errorCode(source.deleteWorkflowTemplate('mock-org-lab', 'preset-scrum-std'))).toBe('WORKFLOW_TEMPLATE_PRESET_READONLY');
  });

  it('filters assigned work by organization and sorts by due date', () => {
    const source = createSource();
    const all = value(source.listMyWork());
    const lab = value(source.listMyWork('mock-org-lab'));
    expect(all.length).toBeGreaterThan(lab.length);
    expect(lab.every(item => item.orgId === 'mock-org-lab')).toBe(true);
    const dated = lab.filter(item => item.dueDate).map(item => item.dueDate!);
    expect(dated).toEqual([...dated].sort());
  });
});
