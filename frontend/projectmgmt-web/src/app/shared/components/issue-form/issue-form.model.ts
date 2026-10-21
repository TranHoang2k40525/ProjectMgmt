import { IssueCoreDto, UpdateIssueRequest } from '../../../core/api/issue-api.models';

export interface IssueFormValue {
  title: string;
  description: string;
  assigneeId: string;
  priorityId: string;
  storyPoints: number | null;
  estimateHours: number | null;
  dueDate: string;
  parentId: string;
}

export interface ParentOption {
  id: string;
  label: string;
}

/** Cùng ngưỡng với IssueDtoValidators phía backend để báo lỗi trước khi gửi. */
export function validateIssueForm(value: IssueFormValue): string | null {
  const title = value.title.trim();
  if (!title) return 'Tiêu đề không được để trống.';
  if (title.length > 500) return 'Tiêu đề tối đa 500 ký tự.';
  if (value.description.length > 100_000) return 'Mô tả tối đa 100000 ký tự.';
  if (value.storyPoints !== null) {
    if (value.storyPoints < 0 || value.storyPoints > 9999.99) return 'Story points phải từ 0 đến 9999.99.';
    if (Math.round(value.storyPoints * 100) !== value.storyPoints * 100) return 'Story points tối đa 2 chữ số thập phân.';
  }
  if (value.estimateHours !== null && (value.estimateHours < 0 || value.estimateHours * 60 > 1_000_000)) return 'Ước lượng không hợp lệ.';
  if (value.dueDate && (value.dueDate < '2000-01-01' || value.dueDate > '2100-12-31')) return 'Hạn hoàn thành không hợp lệ.';
  return null;
}

export function toFormValue(issue: IssueCoreDto): IssueFormValue {
  return {
    title: issue.title,
    description: issue.description ?? '',
    assigneeId: issue.assigneeId ?? '',
    priorityId: issue.priorityId ?? '',
    storyPoints: issue.storyPoints ?? null,
    estimateHours: issue.originalEstimateMinutes == null ? null : Math.round(issue.originalEstimateMinutes / 6) / 10,
    dueDate: issue.dueDate ?? '',
    parentId: issue.parentId ?? ''
  };
}

export function toUpdateRequest(value: IssueFormValue): UpdateIssueRequest {
  return {
    title: value.title.trim(),
    description: value.description || null,
    assigneeId: value.assigneeId || null,
    priorityId: value.priorityId || null,
    storyPoints: value.storyPoints,
    originalEstimateMinutes: value.estimateHours === null ? null : Math.round(value.estimateHours * 60),
    dueDate: value.dueDate || null
  };
}
