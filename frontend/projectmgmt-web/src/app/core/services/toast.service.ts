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

  // Anti-spam / de-duplication cache: Key -> timestamp
  private readonly recentToasts = new Map<string, number>();
  private readonly antiSpamCooldownMs = 3000;

  /**
   * Tính toán thời gian hiển thị tự động từ 1.5s - 10s tùy theo độ dài nội dung thông báo.
   */
  private calculateDynamicDuration(title: string, message?: string): number {
    const totalChars = (title || '').length + (message || '').length;
    // Mỗi ký tự ~60ms đọc, tối thiểu 1.5s, tối đa 10s
    const calculated = totalChars * 65 + 1200;
    return Math.min(10000, Math.max(1500, calculated));
  }

  show(title: string, type: 'success' | 'error' | 'warning' | 'info' = 'success', message?: string, customDuration?: number, actionUrl?: string): void {
    const trimmedTitle = (title || '').trim();
    const trimmedMessage = (message || '').trim();
    const spamKey = `${type}:${trimmedTitle}:${trimmedMessage}`;
    const now = Date.now();

    // Anti-spam: nếu thông báo cùng nội dung vừa được hiển thị trong 3 giây qua, bỏ qua
    const lastSeen = this.recentToasts.get(spamKey);
    if (lastSeen && (now - lastSeen) < this.antiSpamCooldownMs) {
      return;
    }
    this.recentToasts.set(spamKey, now);

    // Dọn dẹp cache anti-spam
    if (this.recentToasts.size > 50) {
      for (const [key, timestamp] of this.recentToasts.entries()) {
        if (now - timestamp > 10000) {
          this.recentToasts.delete(key);
        }
      }
    }

    const duration = customDuration ?? this.calculateDynamicDuration(trimmedTitle, trimmedMessage);
    const id = `toast-${now}-${Math.random().toString(36).substring(2, 7)}`;
    const newToast: ToastMessage = { id, type, title: trimmedTitle, message: trimmedMessage || undefined, duration, actionUrl };

    // Giới hạn tối đa 3 thông báo cùng lúc để tránh tràn màn hình
    this.toasts.update(list => {
      const trimmedList = list.length >= 3 ? list.slice(list.length - 2) : list;
      return [...trimmedList, newToast];
    });

    if (duration > 0) {
      setTimeout(() => {
        this.remove(id);
      }, duration);
    }
  }

  success(title: string, message?: string, actionUrl?: string): void {
    this.show(title, 'success', message, undefined, actionUrl);
  }

  error(title: string, message?: string, actionUrl?: string): void {
    this.show(title, 'error', message, undefined, actionUrl);
  }

  warning(title: string, message?: string, actionUrl?: string): void {
    this.show(title, 'warning', message, undefined, actionUrl);
  }

  info(title: string, message?: string, actionUrl?: string): void {
    this.show(title, 'info', message, undefined, actionUrl);
  }

  remove(id: string): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }
}
