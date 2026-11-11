export interface CommentDto {
  id: string;
  issueId: string;
  userId: string;
  authorName: string;
  parentCommentId?: string | null;
  content: string;
  mentionedUserIds: string[];
  isEdited: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateCommentRequest {
  content: string;
  parentCommentId?: string | null;
  mentionedUserIds: string[];
}

export interface AttachmentDto {
  id: string;
  issueId: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedBy: string;
  uploadedByName: string;
  createdAt: string;
}

export interface WatcherDto {
  userId: string;
  displayName: string;
  createdAt: string;
}

export interface WatchStateDto {
  isWatching: boolean;
  watcherCount: number;
}

/** Thread hiển thị: bình luận gốc kèm các trả lời (backend luôn gắn reply vào gốc). */
export interface CommentThread {
  comment: CommentDto;
  replies: CommentDto[];
}

export function toThreads(comments: CommentDto[]): CommentThread[] {
  const roots = comments.filter(c => !c.parentCommentId);
  return roots.map(comment => ({
    comment,
    replies: comments.filter(reply => reply.parentCommentId === comment.id)
  }));
}

/** Lấy userId của thành viên được nhắc bằng "@Tên hiển thị" trong nội dung. */
export function extractMentions(content: string, members: { userId: string; displayName: string }[]): string[] {
  const text = content.toLocaleLowerCase('vi');
  return members
    .filter(member => member.displayName && text.includes('@' + member.displayName.toLocaleLowerCase('vi')))
    .map(member => member.userId)
    .filter((id, index, all) => all.indexOf(id) === index);
}
