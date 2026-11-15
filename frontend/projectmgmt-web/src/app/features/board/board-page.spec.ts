import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TOKEN_STORE } from '../../core/auth/token-store';
import { BoardPage } from './board-page';
import {
  Project,
  ProjectManagementService,
  ProjectStatus,
  Sprint,
  WorkItem
} from '../../core/services/project-management.service';

describe('BoardPage sprint & workflow scoping', () => {
  let fixture: ComponentFixture<BoardPage>;
  let component: BoardPage;
  let projectService: ProjectManagementService;

  const scrumProject: Project = {
    id: 'p-qlda',
    orgId: 'org-huce',
    projectKey: 'QLDA',
    name: 'Quản lý dự án',
    boardName: 'Bảng Mẫu quản lý dự án nội bộ',
    boardType: 'Scrum',
    boardTabTitle: 'Mẫu quản lý dự án nội bộ',
    workflowName: 'Mẫu quản lý dự án nội bộ',
    managementType: 'Scrum',
    leadUserId: 'u-lead',
    issueCounter: 79,
    isArchived: false,
    isDeleted: false,
    createdAt: '',
    members: [
      { id: 'm1', userId: 'u-lead', displayName: 'Hoàng Hải Hậu', email: 'h***@example.com', role: 'Project Lead', joinedAt: '' }
    ]
  };

  const statuses: ProjectStatus[] = [
    { id: 'st-todo', name: 'Chưa làm', category: 'ToDo', colorHex: '#64748B', isInitial: true, isDone: false, orderIndex: 1 },
    { id: 'st-doing', name: 'Đang làm', category: 'InProgress', colorHex: '#3B82F6', isInitial: false, isDone: false, isActiveWork: true, orderIndex: 2 },
    { id: 'st-done', name: 'Hoàn thành', category: 'Done', colorHex: '#10B981', isInitial: false, isDone: true, orderIndex: 3 }
  ];

  const sprints: Sprint[] = [
    { id: 'sp-1', projectId: 'p-qlda', name: 'QLDA Sprint 1', status: 'completed', workItemsCount: 0, totalStoryPoints: 0 },
    { id: 'sp-3', projectId: 'p-qlda', name: 'QLDA Sprint 3', status: 'active', workItemsCount: 0, totalStoryPoints: 0 }
  ];

  const makeItem = (overrides: Partial<WorkItem>): WorkItem => ({
    id: overrides.id ?? crypto.randomUUID(),
    projectId: 'p-qlda',
    issueKey: overrides.issueKey ?? 'QLDA-1',
    title: overrides.title ?? 'Sample item',
    statusId: overrides.statusId ?? 'st-todo',
    statusName: overrides.statusName ?? 'Chưa làm',
    issueType: overrides.issueType ?? 'Task',
    priority: overrides.priority ?? 'Medium',
    storyPoints: overrides.storyPoints ?? 2,
    reporterName: 'Lead',
    createdAt: '',
    comments: [],
    activities: [],
    ...overrides
  });

  beforeEach(async () => {
    if (typeof window.matchMedia !== 'function') {
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn().mockImplementation(() => ({
          matches: true,
          media: '',
          onchange: null,
          addListener: vi.fn(),
          removeListener: vi.fn(),
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
          dispatchEvent: vi.fn()
        }))
      });
    }

    await TestBed.configureTestingModule({
      imports: [BoardPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: TOKEN_STORE,
          useValue: {
            getAccessToken: () => null,
            getRefreshToken: () => null,
            getSession: () => null,
            setSession: () => {},
            clear: () => {}
          }
        }
      ]
    }).compileComponents();

    projectService = TestBed.inject(ProjectManagementService);
    vi.spyOn(projectService, 'loadWorkspace').mockImplementation(() => {});
    vi.spyOn(projectService, 'loadProjectMembers').mockReturnValue(of([]));

    projectService.currentProject.set(scrumProject);
    fixture = TestBed.createComponent(BoardPage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    projectService.projectStatuses.set(statuses);
    projectService.customIssueTypes.set([
      { id: 't-story', name: 'Story', icon: 'bookmark', color: '#65BA43', isSubtask: false },
      { id: 't-task', name: 'Task', icon: 'task_alt', color: '#4BADE8', isSubtask: false },
      { id: 't-epic', name: 'Epic', icon: 'bolt', color: '#904EE2', isSubtask: false },
      { id: 't-sub', name: 'Sub-task', icon: 'subdirectory_arrow_right', color: '#4BADE8', isSubtask: true }
    ]);
  });

  it('shows 0 cards on Scrum board when active sprint has 0 items even if backlog, epics, completed sprints and subtasks exist', () => {
    projectService.sprints.set(sprints);
    projectService.workItems.set([
      makeItem({ id: 'i-1', issueKey: 'QLDA-1', issueType: 'Task', sprintId: undefined, statusName: 'Chưa làm' }),
      makeItem({ id: 'i-2', issueKey: 'QLDA-2', issueType: 'Epic', title: 'Xây dựng Dashboard', sprintId: undefined }),
      makeItem({ id: 'i-3', issueKey: 'QLDA-3', issueType: 'Story', sprintId: 'sp-1', statusName: 'Hoàn thành' }),
      makeItem({ id: 'i-4', issueKey: 'QLDA-4', issueType: 'Sub-task', parentId: 'i-1', sprintId: undefined })
    ]);
    fixture.detectChanges();

    expect(component.boardScopeItems()).toEqual([]);
    expect(component.filteredItems()).toEqual([]);
    expect(component.getColumnItems('Chưa làm')).toEqual([]);
    expect(component.getColumnItems('Hoàn thành')).toEqual([]);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('QLDA Sprint 3 (Đang chạy)');
    expect(text).toContain('0 thẻ việc');
    expect(text).toContain('hiện chưa có công việc nào');
    expect(text).not.toContain('Xây dựng Dashboard');
  });

  it('displays only top-level non-Epic items belonging to active sprint while counting nested sub-tasks on parent cards', () => {
    projectService.sprints.set(sprints);
    projectService.workItems.set([
      makeItem({ id: 'sp3-story', issueKey: 'QLDA-10', issueType: 'Story', title: 'Story trong Sprint 3', sprintId: 'sp-3', statusName: 'Chưa làm' }),
      makeItem({ id: 'sp3-sub', issueKey: 'QLDA-11', issueType: 'Sub-task', title: 'Subtask con', parentId: 'sp3-story', sprintId: 'sp-3', statusName: 'Hoàn thành', isDone: true }),
      makeItem({ id: 'sp3-epic', issueKey: 'QLDA-12', issueType: 'Epic', title: 'Epic không hiển thị', sprintId: 'sp-3' }),
      makeItem({ id: 'backlog-task', issueKey: 'QLDA-13', issueType: 'Task', title: 'Backlog Task', sprintId: undefined, statusName: 'Đang làm' })
    ]);
    fixture.detectChanges();

    expect(component.boardScopeItems().map(i => i.issueKey)).toEqual(['QLDA-10']);
    expect(component.getSubtaskCount('sp3-story')).toEqual({ total: 1, done: 1 });
    expect(component.getTabCount('Chưa làm')).toBe(1);
    expect(component.getTabCount('Đang làm')).toBe(0);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('1 thẻ việc');
    expect(text).toContain('Story trong Sprint 3');
    expect(text).toContain('Sub-tasks: 1/1');
    expect(text).not.toContain('Subtask con');
    expect(text).not.toContain('Epic không hiển thị');
    expect(text).not.toContain('Backlog Task');
  });

  it('shows empty Scrum state instead of leaking backlog when there is no active sprint', () => {
    projectService.sprints.set([
      { id: 'sp-1', projectId: 'p-qlda', name: 'QLDA Sprint 1', status: 'completed', workItemsCount: 1, totalStoryPoints: 2 }
    ]);
    projectService.workItems.set([
      makeItem({ id: 'backlog-task', issueKey: 'QLDA-1', issueType: 'Task', sprintId: undefined })
    ]);
    fixture.detectChanges();

    expect(component.boardScopeItems()).toEqual([]);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Chưa có Sprint đang chạy');
    expect(text).toContain('0 thẻ việc');
    expect(text).toContain('Đi tới Kế hoạch Sprint & Backlog');
  });

  it('preserves other status tab counts when filtering by a specific status tab and excludes Epic/Sub-task from type filter', () => {
    projectService.sprints.set(sprints);
    projectService.workItems.set([
      makeItem({ id: 'a1', issueKey: 'QLDA-21', issueType: 'Story', sprintId: 'sp-3', statusName: 'Chưa làm' }),
      makeItem({ id: 'a2', issueKey: 'QLDA-22', issueType: 'Task', sprintId: 'sp-3', statusName: 'Đang làm' })
    ]);
    component.selectedStatusTab.set('Chưa làm');
    fixture.detectChanges();

    expect(component.searchAndFacetFilteredItems().length).toBe(2);
    expect(component.filteredItems().map(i => i.issueKey)).toEqual(['QLDA-21']);
    expect(component.getTabCount('Chưa làm')).toBe(1);
    expect(component.getTabCount('Đang làm')).toBe(1);
    expect(component.issueTypes()).toEqual(['Story', 'Task']);
  });

  it('shows continuous non-Epic, non-Sub-task items on Kanban boards excluding completed sprints', () => {
    projectService.currentProject.set({
      ...scrumProject,
      type: 'Kanban',
      boardType: 'Kanban',
      managementType: 'Kanban',
      boardName: 'Bảng Kanban đơn giản',
      boardTabTitle: 'Kanban đơn giản',
      workflowName: 'Kanban đơn giản'
    });
    fixture.detectChanges();
    projectService.sprints.set(sprints);
    projectService.workItems.set([
      makeItem({ id: 'k1', issueKey: 'QLDA-30', issueType: 'Task', sprintId: undefined, statusName: 'Đang làm' }),
      makeItem({ id: 'k2', issueKey: 'QLDA-31', issueType: 'Epic', sprintId: undefined }),
      makeItem({ id: 'k3', issueKey: 'QLDA-32', issueType: 'Story', sprintId: 'sp-1', statusName: 'Hoàn thành' })
    ]);
    fixture.detectChanges();

    expect(component.isKanbanBoard()).toBe(true);
    expect(component.boardScopeItems().map(i => i.issueKey)).toEqual(['QLDA-30']);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Luồng công việc liên tục');
    expect(text).toContain('1 thẻ việc');
  });
});

