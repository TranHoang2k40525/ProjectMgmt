import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message?: string;
  duration?: number;
  actionUrl?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  readonly toasts = signal<ToastMessage[]>([]);

  show(title: string, type: 'success' | 'error' | 'warning' | 'info' = 'success', message?: string, duration = 4000, actionUrl?: string): void {
    const id = `toast-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`;
    const newToast: ToastMessage = { id, type, title, message, duration, actionUrl };
    this.toasts.update(list => [...list, newToast]);

    if (duration > 0) {
      setTimeout(() => {
        this.remove(id);
      }, duration);
    }
  }

  success(title: string, message?: string, actionUrl?: string): void {
    this.show(title, 'success', message, 4000, actionUrl);
  }

  error(title: string, message?: string, actionUrl?: string): void {
    this.show(title, 'error', message, 5000, actionUrl);
  }

  warning(title: string, message?: string, actionUrl?: string): void {
    this.show(title, 'warning', message, 4500, actionUrl);
  }

  info(title: string, message?: string, actionUrl?: string): void {
    this.show(title, 'info', message, 4000, actionUrl);
  }

  remove(id: string): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }
}
