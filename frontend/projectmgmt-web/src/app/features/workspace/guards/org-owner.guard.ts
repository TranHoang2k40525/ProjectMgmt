import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { WorkspaceContextService } from '../workspace-context.service';

export function findOrgId(route: ActivatedRouteSnapshot): string | null {
  for (const snapshot of [...route.pathFromRoot].reverse()) {
    const orgId = snapshot.paramMap.get('orgId');
    if (orgId) return orgId;
  }
  return null;
}

/** Cài đặt tổ chức chỉ dành cho người tạo tổ chức; người khác được đưa về tab Tổng quan. */
export const orgOwnerGuard: CanActivateFn = route => {
  const router = inject(Router);
  const context = inject(WorkspaceContextService);
  const toast = inject(ToastService);
  const orgId = findOrgId(route);
  if (!orgId) return router.createUrlTree(['/for-you']);

  const current = context.currentOrg();
  if (current?.id === orgId) {
    return current.ownerId === context.currentUserId() ? true : router.createUrlTree(['/orgs', orgId, 'overview']);
  }
  return context.openOrganization(orgId).pipe(
    map(org => org.ownerId === context.currentUserId() ? true : router.createUrlTree(['/orgs', orgId, 'overview'])),
    catchError(() => {
      toast.error('Không mở được tổ chức', 'Tổ chức không tồn tại hoặc bạn không còn là thành viên.');
      return of(router.createUrlTree(['/for-you']));
    })
  );
};
