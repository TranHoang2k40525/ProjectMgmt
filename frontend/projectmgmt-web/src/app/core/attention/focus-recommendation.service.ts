import { Injectable, computed, inject } from '@angular/core';
import { IdentityService } from '../services/identity.service';
import { ProjectManagementService, WorkItem } from '../services/project-management.service';
import { FocusAction, FocusContext } from './focus-recommendation.model';
import { RecentWorkService } from './recent-work.service';

const PRIORITY_SCORE: Record<WorkItem['priority'], number> = {
  Urgent: 18, High: 10, Medium: 3, Low: 0
};

function normalizeName(value: string | undefined): string {
  return (value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
}

function taskAction(task: WorkItem, eyebrow: string, reason: string, score: number): FocusAction {
  return {
    id: `task:${task.id}`,
    kind: 'task',
    taskId: task.id,
    icon: task.issueType === 'Bug' ? 'bug_report' : task.statusName === 'Code Review' ? 'rate_review' : 'task_alt',
    eyebrow,
    title: task.title,
    reason,
    score: score + PRIORITY_SCORE[task.priority]
  };
}

export function computeFocusActions(context: FocusContext): FocusAction[] {
  const { user, project, workItems, notifications, lastTaskId, lastRoute } = context;
  const role = user?.roleName ?? '';
  const isAdmin = role.includes('Administrator');
  const isQa = role.includes('QA') || role.includes('Tester');
  const isDeveloper = role.includes('Developer');
  const isScrumMaster = role.includes('Scrum Master');
  const isProductOwner = role.includes('Product Owner');
  const isViewer = role.includes('Viewer');
  const candidates: FocusAction[] = [];
  const scopedItems = workItems.filter(item => item.projectId === project.id && item.statusName !== 'Done');
  const assigned = (item: WorkItem): boolean => !!user && (
    item.assigneeId === user.id || normalizeName(item.assigneeName) === normalizeName(user.displayName)
  );

  if (isAdmin) {
    for (const notification of notifications.filter(item => item.userId === user?.id && !item.isRead)) {
      const security = notification.category === 'Security';
      if (!security && !notification.actionUrl) continue;
      candidates.push({
        id: `notification:${notification.id}`,
        kind: 'route',
        route: notification.actionUrl?.startsWith('/') && !notification.actionUrl.startsWith('//')
          ? notification.actionUrl : '/notifications',
        icon: security ? 'shield' : 'notifications_active',
        eyebrow: security ? 'Cảnh báo tài khoản' : 'Thông báo mới',
        title: notification.title,
        reason: security ? 'Cần xem lại hoạt động bảo mật.' : notification.message,
        score: security ? 110 : 65
      });
    }
  }

  for (const task of scopedItems) {
    if (isViewer) continue;
    if (isDeveloper && assigned(task)) {
      const inProgress = task.statusName === 'In Progress';
      candidates.push(taskAction(task,
        inProgress ? 'Tiếp tục công việc' : 'Việc được giao',
        inProgress ? 'Bạn đang xử lý công việc này — tiếp tục từ lần trước.' : `${task.priority === 'Urgent' ? 'Khẩn cấp' : 'Được giao cho bạn'} và chưa hoàn tất.`,
        inProgress ? 88 : 68));
    } else if (isQa && (task.statusName === 'Code Review' || (task.issueType === 'Bug' && task.priority === 'Urgent'))) {
      candidates.push(taskAction(task, task.statusName === 'Code Review' ? 'Chờ kiểm tra' : 'Lỗi cần xác minh',
        task.statusName === 'Code Review' ? 'Công việc đang chờ review; vai trò QA của bạn có thể xử lý.' : 'Lỗi khẩn cấp cần được kiểm tra.',
        task.statusName === 'Code Review' ? 86 : 90));
    } else if ((isScrumMaster || isAdmin) && task.statusName === 'Code Review') {
      candidates.push(taskAction(task, 'Theo dõi điểm chờ', 'Công việc đang ở bước review; kiểm tra để giữ luồng xử lý.', 68));
    } else if (isProductOwner && task.statusName === 'To Do' && task.priority !== 'Low') {
      candidates.push(taskAction(task, 'Sắp xếp ưu tiên', 'Yêu cầu chưa bắt đầu; xem lại mức ưu tiên trong backlog.', 72));
    }
  }

  const lastTask = scopedItems.find(item => item.id === lastTaskId);
  if (lastTask && !isViewer && (assigned(lastTask) || isAdmin || isScrumMaster || isProductOwner || isQa)) {
    candidates.push(taskAction(lastTask, 'Bạn đang làm dở', 'Mở lại công việc bạn đã xem gần đây.', 84));
  }

  if (lastRoute && /^\/(board|backlog|task-list|roadmap|reports|projects)(\/|$)/.test(lastRoute)) {
    candidates.push({
      id: `route:${lastRoute}`,
      kind: 'route',
      route: lastRoute,
      icon: 'history',
      eyebrow: 'Tiếp tục từ lần trước',
      title: 'Quay lại không gian bạn vừa mở',
      reason: 'Bạn không cần nhớ mình đã dừng ở đâu.',
      score: 58
    });
  }

  candidates.push({
    id: 'route:/project/summary',
    kind: 'route',
    route: '/project/summary',
    icon: 'dashboard',
    eyebrow: 'Bắt đầu tại đây',
    title: isViewer ? 'Xem tiến độ dự án' : 'Nắm tình hình dự án',
    reason: isViewer ? 'Xem các chỉ số và thay đổi mới nhất của dự án.' : 'Xem tổng quan trước khi chọn bước tiếp theo.',
    score: isViewer ? 82 : 20
  });

  if (!isViewer) {
    const route = isProductOwner ? '/backlog' : isDeveloper || isQa ? '/task-list' : '/board';
    candidates.push({
      id: `route:${route}`,
      kind: 'route',
      route,
      icon: isProductOwner ? 'view_list' : route === '/board' ? 'view_kanban' : 'task_alt',
      eyebrow: 'Không gian làm việc',
      title: isProductOwner ? 'Sắp xếp Backlog' : route === '/board' ? 'Mở bảng công việc' : 'Xem danh sách công việc',
      reason: 'Xem các mục còn mở và chọn bước tiếp theo.',
      score: 18
    });
  }

  const unique = new Map<string, FocusAction>();
  for (const action of candidates) {
    if ((unique.get(action.id)?.score ?? -1) < action.score) unique.set(action.id, action);
  }
  return [...unique.values()].sort((a, b) => b.score - a.score || a.id.localeCompare(b.id));
}

@Injectable({ providedIn: 'root' })
export class FocusRecommendationService {
  private readonly identity = inject(IdentityService);
  private readonly project = inject(ProjectManagementService);
  private readonly recent = inject(RecentWorkService);

  readonly actions = computed(() => computeFocusActions({
    user: this.identity.authState().currentUser,
    project: this.project.currentProject(),
    workItems: this.project.workItems(),
    notifications: this.identity.notifications(),
    lastTaskId: this.recent.lastTaskId(),
    lastRoute: this.recent.lastRoute()
  }));
  readonly primary = computed(() => this.actions()[0]);
  readonly secondary = computed(() => this.actions().slice(1, 4));
}
