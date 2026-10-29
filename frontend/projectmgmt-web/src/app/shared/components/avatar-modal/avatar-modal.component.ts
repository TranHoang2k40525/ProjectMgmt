import { Component, ElementRef, EventEmitter, Input, Output, ViewChild, inject, signal, computed } from '@angular/core';
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
    @if (isOpen) {
      <div 
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-fade-in"
        (click)="onBackdropClick($event)"
      >
        <div 
          class="relative w-full max-w-sm bg-white dark:bg-slate-900 rounded-3xl shadow-2xl border border-slate-200 dark:border-slate-800 p-6 flex flex-col items-center text-center gap-5 overflow-hidden animate-scale-up"
          (click)="$event.stopPropagation()"
        >
          <!-- Hidden File Input for Avatar Upload -->
          @if (isCurrentUser) {
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
            class="absolute top-4 right-4 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 p-1.5 rounded-full hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
            title="Đóng"
          >
            <span class="material-symbols-outlined text-[20px]">close</span>
          </button>

          <!-- Modal Title -->
          <div class="flex items-center gap-2 pt-1 text-slate-800 dark:text-slate-100">
            <span class="material-symbols-outlined text-primary text-[22px]">account_circle</span>
            <h3 class="text-base font-bold">Ảnh Đại Diện</h3>
          </div>

          <!-- Avatar Large Preview -->
          <div class="relative group mt-1">
            <div class="w-44 h-44 rounded-full overflow-hidden border-4 border-white dark:border-slate-800 ring-4 ring-primary/20 shadow-xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center">
              @if (resolvedUrl() && !imageLoadFailed()) {
                <img 
                  [src]="resolvedUrl()" 
                  [alt]="effectiveName()" 
                  class="w-full h-full object-cover transition-transform duration-300 group-hover:scale-105"
                  (error)="imageLoadFailed.set(true)"
                />
              } @else {
                <div class="w-full h-full bg-gradient-to-tr from-sky-600 to-primary text-white flex items-center justify-center text-4xl font-extrabold tracking-wider">
                  {{ initials() }}
                </div>
              }
            </div>

            <!-- Upload Badge overlay on hover if isCurrentUser -->
            @if (isCurrentUser) {
              <button
                type="button"
                (click)="triggerFileInput()"
                [disabled]="uploading()"
                class="absolute bottom-2 right-2 w-11 h-11 rounded-full bg-primary hover:bg-primary-container text-white shadow-lg flex items-center justify-center border-2 border-white dark:border-slate-900 transition-transform active:scale-95 cursor-pointer disabled:opacity-50"
                title="Thay đổi ảnh đại diện"
              >
                @if (uploading()) {
                  <span class="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
                } @else {
                  <span class="material-symbols-outlined text-[20px]">photo_camera</span>
                }
              </button>
            }
          </div>

          <!-- User Info Details -->
          <div class="flex flex-col gap-1 w-full">
            <h4 class="text-lg font-bold text-slate-900 dark:text-white truncate">
              {{ effectiveName() }}
            </h4>
            @if (effectiveRole()) {
              <div class="flex justify-center">
                <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-primary/10 text-primary border border-primary/20">
                  {{ effectiveRole() }}
                </span>
              </div>
            }
            @if (effectiveEmail()) {
              <p class="text-xs text-slate-500 dark:text-slate-400 truncate mt-0.5">
                {{ effectiveEmail() }}
              </p>
            }
          </div>

          <!-- Actions -->
          <div class="flex flex-col gap-2 w-full pt-2 border-t border-slate-100 dark:border-slate-800">
            @if (isCurrentUser) {
              <button
                type="button"
                (click)="triggerFileInput()"
                [disabled]="uploading()"
                class="w-full py-2.5 px-4 rounded-xl bg-primary hover:bg-primary-container text-white font-bold text-xs shadow-sm transition-all flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
              >
                @if (uploading()) {
                  <span class="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></span>
                  <span>Đang tải ảnh lên...</span>
                } @else {
                  <span class="material-symbols-outlined text-[18px]">upload</span>
                  <span>Tải ảnh mới từ máy tính</span>
                }
              </button>
            }

            <button
              type="button"
              (click)="closeModal()"
              class="w-full py-2 px-4 rounded-xl text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 font-semibold text-xs transition-colors cursor-pointer"
            >
              Đóng
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: `
    @keyframes scaleUp {
      from { opacity: 0; transform: scale(0.92); }
      to { opacity: 1; transform: scale(1); }
    }
    .animate-scale-up {
      animation: scaleUp 0.2s cubic-bezier(0.16, 1, 0.3, 1) forwards;
    }
  `
})
export class AvatarModalComponent {
  private readonly identityService = inject(IdentityService);
  private readonly toast = inject(ToastService, { optional: true });

  @Input() isOpen = false;
  @Input() user: AvatarUserData | null = null;
  @Input() avatarUrl?: string | null = null;
  @Input() displayName = '';
  @Input() email = '';
  @Input() roleName = '';
  @Input() isCurrentUser = false;

  @Output() isOpenChange = new EventEmitter<boolean>();
  @Output() closed = new EventEmitter<void>();
  @Output() avatarUpdated = new EventEmitter<string>();
  @Output() avatarChanged = new EventEmitter<string>();

  @ViewChild('fileInput') private fileInputRef?: ElementRef<HTMLInputElement>;

  triggerFileInput(): void {
    this.fileInputRef?.nativeElement?.click();
  }

  readonly uploading = signal(false);
  readonly imageLoadFailed = signal(false);

  readonly resolvedUrl = computed(() => {
    this.imageLoadFailed.set(false);
    const raw = this.user?.avatarUrl ?? this.avatarUrl;
    return resolveAvatarUrl(raw);
  });

  readonly effectiveName = computed(() => {
    return this.user?.displayName || this.displayName || 'Người dùng';
  });

  readonly effectiveEmail = computed(() => {
    return this.user?.email || this.email || '';
  });

  readonly effectiveRole = computed(() => {
    return this.user?.roleName || this.roleName || '';
  });

  readonly initials = computed(() => {
    const name = this.effectiveName();
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  });

  closeModal(): void {
    this.isOpen = false;
    this.isOpenChange.emit(false);
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.closeModal();
    }
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
        if (this.user) {
          this.user = { ...this.user, avatarUrl: newAvatarUrl };
        }
        this.avatarUpdated.emit(newAvatarUrl);
        this.avatarChanged.emit(newAvatarUrl);
        this.toast?.success('Thành công', 'Đã cập nhật ảnh đại diện mới.');
        input.value = ''; // Reset input
      },
      error: () => {
        this.uploading.set(false);
        this.toast?.error('Lỗi tải ảnh', 'Không thể tải ảnh đại diện lên. Vui lòng thử lại sau.');
        input.value = '';
      }
    });
  }
}
