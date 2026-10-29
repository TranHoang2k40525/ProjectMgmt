import { ChangeDetectionStrategy, Component, OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { CollaborationApi } from '../../../core/api/collaboration.api';
import { CommentDto, extractMentions, toThreads } from '../../../core/api/collaboration-api.models';
import { apiErrorMessage } from '../../../core/http/api-error-message';
import { ConfirmDialogService } from '../../../core/services/confirm-dialog.service';
import { IdentityService } from '../../../core/services/identity.service';
import { ProjectManagementService } from '../../../core/services/project-management.service';
import { ISSUE_SECTION_STYLES } from './issue-section.styles';

/** Bình luận có trả lời một cấp; gõ "@Tên thành viên" để nhắc người đó. */
@Component({
  selector: 'app-issue-comments',
  imports: [FormsModule, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [ISSUE_SECTION_STYLES, `
    article { padding: .75rem 0; border-bottom: 1px solid #dce3ed; }
    .replies { margin-left: 1.5rem; border-left: 3px solid #dce3ed; padding-left: .75rem; }
    .meta { display: flex; gap: .5rem; align-items: baseline; flex-wrap: wrap; }
    .deleted { font-style: italic; color: #5e6c84; }
  `],
  template: `
    <h2>Bình luận <span class="muted">({{ count() }})</span></h2>
    @if (error()) { <p role="alert" class="error">{{ error() }}</p> }
    @if (canWrite()) {
      <label class="muted" for="new-comment">Viết bình luận — gõ &#64;Tên để nhắc thành viên</label>
      <textarea id="new-comment" [(ngModel)]="draft" rows="3" maxlength="20000"></textarea>
      <div class="row"><button type="button" (click)="post()" [disabled]="busy() || !draft.trim()">{{ busy() ? 'Đang gửi…' : 'Gửi bình luận' }}</button></div>
    }
    @if (loading()) { <p class="muted" role="status">Đang tải bình luận…</p> }
    @for (thread of threads(); track thread.comment.id) {
      <article>
        <div class="meta"><strong>{{ thread.comment.isDeleted ? 'Bình luận đã xóa' : thread.comment.authorName }}</strong>
          <span class="muted">{{ thread.comment.createdAt | date:'dd/MM/yyyy HH:mm' }}{{ thread.comment.isEdited ? ' · đã sửa' : '' }}</span></div>
        @if (editingId() === thread.comment.id) {
          <textarea [(ngModel)]="editDraft" rows="3" aria-label="Sửa bình luận"></textarea>
          <div class="row"><button type="button" (click)="saveEdit(thread.comment)" [disabled]="busy() || !editDraft.trim()">Lưu</button><button type="button" class="link" (click)="editingId.set(null)">Hủy</button></div>
        } @else {
          <p class="preserve" [class.deleted]="thread.comment.isDeleted">{{ thread.comment.isDeleted ? 'Nội dung đã bị xóa.' : thread.comment.content }}</p>
        }
        @if (!thread.comment.isDeleted) {
          <div class="row">
            @if (canWrite()) { <button type="button" class="link" (click)="startReply(thread.comment.id)">Trả lời</button> }
            @if (isMine(thread.comment)) { <button type="button" class="link" (click)="startEdit(thread.comment)">Sửa</button> }
            @if (isMine(thread.comment) || canModerate()) { <button type="button" class="danger" (click)="remove(thread.comment)">Xóa</button> }
          </div>
        }
        <div class="replies">
          @for (reply of thread.replies; track reply.id) {
            <article>
              <div class="meta"><strong>{{ reply.authorName }}</strong><span class="muted">{{ reply.createdAt | date:'dd/MM/yyyy HH:mm' }}{{ reply.isEdited ? ' · đã sửa' : '' }}</span></div>
              @if (editingId() === reply.id) {
                <textarea [(ngModel)]="editDraft" rows="2" aria-label="Sửa trả lời"></textarea>
                <div class="row"><button type="button" (click)="saveEdit(reply)" [disabled]="busy() || !editDraft.trim()">Lưu</button><button type="button" class="link" (click)="editingId.set(null)">Hủy</button></div>
              } @else {
                <p class="preserve">{{ reply.content }}</p>
              }
              <div class="row">
                @if (isMine(reply)) { <button type="button" class="link" (click)="startEdit(reply)">Sửa</button> }
                @if (isMine(reply) || canModerate()) { <button type="button" class="danger" (click)="remove(reply)">Xóa</button> }
              </div>
            </article>
          }
          @if (replyingTo() === thread.comment.id) {
            <textarea [(ngModel)]="replyDraft" rows="2" aria-label="Nội dung trả lời"></textarea>
            <div class="row"><button type="button" (click)="sendReply(thread.comment.id)" [disabled]="busy() || !replyDraft.trim()">Gửi trả lời</button><button type="button" class="link" (click)="replyingTo.set(null)">Hủy</button></div>
          }
        </div>
      </article>
    } @empty {
      @if (!loading()) { <p class="muted">Chưa có bình luận.</p> }
    }
  `
})
export class IssueCommentsComponent implements OnChanges {
  readonly issueId = input.required<string>();
  readonly canWrite = input(true);
  readonly canModerate = input(false);
  readonly changed = output<void>();

  private readonly api = inject(CollaborationApi);
  private readonly projects = inject(ProjectManagementService);
  private readonly identity = inject(IdentityService);
  private readonly confirm = inject(ConfirmDialogService);

  readonly comments = signal<CommentDto[]>([]);
  readonly threads = computed(() => toThreads(this.comments()));
  readonly count = computed(() => this.comments().filter(c => !c.isDeleted).length);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly editingId = signal<string | null>(null);
  readonly replyingTo = signal<string | null>(null);
  draft = '';
  editDraft = '';
  replyDraft = '';

  ngOnChanges(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.getComments(this.issueId()).subscribe({
      next: comments => { this.comments.set(comments); this.loading.set(false); },
      error: e => { this.loading.set(false); this.error.set(apiErrorMessage(e, 'Không tải được bình luận.')); }
    });
  }

  isMine(comment: CommentDto): boolean { return comment.userId === this.identity.authState().currentUser?.id; }

  post(): void {
    const content = this.draft;
    this.run(this.api.addComment(this.issueId(), { content, mentionedUserIds: this.mentions(content) }), () => this.draft = '');
  }

  startReply(commentId: string): void { this.replyingTo.set(commentId); this.replyDraft = ''; }

  sendReply(commentId: string): void {
    const content = this.replyDraft;
    this.run(this.api.reply(commentId, { content, mentionedUserIds: this.mentions(content) }), () => this.replyingTo.set(null));
  }

  startEdit(comment: CommentDto): void { this.editingId.set(comment.id); this.editDraft = comment.content; }

  saveEdit(comment: CommentDto): void { this.run(this.api.updateComment(comment.id, this.editDraft), () => this.editingId.set(null)); }

  remove(comment: CommentDto): void {
    this.confirm.confirm({
      title: 'Xóa bình luận', message: 'Bình luận sẽ bị xóa khỏi Issue. Tiếp tục?', type: 'danger', confirmText: 'Xóa',
      onConfirm: () => this.run(this.api.deleteComment(comment.id))
    });
  }

  private mentions(content: string): string[] { return extractMentions(content, this.projects.currentProject().members ?? []); }

  private run(request: Observable<unknown>, done?: () => void): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: () => { this.busy.set(false); done?.(); this.load(); this.changed.emit(); },
      error: e => { this.busy.set(false); this.error.set(apiErrorMessage(e, 'Không lưu được bình luận.')); }
    });
  }
}
