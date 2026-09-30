import { of } from 'rxjs';
import { describe, beforeEach, it, expect } from 'vitest';
import { RbacAdminPageComponent } from './rbac-admin-page';
import { IdentityService } from '../../core/services/identity.service';
import { FormBuilder } from '@angular/forms';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccountApi } from '../../core/api/account.api';
import { IdentityApi } from '../../core/api/identity.api';
import { NotificationApi } from '../../core/api/notification.api';
import { RbacApi } from '../../core/api/rbac.api';
import { IdentityMockDb } from '../../core/mocks/identity-mock-db';
import { TOKEN_STORE } from '../../core/auth/token-store';

describe('RbacAdminPageComponent (Unit Tests)', () => {
  let component: RbacAdminPageComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        IdentityService,
        FormBuilder,
        { provide: AccountApi, useValue: {} },
        { provide: IdentityApi, useValue: {} },
        { provide: NotificationApi, useValue: { getInbox: () => of({ items: [] }) } },
        {
          provide: RbacApi,
          useValue: {
            getRoles: () => of({
              success: true,
              items: IdentityMockDb.roles.map(r => ({
                roleId: r.id,
                name: r.name,
                isSystem: r.isSystem,
                description: r.description
              }))
            }),
            getPermissions: () => of({
              success: true,
              items: IdentityMockDb.permissions.map(p => ({
                permissionId: p.id,
                code: p.code,
                grouping: p.category,
                description: p.description
              }))
            }),
            updateRolePermissions: () => of({ success: true }),
            createRole: (req: { name?: string; description?: string }) => of({
              success: true,
              roleId: 'new-role',
              name: req.name,
              description: req.description
            })
          }
        },
        {
          provide: TOKEN_STORE,
          useValue: {
            getAccessToken: () => null,
            getRefreshToken: () => null,
            getSession: () => null,
            setSession: () => undefined,
            clear: () => undefined
          }
        }
      ]
    });

    const injector = TestBed.inject(EnvironmentInjector);
    runInInjectionContext(injector, () => {
      component = new RbacAdminPageComponent();
      component.ngOnInit();
    });
  });

  it('should create RbacAdminPageComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should load initial roles, users, and permissions', () => {
    expect(component.roles().length).toBeGreaterThan(0);
    expect(component.users().length).toBeGreaterThan(0);
    expect(component.permissions().length).toBeGreaterThan(0);
  });

  it('should toggle permission in role matrix state', () => {
    const role = component.roles()[0];
    const testPerm = 'WORK_ITEM_CREATE';

    component.togglePermission(role, testPerm);
    expect(role.permissionCodes.includes(testPerm)).toBe(true);
  });

  it('should filter users based on query', () => {
    component.searchQuery = 'admin';
    expect(component.filteredUsers.length).toBeGreaterThan(0);
    expect(component.filteredUsers[0].email).toContain('admin');
  });
});
