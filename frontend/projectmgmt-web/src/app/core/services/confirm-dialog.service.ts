import { Injectable, signal } from '@angular/core';

export interface ConfirmDialogOptions {
  title: string;
  message: string;
  type?: 'danger' | 'warning' | 'info';
  confirmText?: string;
  cancelText?: string;
  onConfirm: () => void;
  onCancel?: () => void;
}

@Injectable({
  providedIn: 'root'
})
export class ConfirmDialogService {
  readonly state = signal<ConfirmDialogOptions | null>(null);

  confirm(options: ConfirmDialogOptions): void {
    this.state.set(options);
  }

  close(): void {
    const current = this.state();
    if (current?.onCancel) {
      current.onCancel();
    }
    this.state.set(null);
  }

  proceed(): void {
    const current = this.state();
    if (current) {
      current.onConfirm();
    }
    this.state.set(null);
  }
}
