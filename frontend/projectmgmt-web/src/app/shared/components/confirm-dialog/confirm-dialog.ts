import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (dialogService.state(); as dialog) {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in font-body text-slate-900 dark:text-slate-100">
        <!-- Backdrop close -->
        <div class="fixed inset-0" (click)="dialogService.close()"></div>

        <!-- Custom Popup Dialog Box -->
        <div class="relative z-10 w-full max-w-md bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col p-6 space-y-4 animate-scale-up">
          
          <div class="flex items-start gap-4">
            <div
              class="w-12 h-12 rounded-2xl flex items-center justify-center shrink-0 shadow-xs"
              [class.bg-red-100]="dialog.type === 'danger'"
              [class.text-red-600]="dialog.type === 'danger'"
              [class.bg-amber-100]="dialog.type === 'warning'"
              [class.text-amber-600]="dialog.type === 'warning'"
              [class.bg-primary/10]="!dialog.type || dialog.type === 'info'"
              [class.text-primary]="!dialog.type || dialog.type === 'info'"
            >
              <span class="material-symbols-outlined text-[28px]">
                {{ dialog.type === 'danger' ? 'warning' : dialog.type === 'warning' ? 'error_outline' : 'help_outline' }}
              </span>
            </div>

            <div class="flex flex-col gap-1 flex-1">
              <h3 class="text-lg font-bold text-slate-900 dark:text-white leading-tight">
                {{ dialog.title }}
              </h3>
              <p class="text-sm text-slate-600 dark:text-slate-300 leading-relaxed">
                {{ dialog.message }}
              </p>
            </div>
          </div>

          <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
            <button
              (click)="dialogService.close()"
              class="px-4 py-2 rounded-xl text-sm font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
            >
              {{ dialog.cancelText || 'Hủy bỏ' }}
            </button>
            <button
              (click)="dialogService.proceed()"
              class="px-5 py-2 rounded-xl text-sm font-bold text-white shadow-xs transition-all cursor-pointer"
              [class.bg-red-600]="dialog.type === 'danger'"
              [class.hover:bg-red-700]="dialog.type === 'danger'"
              [class.bg-amber-600]="dialog.type === 'warning'"
              [class.hover:bg-amber-700]="dialog.type === 'warning'"
              [class.bg-primary]="!dialog.type || dialog.type === 'info'"
              [class.hover:bg-primary-container]="!dialog.type || dialog.type === 'info'"
            >
              {{ dialog.confirmText || 'Xác nhận' }}
            </button>
          </div>

        </div>
      </div>
    }
  `
})
export class ConfirmDialogComponent {
  readonly dialogService = inject(ConfirmDialogService);
}
