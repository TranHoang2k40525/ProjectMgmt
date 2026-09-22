import { NotificationModel, UserProfileModel } from '../mocks/identity-mock-db';
import { Project, WorkItem } from '../services/project-management.service';

export interface FocusAction {
  id: string;
  kind: 'task' | 'route';
  taskId?: string;
  route?: string;
  icon: string;
  eyebrow: string;
  title: string;
  reason: string;
  score: number;
}

export interface FocusContext {
  user: UserProfileModel | null;
  project: Project;
  workItems: WorkItem[];
  notifications: NotificationModel[];
  lastTaskId: string | null;
  lastRoute: string | null;
}
