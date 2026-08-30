import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TOKEN_STORE } from '../auth/token-store';

export const authGuard: CanActivateFn = () => {
  const tokenStore = inject(TOKEN_STORE);
  return tokenStore.getAccessToken() ? true : inject(Router).createUrlTree(['/auth']);
};
