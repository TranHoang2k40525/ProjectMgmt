import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, convertToParamMap, provideRouter } from '@angular/router';
import { Observable, firstValueFrom, isObservable, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ToastService } from '../../../core/services/toast.service';
import { WorkspaceContextService } from '../workspace-context.service';
import { orgOwnerGuard } from './org-owner.guard';

function routeFor(orgId: string): ActivatedRouteSnapshot {
  const parent = { paramMap: convertToParamMap({ orgId }) } as ActivatedRouteSnapshot;
  const child = { paramMap: convertToParamMap({}) } as ActivatedRouteSnapshot;
  return { pathFromRoot: [parent, child] } as unknown as ActivatedRouteSnapshot;
}

describe('orgOwnerGuard', () => {
  const currentOrg = signal<{ id: string; ownerId: string } | null>(null);
  const openOrganization = vi.fn();

  beforeEach(() => {
    currentOrg.set(null);
    openOrganization.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ToastService, useValue: { error: vi.fn() } },
        { provide: WorkspaceContextService, useValue: { currentOrg, currentUserId: signal('me'), openOrganization } }
      ]
    });
  });

  async function run(orgId: string): Promise<boolean | UrlTree> {
    const result = TestBed.runInInjectionContext(() => orgOwnerGuard(routeFor(orgId), {} as RouterStateSnapshot));
    return isObservable(result) ? firstValueFrom(result as Observable<boolean | UrlTree>) : result as boolean | UrlTree;
  }

  it('lets the owner in and sends other members to the overview', async () => {
    currentOrg.set({ id: 'org', ownerId: 'me' });
    expect(await run('org')).toBe(true);
    currentOrg.set({ id: 'org', ownerId: 'someone-else' });
    const tree = await run('org') as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(tree)).toBe('/orgs/org/overview');
  });

  it('loads the organization when it is not open yet and redirects home on failure', async () => {
    openOrganization.mockReturnValue(of({ id: 'org', ownerId: 'me' }));
    expect(await run('org')).toBe(true);
    openOrganization.mockReturnValue(throwError(() => new Error('404')));
    const tree = await run('gone') as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(tree)).toBe('/for-you');
  });
});
