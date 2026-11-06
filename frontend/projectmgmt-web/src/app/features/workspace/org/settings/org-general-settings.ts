import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { WorkspaceContextService } from '../../workspace-context.service';
import { WorkspaceDataSource } from '../../data/workspace-data-source';
import { ToastService } from '../../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../../core/services/confirm-dialog.service';
import { workspaceErrorMessage } from '../../workspace-error';
import { UI } from '../../components/ui-classes';

@Component({
  selector: 'app-org-general-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-6">
      <!-- Header -->
      <section class="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 shadow-xs">
        <h2 class="text-lg font-bold text-slate-900 dark:text-white mb-1">Cài đặt chung của Tổ chức</h2>
        <p class="text-xs text-slate-500">Quản lý định danh cơ bản và quyền hạn vận hành của tổ chức.</p>

        @if (org(); as current) {
          <div class="mt-5 grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-bold text-slate-700 dark:text-slate-300">Tên tổ chức</label>
              <input
                type="text"
                readonly
                [value]="current.name"
                class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs text-slate-800 dark:text-slate-200 select-all cursor-default"
              />
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-bold text-slate-700 dark:text-slate-300">Đường dẫn định danh (Slug)</label>
              <input
                type="text"
                readonly
                [value]="current.slug || 'Chưa thiết lập'"
                class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs text-slate-800 dark:text-slate-200 select-all cursor-default font-mono"
              />
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-bold text-slate-700 dark:text-slate-300">Chủ sở hữu tổ chức</label>
              <input
                type="text"
                readonly
                [value]="current.ownerName"
                class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs text-slate-800 dark:text-slate-200 select-all cursor-default"
              />
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-bold text-slate-700 dark:text-slate-300">Quy mô hoạt động</label>
              <input
                type="text"
                readonly
                [value]="current.projectCount + ' dự án · ' + current.memberCount + ' thành viên'"
                class="h-9 px-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs text-slate-800 dark:text-slate-200 select-all cursor-default"
              />
            </div>
          </div>
        }
      </section>

      <!-- Danger Zone: Delete Organization -->
      <section class="rounded-2xl border border-rose-200 dark:border-rose-900/60 bg-rose-50/50 dark:bg-rose-950/20 p-6 shadow-xs">
        <div class="flex items-center gap-2.5 text-rose-700 dark:text-rose-400 mb-2">
          <span class="material-symbols-outlined text-[22px]">warning</span>
          <h2 class="text-base font-bold">Vùng nguy hiểm: Xóa tổ chức</h2>
        </div>
        <p class="text-xs text-rose-800 dark:text-rose-300 leading-relaxed max-w-2xl">
          Khi xóa tổ chức, toàn bộ các dự án, bảng công việc, tài liệu Spec Hub, mẫu quy trình và liên kết thành viên thuộc tổ chức này sẽ bị xóa hoàn toàn khỏi hệ thống. Thao tác này <strong>không thể hoàn tác</strong>.
        </p>

        <div class="mt-5 pt-4 border-t border-rose-200/80 dark:border-rose-900/60 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div class="flex flex-col text-xs text-slate-600 dark:text-slate-400">
            <span class="font-semibold text-slate-800 dark:text-slate-200">Quyền xóa tổ chức:</span>
            <span>Chỉ Chủ tổ chức (Owner) hoặc Quản trị viên hệ thống mới có quyền thực hiện.</span>
          </div>

          <button
            type="button"
            (click)="onDeleteOrganization()"
            [disabled]="isDeleting()"
            class="h-9 px-4 rounded-xl bg-rose-600 hover:bg-rose-700 disabled:opacity-50 text-white text-xs font-bold transition-all shadow-xs flex items-center justify-center gap-2 cursor-pointer shrink-0"
          >
            <span class="material-symbols-outlined text-[18px]">delete_forever</span>
            <span>{{ isDeleting() ? 'Đang xóa...' : 'Xóa vĩnh viễn tổ chức' }}</span>
          </button>
        </div>
      </section>
    </div>
  `
})
export class OrgGeneralSettingsComponent {
  private readonly context = inject(WorkspaceContextService);
  private readonly data = inject(WorkspaceDataSource);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmDialogService);
  private readonly router = inject(Router);

  readonly org = this.context.currentOrg;
  readonly isDeleting = signal(false);

  onDeleteOrganization(): void {
    const current = this.org();
    if (!current) return;

    this.confirm.confirm({
      title: `Xóa tổ chức "${current.name}"`,
      message: `Bạn có chắc chắn muốn xóa vĩnh viễn tổ chức "${current.name}" cùng tất cả dự án, công việc và tài liệu? Hành động này sẽ được ghi log bảo mật và không thể phục hồi.`,
      confirmText: 'Tôi hiểu và muốn xóa',
      cancelText: 'Hủy thao tác',
      type: 'danger',
      onConfirm: () => {
        this.isDeleting.set(true);
        this.data.deleteOrganization(current.id).subscribe({
          next: () => {
            this.isDeleting.set(false);
            this.toast.success('Đã xóa tổ chức', `Tổ chức "${current.name}" đã được xóa thành công.`);
            this.context.currentOrg.set(null);
            void this.router.navigate(['/for-you']);
          },
          error: error => {
            this.isDeleting.set(false);
            this.toast.error('Không thể xóa tổ chức', workspaceErrorMessage(error, 'Bạn không có quyền hoặc tổ chức không thể xóa.'));
          }
        });
      }
    });
  }
}

