import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { CommentDto, extractMentions, toThreads } from './collaboration-api.models';
import { CollaborationApi } from './collaboration.api';

describe('CollaborationApi', () => {
  let api: CollaborationApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/v1`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(CollaborationApi);
    http = TestBed.inject(HttpTestingController);
  });

  it('gửi bình luận kèm người được nhắc và trả lời theo comment gốc', () => {
    api.addComment('i1', { content: 'hi @An', mentionedUserIds: ['u1'] }).subscribe();
    const add = http.expectOne(`${baseUrl}/issues/i1/comments`);
    expect(add.request.method).toBe('POST');
    expect(add.request.body).toEqual({ content: 'hi @An', mentionedUserIds: ['u1'] });
    add.flush({});

    api.reply('c1', { content: 'ok', mentionedUserIds: [] }).subscribe();
    expect(http.expectOne(`${baseUrl}/comments/c1/replies`).request.method).toBe('POST');
    http.verify();
  });

  it('tải tệp bằng multipart và tải xuống dạng blob', () => {
    api.upload('i1', new File(['x'], 'a.txt')).subscribe();
    const upload = http.expectOne(`${baseUrl}/issues/i1/attachments`);
    expect(upload.request.body instanceof FormData).toBe(true);
    upload.flush({});

    api.download('a1').subscribe();
    const download = http.expectOne(`${baseUrl}/attachments/a1/download`);
    expect(download.request.responseType).toBe('blob');
    download.flush(new Blob(['x']));
    http.verify();
  });

  it('theo dõi và bỏ theo dõi dùng watchers/me', () => {
    api.watch('i1').subscribe();
    expect(http.expectOne(`${baseUrl}/issues/i1/watchers/me`).request.method).toBe('POST');
    api.unwatch('i1').subscribe();
    expect(http.expectOne(`${baseUrl}/issues/i1/watchers/me`).request.method).toBe('DELETE');
  });
});

describe('collaboration helpers', () => {
  const comment = (id: string, parent: string | null = null): CommentDto => ({
    id, issueId: 'i1', userId: 'u', authorName: 'A', parentCommentId: parent, content: id,
    mentionedUserIds: [], isEdited: false, isDeleted: false, createdAt: ''
  });

  it('gom trả lời vào bình luận gốc', () => {
    const threads = toThreads([comment('a'), comment('b'), comment('r1', 'a'), comment('r2', 'a')]);
    expect(threads.map(t => [t.comment.id, t.replies.map(r => r.id)])).toEqual([['a', ['r1', 'r2']], ['b', []]]);
  });

  it('nhận diện @mention theo tên hiển thị, không phân biệt hoa thường, không trùng', () => {
    const members = [{ userId: 'u1', displayName: 'Trần An' }, { userId: 'u2', displayName: 'Bình' }];
    expect(extractMentions('Nhờ @trần an và @Trần An xem, cc @Khác', members)).toEqual(['u1']);
    expect(extractMentions('không nhắc ai', members)).toEqual([]);
  });
});
