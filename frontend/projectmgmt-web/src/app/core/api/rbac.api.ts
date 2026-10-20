import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RoleDto } from './rbac-api.models';

@Injectable({ providedIn: 'root' })
export class RbacApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  getRoles(scope?: string): Observable<RoleDto> {
    let params = new HttpParams();
    if (scope?.trim()) params = params.set('scope', scope.trim());
    return this.http.get<RoleDto>(`${this.baseUrl}/roles`, { params });
  }

  getPermissions(): Observable<RoleDto> {
    return this.http.get<RoleDto>(`${this.baseUrl}/permissions`);
  }

  createRole(request: Partial<RoleDto>): Observable<RoleDto> {
    return this.http.post<RoleDto>(`${this.baseUrl}/roles`, request);
  }

  updateRolePermissions(roleId: string, permissionIds: string[]): Observable<RoleDto> {
    return this.http.put<RoleDto>(`${this.baseUrl}/roles/${roleId}/permissions`, { permissionIds });
  }

  getUserRoles(userId: string, scopeType?: string, scopeId?: string): Observable<RoleDto> {
    let params = new HttpParams();
    if (scopeType?.trim()) params = params.set('scopeType', scopeType.trim());
    if (scopeId?.trim()) params = params.set('scopeId', scopeId.trim());
    return this.http.get<RoleDto>(`${this.baseUrl}/users/${userId}/roles`, { params });
  }

  assignUserRole(userId: string, request: Partial<RoleDto>): Observable<RoleDto> {
    return this.http.post<RoleDto>(`${this.baseUrl}/users/${userId}/roles`, request);
  }

  removeUserRole(userId: string, userRoleId: string): Observable<RoleDto> {
    return this.http.delete<RoleDto>(`${this.baseUrl}/users/${userId}/roles/${userRoleId}`);
  }

  getProjectMembers(projectId: string): Observable<RoleDto> {
    return this.http.get<RoleDto>(`${this.baseUrl}/projects/${projectId}/members`);
  }

  addProjectMember(projectId: string, userId: string, roleId: string): Observable<RoleDto> {
    return this.http.post<RoleDto>(`${this.baseUrl}/projects/${projectId}/members`, { userId, roleId });
  }

  changeProjectMemberRole(projectId: string, userId: string, roleId: string): Observable<RoleDto> {
    return this.http.put<RoleDto>(`${this.baseUrl}/projects/${projectId}/members/${userId}/role`, { roleId });
  }

  removeProjectMember(projectId: string, userId: string): Observable<RoleDto> {
    return this.http.delete<RoleDto>(`${this.baseUrl}/projects/${projectId}/members/${userId}`);
  }
}
