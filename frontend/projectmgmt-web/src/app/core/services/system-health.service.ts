import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { catchError, of, take } from 'rxjs';
import { environment } from '../../../environments/environment';

export type BackendStatus = 'checking' | 'online' | 'offline';

@Injectable({ providedIn: 'root' })
export class SystemHealthService {
  private readonly http = inject(HttpClient);
  readonly status = signal<BackendStatus>('checking');

  check(): void {
    const host = environment.apiBaseUrl.replace(/\/api\/?$/, '');
    this.status.set('checking');
    this.http.get(`${host}/health/live`, { responseType: 'text' })
      .pipe(catchError(() => of(null)), take(1))
      .subscribe(result => this.status.set(result === null ? 'offline' : 'online'));
  }
}
