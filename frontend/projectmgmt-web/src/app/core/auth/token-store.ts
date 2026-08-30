import { Injectable, InjectionToken } from '@angular/core';

export interface TokenStore {
  getAccessToken(): string | null;
  setAccessToken(token: string): void;
  clear(): void;
}

export const TOKEN_STORE = new InjectionToken<TokenStore>('TOKEN_STORE');

@Injectable()
export class BrowserSessionTokenStore implements TokenStore {
  private readonly key = 'projectmgmt.access-token';
  private inMemoryToken: string | null = null;

  getAccessToken(): string | null {
    if (typeof sessionStorage === 'undefined') {
      return this.inMemoryToken;
    }

    return sessionStorage.getItem(this.key);
  }

  setAccessToken(token: string): void {
    this.inMemoryToken = token;
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(this.key, token);
    }
  }

  clear(): void {
    this.inMemoryToken = null;
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.removeItem(this.key);
    }
  }
}
