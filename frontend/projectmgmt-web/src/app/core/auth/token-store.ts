import { Injectable, InjectionToken } from '@angular/core';

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt?: string | null;
  refreshTokenExpiresAt?: string | null;
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface TokenStore {
  getAccessToken(): string | null;
  getRefreshToken(): string | null;
  getSession(): AuthSession | null;
  setSession(session: AuthSession): void;
  clear(): void;
}

export const TOKEN_STORE = new InjectionToken<TokenStore>('TOKEN_STORE');

@Injectable()
export class BrowserSessionTokenStore implements TokenStore {
  private readonly key = 'projectmgmt.auth-session';
  private inMemorySession: AuthSession | null = null;

  getAccessToken(): string | null {
    return this.getSession()?.accessToken ?? null;
  }

  getRefreshToken(): string | null {
    return this.getSession()?.refreshToken ?? null;
  }

  getSession(): AuthSession | null {
    if (typeof sessionStorage === 'undefined') {
      return this.inMemorySession;
    }

    const serialized = sessionStorage.getItem(this.key);
    if (!serialized) return null;

    try {
      const session = JSON.parse(serialized) as Partial<AuthSession>;
      return isValidSession(session) ? session : null;
    } catch {
      sessionStorage.removeItem(this.key);
      return null;
    }
  }

  setSession(session: AuthSession): void {
    this.inMemorySession = session;
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(this.key, JSON.stringify(session));
    }
  }

  clear(): void {
    this.inMemorySession = null;
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.removeItem(this.key);
    }
  }
}

function isValidSession(session: Partial<AuthSession>): session is AuthSession {
  return typeof session.accessToken === 'string'
    && session.accessToken.length > 0
    && typeof session.refreshToken === 'string'
    && session.refreshToken.length > 0
    && typeof session.userId === 'string'
    && session.userId.length > 0
    && typeof session.email === 'string'
    && typeof session.fullName === 'string'
    && Array.isArray(session.roles);
}
