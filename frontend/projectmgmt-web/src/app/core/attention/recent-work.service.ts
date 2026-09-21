import { DestroyRef, Injectable, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ProjectManagementService } from '../services/project-management.service';

const TASK_KEY = 'projectmgmt-last-task';
const ROUTE_KEY = 'projectmgmt-last-route';

@Injectable({ providedIn: 'root' })
export class RecentWorkService {
  private readonly router = inject(Router);
  private readonly project = inject(ProjectManagementService);
  private readonly destroyRef = inject(DestroyRef);

  readonly lastTaskId = signal<string | null>(this.read(TASK_KEY));
  readonly lastRoute = signal<string | null>(this.read(ROUTE_KEY));

  constructor() {
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(event => this.rememberRoute(event.urlAfterRedirects));

    effect(() => {
      const task = this.project.activeDrawerTask();
      if (task) this.rememberTask(task.id);
    });
  }

  rememberTask(taskId: string): void {
    this.lastTaskId.set(taskId);
    this.write(TASK_KEY, taskId);
  }

  private rememberRoute(route: string): void {
    if (route === '/for-you' || route.startsWith('/auth') || route.startsWith('/profile')) return;
    if (!route.startsWith('/') || route.startsWith('//')) return;
    this.lastRoute.set(route);
    this.write(ROUTE_KEY, route);
  }

  private read(key: string): string | null {
    try { return typeof localStorage === 'undefined' ? null : localStorage.getItem(key); }
    catch { return null; }
  }

  private write(key: string, value: string): void {
    try { if (typeof localStorage !== 'undefined') localStorage.setItem(key, value); }
    catch { /* Private browsing may disable storage; the signal still works for this session. */ }
  }
}
