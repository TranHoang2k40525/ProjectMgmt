/** DTO của module DeliveryIntelligence (C) — khớp IssueDetailDto/IssueListItemDto phía backend. */
export interface IssueCoreDto {
  id: string;
  projectId: string;
  issueNumber: number;
  issueKey?: string | null;
  issueTypeId: string;
  statusId: string;
  priorityId?: string | null;
  sprintId?: string | null;
  parentId?: string | null;
  epicId?: string | null;
  assigneeId?: string | null;
  reporterId: string;
  title: string;
  description?: string | null;
  storyPoints?: number | null;
  originalEstimateMinutes?: number | null;
  timeSpentMinutes: number;
  rankOrder: number;
  dueDate?: string | null;
  startedAt?: string | null;
  resolvedAt?: string | null;
  isAiGenerated: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface IssueListItemDto {
  id: string;
  projectId: string;
  issueNumber: number;
  title: string;
  issueTypeId: string;
  statusId: string;
  priorityId?: string | null;
  assigneeId?: string | null;
  sprintId?: string | null;
  parentId?: string | null;
  epicId?: string | null;
  storyPoints?: number | null;
  rankOrder: number;
  dueDate?: string | null;
  updatedAt?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface IssueListQuery {
  sprintId?: string;
  backlogOnly?: boolean;
  statusId?: string;
  assigneeId?: string;
  issueTypeId?: string;
  parentId?: string;
  epicId?: string;
  searchText?: string;
  page?: number;
  pageSize?: number;
}

export interface UpdateIssueRequest {
  title: string;
  description?: string | null;
  priorityId?: string | null;
  assigneeId?: string | null;
  storyPoints?: number | null;
  originalEstimateMinutes?: number | null;
  dueDate?: string | null;
}

export interface IssueRankDto {
  issueId: string;
  rankOrder: number;
  rebalanced: boolean;
}

/** Priority dùng chung do migration SeedPlanningReferenceData tạo (Id cố định). */
export const ISSUE_PRIORITIES: readonly { id: string; name: string }[] = [
  { id: '33333333-0000-0000-0000-000000000001', name: 'Highest' },
  { id: '33333333-0000-0000-0000-000000000002', name: 'High' },
  { id: '33333333-0000-0000-0000-000000000003', name: 'Medium' },
  { id: '33333333-0000-0000-0000-000000000004', name: 'Low' },
  { id: '33333333-0000-0000-0000-000000000005', name: 'Lowest' }
];
