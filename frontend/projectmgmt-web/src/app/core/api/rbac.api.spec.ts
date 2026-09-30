import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { RbacApi } from './rbac.api';

describe('RbacApi', () => {
  let api: RbacApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/v1`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    api = TestBed.inject(RbacApi);
    http = TestBed.inject(HttpTestingController);
  });

  it('lấy danh sách roles và permissions', () => {
    api.getRoles('Project').subscribe();
    const reqRoles = http.expectOne(`${baseUrl}/roles?scope=Project`);
    expect(reqRoles.request.method).toBe('GET');
    reqRoles.flush({ success: true, roles: [] });

    api.getPermissions().subscribe();
    const reqPerms = http.expectOne(`${baseUrl}/permissions`);
    expect(reqPerms.request.method).toBe('GET');
    reqPerms.flush({ success: true, permissions: [] });
    http.verify();
  });

  it('tạo role và cập nhật permissions của role', () => {
    api.createRole({ name: 'Tech Lead', description: 'Trưởng nhóm kỹ thuật' }).subscribe();
    const reqCreate = http.expectOne(`${baseUrl}/roles`);
    expect(reqCreate.request.method).toBe('POST');
    reqCreate.flush({ success: true, roleId: 'role-123' });

    api.updateRolePermissions('role-123', ['perm-1', 'perm-2']).subscribe();
    const reqUpdate = http.expectOne(`${baseUrl}/roles/role-123/permissions`);
    expect(reqUpdate.request.method).toBe('PUT');
    expect(reqUpdate.request.body).toEqual({ permissionIds: ['perm-1', 'perm-2'] });
    reqUpdate.flush({ success: true, updatedPermissionCount: 2 });
    http.verify();
  });

  it('lấy và quản lý thành viên dự án bằng projectId', () => {
    const projectId = 'proj-999';
    api.getProjectMembers(projectId).subscribe();
    const reqList = http.expectOne(`${baseUrl}/projects/${projectId}/members`);
    expect(reqList.request.method).toBe('GET');
    reqList.flush({ success: true, members: [] });

    api.addProjectMember(projectId, 'user-1', 'role-1').subscribe();
    const reqAdd = http.expectOne(`${baseUrl}/projects/${projectId}/members`);
    expect(reqAdd.request.method).toBe('POST');
    expect(reqAdd.request.body).toEqual({ userId: 'user-1', roleId: 'role-1' });
    reqAdd.flush({ success: true });

    api.changeProjectMemberRole(projectId, 'user-1', 'role-2').subscribe();
    const reqChange = http.expectOne(`${baseUrl}/projects/${projectId}/members/user-1/role`);
    expect(reqChange.request.method).toBe('PUT');
    expect(reqChange.request.body).toEqual({ roleId: 'role-2' });
    reqChange.flush({ success: true });

    api.removeProjectMember(projectId, 'user-1').subscribe();
    const reqRemove = http.expectOne(`${baseUrl}/projects/${projectId}/members/user-1`);
    expect(reqRemove.request.method).toBe('DELETE');
    reqRemove.flush({ success: true, removed: true });
    http.verify();
  });
});
