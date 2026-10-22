import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ISSUE_PRIORITIES, PagedResult } from './issue-api.models';

export interface ActivityDto {
  id: string;
  action: string;
  fieldName?: string | null;
  oldValue?: string | null;
  newValue?: string | null;
  actorId?: string | null;
  actorName: string;
  source: 'User' | 'System' | 'AI' | string;
  createdAt: string;
}

export interface StatusChangeDto {
  id: string;
  fromStatusName?: string | null;
  toStatusName: string;
  fromCategory?: string | null;
  toCategory: string;
  changedByName: string;
  durationSeconds?: number | null;
  changedAt: string;
}

export interface AssignmentChangeDto {
  id: string;
  fromAssigneeName?: string | null;
  toAssigneeName?: string | null;
  assignedByName: string;
  assignmentSource: 'Manual' | 'AiSuggested' | 'AiAuto' | 'SelfPick' | string;
  storyPointsAtTime?: number | null;
  reason?: string | null;
  assignedAt: string;
}

const FIELD_LABELS: Record<string, string> = {
  Title: 'tiêu đề', Description: 'mô tả', PriorityId: 'độ ưu tiên', AssigneeId: 'người xử lý', StoryPoints: 'story points',
  OriginalEstimateMinutes: 'ước lượng (phút)', DueDate: 'hạn hoàn thành', ParentId: 'công việc cha', RankOrder: 'thứ tự',
  SprintId: 'Sprint', StatusId: 'trạng thái', Label: 'nhãn'
};

const ACTION_LABELS: Record<string, string> = {
  Created: 'đã tạo công việc', Deleted: 'đã xóa công việc', CommentAdded: 'đã bình luận', CommentDeleted: 'đã xóa một bình luận',
  AttachmentAdded: 'đã đính kèm', AttachmentRemoved: 'đã gỡ tệp', LabelAdded: 'đã gắn nhãn', LabelRemoved: 'đã gỡ nhãn',
  LinkAdded: 'đã thêm liên kết', LinkRemoved: 'đã gỡ liên kết', Ranked: 'đã sắp xếp lại thứ tự', SprintChanged: 'đã chuyển Sprint',
  ParentChanged: 'đã đổi công việc cha'
};

export const ASSIGNMENT_SOURCE_LABELS: Record<string, string> = {
  Manual: 'giao thủ công', AiSuggested: 'theo gợi ý AI', AiAuto: 'AI tự giao', SelfPick: 'tự nhận'
};

/** Câu hiển thị cho một dòng ActivityLog, ví dụ "đổi trạng thái To Do → In Progress". */
export function describeActivity(activity: ActivityDto): string {
  const field = activity.fieldName ? FIELD_LABELS[activity.fieldName] ?? activity.fieldName : null;
  const display = (value?: string | null) => {
    if (!value) return 'trống';
    if (activity.fieldName === 'PriorityId') return ISSUE_PRIORITIES.find(p => p.id === value)?.name ?? value;
    return value.length > 80 ? value.slice(0, 77) + '…' : value;
  };
  if (activity.action === 'StatusChanged') return `đổi trạng thái ${display(activity.oldValue)} → ${display(activity.newValue)}`;
  if (activity.action === 'Updated' && field) {
    if (activity.fieldName === 'Description') return 'đã cập nhật mô tả';
    return `đổi ${field}: ${display(activity.oldValue)} → ${display(activity.newValue)}`;
  }
  const base = ACTION_LABELS[activity.action] ?? activity.action;
  const detail = activity.newValue ?? activity.oldValue;
  return detail && ['AttachmentAdded', 'AttachmentRemoved', 'LabelAdded', 'LabelRemoved'].includes(activity.action) ? `${base} "${detail}"` : base;
}

export function formatDuration(seconds?: number | null): string {
  if (seconds == null) return '';
  if (seconds < 60) return `${seconds} giây`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} phút`;
  const hours = Math.floor(minutes / 60);
  if (hours < 48) return `${hours} giờ ${minutes % 60} phút`;
  return `${Math.floor(hours / 24)} ngày ${hours % 24} giờ`;
}

/** Lịch sử Issue của module DeliveryIntelligence (C). */
@Injectable({ providedIn: 'root' })
export class HistoryApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1/issues`;

  activity(issueId: string, page = 1, pageSize = 20): Observable<PagedResult<ActivityDto>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<ActivityDto>>(`${this.baseUrl}/${issueId}/history`, { params });
  }

  statuses(issueId: string): Observable<StatusChangeDto[]> {
    return this.http.get<StatusChangeDto[]>(`${this.baseUrl}/${issueId}/status-history`);
  }

  assignments(issueId: string): Observable<AssignmentChangeDto[]> {
    return this.http.get<AssignmentChangeDto[]>(`${this.baseUrl}/${issueId}/assignment-history`);
  }
}
