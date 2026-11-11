import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AttachmentDto, CommentDto, CreateCommentRequest, WatchStateDto, WatcherDto } from './collaboration-api.models';

/** Comment / Attachment / Watcher của module DeliveryIntelligence (C). */
@Injectable({ providedIn: 'root' })
export class CollaborationApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}`;

  getComments(issueId: string): Observable<CommentDto[]> {
    return this.http.get<CommentDto[]>(`${this.baseUrl}/issues/${issueId}/comments`);
  }

  addComment(issueId: string, request: CreateCommentRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/issues/${issueId}/comments`, request);
  }

  reply(commentId: string, request: CreateCommentRequest): Observable<CommentDto> {
    return this.http.post<CommentDto>(`${this.baseUrl}/comments/${commentId}/replies`, request);
  }

  updateComment(commentId: string, content: string): Observable<CommentDto> {
    return this.http.put<CommentDto>(`${this.baseUrl}/comments/${commentId}`, { content });
  }

  deleteComment(commentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/comments/${commentId}`);
  }

  getAttachments(issueId: string): Observable<AttachmentDto[]> {
    return this.http.get<AttachmentDto[]>(`${this.baseUrl}/issues/${issueId}/attachments`);
  }

  upload(issueId: string, file: File): Observable<unknown> {
    const body = new FormData();
    body.append('file', file);
    return this.http.post(`${this.baseUrl}/issues/${issueId}/attachments`, body);
  }

  download(attachmentId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/attachments/${attachmentId}/download`, { responseType: 'blob' });
  }

  deleteAttachment(attachmentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/attachments/${attachmentId}`);
  }

  getWatchers(issueId: string): Observable<WatcherDto[]> {
    return this.http.get<WatcherDto[]>(`${this.baseUrl}/issues/${issueId}/watchers`);
  }

  watch(issueId: string): Observable<WatchStateDto> {
    return this.http.post<WatchStateDto>(`${this.baseUrl}/issues/${issueId}/watchers/me`, {});
  }

  unwatch(issueId: string): Observable<WatchStateDto> {
    return this.http.delete<WatchStateDto>(`${this.baseUrl}/issues/${issueId}/watchers/me`);
  }
}
