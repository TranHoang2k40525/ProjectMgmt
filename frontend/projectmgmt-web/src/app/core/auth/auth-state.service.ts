import { Injectable, computed, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  readonly userId = signal<string | null>(null);
  readonly permissions = signal<ReadonlySet<string>>(new Set<string>());
  readonly isAuthenticated = computed(() => this.userId() !== null);

  setSession(userId: string, permissions: Iterable<string> = []): void {
    this.userId.set(userId);
    this.permissions.set(new Set(permissions));
  }

  clear(): void {
    this.userId.set(null);
    this.permissions.set(new Set<string>());
  }
}
