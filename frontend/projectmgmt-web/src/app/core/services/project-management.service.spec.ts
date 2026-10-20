import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { beforeEach, afterEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { errorInterceptor } from '../http/error.interceptor';
import { Project, ProjectManagementService } from './project-management.service';

const project = (id: string): Project => ({
  id, orgId: 'organization', projectKey: id.toUpperCase(), name: id,
  leadUserId: 'lead', issueCounter: 0, isArchived: false, isDeleted: false, createdAt: ''
});

describe('Project API integration and state', () => {
  let service: ProjectManagementService;
  let http: HttpTestingController;
  const api = environment.apiBaseUrl;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([errorInterceptor])), provideHttpClientTesting()
    ] });
    service = TestBed.inject(ProjectManagementService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('serializes dependent writes and reloads the persisted workspace after saving', () => {
    service.currentProject.set(project('aa'));
    const task = service.addWorkItem({ title: 'First title' });
    const create = http.expectOne(`${api}/v1/projects/aa/issues`);
    service.updateWorkItem({ ...task, title: 'Edited while creating' });
    expect(service.pendingSaves()).toBe(2);
    http.expectNone(`${api}/v1/issues/${task.id}`);
    create.flush({ ...task, issueKey: 'AA-1' });
    const update = http.expectOne(`${api}/v1/issues/${task.id}`);
    expect(update.request.body.title).toBe('Edited while creating');
    update.flush({ ...task, title: 'Edited while creating', issueKey: 'AA-1' });
    expect(service.pendingSaves()).toBe(0);
    http.expectOne(`${api}/v1/projects/aa/workspace`).flush({ workItems: [{ ...task, title: 'Edited while creating', issueKey: 'AA-1' }], sprints: [], epics: [], statuses: [] });
    http.expectOne(`${api}/v1/projects/aa/members`).flush({ members: [] });
    expect(service.workItems()[0].issueKey).toBe('AA-1');
    expect(service.workItems()[0].title).toBe('Edited while creating');
  });

  it('discards dependent writes on failure and restores server state', () => {
    service.currentProject.set(project('aa'));
    const task = service.addWorkItem({ title: 'Unsaved' });
    service.updateWorkItem({ ...task, title: 'Dependent edit' });
    http.expectOne(`${api}/v1/projects/aa/issues`).flush({ message: 'Permission denied' }, { status: 403, statusText: 'Forbidden' });
    http.expectNone(`${api}/v1/issues/${task.id}`);
    http.expectOne(`${api}/v1/projects/aa/workspace`).flush({ workItems: [], sprints: [], epics: [], statuses: [] });
    http.expectOne(`${api}/v1/projects/aa/members`).flush({ members: [] });
    expect(service.pendingSaves()).toBe(0);
    expect(service.saveError()).toBeTruthy();
    expect(service.workItems()).toEqual([]);
  });

  it('starts without fabricated projects and reloads persisted data', () => {
    expect(service.allProjectsList()).toEqual([]);
    service.getProjects().subscribe();
    http.expectOne(`${api}/v1/projects`).flush([project('aa'), project('bb')]);
    service.currentProject.set(project('bb'));
    service.getProjects().subscribe();
    http.expectOne(`${api}/v1/projects`).flush([project('aa'), { ...project('bb'), name: 'Persisted' }]);
    expect(service.currentProject().name).toBe('Persisted');
  });

  it('updates B without replacing its identity with current project A', () => {
    service.allProjectsList.set([project('aa'), project('bb')]);
    service.currentProject.set(project('aa'));
    service.updateProject('bb', { name: 'Updated B', leadUserId: 'lead' }).subscribe();
    const request = http.expectOne(`${api}/v1/projects/bb`);
    expect(request.request.method).toBe('PUT');
    request.flush({ ...project('bb'), name: 'Updated B' });
    expect(service.allProjectsList().map(p => p.id)).toEqual(['aa', 'bb']);
    expect(service.currentProject().id).toBe('aa');
    expect(service.allProjectsList()[1].name).toBe('Updated B');
  });

  it('does not fall back to another project on 404', () => {
    service.currentProject.set(project('aa'));
    let status = 0;
    service.getProjectById('missing').subscribe({ error: err => status = err.status });
    http.expectOne(`${api}/v1/projects/missing`).flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    expect(status).toBe(404);
    expect(service.currentProject().id).toBe('aa');
  });

  it('waits for server success before adding a created project', () => {
    service.createProject({ orgId: 'org', name: 'New', projectKey: 'NEW', leadUserId: 'lead' }).subscribe();
    expect(service.allProjectsList()).toEqual([]);
    const request = http.expectOne(`${api}/v1/projects`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.orgId).toBe('org');
    request.flush(project('new'));
    expect(service.currentProject().id).toBe('new');
  });

  it('preserves state when a delete fails and selects another project after success', () => {
    service.allProjectsList.set([project('aa'), project('bb')]);
    service.currentProject.set(project('aa'));
    service.deleteProject('aa').subscribe({ error: () => {} });
    http.expectOne(`${api}/v1/projects/aa`).flush({}, { status: 409, statusText: 'Conflict' });
    expect(service.allProjectsList()).toHaveLength(2);
    service.deleteProject('aa').subscribe();
    http.expectOne(`${api}/v1/projects/aa`).flush(null);
    expect(service.allProjectsList().map(p => p.id)).toEqual(['bb']);
    expect(service.currentProject().id).toBe('bb');
  });

  it('sends the complete column order and propagates failure', () => {
    let failed = false;
    service.reorderBoardColumns('board', ['second', 'first']).subscribe({ error: () => failed = true });
    const request = http.expectOne(`${api}/boards/board/columns/reorder`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ orderedColumnIds: ['second', 'first'] });
    request.flush({ detail: 'Invalid order' }, { status: 400, statusText: 'Bad Request' });
    expect(failed).toBe(true);
  });

  it('loads organizations, users and settings from their API routes', () => {
    service.getOrganizations().subscribe(); http.expectOne(`${api}/v1/organizations`).flush([]);
    service.getUsers().subscribe(); http.expectOne(`${api}/users`).flush([]);
    service.getProjectStatuses('aa').subscribe(); http.expectOne(`${api}/v1/projects/aa/statuses`).flush([]);
    service.getWorkflowTransitions('aa').subscribe(); http.expectOne(`${api}/v1/projects/aa/workflow/transitions`).flush([]);
    service.getBoards('aa').subscribe(); http.expectOne(`${api}/v1/projects/aa/boards`).flush([]);
    service.getBoardColumns('board').subscribe(); http.expectOne(`${api}/boards/board/columns`).flush([]);
  });

  it('updates only column presentation and WIP through PUT', () => {
    service.updateBoardColumn('column', { name: null, wipLimit: null }).subscribe();
    const request = http.expectOne(`${api}/boards/columns/column`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: null, wipLimit: null });
    request.flush({ id: 'column', statusId: 'todo', orderIndex: 1, name: null, wipLimit: null });
  });

  it('persists transition and column mutations through HTTP', () => {
    const transition = { fromStatusId: 'todo', toStatusId: 'done' };
    service.createWorkflowTransition('aa', transition).subscribe();
    const createdTransition = http.expectOne(`${api}/v1/projects/aa/workflow/transitions`);
    expect(createdTransition.request.body).toEqual(transition); createdTransition.flush({ id: 'transition' });
    service.deleteWorkflowTransition('transition').subscribe();
    const deletedTransition = http.expectOne(`${api}/workflow/transitions/transition`);
    expect(deletedTransition.request.method).toBe('DELETE'); deletedTransition.flush(null);
    service.createBoardColumn('board', { statusId: 'todo', wipLimit: 2 }).subscribe();
    const createdColumn = http.expectOne(`${api}/boards/board/columns`);
    expect(createdColumn.request.body.wipLimit).toBe(2); createdColumn.flush({ id: 'column' });
    service.deleteBoardColumn('column').subscribe();
    const deletedColumn = http.expectOne(`${api}/boards/columns/column`);
    expect(deletedColumn.request.method).toBe('DELETE'); deletedColumn.flush(null);
  });
});
