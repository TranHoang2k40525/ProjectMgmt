import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TOKEN_STORE } from '../auth/token-store';

/** Chưa đăng nhập thì sang /auth kèm returnUrl để quay lại đúng trang (vd link mời /invite/:token). */
export const authGuard: CanActivateFn = (_route, state) => {
  const tokenStore = inject(TOKEN_STORE);
  if (tokenStore.getAccessToken()) return true;
  const returnUrl = safeReturnUrl(state.url) && state.url !== '/' ? state.url : null;
  return inject(Router).createUrlTree(['/auth'], returnUrl ? { queryParams: { returnUrl } } : {});
};

/** Chỉ nhận đường dẫn nội bộ (không phải //host hay /\host) để tránh chuyển hướng ra trang ngoài. */
export function safeReturnUrl(value: string | null | undefined): string | null {
  if (!value || !/^\/(?![/\\])/.test(value) || value.startsWith('/auth')) return null;
  return value;
}
