import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { beforeEach, afterEach, describe, expect, it } from 'vitest';
import { IdentityService } from './identity.service';
import { TOKEN_STORE } from '../auth/token-store';
import { environment } from '../../../environments/environment';

describe('Identity RBAC API mapping', () => {
  let service: IdentityService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}`;
  const permission = { permissionId: 'ad453b64-a07c-477f-bf5e-bdc3946560c6', code: 'project.read' };
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: TOKEN_STORE, useValue: { getSession: () => null, getAccessToken: () => null } }
    ] });
    service = TestBed.inject(IdentityService); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('loads the saved permission IDs as selected permission codes', () => {
    let codes: string[] = [];
    service.getRoles().subscribe(roles => codes = roles[0].permissionCodes);
    http.expectOne(`${base}/roles`).flush({ items: [{ roleId: 'r', permissionIds: [permission.permissionId] }] });
    http.expectOne(`${base}/permissions`).flush({ items: [permission] });
    expect(codes).toEqual(['project.read']);
  });
  it('sends GUIDs when saving the matrix', () => {
    service.updateRolePermissions('r', ['project.read']).subscribe();
    http.expectOne(`${base}/permissions`).flush({ items: [permission] });
    const req = http.expectOne(`${base}/roles/r/permissions`);
    expect(req.request.body).toEqual({ permissionIds: [permission.permissionId] });
    req.flush({ success: true });
  });
  it('sends initial permissions in the same create request', () => {
    service.createRole('Auditor', 'Review', ['project.read']).subscribe();
    http.expectOne(`${base}/permissions`).flush({ items: [permission] });
    const req = http.expectOne(`${base}/roles`);
    expect(req.request.body.permissionIds).toEqual([permission.permissionId]);
    req.flush({ roleId: 'r', name: 'Auditor' });
  });
  it('rejects stale permission codes before sending a write', () => {
    let failed = false;
    service.updateRolePermissions('r', ['missing']).subscribe({ error: () => failed = true });
    http.expectOne(`${base}/permissions`).flush({ items: [permission] });
    http.expectNone(`${base}/roles/r/permissions`);
    expect(failed).toBe(true);
  });
  it('does not fabricate roles when the server returns none', () => {
    let count = -1;
    service.getRoles().subscribe(roles => count = roles.length);
    http.expectOne(`${base}/roles`).flush({ items: [] });
    http.expectOne(`${base}/permissions`).flush({ items: [] });
    expect(count).toBe(0);
  });
  it('loads the protected admin directory including inactive accounts and scoped roles', () => {
    service.getUsers().subscribe(users => {
      expect(users[0].isActive).toBe(false);
      expect(users[0].roles?.[0].scopeId).toBe('project');
    });
    http.expectOne(`${base}/admin/users`).flush([{ id: 'u', isActive: false,
      roles: [{ roleId: 'r', roleName: 'Viewer', scopeType: 'Project', scopeId: 'project' }] }]);
  });
});
