import { HttpClient } from '@angular/common/http';
import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { EMPTY, Observable, Subject, catchError, concatMap, defer, finalize, forkJoin, map, tap } from 'rxjs';
import { ToastService } from './toast.service';
import { RbacApi } from '../api/rbac.api';
import { environment } from '../../../environments/environment';

export interface Organization {
  id: string;
  name: string;
  slug: string;
  ownerId: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateOrganizationRequest {
  name: string;
  slug?: string;
  ownerId?: string;
}

export interface ProjectMember {
  id: string;
  userId: string;
  displayName: string;
  email: string;
  avatarUrl?: string;
  role: 'Project Lead' | 'Scrum Master' | 'Developer' | 'QA Engineer' | 'Viewer' | 'ProjectManager' | 'ScrumMaster' | 'ProductOwner';
  joinedAt: string;
}

export interface Project {
  id: string;
  orgId: string;
  projectKey: string;
  name: string;
  type?: string;
  description?: string | null;
  leadUserId: string;
  leadName?: string;
  issueCounter: number;
  isArchived: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string | null;
  members?: ProjectMember[];
}

export interface CreateProjectRequest {
  orgId: string;
  projectKey: string;
  name: string;
  description?: string | null;
  leadUserId: string;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string | null;
  leadUserId: string;
}

export interface ProjectStatus {
  id: string;
  name: string;
  category: string;
  isInitial: boolean;
  orderIndex: number;
}

export interface WorkflowTransition {
  id: string;
  projectId: string;
  fromStatusId: string;
  fromStatusName: string;
  toStatusId: string;
  toStatusName: string;
  name?: string | null;
  requiredPermissionCode?: string | null;
}

export interface CreateWorkflowTransitionRequest {
  fromStatusId: string;
  toStatusId: string;
  name?: string | null;
  requiredPermissionCode?: string | null;
}

export interface Board {
  id: string;
  projectId: string;
  name: string;
  type: string;
  isDefault: boolean;
  createdAt: string;
}

export interface CreateBoardRequest {
  name: string;
  type: string;
  isDefault: boolean;
}

export interface BoardColumn {
  id: string;
  boardId: string;
  statusId: string;
  statusName: string;
  name?: string | null;
  orderIndex: number;
  wipLimit?: number | null;
}

export interface CreateBoardColumnRequest {
  statusId: string;
  name?: string | null;
  wipLimit?: number | null;
}

export interface UpdateBoardColumnRequest {
  name: string | null;
  wipLimit: number | null;
}

export interface UserDisplayInfo {
  userId: string;
  displayName: string;
  avatarUrl?: string | null;
}

export interface Sprint {
  id: string;
  projectId: string;
  name: string;
  goal?: string;
  startDate?: string;
  endDate?: string;
  status: 'active' | 'future' | 'completed';
  workItemsCount: number;
  totalStoryPoints: number;
}

export interface TaskComment {
  id: string;
  authorName: string;
  authorAvatar?: string;
  content: string;
  createdAt: string;
}

export interface TaskActivity {
  id: string;
  actorName: string;
  action: string;
  timestamp: string;
}

export interface EpicItem {
  id: string;
  name: string;
  key: string;
  color: string;
  description?: string;
  status: 'In Progress' | 'Done' | 'To Do';
}

export interface CustomIssueType {
  id: string;
  name: string;
  icon: string; // Emoji or SVG icon identifier
  color: string; // Tailwind color name or hex code
  description?: string;
  isCustom?: boolean;
}

export interface WorkflowTemplate {
  id: string;
  name: string;
  description: string;
  statuses: ProjectStatus[];
}

export interface WorkItem {
  id: string;
  issueKey: string;
  projectId: string;
  parentId?: string;
  epicId?: string; // Parent Task ID if this is a Sub-task
  title: string;
  description?: string;
  statusId: string;
  statusName: string;
  issueType: string; // Story, Task, Bug, Epic, Sub-task, Use-Case or Custom Name
  priority: 'Low' | 'Medium' | 'High' | 'Urgent';
  storyPoints: number;
  assigneeId?: string;
  assigneeName?: string;
  assigneeAvatar?: string;
  reporterName: string;
  sprintId?: string;
  sprintName?: string;
  epicName?: string;
  epicColor?: string;
  labels?: string[];
  createdAt: string;
  updatedAt?: string;
  comments?: TaskComment[];
  activities?: TaskActivity[];
  attachments?: { id: string; fileName: string; fileSizeBytes: number }[];
  criteria?: { id: string; content: string; isMet: boolean }[];
}

@Injectable({ providedIn: 'root' })
export class ProjectManagementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  // Active Projects Signal List
  readonly allProjectsList = signal<Project[]>([]);
  // An empty selection keeps existing feature consumers safe until real projects load.
  private readonly emptyProject: Project = {
    id: '', orgId: '', projectKey: '', name: 'Chưa chọn dự án', leadUserId: '',
    issueCounter: 0, isArchived: false, isDeleted: false, createdAt: ''
  };
  readonly currentProject = signal<Project>(this.emptyProject);

  // Issue Types Signal (Standard Scrum Types)
  readonly customIssueTypes = signal<CustomIssueType[]>([
    { id: 'it-1', name: 'Task', icon: '📘', color: 'blue', description: 'Công việc phát triển' },
    { id: 'it-2', name: 'Story', icon: '📗', color: 'emerald', description: 'Yêu cầu tính năng người dùng' },
    { id: 'it-3', name: 'Bug', icon: '📕', color: 'red', description: 'Lỗi phát sinh cần sửa' },
    { id: 'it-4', name: 'Use-Case', icon: '🩵', color: 'cyan', description: 'Kịch bản kiểm thử / sử dụng' },
    { id: 'it-5', name: 'Epic', icon: '⚡', color: 'purple', description: 'Hạng mục dự án lớn' },
    { id: 'it-6', name: 'Sub-task', icon: '📙', color: 'amber', description: 'Nhiệm vụ con phân rã' }
  ]);

  // Active Project Statuses Signal (Default Scrum workflow)
  readonly projectStatuses = signal<ProjectStatus[]>([
    { id: 'st-1', name: 'To Do', category: 'To Do', isInitial: true, orderIndex: 1 },
    { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 2 },
    { id: 'st-3', name: 'Code Review', category: 'In Progress', isInitial: false, orderIndex: 3 },
    { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 4 }
  ]);

  // Epics Signal List (empty by default)
  readonly epics = signal<EpicItem[]>([]);

  // Sprints Signal List (empty by default)
  readonly sprints = signal<Sprint[]>([]);

  // WorkItems Signal List (empty by default)
  readonly workItems = signal<WorkItem[]>([]);

  // Selected Drawer Task State
  readonly activeDrawerTask = signal<WorkItem | null>(null);

  private readonly toast = inject(ToastService);
  private readonly rbac = inject(RbacApi);
  readonly pendingSaves = signal(0);
  readonly saveError = signal<string | null>(null);
  readonly workspaceLoading = signal(false);
  private selectedProjectId = '';
  private queueEpoch = 0;
  private readonly writes = new Subject<{ epoch: number; request: () => Observable<unknown> }>();

  constructor() {
    this.writes.pipe(concatMap(job => defer(() => job.epoch === this.queueEpoch ? job.request() : EMPTY).pipe(
      catchError((error: { title?: string; message?: string; detail?: string }) => {
        this.queueEpoch++;
        const message = error.detail ?? error.title ?? error.message ?? 'Không thể lưu thay đổi.';
        this.saveError.set(message);
        this.toast.error('Chưa lưu được thay đổi', message);
        return EMPTY;
      }),
      finalize(() => {
        this.pendingSaves.update(n => n - 1);
        if (this.pendingSaves() === 0 && this.currentProject().id) {
          if (!this.saveError()) this.toast.success('Đã lưu thay đổi');
          this.loadWorkspace(this.currentProject().id);
        }
      })
    ))).subscribe();
    effect(() => {
      const id = this.currentProject().id;
      if (id === this.selectedProjectId) return;
      this.selectedProjectId = id;
      untracked(() => {
        if (typeof sessionStorage !== 'undefined' && id) sessionStorage.setItem('projectmgmt.selected-project', id);
        this.workItems.set([]); this.sprints.set([]); this.epics.set([]); this.activeDrawerTask.set(null);
        if (id) this.loadWorkspace(id);
      });
    });
  }

  private enqueue(request: () => Observable<unknown>): void {
    this.saveError.set(null); this.pendingSaves.update(n => n + 1);
    this.writes.next({ epoch: this.queueEpoch, request });
  }

  loadWorkspace(projectId = this.currentProject().id): void {
    if (!projectId) return;
    this.workspaceLoading.set(true);
    forkJoin({
      data: this.http.get<{ workItems: WorkItem[]; sprints: Sprint[]; epics: EpicItem[]; statuses: ProjectStatus[] }>(`${this.baseUrl}/v1/projects/${projectId}/workspace`),
      members: this.rbac.getProjectMembers(projectId)
    }).subscribe({
      next: ({data, members}) => {
        if (this.currentProject().id !== projectId) return;
        if (this.pendingSaves() === 0) {
          this.workItems.set(data.workItems); this.sprints.set(data.sprints); this.epics.set(data.epics); this.projectStatuses.set(data.statuses);
          const openId = this.activeDrawerTask()?.id;
          if (openId) this.activeDrawerTask.set(data.workItems.find(i => i.id === openId) ?? null);
        }
        this.currentProject.update(p => ({ ...p, members: (members.members ?? []).map(m => ({ id: m.userId ?? '', userId: m.userId ?? '', displayName: m.fullName ?? m.email ?? '', email: m.email ?? '', role: (m.roleName ?? 'Developer') as ProjectMember['role'], joinedAt: m.grantedAt ?? '', avatarUrl: m.avatarUrl ?? undefined })) }));
      },
      error: (e: { title?: string }) => { if (this.currentProject().id === projectId) this.saveError.set(e.title ?? 'Không thể tải công việc.'); this.workspaceLoading.set(false); },
      complete: () => this.workspaceLoading.set(false)
    });
  }

  private persist(item: WorkItem, create = false): void {
    const body = { ...item, parentId: item.parentId || null, epicId: item.epicId || null, assigneeId: item.assigneeId || null, sprintId: item.sprintId || null, labels: item.labels ?? [] };
    const url = create ? `${this.baseUrl}/v1/projects/${item.projectId}/issues` : `${this.baseUrl}/v1/issues/${item.id}`;
    this.enqueue(() => create ? this.http.post(url, body) : this.http.put(url, body));
  }

  addEpic(name: string, color = 'indigo', description = ''): EpicItem {
    const item = this.addWorkItem({ title: name, issueType: 'Epic', description });
    const epic: EpicItem = { id: item.id, key: item.issueKey, name, color, description, status: 'To Do' };
    this.epics.update(items => [...items, epic]); return epic;
  }
  createSubTask(parentId: string, title: string): WorkItem {
    const parent = this.workItems().find(i => i.id === parentId);
    return this.addWorkItem({ title, parentId, issueType: 'Sub-task', sprintId: parent?.sprintId, assigneeId: parent?.assigneeId, assigneeName: parent?.assigneeName, epicId: parent?.epicId, epicName: parent?.epicName });
  }
  updateWorkItem(item: WorkItem): void {
    const updated = { ...item, updatedAt: new Date().toISOString() };
    this.workItems.update(items => items.map(i => i.id === item.id ? updated : i));
    if (this.activeDrawerTask()?.id === item.id) this.activeDrawerTask.set(updated);
    this.persist(updated);
  }
  private patch(id: string, changes: Partial<WorkItem>): void {
    const item = this.workItems().find(i => i.id === id);
    if (item) this.updateWorkItem({ ...item, ...changes });
  }
  updateWorkItemStatus(id: string, statusName: string): void { this.patch(id, { statusName }); }
  updateWorkItemAssignee(id: string, assigneeName: string): void {
    const member = this.currentProject().members?.find(m => m.displayName === assigneeName);
    this.patch(id, { assigneeName, assigneeId: member?.userId });
  }
  updateWorkItemPriority(id: string, priority: WorkItem['priority']): void { this.patch(id, { priority }); }
  updateWorkItemEpic(id: string, epicName: string, epicColor = 'indigo'): void { this.patch(id, { epicName, epicColor, epicId: this.epics().find(e => e.name === epicName)?.id }); }
  updateWorkItemIssueType(id: string, issueType: string): void { this.patch(id, { issueType }); }
  updateWorkItemStoryPoints(id: string, storyPoints: number): void { this.patch(id, { storyPoints }); }
  updateWorkItemSprint(id: string, sprintId: string, sprintName: string): void { this.patch(id, { sprintId: sprintId === 'sp-backlog' ? '' : sprintId, sprintName }); }
  cloneWorkItem(id: string): WorkItem | null {
    const item = this.workItems().find(i => i.id === id);
    return item ? this.addWorkItem({ ...item, title: `${item.title} (Bản sao)`, comments: [], activities: [] }) : null;
  }
  deleteWorkItem(id: string): void {
    this.workItems.update(items => items.filter(i => i.id !== id && i.parentId !== id));
    if (this.activeDrawerTask()?.id === id) this.activeDrawerTask.set(null);
    this.enqueue(() => this.http.delete(`${this.baseUrl}/v1/issues/${id}`));
  }
  bulkUpdateStatus(ids: string[], status: string): void { ids.forEach(id => this.updateWorkItemStatus(id, status)); }
  bulkUpdateSprint(ids: string[], sprintId: string, sprintName: string): void { ids.forEach(id => this.updateWorkItemSprint(id, sprintId, sprintName)); }
  bulkDelete(ids: string[]): void { ids.forEach(id => this.deleteWorkItem(id)); }
  reorderWorkItems(ids: string[]): void {
    const items = this.workItems(); const byId = new Map(items.map(i => [i.id, i]));
    const selected = new Set(ids); let cursor = 0;
    const ordered = items.map(i => selected.has(i.id) ? byId.get(ids[cursor++])! : i);
    this.workItems.set(ordered);
    const projectId = this.currentProject().id;
    const orderedIds = ordered.map(i => i.id);
    this.enqueue(() => this.http.put(`${this.baseUrl}/v1/projects/${projectId}/issues/order`, { orderedIds }));
  }
  addNewSprint(): Sprint {
    const sprint: Sprint = { id: crypto.randomUUID(), projectId: this.currentProject().id, name: `${this.currentProject().projectKey} Sprint ${this.sprints().length + 1}`, status: 'future', startDate: new Date().toISOString().slice(0, 10), endDate: new Date(Date.now() + 14 * 86400000).toISOString().slice(0, 10), workItemsCount: 0, totalStoryPoints: 0 };
    this.sprints.update(items => [...items, sprint]);
    this.enqueue(() => this.http.post(`${this.baseUrl}/v1/projects/${sprint.projectId}/sprints`, sprint)); return sprint;
  }
  private patchSprint(id: string, changes: Partial<Sprint>): void {
    const sprint = this.sprints().find(s => s.id === id); if (!sprint) return;
    const updated = { ...sprint, ...changes };
    this.sprints.update(items => items.map(s => s.id === id ? updated : s));
    this.enqueue(() => this.http.put(`${this.baseUrl}/v1/sprints/${id}`, updated));
  }
  updateSprintStatus(id: string, status: Sprint['status']): void { this.patchSprint(id, { status }); }
  updateSprintGoal(id: string, goal: string): void { this.patchSprint(id, { goal }); }
  updateSprintInfo(id: string, name: string, startDate?: string, endDate?: string): void {
    this.patchSprint(id, { name, ...(startDate ? {startDate} : {}), ...(endDate ? {endDate} : {}) });
  }
  moveSprintItemsToBacklog(id: string): void { this.workItems().filter(i => i.sprintId === id).forEach(i => this.updateWorkItemSprint(i.id, '', 'Backlog')); }
  addNewProject(request: { name: string; projectKey: string; type?: string; description?: string }): Project {
    // Retained for callers that stage a project locally; real creation uses createProject().
    return { ...this.emptyProject, ...request, id: crypto.randomUUID() };
  }
  addWorkItem(request: Partial<WorkItem>): WorkItem {
    if (!this.currentProject().id) throw new Error('Hãy chọn hoặc tạo dự án trước.');
    if (!request.sprintId && request.sprintName) request = { ...request, sprintId: this.sprints().find(s => s.name === request.sprintName)?.id };
    const initial = this.projectStatuses().find(s => s.isInitial) ?? this.projectStatuses()[0];
    const item: WorkItem = { ...request, id: crypto.randomUUID(), projectId: this.currentProject().id, issueKey: 'Đang lưu…', title: request.title || 'Công việc mới', statusId: initial?.id ?? '', statusName: request.statusName ?? initial?.name ?? 'To Do', issueType: request.issueType ?? 'Task', priority: request.priority ?? 'Medium', storyPoints: request.storyPoints ?? 0, reporterName: 'Tôi', createdAt: new Date().toISOString(), comments: [], activities: [] };
    this.workItems.update(items => [item, ...items]); this.persist(item, true); return item;
  }
  addCommentToTask(id: string, content: string): void {
    this.enqueue(() => this.http.post(`${this.baseUrl}/v1/issues/${id}/comments`, { content }));
  }
  getIssue(id: string): Observable<WorkItem> { return this.http.get<WorkItem>(`${this.baseUrl}/v1/issues/${id}`); }
  saveIssue(item: WorkItem): Observable<WorkItem> { return this.http.put<WorkItem>(`${this.baseUrl}/v1/issues/${item.id}`, { ...item, parentId: item.parentId || null, assigneeId: item.assigneeId || null, epicId: item.epicId || null, sprintId: item.sprintId || null, labels: item.labels ?? [] }); }
  createIssue(projectId: string, item: Partial<WorkItem>): Observable<WorkItem> { return this.http.post<WorkItem>(`${this.baseUrl}/v1/projects/${projectId}/issues`, item); }
  postComment(id: string, content: string): Observable<WorkItem> { return this.http.post<WorkItem>(`${this.baseUrl}/v1/issues/${id}/comments`, { content }); }
  uploadAttachment(id: string, file: File): Observable<unknown> {
    const body = new FormData(); body.append('file', file); return this.http.post(`${this.baseUrl}/v1/issues/${id}/attachments`, body);
  }
  downloadAttachment(id: string): Observable<Blob> { return this.http.get(`${this.baseUrl}/v1/attachments/${id}`, { responseType: 'blob' }); }
  deleteAttachment(id: string): Observable<void> { return this.http.delete<void>(`${this.baseUrl}/v1/attachments/${id}`); }
  addCriterion(id: string, content: string): Observable<unknown> { return this.http.post(`${this.baseUrl}/v1/issues/${id}/criteria`, { content }); }
  updateCriterion(id: string, content: string, isMet: boolean): Observable<unknown> { return this.http.put(`${this.baseUrl}/v1/criteria/${id}`, { content, isMet }); }

  // HTTP API Operations
  getOrganizations(): Observable<Organization[]> {
    return this.http.get<Organization[]>(`${this.baseUrl}/v1/organizations`);
  }

  createOrganization(req: CreateOrganizationRequest): Observable<Organization> {
    return this.http.post<Organization>(`${this.baseUrl}/v1/organizations`, req);
  }

  getProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.baseUrl}/v1/projects`).pipe(tap(projects => {
      this.allProjectsList.set(projects);
      const selected = this.currentProject().id || (typeof sessionStorage !== 'undefined' ? sessionStorage.getItem('projectmgmt.selected-project') : null);
      const next = projects.find(p => p.id === selected) ?? projects[0] ?? this.emptyProject;
      this.currentProject.set(next.id === this.currentProject().id ? { ...this.currentProject(), ...next } : next);
    }));
  }

  createProject(req: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>(`${this.baseUrl}/v1/projects`, req).pipe(tap(project => {
      this.cacheProject(project);
      this.currentProject.set(project);
    }));
  }

  addProjectMember(memberReq: Pick<ProjectMember, 'displayName' | 'email' | 'role'>): Observable<boolean> {
    const projectId = this.currentProject().id;
    return this.http.post(`${this.baseUrl}/v1/projects/${projectId}/members/invite`, { email: memberReq.email, roleName: memberReq.role }).pipe(tap(() => this.loadWorkspace(projectId)), map(() => true));
  }

  getUsers(): Observable<UserDisplayInfo[]> {
    return this.http.get<UserDisplayInfo[]>(`${this.baseUrl}/users`);
  }

  getProjectById(id: string): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/v1/projects/${id}`).pipe(tap(project => this.cacheProject(project)));
  }

  updateProject(id: string, req: UpdateProjectRequest): Observable<Project> {
    return this.http.put<Project>(`${this.baseUrl}/v1/projects/${id}`, req).pipe(tap(project => this.cacheProject(project)));
  }

  deleteProject(id: string): Observable<boolean> {
    return this.http.delete<void>(`${this.baseUrl}/v1/projects/${id}`).pipe(tap(() => {
      this.allProjectsList.update(projects => projects.filter(p => p.id !== id));
      if (this.currentProject().id === id) this.currentProject.set(this.allProjectsList()[0] ?? this.emptyProject);
    }), map(() => true));
  }

  private cacheProject(project: Project): void {
    this.allProjectsList.update(projects => {
      const exists = projects.some(p => p.id === project.id);
      return exists ? projects.map(p => p.id === project.id ? project : p) : [...projects, project];
    });
    if (this.currentProject().id === project.id) this.currentProject.update(current => ({ ...current, ...project }));
  }

  // Custom Issue Type Creation
  addCustomIssueType(name: string, icon: string, color: string, description?: string): CustomIssueType {
    const newType: CustomIssueType = {
      id: `it-${Date.now()}`,
      name,
      icon,
      color,
      description,
      isCustom: true
    };
    this.customIssueTypes.update(types => [...types, newType]);
    return newType;
  }

  // Workflow Status Management
  getProjectStatuses(projectId: string): Observable<ProjectStatus[]> {
    return this.http.get<ProjectStatus[]>(`${this.baseUrl}/v1/projects/${projectId}/statuses`);
  }

  addProjectStatus(name: string, category = 'In Progress'): ProjectStatus {
    const newStatus: ProjectStatus = {
      id: `st-${Date.now()}`,
      name,
      category,
      isInitial: false,
      orderIndex: this.projectStatuses().length + 1
    };
    this.projectStatuses.update(list => [...list, newStatus]);
    return newStatus;
  }

  deleteProjectStatus(id: string): void {
    this.projectStatuses.update(list => list.filter(s => s.id !== id));
  }

  applyWorkflowTemplateToProject(templateId: string): ProjectStatus[] {
    let newStatuses: ProjectStatus[];
    if (templateId === 'scrum-std') {
      newStatuses = [
        { id: 'st-1', name: 'To Do', category: 'To Do', isInitial: true, orderIndex: 1 },
        { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 2 },
        { id: 'st-3', name: 'Code Review', category: 'In Progress', isInitial: false, orderIndex: 3 },
        { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 4 }
      ];
    } else if (templateId === 'agile-qa') {
      newStatuses = [
        { id: 'st-1', name: 'To Do', category: 'To Do', isInitial: true, orderIndex: 1 },
        { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 2 },
        { id: 'st-3', name: 'Code Review', category: 'In Progress', isInitial: false, orderIndex: 3 },
        { id: 'st-qa', name: 'QA Testing', category: 'In Progress', isInitial: false, orderIndex: 4 },
        { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 5 }
      ];
    } else if (templateId === 'kanban-simple') {
      newStatuses = [
        { id: 'st-1', name: 'To Do', category: 'To Do', isInitial: true, orderIndex: 1 },
        { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 2 },
        { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 3 }
      ];
    } else if (templateId === 'enterprise') {
      newStatuses = [
        { id: 'st-1', name: 'Backlog', category: 'To Do', isInitial: true, orderIndex: 1 },
        { id: 'st-spec', name: 'Spec Review', category: 'To Do', isInitial: false, orderIndex: 2 },
        { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 3 },
        { id: 'st-3', name: 'Code Review', category: 'In Progress', isInitial: false, orderIndex: 4 },
        { id: 'st-qa', name: 'QA Verification', category: 'In Progress', isInitial: false, orderIndex: 5 },
        { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 6 }
      ];
    } else {
      newStatuses = this.projectStatuses();
    }

    this.projectStatuses.set(newStatuses);
    return newStatuses;
  }

  getWorkflowTransitions(projectId: string): Observable<WorkflowTransition[]> {
    return this.http.get<WorkflowTransition[]>(`${this.baseUrl}/v1/projects/${projectId}/workflow/transitions`);
  }

  createWorkflowTransition(projectId: string, req: CreateWorkflowTransitionRequest): Observable<WorkflowTransition> {
    return this.http.post<WorkflowTransition>(`${this.baseUrl}/v1/projects/${projectId}/workflow/transitions`, req);
  }

  deleteWorkflowTransition(id: string): Observable<boolean> {
    return this.http.delete<void>(`${this.baseUrl}/workflow/transitions/${id}`).pipe(map(() => true));
  }

  getBoards(projectId: string): Observable<Board[]> {
    return this.http.get<Board[]>(`${this.baseUrl}/v1/projects/${projectId}/boards`);
  }

  createBoard(projectId: string, req: CreateBoardRequest): Observable<Board> {
    return this.http.post<Board>(`${this.baseUrl}/v1/projects/${projectId}/boards`, req);
  }

  getBoardColumns(boardId: string): Observable<BoardColumn[]> {
    return this.http.get<BoardColumn[]>(`${this.baseUrl}/boards/${boardId}/columns`);
  }

  createBoardColumn(boardId: string, req: CreateBoardColumnRequest): Observable<BoardColumn> {
    return this.http.post<BoardColumn>(`${this.baseUrl}/boards/${boardId}/columns`, req);
  }

  reorderBoardColumns(boardId: string, orderedIds: string[]): Observable<boolean> {
    return this.http.put<void>(`${this.baseUrl}/boards/${boardId}/columns/reorder`, { orderedColumnIds: orderedIds }).pipe(map(() => true));
  }

  deleteBoardColumn(id: string): Observable<boolean> {
    return this.http.delete<void>(`${this.baseUrl}/boards/columns/${id}`).pipe(map(() => true));
  }

  updateBoardColumn(id: string, req: UpdateBoardColumnRequest): Observable<BoardColumn> {
    return this.http.put<BoardColumn>(`${this.baseUrl}/boards/columns/${id}`, req);
  }
}
