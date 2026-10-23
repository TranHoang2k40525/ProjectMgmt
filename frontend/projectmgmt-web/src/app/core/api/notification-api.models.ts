import { ApiResult } from './account-api.models';

export interface NotificationDto extends ApiResult {
  notificationId?: string | null;
  userId?: string | null;
  type?: string | null;
  title?: string | null;
  content?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  projectId?: string | null;
  actorId?: string | null;
  isRead?: boolean | null;
  readAt?: string | null;
  createdAt?: string | null;
  roleName?: string | null;
  sendEmail?: boolean;
  items?: NotificationDto[] | null;
  totalCount?: number | null;
  unreadCount?: number | null;
  page?: number | null;
  pageSize?: number | null;
}
