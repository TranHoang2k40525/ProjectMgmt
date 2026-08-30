import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStateService } from '../auth/auth-state.service';

export const permissionGuard: CanActivateFn = route => {
  const requiredPermission = route.data['permission'];
  if (typeof requiredPermission !== 'string') {
    return true;
  }

  return inject(AuthStateService).permissions().has(requiredPermission)
    ? true
    : inject(Router).createUrlTree(['/projects'], { queryParams: { forbidden: requiredPermission } });
};
