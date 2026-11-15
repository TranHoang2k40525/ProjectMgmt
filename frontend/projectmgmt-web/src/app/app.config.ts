import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { TOKEN_STORE, BrowserSessionTokenStore } from './core/auth/token-store';
import { authInterceptor } from './core/http/auth.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';
import { routes } from './app.routes';
import { provideWorkspaceData } from './features/workspace/data/provide-workspace-data';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    { provide: TOKEN_STORE, useClass: BrowserSessionTokenStore },
    provideWorkspaceData()
  ]
};
