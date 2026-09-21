import { describe, expect, it } from 'vitest';
import { computeFocusActions } from './focus-recommendation.service';
import { FocusContext } from './focus-recommendation.model';
import { Project, WorkItem } from '../services/project-management.service';
import { UserProfileModel } from '../mocks/identity-mock-db';

const project: Project = {
  id: 'p1', orgId: 'o1', projectKey: 'PM', name: 'ProjectMgmt', leadUserId: 'u1',
  issueCounter: 2, isArchived: false, isDeleted: false, createdAt: '2026-01-01'
};
const developer: UserProfileModel = {
  id: 'u1', displayName: 'Nguyễn Văn Dev', email: 'dev@example.com', avatarUrl: null,
  jobTitle: 'Developer', roleId: 'role-5', roleName: 'Developer Engineer',
  isActive: true, twoFactorEnabled: false, createdAt: '2026-01-01', lastLoginAt: '2026-01-01'
};
const inProgress: WorkItem = {
  id: 't1', issueKey: 'PM-1', projectId: 'p1', title: 'Tiếp tục API', statusId: 's2',
  statusName: 'In Progress', issueType: 'Task', priority: 'High', storyPoints: 3,
  assigneeName: 'Nguyễn Văn Dev', reporterName: 'Hoàng', createdAt: '2026-01-01'
};
const urgentOther: WorkItem = {
  ...inProgress, id: 't2', issueKey: 'PM-2', title: 'Lỗi của QA', statusName: 'To Do',
  priority: 'Urgent', issueType: 'Bug', assigneeName: 'Người khác'
};

function context(overrides: Partial<FocusContext> = {}): FocusContext {
  return {
    user: developer, project, workItems: [inProgress, urgentOther], notifications: [],
    lastTaskId: null, lastRoute: null, ...overrides
  };
}

describe('computeFocusActions', () => {
  it('calls a developer back to their own in-progress task, not someone else’s urgent bug', () => {
    const actions = computeFocusActions(context());
    expect(actions[0].taskId).toBe('t1');
    expect(actions.some(action => action.taskId === 't2')).toBe(false);
  });

  it('prioritizes a QA review task and keeps a viewer read-only', () => {
    const review = { ...inProgress, id: 't3', statusName: 'Code Review' };
    const qa = { ...developer, roleName: 'QA / Tester' };
    expect(computeFocusActions(context({ user: qa, workItems: [review] }))[0].taskId).toBe('t3');
    const viewer = { ...developer, roleName: 'Guest Viewer' };
    expect(computeFocusActions(context({ user: viewer }))[0].route).toBe('/project/summary');
    expect(computeFocusActions(context({ user: viewer })).every(action => action.kind === 'route')).toBe(true);
  });

  it('does not use a finished task or unsafe remembered route', () => {
    const actions = computeFocusActions(context({
      workItems: [{ ...inProgress, statusName: 'Done' }],
      lastTaskId: 't1', lastRoute: '//external.example'
    }));
    expect(actions[0].route).toBe('/project/summary');
    expect(actions.some(action => action.taskId === 't1')).toBe(false);
  });
});
