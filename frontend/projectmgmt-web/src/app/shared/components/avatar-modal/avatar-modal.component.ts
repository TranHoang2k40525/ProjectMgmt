import { Component, ElementRef, EventEmitter, Output, ViewChild, inject, signal, computed, model, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IdentityService } from '../../../core/services/identity.service';
import { ToastService } from '../../../core/services/toast.service';
import { resolveAvatarUrl } from '../../../core/utils/avatar.utils';

export interface AvatarUserData {
  id?: string;
  displayName: string;
  email?: string;
  avatarUrl?: string | null;
  roleName?: string;
}

@Component({
  selector: 'app-avatar-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (isOpen()) {
      <div 
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-fade-in"
        (click)="onBackdropClick($event)"
      >
        <div 
          class="avatar-modal relative w-full max-w-sm bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 p-6 flex flex-col items-center text-center gap-5 overflow-hidden animate-scale-up"
          (click)="$event.stopPropagation()"
        >
          <!-- Hidden File Input for Avatar Upload -->
          @if (isCurrentUser()) {
            <input 
              #fileInput 
              type="file" 
              class="hidden" 
              accept="image/png,image/jpeg,image/webp" 
              (change)="onFileSelected($event)" 
            />
          }

          <!-- Close Button -->
          <button 
            type="button" 
            (click)="closeModal()" 
            class="absolute top-4 right-4 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 p-1.5 rounded-md hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
            title="Đóng"
          >
            <span class="material-symbols-outlined text-[20px]">close</span>
          </button>

          <!-- Modal Title -->
          <div class="flex items-center gap-2 pt-1 text-slate-800 dark:text-slate-100">
            <span class="material-symbols-outlined text-primary text-[22px]">account_circle</span>
            <h3 class="text-base font-bold">Ảnh đại diện</h3>
          </div>

          <!-- View 1: Full Image View Mode -->
          @if (isFullPreview()) {
            <div class="flex flex-col items-center gap-4 w-full animate-fade-in">
              <div class="w-full max-h-[50vh] flex items-center justify-center overflow-hidden rounded-xl bg-slate-950 p-2">
                <img 
                  [src]="resolvedUrl()" 
                  [alt]="effectiveName()" 
                  class="max-w-full max-h-[45vh] object-contain rounded-lg"
                  (error)="onImageError()"
                />
              </div>
              <button 
                type="button" 
                (click)="isFullPreview.set(false)" 
                class="px-4 py-2 rounded-md bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-200 flex items-center gap-1.5 cursor-pointer transition-colors"
              >
                <span class="material-symbols-outlined text-[16px]">arrow_back</span>
                <span>Quay lại</span>
              </button>
            </div>
          } @else {
            <!-- View 2: Avatar Thumbnail & Options -->
            <div class="relative group mt-1">
              <div class="w-36 h-36 rounded-full overflow-hidden border-3 border-white dark:border-slate-800 ring-3 ring-slate-200 dark:ring-slate-700 shadow-lg bg-slate-100 dark:bg-slate-800 flex items-center justify-center">
                @if (resolvedUrl() && !imageLoadFailed()) {
                  <img 
                    [src]="resolvedUrl()" 
                    [alt]="effectiveName()" 
                    class="w-full h-full object-cover"
                    (error)="imageLoadFailed.set(true)"
                  />
                } @else {
                  <div class="w-full h-full bg-slate-800 text-white flex items-center justify-center text-3xl font-bold tracking-wider">
                    {{ initials() }}
                  </div>
                }
              </div>
            </div>

            <!-- User Info Details -->
            <div class="flex flex-col gap-0.5 w-full">
              <h4 class="text-base font-bold text-slate-900 dark:text-white truncate">
                {{ effectiveName() }}
              </h4>
              @if (effectiveEmail()) {
                <p class="text-xs text-slate-500 dark:text-slate-400 truncate">
                  {{ effectiveEmail() }}
                </p>
              }
            </div>

            <!-- 3 Clear Action Options: Xem ảnh, Thay ảnh, Xóa avatar -->
            <div class="flex flex-col gap-2 w-full pt-3 border-t border-slate-100 dark:border-slate-800">
              <!-- Option 1: Xem ảnh -->
              @if (resolvedUrl() && !imageLoadFailed()) {
                <button
                  type="button"
                  (click)="viewFullImage()"
                  class="w-full py-2 px-3 rounded-md bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-750 text-slate-800 dark:text-slate-200 font-medium text-xs transition-colors flex items-center justify-center gap-2 cursor-pointer"
                >
                  <span class="material-symbols-outlined text-[18px]">visibility</span>
                  <span>Xem ảnh</span>
                </button>
              }

              <!-- Option 2: Thay ảnh -->
              @if (isCurrentUser()) {
                <button
                  type="button"
                  (click)="triggerFileInput()"
                  [disabled]="uploading() || deleting()"
                  class="w-full py-2 px-3 rounded-md bg-slate-900 hover:bg-slate-800 text-white font-medium text-xs shadow-xs transition-colors flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
                >
                  @if (uploading()) {
                    <span class="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
                    <span>Đang cập nhật...</span>
                  } @else {
                    <span class="material-symbols-outlined text-[18px]">photo_camera</span>
                    <span>Thay ảnh</span>
                  }
                </button>
              }

              <!-- Option 3: Xóa avatar -->
              @if (isCurrentUser() && resolvedUrl() && !imageLoadFailed()) {
                <button
                  type="button"
                  (click)="deleteAvatar()"
                  [disabled]="uploading() || deleting()"
                  class="w-full py-2 px-3 rounded-md border border-rose-200 dark:border-rose-900/50 hover:bg-rose-50 dark:hover:bg-rose-950/30 text-rose-600 dark:text-rose-400 font-medium text-xs transition-colors flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
                >
                  @if (deleting()) {
                    <span class="w-4 h-4 border-2 border-rose-600 border-t-transparent rounded-full animate-spin"></span>
                    <span>Đang xóa...</span>
                  } @else {
                    <span class="material-symbols-outlined text-[18px]">delete</span>
                    <span>Xóa avatar</span>
                  }
                </button>
              }

              <button
                type="button"
                (click)="closeModal()"
                class="w-full py-1.5 px-3 rounded-md text-slate-500 hover:bg-slate-50 dark:hover:bg-slate-800/60 font-medium text-xs transition-colors cursor-pointer mt-1"
              >
                Đóng
              </button>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: `
    @keyframes scaleUp {
      from { opacity: 0; transform: scale(0.95); }
      to { opacity: 1; transform: scale(1); }
    }
    .animate-scale-up {
      animation: scaleUp 0.15s cubic-bezier(0.16, 1, 0.3, 1) forwards;
    }
  `
})
export class AvatarModalComponent {
  private readonly identityService = inject(IdentityService);
  private readonly toast = inject(ToastService, { optional: true });

  readonly isOpen = model<boolean>(false);
  readonly user = input<AvatarUserData | null>(null);
  readonly avatarUrl = input<string | null | undefined>(null);
  readonly displayName = input<string>('');
  readonly email = input<string>('');
  readonly roleName = input<string>('');
  readonly isCurrentUser = input<boolean>(false);

  @Output() closed = new EventEmitter<void>();
  @Output() avatarUpdated = new EventEmitter<string>();
  @Output() avatarChanged = new EventEmitter<string>();

  @ViewChild('fileInput') private fileInputRef?: ElementRef<HTMLInputElement>;

  readonly uploading = signal(false);
  readonly deleting = signal(false);
  readonly isFullPreview = signal(false);
  readonly imageLoadFailed = signal(false);
  readonly localAvatarUrl = signal<string | null | undefined>(undefined);

  triggerFileInput(): void {
    this.fileInputRef?.nativeElement?.click();
  }

  readonly resolvedUrl = computed(() => {
    const local = this.localAvatarUrl();
    const raw = local !== undefined ? local : (this.user()?.avatarUrl ?? this.avatarUrl());
    return resolveAvatarUrl(raw);
  });

  readonly effectiveName = computed(() => {
    return this.user()?.displayName || this.displayName() || 'Người dùng';
  });

  readonly effectiveEmail = computed(() => {
    return this.user()?.email || this.email() || '';
  });

  readonly initials = computed(() => {
    const name = this.effectiveName();
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  });

  closeModal(): void {
    this.isFullPreview.set(false);
    this.imageLoadFailed.set(false);
    this.localAvatarUrl.set(undefined);
    this.isOpen.set(false);
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.closeModal();
    }
  }

  viewFullImage(): void {
    if (this.resolvedUrl()) {
      this.isFullPreview.set(true);
    }
  }

  onImageError(): void {
    this.imageLoadFailed.set(true);
    this.isFullPreview.set(false);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      this.toast?.error('Lỗi định dạng', 'Chỉ chấp nhận ảnh định dạng JPG, PNG hoặc WEBP.');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      this.toast?.error('Kích thước quá lớn', 'Ảnh đại diện tối đa là 5MB.');
      return;
    }

    this.uploading.set(true);
    this.identityService.uploadAvatar(file).subscribe({
      next: (newAvatarUrl: string) => {
        this.uploading.set(false);
        this.localAvatarUrl.set(newAvatarUrl);
        this.imageLoadFailed.set(false);
        this.avatarUpdated.emit(newAvatarUrl);
        this.avatarChanged.emit(newAvatarUrl);
        this.toast?.success('Thành công', 'Đã cập nhật ảnh đại diện mới.');
        input.value = '';
      },
      error: () => {
        this.uploading.set(false);
        this.toast?.error('Lỗi tải ảnh', 'Không thể tải ảnh đại diện lên. Vui lòng thử lại sau.');
        input.value = '';
      }
    });
  }

  deleteAvatar(): void {
    if (!confirm('Bạn có chắc chắn muốn xóa ảnh đại diện này không?')) {
      return;
    }

    this.deleting.set(true);
    this.identityService.deleteAvatar().subscribe({
      next: () => {
        this.deleting.set(false);
        this.localAvatarUrl.set(null);
        this.avatarUpdated.emit('');
        this.avatarChanged.emit('');
        this.toast?.success('Thành công', 'Đã xóa ảnh đại diện.');
      },
      error: () => {
        this.deleting.set(false);
        this.toast?.error('Lỗi', 'Không thể xóa ảnh đại diện.');
      }
    });
  }
}
