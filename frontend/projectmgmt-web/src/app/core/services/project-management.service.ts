import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
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

export interface Project {
  id: string;
  orgId: string;
  projectKey: string;
  name: string;
  description?: string | null;
  leadUserId: string;
  issueCounter: number;
  isArchived: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string | null;
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

export interface UserDisplayInfo {
  userId: string;
  displayName: string;
  avatarUrl?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ProjectManagementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  // Organizations
  getOrganizations(): Observable<Organization[]> {
    return this.http.get<Organization[]>(`${this.baseUrl}/organizations`);
  }

  createOrganization(request: CreateOrganizationRequest): Observable<Organization> {
    return this.http.post<Organization>(`${this.baseUrl}/organizations`, request);
  }

  // Users
  getUsers(): Observable<UserDisplayInfo[]> {
    return this.http.get<UserDisplayInfo[]>(`${this.baseUrl}/users`);
  }

  // Projects
  getProjects(orgId?: string): Observable<Project[]> {
    const url = orgId ? `${this.baseUrl}/projects?orgId=${orgId}` : `${this.baseUrl}/projects`;
    return this.http.get<Project[]>(url);
  }

  getProjectById(projectId: string): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/projects/${projectId}`);
  }

  createProject(request: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>(`${this.baseUrl}/projects`, request);
  }

  updateProject(projectId: string, request: UpdateProjectRequest): Observable<Project> {
    return this.http.put<Project>(`${this.baseUrl}/projects/${projectId}`, request);
  }

  deleteProject(projectId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/projects/${projectId}`);
  }

  getProjectStatuses(projectId: string): Observable<ProjectStatus[]> {
    return this.http.get<ProjectStatus[]>(`${this.baseUrl}/projects/${projectId}/statuses`);
  }

  // Workflow Transitions
  getWorkflowTransitions(projectId: string): Observable<WorkflowTransition[]> {
    return this.http.get<WorkflowTransition[]>(`${this.baseUrl}/projects/${projectId}/workflow/transitions`);
  }

  createWorkflowTransition(projectId: string, request: CreateWorkflowTransitionRequest): Observable<WorkflowTransition> {
    return this.http.post<WorkflowTransition>(`${this.baseUrl}/projects/${projectId}/workflow/transitions`, request);
  }

  deleteWorkflowTransition(transitionId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/workflow/transitions/${transitionId}`);
  }

  // Boards & Columns
  getBoards(projectId: string): Observable<Board[]> {
    return this.http.get<Board[]>(`${this.baseUrl}/projects/${projectId}/boards`);
  }

  createBoard(projectId: string, request: CreateBoardRequest): Observable<Board> {
    return this.http.post<Board>(`${this.baseUrl}/projects/${projectId}/boards`, request);
  }

  getBoardColumns(boardId: string): Observable<BoardColumn[]> {
    return this.http.get<BoardColumn[]>(`${this.baseUrl}/boards/${boardId}/columns`);
  }

  createBoardColumn(boardId: string, request: CreateBoardColumnRequest): Observable<BoardColumn> {
    return this.http.post<BoardColumn>(`${this.baseUrl}/boards/${boardId}/columns`, request);
  }

  reorderBoardColumns(boardId: string, orderedColumnIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/boards/${boardId}/columns/reorder`, { orderedColumnIds });
  }

  deleteBoardColumn(columnId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/boards/columns/${columnId}`);
  }
}
