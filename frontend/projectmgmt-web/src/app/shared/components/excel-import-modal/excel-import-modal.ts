import { Component, EventEmitter, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExcelDataService, ImportedTaskRow } from '../../../core/services/excel-data.service';
import { ProjectManagementService, WorkItem } from '../../../core/services/project-management.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-excel-import-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="responsive-modal-backdrop fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs animate-fade-in font-body text-slate-900 dark:text-slate-100">
      <div class="responsive-modal-panel w-full max-w-xl bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col">
        
        <!-- Header -->
        <div class="responsive-modal-header flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/50">
          <div class="flex items-center gap-2.5">
            <div class="w-8 h-8 rounded-lg bg-emerald-500/10 text-emerald-600 flex items-center justify-center">
              <span class="material-symbols-outlined text-[20px]">table_chart</span>
            </div>
            <div class="flex flex-col">
              <h3 class="text-sm font-bold text-slate-900 dark:text-white">Import Công Việc Hàng Loạt Từ Excel</h3>
              <p class="text-sm text-slate-500">Nạp nhanh danh sách công việc & Sprint không cần nhập tay</p>
            </div>
          </div>

          <button (click)="dismissed.emit()" class="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-slate-200 dark:hover:bg-slate-800 text-slate-500">
            <span class="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        <!-- Body -->
        <div class="responsive-modal-body p-6 flex flex-col gap-6">
          
          <!-- Download Template CTA -->
          <div class="template-download-row flex items-center justify-between p-3.5 rounded-xl bg-indigo-50/60 dark:bg-indigo-950/40 border border-indigo-100 dark:border-indigo-900/40">
            <div class="flex items-center gap-3">
              <span class="material-symbols-outlined text-indigo-600 dark:text-indigo-400">download</span>
              <div class="flex flex-col">
                <span class="text-sm font-semibold text-slate-900 dark:text-white">Tải file mẫu Excel chuẩn</span>
                <span class="text-sm text-slate-500">Định dạng file CSV/XLSX gồm các cột Tiêu đề, Loại, Điểm, Sprint</span>
              </div>
            </div>
            <button
              (click)="downloadTemplate()"
              class="px-3.5 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold shadow-xs transition-colors"
            >
              Tải mẫu Excel
            </button>
          </div>

          <!-- Drag and Drop Dropzone -->
          <div
            (dragover)="$event.preventDefault()"
            (drop)="onFileDrop($event)"
            class="flex flex-col items-center justify-center p-8 rounded-2xl border-2 border-dashed border-slate-300 dark:border-slate-700 bg-slate-50/50 dark:bg-slate-800/30 hover:bg-slate-100 dark:hover:bg-slate-800/60 transition-colors cursor-pointer text-center"
          >
            <input type="file" accept=".csv,.xlsx" #fileInput (change)="onFileSelected($event)" class="hidden" />
            
            <div class="w-12 h-12 rounded-full bg-primary/10 text-primary flex items-center justify-center mb-3">
              <span class="material-symbols-outlined text-[28px]">cloud_upload</span>
            </div>
            <p class="text-sm font-bold text-slate-800 dark:text-slate-200">Kéo và thả file Excel / CSV vào đây</p>
            <p class="text-sm text-slate-500 mt-1">hoặc click nút bên dưới để chọn file từ máy tính</p>
            
            <button
              (click)="fileInput.click()"
              class="mt-4 px-4 py-2 rounded-xl bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 text-sm font-semibold text-slate-800 dark:text-slate-200 transition-colors"
            >
              Chọn file từ máy
            </button>
          </div>

          <!-- Parsed Items Preview Table -->
          @if (parsedRows.length > 0) {
            <div class="flex flex-col gap-2">
              <span class="text-sm font-bold text-emerald-600 flex items-center gap-1">
                <span class="material-symbols-outlined text-[16px]">check_circle</span>
                Đã đọc thành công {{ parsedRows.length }} dòng công việc:
              </span>

              <div class="max-h-40 overflow-y-auto border border-slate-200 dark:border-slate-800 rounded-xl p-2 bg-slate-50 dark:bg-slate-900">
                @for (row of parsedRows; track row.title) {
                  <div class="import-preview-row flex items-center justify-between py-1.5 px-2 text-sm border-b border-slate-200/50 dark:border-slate-800 last:border-none">
                    <span class="font-medium text-slate-800 dark:text-slate-200 truncate max-w-xs">{{ row.title }}</span>
                    <div class="flex items-center gap-2 text-sm">
                      <span class="px-1.5 py-0.5 rounded bg-slate-200 dark:bg-slate-800 font-mono">{{ row.storyPoints }} pts</span>
                      <span class="px-1.5 py-0.5 rounded bg-indigo-100 text-indigo-700 font-semibold">{{ row.sprintName }}</span>
                    </div>
                  </div>
                }
              </div>
            </div>
          }

        </div>

        <!-- Footer -->
        <div class="responsive-modal-footer px-6 py-4 border-t border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/50 flex items-center justify-end gap-3">
          <button (click)="dismissed.emit()" class="px-4 py-2 rounded-xl text-sm font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-800 transition-colors">
            Hủy
          </button>
          <button
            [disabled]="parsedRows.length === 0"
            (click)="importTasks()"
            class="px-5 py-2 rounded-xl bg-primary hover:bg-primary-container text-white text-sm font-bold shadow-sm disabled:opacity-50 disabled:cursor-not-allowed transition-all"
          >
            Import {{ parsedRows.length }} công việc ngay
          </button>
        </div>

      </div>
    </div>
  `,
  styles: [`
    @media (max-width: 1279px) {
      .responsive-modal-panel { max-height: calc(100dvh - 2rem); }
      .responsive-modal-body { min-width: 0; overflow-y: auto; }
      .responsive-modal-panel button, .responsive-modal-panel input { min-height: 40px; }
    }

    @media (max-width: 767px) {
      .responsive-modal-backdrop { align-items: flex-end; padding: 0; }
      .responsive-modal-panel { max-height: calc(100dvh - .5rem); border-radius: 1rem 1rem 0 0; }
      .responsive-modal-header, .responsive-modal-body, .responsive-modal-footer { padding: 1rem; }
      .responsive-modal-header { align-items: flex-start; gap: .75rem; }
      .template-download-row, .import-preview-row { align-items: stretch; flex-direction: column; gap: .75rem; }
      .template-download-row button { width: 100%; }
      .import-preview-row > span { max-width: none; white-space: normal; }
      .responsive-modal-footer { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .responsive-modal-footer button { min-height: 44px; }
    }

    @media (max-width: 932px) and (orientation: landscape) and (max-height: 520px) {
      .responsive-modal-backdrop { align-items: stretch; padding: .5rem; }
      .responsive-modal-panel { max-width: 720px; max-height: calc(100dvh - 1rem); margin: auto; border-radius: 1rem; }
      .responsive-modal-header, .responsive-modal-footer { padding-block: .625rem; }
      .responsive-modal-body { padding-block: .75rem; }
    }
  `]
})
export class ExcelImportModalComponent {
  @Output() dismissed = new EventEmitter<void>();

  private readonly excelService = inject(ExcelDataService);
  private readonly projectService = inject(ProjectManagementService);
  private readonly toastService = inject(ToastService);

  parsedRows: ImportedTaskRow[] = [];

  downloadTemplate(): void {
    this.excelService.downloadTemplate();
    this.toastService.info('Tải Mẫu Excel', 'Đã tải xuống file template mẫu Excel');
  }

  onFileSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    if (target.files && target.files.length > 0) {
      this.readFile(target.files[0]);
    }
  }

  onFileDrop(event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
      this.readFile(event.dataTransfer.files[0]);
    }
  }

  private readFile(file: File): void {
    const reader = new FileReader();
    reader.onload = (e) => {
      const text = e.target?.result as string;
      if (text) {
        this.parsedRows = this.excelService.parseCsv(text);
        this.toastService.success('Đọc File Excel Thành Công', `Nạp được ${this.parsedRows.length} bản ghi công việc từ ${file.name}`);
      }
    };
    reader.readAsText(file);
  }

  importTasks(): void {
    for (const row of this.parsedRows) {
      this.projectService.addWorkItem({
        title: row.title,
        description: row.description,
        issueType: row.type || 'Task',
        priority: this.isPriority(row.priority) ? row.priority : 'Medium',
        storyPoints: row.storyPoints || 3,
        sprintName: row.sprintName || 'SCRUMAI Sprint 2',
        assigneeName: row.assigneeName || 'Trần Văn Hoàng'
      });
    }
    this.toastService.success('Import Hoàn Tất', `Đã thêm ${this.parsedRows.length} công việc vào dự án`);
    this.dismissed.emit();
  }

  private isPriority(value: string | undefined): value is WorkItem['priority'] {
    return value === 'Low' || value === 'Medium' || value === 'High' || value === 'Urgent';
  }
}
