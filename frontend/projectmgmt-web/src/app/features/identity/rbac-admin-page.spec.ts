import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RbacAdminPageComponent } from './rbac-admin-page';
import { IdentityService } from '../../core/services/identity.service';
import { FormBuilder } from '@angular/forms';

describe('RBAC administration', () => {
  let component: RbacAdminPageComponent;
  const service = {
    getUsers: vi.fn(), getRoles: vi.fn(), getPermissions: vi.fn(),
    updateRolePermissions: vi.fn(), createRole: vi.fn()
  };
  beforeEach(() => {
    vi.resetAllMocks();
    service.getUsers.mockReturnValue(of([{ id: 'u', displayName: 'User', email: 'u@example.test', roles: [{ roleId: 'r', roleName: 'Viewer', scopeType: 'Project' }] }]));
    service.getRoles.mockReturnValue(of([{ id: 'r', name: 'Viewer', permissionCodes: ['project.read'] }]));
    service.getPermissions.mockReturnValue(of([{ id: 'p', code: 'project.read', module: 'Planning' }]));
    TestBed.configureTestingModule({ providers: [FormBuilder, { provide: IdentityService, useValue: service }] });
    component = TestBed.runInInjectionContext(() => new RbacAdminPageComponent());
    component.ngOnInit();
  });
  it('loads persisted permissions and scoped users', () => {
    expect(component.hasPermission(component.roles()[0], 'project.read')).toBe(true);
    component.selectedRoleFilter = 'r';
    expect(component.filteredUsers).toHaveLength(1);
  });
  it('does not send unchanged roles', () => {
    component.saveMatrix();
    expect(service.updateRolePermissions).not.toHaveBeenCalled();
    expect(component.loading()).toBe(false);
  });
  it('keeps changes retryable and clears loading after failure', () => {
    component.togglePermission(component.roles()[0], 'issue.read');
    service.updateRolePermissions.mockReturnValue(throwError(() => new Error('failed')));
    component.saveMatrix();
    expect(component.errorMsg()).toBeTruthy();
    expect(component.loading()).toBe(false);
    expect(component.successMsg()).toBeNull();
    service.updateRolePermissions.mockReturnValue(of({ id: 'r', permissionCodes: ['project.read', 'issue.read'] }));
    component.saveMatrix();
    expect(service.updateRolePermissions).toHaveBeenCalledTimes(2);
    component.saveMatrix();
    expect(service.updateRolePermissions).toHaveBeenCalledTimes(2);
  });
  it('shows load errors instead of fabricated data', () => {
    service.getUsers.mockReturnValue(throwError(() => new Error('forbidden')));
    component.loadAllData();
    expect(component.errorMsg()).toBeTruthy();
    expect(component.loading()).toBe(false);
  });
  it('keeps the create role form open when saving fails', () => {
    component.openRoleModal();
    component.roleForm.setValue({ name: 'Auditor', description: 'Review' });
    service.createRole.mockReturnValue(throwError(() => new Error('forbidden')));
    component.submitNewRole();
    expect(component.showRoleModal).toBe(true);
    expect(component.roleForm.value.name).toBe('Auditor');
    expect(component.loading()).toBe(false);
  });
  it('filters users based on search query', () => {
    component.searchQuery = 'User';
    expect(component.filteredUsers.length).toBe(1);
    component.searchQuery = 'nobody';
    expect(component.filteredUsers.length).toBe(0);
  });
});
