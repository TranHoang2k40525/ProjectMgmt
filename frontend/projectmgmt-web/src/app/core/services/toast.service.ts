import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message?: string;
  duration?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  readonly toasts = signal<ToastMessage[]>([]);

  show(title: string, type: 'success' | 'error' | 'warning' | 'info' = 'success', message?: string, duration = 4000): void {
    const id = `toast-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`;
    const newToast: ToastMessage = { id, type, title, message, duration };
    this.toasts.update(list => [...list, newToast]);

    if (duration > 0) {
      setTimeout(() => {
        this.remove(id);
      }, duration);
    }
  }

  success(title: string, message?: string): void {
    this.show(title, 'success', message);
  }

  error(title: string, message?: string): void {
    this.show(title, 'error', message, 5000);
  }

  warning(title: string, message?: string): void {
    this.show(title, 'warning', message, 4500);
  }

  info(title: string, message?: string): void {
    this.show(title, 'info', message);
  }

  remove(id: string): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }
}
