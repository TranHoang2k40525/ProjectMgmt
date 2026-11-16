import { ChangeDetectionStrategy, Component, OnChanges, inject, input, output, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Observable } from 'rxjs';
import { AttachmentDto } from '../../../core/api/collaboration-api.models';
import { CollaborationApi } from '../../../core/api/collaboration.api';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { ISSUE_SECTION_STYLES } from './issue-section.styles';

export const ATTACHMENT_ACCEPT = '.pdf,.png,.jpg,.jpeg,.txt,.csv,.docx,.xlsx,.zip';
export const ATTACHMENT_MAX_BYTES = 10 * 1024 * 1024;

@Component({
  selector: 'app-issue-attachments',
  imports: [DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [ISSUE_SECTION_STYLES, `li { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; padding: .4rem 0; border-bottom: 1px solid #eef1f5; } ul { list-style: none; padding: 0; }`],
  template: `
    <h2>Tệp đính kèm <span class="muted">({{ attachments().length }})</span></h2>
    <p class="muted">PDF, ảnh, TXT, CSV, DOCX, XLSX, ZIP · tối đa 10 MB.</p>
    @if (canWrite()) {
      <input type="file" aria-label="Tải tệp đính kèm" [accept]="accept" (change)="upload($event)" [disabled]="busy()" />
    }
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
    @if (busy()) { <p class="muted" role="status">Đang xử lý…</p> }
    <ul>
      @for (file of attachments(); track file.id) {
        <li>
          <strong>{{ file.fileName }}</strong>
          <span class="muted">{{ file.fileSizeBytes / 1024 | number:'1.0-1' }} KB · {{ file.uploadedByName || 'Không rõ' }} · {{ file.createdAt | date:'dd/MM/yyyy HH:mm' }}</span>
          <button type="button" class="link" (click)="download(file)">Tải xuống</button>
          @if (canWrite()) { <button type="button" class="danger" (click)="remove(file)" [disabled]="busy()">Xóa</button> }
        </li>
      } @empty { <li class="muted">Chưa có tệp đính kèm.</li> }
    </ul>
  `
})
export class IssueAttachmentsComponent implements OnChanges {
  readonly issueId = input.required<string>();
  readonly canWrite = input(true);
  readonly changed = output<void>();

  private readonly api = inject(CollaborationApi);
  private readonly confirm = inject(ConfirmDialogService);
  readonly accept = ATTACHMENT_ACCEPT;
  readonly attachments = signal<AttachmentDto[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.api.getAttachments(this.issueId()).subscribe({
      next: files => this.attachments.set(files),
      error: e => this.error.set(apiErrorMessage(e, 'Không tải được tệp đính kèm.'))
    });
  }

  upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (file.size === 0 || file.size > ATTACHMENT_MAX_BYTES || !ATTACHMENT_ACCEPT.split(',').includes(extension)) {
      this.error.set('Chọn PDF, ảnh, TXT, CSV, DOCX, XLSX hoặc ZIP tối đa 10 MB.');
      input.value = '';
      return;
    }
    this.run(this.api.upload(this.issueId(), file), () => input.value = '');
  }

  download(file: AttachmentDto): void {
    this.api.download(file.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = file.fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      },
      error: e => this.error.set(apiErrorMessage(e, 'Không tải xuống được tệp.'))
    });
  }

  remove(file: AttachmentDto): void {
    this.confirm.confirm({
      title: 'Xóa tệp đính kèm', message: `Xóa "${file.fileName}" khỏi Issue?`, type: 'danger', confirmText: 'Xóa',
      onConfirm: () => this.run(this.api.deleteAttachment(file.id))
    });
  }

  private run(request: Observable<unknown>, done?: () => void): void {
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: () => { this.busy.set(false); done?.(); this.load(); this.changed.emit(); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e, 'Không lưu được tệp đính kèm.')); }
    });
  }
}
