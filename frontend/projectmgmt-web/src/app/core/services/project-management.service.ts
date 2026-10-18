import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, of, tap, throwError } from 'rxjs';
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
  role: 'Project Lead' | 'Scrum Master' | 'Developer' | 'QA Engineer' | 'Viewer';
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
  parentId?: string; // Parent Task ID if this is a Sub-task
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

  // --- Epic Management ---
  addEpic(name: string, color = 'indigo', description = ''): EpicItem {
    const key = `E-${this.epics().length + 1}`;
    const newEpic: EpicItem = {
      id: `epic-${Date.now()}`,
      key,
      name,
      color,
      description,
      status: 'In Progress'
    };
    this.epics.update(list => [...list, newEpic]);
    return newEpic;
  }

  // --- Sub-task Creation ---
  createSubTask(parentId: string, title: string): WorkItem {
    const parent = this.workItems().find(w => w.id === parentId);
    const subCounter = this.workItems().filter(w => w.parentId === parentId).length + 1;
    const issueKey = parent ? `${parent.issueKey}-${subCounter}` : `SUB-${Date.now()}`;

    const newSub: WorkItem = {
      id: `wi-sub-${Date.now()}`,
      issueKey,
      projectId: this.currentProject()?.id ?? '',
      parentId,
      title: title || 'Sub-task mới',
      statusId: 'st-1',
      statusName: 'To Do',
      issueType: 'Sub-task',
      priority: 'Medium',
      storyPoints: 1,
      assigneeName: parent?.assigneeName || '',
      reporterName: parent?.reporterName || 'Tôi',
      sprintId: parent?.sprintId || '',
      sprintName: parent?.sprintName || '',
      epicName: parent?.epicName,
      epicColor: parent?.epicColor,
      createdAt: new Date().toISOString().split('T')[0]
    };

    this.workItems.update(list => [...list, newSub]);
    return newSub;
  }

  // --- WorkItem Helper Mutations ---
  updateWorkItem(updatedItem: WorkItem): void {
    this.workItems.update(items =>
      items.map(item => item.id === updatedItem.id ? { ...updatedItem, updatedAt: new Date().toISOString().split('T')[0] } : item)
    );
    if (this.activeDrawerTask()?.id === updatedItem.id) {
      this.activeDrawerTask.set({ ...updatedItem });
    }
  }

  updateWorkItemStatus(id: string, newStatus: string): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === id) {
          const updated = {
            ...item,
            statusName: newStatus,
            updatedAt: new Date().toISOString().split('T')[0],
            activities: [
              ...(item.activities || []),
              {
                id: `act-${Date.now()}`,
                actorName: 'Bạn',
                action: `Đã chuyển trạng thái sang ${newStatus}`,
                timestamp: 'Vừa xong'
              }
            ]
          };
          if (this.activeDrawerTask()?.id === id) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemAssignee(taskId: string, assigneeName: string): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, assigneeName, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemPriority(taskId: string, priority: 'Low' | 'Medium' | 'High' | 'Urgent'): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, priority, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemEpic(taskId: string, epicName: string, epicColor = 'indigo'): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, epicName, epicColor, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemIssueType(taskId: string, issueType: WorkItem['issueType']): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, issueType, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemStoryPoints(taskId: string, storyPoints: number): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, storyPoints, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemSprint(taskId: string, newSprintId: string, newSprintName: string): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, sprintId: newSprintId, sprintName: newSprintName, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  cloneWorkItem(taskId: string): WorkItem | null {
    const item = this.workItems().find(w => w.id === taskId);
    if (!item) return null;

    const proj = this.currentProject();
    const counter = (proj?.issueCounter ?? 0) + 1;
    const issueKey = proj ? `${proj.projectKey}-${counter}` : `TASK-${counter}`;
    const cloned: WorkItem = {
      ...item,
      id: `wi-${Date.now()}`,
      issueKey,
      title: `${item.title} (Bản sao)`,
      createdAt: new Date().toISOString().split('T')[0],
      comments: [],
      activities: []
    };

    this.workItems.update(list => [cloned, ...list]);
    return cloned;
  }

  deleteWorkItem(taskId: string): void {
    this.workItems.update(items => items.filter(item => item.id !== taskId && item.parentId !== taskId));
    if (this.activeDrawerTask()?.id === taskId) {
      this.activeDrawerTask.set(null);
    }
  }

  // --- Bulk Operations ---
  bulkUpdateStatus(ids: string[], newStatus: WorkItem['statusName']): void {
    this.workItems.update(items =>
      items.map(item => ids.includes(item.id) ? { ...item, statusName: newStatus } : item)
    );
  }

  bulkUpdateSprint(ids: string[], sprintId: string, sprintName: string): void {
    this.workItems.update(items =>
      items.map(item => ids.includes(item.id) ? { ...item, sprintId, sprintName } : item)
    );
  }

  bulkDelete(ids: string[]): void {
    this.workItems.update(items => items.filter(item => !ids.includes(item.id)));
  }

  // --- Sprint Management ---
  addNewSprint(): Sprint {
    const proj = this.currentProject();
    const nextIdx = this.sprints().length + 1;
    const name = proj ? `${proj.projectKey} Sprint ${nextIdx}` : `Sprint ${nextIdx}`;
    const newSp: Sprint = {
      id: `sp-${Date.now()}`,
      projectId: proj?.id ?? '',
      name,
      goal: `Mục tiêu Sprint ${nextIdx}`,
      status: 'future',
      startDate: new Date().toISOString().split('T')[0],
      endDate: new Date(Date.now() + 14 * 86400000).toISOString().split('T')[0],
      workItemsCount: 0,
      totalStoryPoints: 0
    };
    this.sprints.update(list => [...list, newSp]);
    return newSp;
  }

  updateSprintStatus(sprintId: string, status: 'active' | 'future' | 'completed'): void {
    this.sprints.update(list =>
      list.map(sp => sp.id === sprintId ? { ...sp, status } : sp)
    );
  }

  updateSprintGoal(sprintId: string, goal: string): void {
    this.sprints.update(list =>
      list.map(sp => sp.id === sprintId ? { ...sp, goal } : sp)
    );
  }

  updateSprintInfo(sprintId: string, name: string, startDate?: string, endDate?: string): void {
    this.sprints.update(list =>
      list.map(sp => sp.id === sprintId ? { ...sp, name, startDate: startDate ?? sp.startDate, endDate: endDate ?? sp.endDate } : sp)
    );
  }

  moveSprintItemsToBacklog(sprintId: string): void {
    this.workItems.update(items =>
      items.map(item => item.sprintId === sprintId ? { ...item, sprintId: 'sp-backlog', sprintName: 'Backlog Pool' } : item)
    );
  }

  // --- Project Management Methods ---
  addNewProject(newProj: { name: string; projectKey: string; type?: string; description?: string }): Project {
    const created: Project = {
      id: `proj-${Date.now()}`,
      orgId: '',
      projectKey: newProj.projectKey.toUpperCase(),
      name: newProj.name,
      type: newProj.type || 'Software space',
      description: newProj.description || '',
      leadUserId: '',
      leadName: '',
      issueCounter: 0,
      isArchived: false,
      isDeleted: false,
      createdAt: new Date().toISOString()
    };

    this.allProjectsList.update(list => [created, ...list]);
    this.currentProject.set(created);
    return created;
  }

  // --- WorkItem CRUD Methods ---
  addWorkItem(newItem: Partial<WorkItem>): WorkItem {
    const proj = this.currentProject();
    const counter = (proj?.issueCounter ?? 0) + 1;
    const key = proj ? `${proj.projectKey}-${counter}` : `TASK-${counter}`;
    
    const createdItem: WorkItem = {
      id: `wi-${Date.now()}`,
      issueKey: key,
      projectId: proj?.id ?? '',
      parentId: newItem.parentId,
      title: newItem.title || 'Công việc mới',
      description: newItem.description || '',
      statusId: 'st-1',
      statusName: newItem.statusName || 'To Do',
      issueType: newItem.issueType || 'Task',
      priority: newItem.priority || 'Medium',
      storyPoints: newItem.storyPoints || 3,
      assigneeName: newItem.assigneeName || '',
      reporterName: newItem.reporterName || 'Tôi',
      sprintId: newItem.sprintId || '',
      sprintName: newItem.sprintName || '',
      epicName: newItem.epicName || '',
      epicColor: newItem.epicColor || 'blue',
      labels: newItem.labels || ['task'],
      createdAt: new Date().toISOString().split('T')[0],
      updatedAt: new Date().toISOString().split('T')[0]
    };

    this.workItems.update(list => [createdItem, ...list]);
    return createdItem;
  }

  addCommentToTask(taskId: string, commentText: string): void {
    const newComment: TaskComment = {
      id: `comment-${Date.now()}`,
      authorName: 'Tôi',
      content: commentText,
      createdAt: new Date().toLocaleString()
    };

    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = {
            ...item,
            comments: [...(item.comments || []), newComment]
          };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  // HTTP API Operations
  getOrganizations(): Observable<Organization[]> {
    return this.http.get<Organization[]>(`${this.baseUrl}/organizations`);
  }

  createOrganization(req: CreateOrganizationRequest): Observable<Organization> {
    return this.http.post<Organization>(`${this.baseUrl}/organizations`, req);
  }

  getProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.baseUrl}/projects`).pipe(tap(projects => {
      this.allProjectsList.set(projects);
      this.currentProject.set(projects.find(p => p.id === this.currentProject().id) ?? projects[0] ?? this.emptyProject);
    }));
  }

  createProject(req: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>(`${this.baseUrl}/projects`, req).pipe(tap(project => {
      this.cacheProject(project);
      this.currentProject.set(project);
    }));
  }

  addProjectMember(memberReq: Pick<ProjectMember, 'displayName' | 'email' | 'role'>): Observable<boolean> {
    void memberReq;
    return throwError(() => ({ title: 'Chức năng thêm thành viên chưa được tích hợp.' }));
  }

  getUsers(): Observable<UserDisplayInfo[]> {
    return this.http.get<UserDisplayInfo[]>(`${this.baseUrl}/users`);
  }

  getProjectById(id: string): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/projects/${id}`).pipe(tap(project => this.cacheProject(project)));
  }

  updateProject(id: string, req: UpdateProjectRequest): Observable<Project> {
    return this.http.put<Project>(`${this.baseUrl}/projects/${id}`, req).pipe(tap(project => this.cacheProject(project)));
  }

  deleteProject(id: string): Observable<boolean> {
    return this.http.delete<void>(`${this.baseUrl}/projects/${id}`).pipe(tap(() => {
      this.allProjectsList.update(projects => projects.filter(p => p.id !== id));
      if (this.currentProject().id === id) this.currentProject.set(this.allProjectsList()[0] ?? this.emptyProject);
    }), map(() => true));
  }

  private cacheProject(project: Project): void {
    this.allProjectsList.update(projects => {
      const exists = projects.some(p => p.id === project.id);
      return exists ? projects.map(p => p.id === project.id ? project : p) : [...projects, project];
    });
    if (this.currentProject().id === project.id) this.currentProject.set(project);
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
    return this.http.get<ProjectStatus[]>(`${this.baseUrl}/projects/${projectId}/statuses`);
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
    return this.http.get<WorkflowTransition[]>(`${this.baseUrl}/projects/${projectId}/workflow/transitions`);
  }

  createWorkflowTransition(projectId: string, req: CreateWorkflowTransitionRequest): Observable<WorkflowTransition> {
    return this.http.post<WorkflowTransition>(`${this.baseUrl}/projects/${projectId}/workflow/transitions`, req);
  }

  deleteWorkflowTransition(id: string): Observable<boolean> {
    return this.http.delete<void>(`${this.baseUrl}/workflow/transitions/${id}`).pipe(map(() => true));
  }

  getBoards(projectId: string): Observable<Board[]> {
    return this.http.get<Board[]>(`${this.baseUrl}/projects/${projectId}/boards`);
  }

  createBoard(projectId: string, req: CreateBoardRequest): Observable<Board> {
    return this.http.post<Board>(`${this.baseUrl}/projects/${projectId}/boards`, req);
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
