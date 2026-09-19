import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, of } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Organization {
  id: string;
  name: string;
  slug: string;
  ownerId: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateOrganizationRequest {
  name: string;
  slug?: string;
  ownerId?: string;
}

export interface ProjectMember {
  id: string;
  userId: string;
  displayName: string;
  email: string;
  avatarUrl?: string;
  role: 'Project Lead' | 'Scrum Master' | 'Developer' | 'QA Engineer' | 'Viewer';
  joinedAt: string;
}

export interface Project {
  id: string;
  orgId: string;
  projectKey: string;
  name: string;
  type?: string;
  description?: string | null;
  leadUserId: string;
  leadName?: string;
  issueCounter: number;
  isArchived: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string | null;
  members?: ProjectMember[];
}

export interface CreateProjectRequest {
  orgId: string;
  projectKey: string;
  name: string;
  description?: string | null;
  leadUserId: string;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string | null;
  leadUserId: string;
}

export interface ProjectStatus {
  id: string;
  name: string;
  category: string;
  isInitial: boolean;
  orderIndex: number;
}

export interface WorkflowTransition {
  id: string;
  projectId: string;
  fromStatusId: string;
  fromStatusName: string;
  toStatusId: string;
  toStatusName: string;
  name?: string | null;
  requiredPermissionCode?: string | null;
}

export interface CreateWorkflowTransitionRequest {
  fromStatusId: string;
  toStatusId: string;
  name?: string | null;
  requiredPermissionCode?: string | null;
}

export interface Board {
  id: string;
  projectId: string;
  name: string;
  type: string;
  isDefault: boolean;
  createdAt: string;
}

export interface CreateBoardRequest {
  name: string;
  type: string;
  isDefault: boolean;
}

export interface BoardColumn {
  id: string;
  boardId: string;
  statusId: string;
  statusName: string;
  name?: string | null;
  orderIndex: number;
  wipLimit?: number | null;
}

export interface CreateBoardColumnRequest {
  statusId: string;
  name?: string | null;
  wipLimit?: number | null;
}

export interface UserDisplayInfo {
  userId: string;
  displayName: string;
  avatarUrl?: string | null;
}

export interface Sprint {
  id: string;
  projectId: string;
  name: string;
  goal?: string;
  startDate?: string;
  endDate?: string;
  status: 'active' | 'future' | 'completed';
  workItemsCount: number;
  totalStoryPoints: number;
}

export interface TaskComment {
  id: string;
  authorName: string;
  authorAvatar?: string;
  content: string;
  createdAt: string;
}

export interface TaskActivity {
  id: string;
  actorName: string;
  action: string;
  timestamp: string;
}

export interface EpicItem {
  id: string;
  name: string;
  key: string;
  color: string;
  description?: string;
  status: 'In Progress' | 'Done' | 'To Do';
}

export interface CustomIssueType {
  id: string;
  name: string;
  icon: string; // Emoji or SVG icon identifier
  color: string; // Tailwind color name or hex code
  description?: string;
  isCustom?: boolean;
}

export interface WorkflowTemplate {
  id: string;
  name: string;
  description: string;
  statuses: ProjectStatus[];
}

export interface WorkItem {
  id: string;
  issueKey: string;
  projectId: string;
  parentId?: string; // Parent Task ID if this is a Sub-task
  title: string;
  description?: string;
  statusId: string;
  statusName: string;
  issueType: string; // Story, Task, Bug, Epic, Sub-task, Use-Case or Custom Name
  priority: 'Low' | 'Medium' | 'High' | 'Urgent';
  storyPoints: number;
  assigneeId?: string;
  assigneeName?: string;
  assigneeAvatar?: string;
  reporterName: string;
  sprintId?: string;
  sprintName?: string;
  epicName?: string;
  epicColor?: string;
  labels?: string[];
  createdAt: string;
  updatedAt?: string;
  comments?: TaskComment[];
  activities?: TaskActivity[];
}

@Injectable({ providedIn: 'root' })
export class ProjectManagementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  // Active Projects Signal List
  readonly allProjectsList = signal<Project[]>([
    {
      id: 'proj-huce-01',
      orgId: 'org-huce',
      projectKey: 'SCRUMAI',
      name: 'HUCE AI Lab Project Management',
      type: 'Software space',
      description: 'Hệ thống Quản lý Scrum AI dành cho Trường Đại học Xây dựng Hà Nội',
      leadUserId: 'user-01',
      leadName: 'Trần Văn Hoàng',
      issueCounter: 32,
      isArchived: false,
      isDeleted: false,
      createdAt: '2026-08-01T08:00:00Z',
      members: [
        { id: 'm-1', userId: 'user-01', displayName: 'Trần Văn Hoàng', email: 'hoangtv@huce.edu.vn', role: 'Project Lead', joinedAt: '2026-08-01', avatarUrl: 'https://lh3.googleusercontent.com/aida/AEtjO1XtHjbcVDz-UBjctM0LfVUGmqUeR39WQIoExjpXXhoH4Wu_I9_Qpk42NbzSi5dIoyYQuxC6PmMSr__bvB8G1iRv51zwwYHyLI9epDcSeaSJ_23pgMSoDqCavTtJMnh_VUr1XiaX_dfIJVrOpVNtqTGgwFT5FuXj3i9tQVUICLqvROYKaCksEl97aHgdr2n-KIVSDs0bZzIZ5hDOpx0AeyayjF5Y_iybOHjJnpBXyyQz40C4Rx5A_y32U-v-DZxAw_gb18RoyaUMMA' },
        { id: 'm-2', userId: 'user-02', displayName: 'Nguyễn Thanh Hà', email: 'hant@huce.edu.vn', role: 'Scrum Master', joinedAt: '2026-08-02' },
        { id: 'm-3', userId: 'user-03', displayName: 'Phạm Đức Anh', email: 'anhpd@huce.edu.vn', role: 'Developer', joinedAt: '2026-08-05' },
        { id: 'm-4', userId: 'user-04', displayName: 'Lê Minh Khiêm', email: 'khiemlm@huce.edu.vn', role: 'QA Engineer', joinedAt: '2026-08-10' }
      ]
    },
    {
      id: 'proj-eform',
      orgId: 'org-huce',
      projectKey: 'EFORM',
      name: 'BTP - Hệ thống báo cáo (eForm)',
      type: 'Business space',
      description: 'Quy trình phê duyệt báo cáo eform cấp dưới',
      leadUserId: 'user-02',
      leadName: 'Nguyễn Thanh Hà',
      issueCounter: 18,
      isArchived: false,
      isDeleted: false,
      createdAt: '2026-08-15T08:00:00Z'
    },
    {
      id: 'proj-okr',
      orgId: 'org-huce',
      projectKey: 'BEDX',
      name: 'OKR Native BEDX System',
      type: 'Software space',
      description: 'Thử nghiệm dữ liệu và đo lường OKRs',
      leadUserId: 'user-01',
      leadName: 'Trần Văn Hoàng',
      issueCounter: 12,
      isArchived: false,
      isDeleted: false,
      createdAt: '2026-08-20T08:00:00Z'
    }
  ]);

  // Currently Selected Active Project
  readonly currentProject = signal<Project>(this.allProjectsList()[0]);

  // Issue Types Signal (Defaults + Custom Issue Types)
  readonly customIssueTypes = signal<CustomIssueType[]>([
    { id: 'it-1', name: 'Task', icon: '📘', color: 'blue', description: 'Công việc phát triển' },
    { id: 'it-2', name: 'Story', icon: '📗', color: 'emerald', description: 'Yêu cầu tính năng người dùng' },
    { id: 'it-3', name: 'Bug', icon: '📕', color: 'red', description: 'Lỗi phát sinh cần sửa' },
    { id: 'it-4', name: 'Use-Case', icon: '🩵', color: 'cyan', description: 'Kịch bản kiểm thử / sử dụng' },
    { id: 'it-5', name: 'Epic', icon: '⚡', color: 'purple', description: 'Hạng mục dự án lớn' },
    { id: 'it-6', name: 'Sub-task', icon: '📙', color: 'amber', description: 'Nhiệm vụ con phân rã' }
  ]);

  // Active Project Statuses Signal
  readonly projectStatuses = signal<ProjectStatus[]>([
    { id: 'st-1', name: 'To Do', category: 'To Do', isInitial: true, orderIndex: 1 },
    { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 2 },
    { id: 'st-3', name: 'Code Review', category: 'In Progress', isInitial: false, orderIndex: 3 },
    { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 4 }
  ]);

  // Epics Signal List
  readonly epics = signal<EpicItem[]>([
    { id: 'epic-1', key: 'E-1', name: 'Core Auth & Security', color: 'purple', description: 'Hệ thống Đăng nhập & Bảo mật JWT OAuth', status: 'In Progress' },
    { id: 'epic-2', key: 'E-2', name: 'UI Design System', color: 'blue', description: 'Bộ giao diện responsive & Glassmorphism', status: 'Done' },
    { id: 'epic-3', key: 'E-3', name: 'Agile Engine', color: 'amber', description: 'Bộ công cụ Backlog, Board, Sub-tasks & Sprints', status: 'In Progress' },
    { id: 'epic-4', key: 'E-4', name: 'Data & Telemetry', color: 'emerald', description: 'Báo cáo Burndown, Excel Import/Export & SignalR', status: 'To Do' }
  ]);

  readonly sprints = signal<Sprint[]>([
    { id: 'sp-1', projectId: 'proj-huce-01', name: 'SCRUMAI Sprint 1', goal: 'Xây dựng Core Auth & Docker Architecture', status: 'completed', startDate: '2026-08-10', endDate: '2026-08-24', workItemsCount: 12, totalStoryPoints: 34 },
    { id: 'sp-2', projectId: 'proj-huce-01', name: 'SCRUMAI Sprint 2', goal: 'Hoàn thiện Planning, Backlog & Excel Engine', status: 'active', startDate: '2026-08-25', endDate: '2026-09-08', workItemsCount: 10, totalStoryPoints: 28 },
    { id: 'sp-3', projectId: 'proj-huce-01', name: 'SCRUMAI Sprint 3', goal: 'Tích hợp Delivery Intelligence & Burndown Charts', status: 'future', startDate: '2026-09-09', endDate: '2026-09-23', workItemsCount: 8, totalStoryPoints: 22 },
    { id: 'sp-4', projectId: 'proj-huce-01', name: 'SCRUMAI Sprint 4', goal: 'Tối ưu AI Telemetry & Notification Engine', status: 'future', startDate: '2026-09-24', endDate: '2026-10-08', workItemsCount: 6, totalStoryPoints: 18 }
  ]);

  readonly workItems = signal<WorkItem[]>([
    // Sprint 1 (Completed)
    { id: 'wi-101', issueKey: 'SCRUMAI-101', projectId: 'proj-huce-01', title: 'Thiết kế CSDL MySQL Optimized & Docker Compose', statusId: 'st-4', statusName: 'Done', issueType: 'Task', priority: 'High', storyPoints: 5, assigneeName: 'Trần Văn Hoàng', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-1', sprintName: 'SCRUMAI Sprint 1', epicName: 'Core Auth & Security', epicColor: 'purple', labels: ['database', 'docker'], createdAt: '2026-08-11', updatedAt: '2026-08-15' },
    { id: 'wi-102', issueKey: 'SCRUMAI-102', projectId: 'proj-huce-01', title: 'Xây dựng Auth Page & JWT Security Interceptor', statusId: 'st-4', statusName: 'Done', issueType: 'Use-Case', priority: 'High', storyPoints: 8, assigneeName: 'Nguyễn Thanh Hà', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-1', sprintName: 'SCRUMAI Sprint 1', epicName: 'Core Auth & Security', epicColor: 'purple', labels: ['auth', 'security'], createdAt: '2026-08-12', updatedAt: '2026-08-18' },
    { id: 'wi-103', issueKey: 'SCRUMAI-103', projectId: 'proj-huce-01', title: 'Cấu hình Angular AppShell Layout & Glassmorphism', statusId: 'st-4', statusName: 'Done', issueType: 'Task', priority: 'Medium', storyPoints: 3, assigneeName: 'Phạm Đức Anh', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-1', sprintName: 'SCRUMAI Sprint 1', epicName: 'UI Design System', epicColor: 'blue', labels: ['ui/ux', 'frontend'], createdAt: '2026-08-14', updatedAt: '2026-08-20' },

    // Sprint 2 (Active)
    { id: 'wi-201', issueKey: 'SCRUMAI-201', projectId: 'proj-huce-01', title: 'Phát triển Collapsible Sidebar & Sub-header Navigation', statusId: 'st-2', statusName: 'In Progress', issueType: 'Use-Case', priority: 'High', storyPoints: 5, assigneeName: 'Trần Văn Hoàng', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-2', sprintName: 'SCRUMAI Sprint 2', epicName: 'UI Design System', epicColor: 'blue', labels: ['sidebar', 'navigation'], createdAt: '2026-08-26', updatedAt: '2026-09-19', comments: [{ id: 'c1', authorName: 'Nguyễn Thanh Hà', content: 'Cần chú ý hiệu ứng smooth khi thu mở trên di động', createdAt: '2026-09-19T10:00:00Z' }], activities: [{ id: 'a1', actorName: 'Trần Văn Hoàng', action: 'Đã chuyển trạng thái sang In Progress', timestamp: '2026-08-26' }] },
    
    // Sub-tasks under wi-201
    { id: 'wi-201-s1', issueKey: 'SCRUMAI-201-1', parentId: 'wi-201', projectId: 'proj-huce-01', title: 'Thiết kế hiệu ứng CSS Hover & Tooltip cho Icon', statusId: 'st-4', statusName: 'Done', issueType: 'Sub-task', priority: 'Medium', storyPoints: 1, assigneeName: 'Trần Văn Hoàng', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-2', sprintName: 'SCRUMAI Sprint 2', epicName: 'UI Design System', epicColor: 'blue', createdAt: '2026-08-26' },
    { id: 'wi-201-s2', issueKey: 'SCRUMAI-201-2', parentId: 'wi-201', projectId: 'proj-huce-01', title: 'Đồng bộ trạng thái thu mở Space danh mục', statusId: 'st-2', statusName: 'In Progress', issueType: 'Sub-task', priority: 'High', storyPoints: 2, assigneeName: 'Trần Văn Hoàng', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-2', sprintName: 'SCRUMAI Sprint 2', epicName: 'UI Design System', epicColor: 'blue', createdAt: '2026-08-26' },

    { id: 'wi-202', issueKey: 'SCRUMAI-202', projectId: 'proj-huce-01', title: 'Xây dựng Reusable Slide-over Task Detail Drawer', statusId: 'st-2', statusName: 'In Progress', issueType: 'Story', priority: 'High', storyPoints: 5, assigneeName: 'Trần Văn Hoàng', reporterName: 'Nguyễn Thanh Hà', sprintId: 'sp-2', sprintName: 'SCRUMAI Sprint 2', epicName: 'Agile Engine', epicColor: 'amber', labels: ['drawer', 'components'], createdAt: '2026-08-27', updatedAt: '2026-09-19' },
    { id: 'wi-203', issueKey: 'SCRUMAI-203', projectId: 'proj-huce-01', title: 'Tích hợp Excel Import / Export Engine cho Task List', statusId: 'st-1', statusName: 'To Do', issueType: 'Story', priority: 'High', storyPoints: 8, assigneeName: 'Phạm Đức Anh', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-2', sprintName: 'SCRUMAI Sprint 2', epicName: 'Data & Telemetry', epicColor: 'emerald', labels: ['excel', 'data-export'], createdAt: '2026-08-28', updatedAt: '2026-08-28' },
    { id: 'wi-204', issueKey: 'SCRUMAI-204', projectId: 'proj-huce-01', title: 'Xây dựng Trang Bkav eTask Style Task List View', statusId: 'st-3', statusName: 'Code Review', issueType: 'Task', priority: 'Medium', storyPoints: 3, assigneeName: 'Lê Minh Khiêm', reporterName: 'Nguyễn Thanh Hà', sprintId: 'sp-2', sprintName: 'SCRUMAI Sprint 2', epicName: 'Agile Engine', epicColor: 'amber', labels: ['table', 'task-list'], createdAt: '2026-08-29', updatedAt: '2026-09-18' },
    { id: 'wi-205', issueKey: 'SCRUMAI-205', projectId: 'proj-huce-01', title: 'Sửa lỗi alignment mật khẩu & nút Google OAuth', statusId: 'st-4', statusName: 'Done', issueType: 'Bug', priority: 'Urgent', storyPoints: 2, assigneeName: 'Trần Văn Hoàng', reporterName: 'Lê Minh Khiêm', sprintId: 'sp-2', sprintName: 'SCRUMAI Sprint 2', epicName: 'Core Auth & Security', epicColor: 'purple', labels: ['bugfix', 'auth'], createdAt: '2026-09-01', updatedAt: '2026-09-15' },

    // Sprint 3 (Planning)
    { id: 'wi-301', issueKey: 'SCRUMAI-301', projectId: 'proj-huce-01', title: 'Xây dựng Delivery Intelligence Dashboard & Burndown Chart', statusId: 'st-1', statusName: 'To Do', issueType: 'Use-Case', priority: 'High', storyPoints: 8, assigneeName: 'Trần Văn Hoàng', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-3', sprintName: 'SCRUMAI Sprint 3', epicName: 'Data & Telemetry', epicColor: 'emerald', labels: ['analytics', 'burndown'], createdAt: '2026-09-05' },
    { id: 'wi-302', issueKey: 'SCRUMAI-302', projectId: 'proj-huce-01', title: 'Tích hợp Claude AI Auto Sprint Breakdown CTA', statusId: 'st-1', statusName: 'To Do', issueType: 'Sub-task', priority: 'Medium', storyPoints: 5, assigneeName: 'Nguyễn Thanh Hà', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-3', sprintName: 'SCRUMAI Sprint 3', epicName: 'Agile Engine', epicColor: 'amber', labels: ['ai', 'automation'], createdAt: '2026-09-06' },

    // Sprint 4 (Future)
    { id: 'wi-401', issueKey: 'SCRUMAI-401', projectId: 'proj-huce-01', title: 'Tích hợp Realtime SignalR Telemetry & Desktop Push', statusId: 'st-1', statusName: 'To Do', issueType: 'Task', priority: 'Low', storyPoints: 5, assigneeName: 'Phạm Đức Anh', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-4', sprintName: 'SCRUMAI Sprint 4', epicName: 'Data & Telemetry', epicColor: 'emerald', labels: ['realtime', 'notifications'], createdAt: '2026-09-08' },

    // Backlog Pool (Unassigned Work Items)
    { id: 'wi-501', issueKey: 'SCRUMAI-501', projectId: 'proj-huce-01', title: '[DOC/CLOSE] User guide, handover & project closure', statusId: 'st-1', statusName: 'To Do', issueType: 'Task', priority: 'Medium', storyPoints: 3, assigneeName: 'Nguyễn Thanh Hà', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-backlog', sprintName: 'Backlog Pool', epicName: 'UI Design System', epicColor: 'purple', createdAt: '2026-09-10' },
    { id: 'wi-502', issueKey: 'SCRUMAI-502', projectId: 'proj-huce-01', title: '[AI-DATA/RELEASE] Freeze v1.0 evaluation dataset/model metadata', statusId: 'st-1', statusName: 'To Do', issueType: 'Story', priority: 'High', storyPoints: 5, assigneeName: 'Trần Văn Hoàng', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-backlog', sprintName: 'Backlog Pool', epicName: 'Data & Telemetry', epicColor: 'emerald', createdAt: '2026-09-12' },
    { id: 'wi-503', issueKey: 'SCRUMAI-503', projectId: 'proj-huce-01', title: '[QA/PROD] Final production regression & AI smoke', statusId: 'st-1', statusName: 'To Do', issueType: 'Bug', priority: 'Urgent', storyPoints: 2, assigneeName: 'Lê Minh Khiêm', reporterName: 'Trần Văn Hoàng', sprintId: 'sp-backlog', sprintName: 'Backlog Pool', epicName: 'Agile Engine', epicColor: 'amber', createdAt: '2026-09-15' }
  ]);

  // Selected Drawer Task State
  readonly activeDrawerTask = signal<WorkItem | null>(null);

  // --- Epic Management ---
  addEpic(name: string, color = 'indigo', description = ''): EpicItem {
    const key = `E-${this.epics().length + 1}`;
    const newEpic: EpicItem = {
      id: `epic-${Date.now()}`,
      key,
      name,
      color,
      description,
      status: 'In Progress'
    };
    this.epics.update(list => [...list, newEpic]);
    return newEpic;
  }

  // --- Sub-task Creation ---
  createSubTask(parentId: string, title: string): WorkItem {
    const parent = this.workItems().find(w => w.id === parentId);
    const subCounter = this.workItems().filter(w => w.parentId === parentId).length + 1;
    const issueKey = parent ? `${parent.issueKey}-${subCounter}` : `SUB-${Date.now()}`;

    const newSub: WorkItem = {
      id: `wi-sub-${Date.now()}`,
      issueKey,
      projectId: this.currentProject().id,
      parentId,
      title: title || 'Sub-task mới',
      statusId: 'st-1',
      statusName: 'To Do',
      issueType: 'Sub-task',
      priority: 'Medium',
      storyPoints: 1,
      assigneeName: parent?.assigneeName || 'Trần Văn Hoàng',
      reporterName: 'Trần Văn Hoàng',
      sprintId: parent?.sprintId || 'sp-2',
      sprintName: parent?.sprintName || 'SCRUMAI Sprint 2',
      epicName: parent?.epicName,
      epicColor: parent?.epicColor,
      createdAt: new Date().toISOString().split('T')[0]
    };

    this.workItems.update(list => [...list, newSub]);
    return newSub;
  }

  // --- WorkItem Helper Mutations ---
  updateWorkItem(updatedItem: WorkItem): void {
    this.workItems.update(items =>
      items.map(item => item.id === updatedItem.id ? { ...updatedItem, updatedAt: new Date().toISOString().split('T')[0] } : item)
    );
    if (this.activeDrawerTask()?.id === updatedItem.id) {
      this.activeDrawerTask.set({ ...updatedItem });
    }
  }

  updateWorkItemStatus(id: string, newStatus: string): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === id) {
          const updated = {
            ...item,
            statusName: newStatus,
            updatedAt: new Date().toISOString().split('T')[0],
            activities: [
              ...(item.activities || []),
              {
                id: `act-${Date.now()}`,
                actorName: 'Bạn',
                action: `Đã chuyển trạng thái sang ${newStatus}`,
                timestamp: 'Vừa xong'
              }
            ]
          };
          if (this.activeDrawerTask()?.id === id) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemAssignee(taskId: string, assigneeName: string): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, assigneeName, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemPriority(taskId: string, priority: 'Low' | 'Medium' | 'High' | 'Urgent'): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, priority, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemEpic(taskId: string, epicName: string, epicColor = 'indigo'): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, epicName, epicColor, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemIssueType(taskId: string, issueType: WorkItem['issueType']): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, issueType, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemStoryPoints(taskId: string, storyPoints: number): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, storyPoints, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  updateWorkItemSprint(taskId: string, newSprintId: string, newSprintName: string): void {
    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = { ...item, sprintId: newSprintId, sprintName: newSprintName, updatedAt: new Date().toISOString().split('T')[0] };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  cloneWorkItem(taskId: string): WorkItem | null {
    const item = this.workItems().find(w => w.id === taskId);
    if (!item) return null;

    const counter = this.currentProject().issueCounter + 1;
    const cloned: WorkItem = {
      ...item,
      id: `wi-${Date.now()}`,
      issueKey: `${this.currentProject().projectKey}-${counter}`,
      title: `${item.title} (Bản sao)`,
      createdAt: new Date().toISOString().split('T')[0],
      comments: [],
      activities: []
    };

    this.workItems.update(list => [cloned, ...list]);
    return cloned;
  }

  deleteWorkItem(taskId: string): void {
    this.workItems.update(items => items.filter(item => item.id !== taskId && item.parentId !== taskId));
    if (this.activeDrawerTask()?.id === taskId) {
      this.activeDrawerTask.set(null);
    }
  }

  // --- Bulk Operations ---
  bulkUpdateStatus(ids: string[], newStatus: WorkItem['statusName']): void {
    this.workItems.update(items =>
      items.map(item => ids.includes(item.id) ? { ...item, statusName: newStatus } : item)
    );
  }

  bulkUpdateSprint(ids: string[], sprintId: string, sprintName: string): void {
    this.workItems.update(items =>
      items.map(item => ids.includes(item.id) ? { ...item, sprintId, sprintName } : item)
    );
  }

  bulkDelete(ids: string[]): void {
    this.workItems.update(items => items.filter(item => !ids.includes(item.id)));
  }

  // --- Sprint Management ---
  addNewSprint(): Sprint {
    const nextIdx = this.sprints().length + 1;
    const newSp: Sprint = {
      id: `sp-${Date.now()}`,
      projectId: this.currentProject().id,
      name: `SCRUMAI Sprint ${nextIdx}`,
      goal: `Mục tiêu Sprint ${nextIdx}`,
      status: 'future',
      startDate: new Date().toISOString().split('T')[0],
      endDate: new Date(Date.now() + 14 * 86400000).toISOString().split('T')[0],
      workItemsCount: 0,
      totalStoryPoints: 0
    };
    this.sprints.update(list => [...list, newSp]);
    return newSp;
  }

  updateSprintStatus(sprintId: string, status: 'active' | 'future' | 'completed'): void {
    this.sprints.update(list =>
      list.map(sp => sp.id === sprintId ? { ...sp, status } : sp)
    );
  }

  updateSprintGoal(sprintId: string, goal: string): void {
    this.sprints.update(list =>
      list.map(sp => sp.id === sprintId ? { ...sp, goal } : sp)
    );
  }

  updateSprintInfo(sprintId: string, name: string, startDate?: string, endDate?: string): void {
    this.sprints.update(list =>
      list.map(sp => sp.id === sprintId ? { ...sp, name, startDate: startDate ?? sp.startDate, endDate: endDate ?? sp.endDate } : sp)
    );
  }

  moveSprintItemsToBacklog(sprintId: string): void {
    this.workItems.update(items =>
      items.map(item => item.sprintId === sprintId ? { ...item, sprintId: 'sp-backlog', sprintName: 'Backlog Pool' } : item)
    );
  }

  // --- Project Management Methods ---
  addNewProject(newProj: { name: string; projectKey: string; type?: string; description?: string }): Project {
    const created: Project = {
      id: `proj-${Date.now()}`,
      orgId: 'org-huce',
      projectKey: newProj.projectKey.toUpperCase(),
      name: newProj.name,
      type: newProj.type || 'Software space',
      description: newProj.description || '',
      leadUserId: 'user-01',
      leadName: 'Trần Văn Hoàng',
      issueCounter: 0,
      isArchived: false,
      isDeleted: false,
      createdAt: new Date().toISOString()
    };

    this.allProjectsList.update(list => [created, ...list]);
    this.currentProject.set(created);
    return created;
  }

  // --- WorkItem CRUD Methods ---
  addWorkItem(newItem: Partial<WorkItem>): WorkItem {
    const counter = this.currentProject().issueCounter + 1;
    const key = `${this.currentProject().projectKey}-${counter}`;
    
    const createdItem: WorkItem = {
      id: `wi-${Date.now()}`,
      issueKey: key,
      projectId: this.currentProject().id,
      parentId: newItem.parentId,
      title: newItem.title || 'Công việc mới',
      description: newItem.description || '',
      statusId: 'st-1',
      statusName: newItem.statusName || 'To Do',
      issueType: newItem.issueType || 'Task',
      priority: newItem.priority || 'Medium',
      storyPoints: newItem.storyPoints || 3,
      assigneeName: newItem.assigneeName || 'Trần Văn Hoàng',
      reporterName: 'Trần Văn Hoàng',
      sprintId: newItem.sprintId || 'sp-2',
      sprintName: newItem.sprintName || 'SCRUMAI Sprint 2',
      epicName: newItem.epicName || 'Agile Engine',
      epicColor: newItem.epicColor || 'amber',
      labels: newItem.labels || ['task'],
      createdAt: new Date().toISOString().split('T')[0],
      updatedAt: new Date().toISOString().split('T')[0]
    };

    this.workItems.update(list => [createdItem, ...list]);
    return createdItem;
  }

  addCommentToTask(taskId: string, commentText: string): void {
    const newComment: TaskComment = {
      id: `comment-${Date.now()}`,
      authorName: 'Trần Văn Hoàng',
      content: commentText,
      createdAt: new Date().toLocaleString()
    };

    this.workItems.update(items =>
      items.map(item => {
        if (item.id === taskId) {
          const updated = {
            ...item,
            comments: [...(item.comments || []), newComment]
          };
          if (this.activeDrawerTask()?.id === taskId) {
            this.activeDrawerTask.set(updated);
          }
          return updated;
        }
        return item;
      })
    );
  }

  // HTTP Mocks for Settings Pages
  getOrganizations(): Observable<Organization[]> {
    return of([
      { id: 'org-huce', name: 'Trường ĐH Xây dựng Hà Nội', slug: 'huce', ownerId: 'user-01', isActive: true, createdAt: '2026-08-01' }
    ]);
  }

  getProjects(): Observable<Project[]> {
    return of(this.allProjectsList());
  }

  createProject(req: any): Observable<Project> {
    const proj = this.addNewProject({ name: req.name || 'Dự án mới', projectKey: req.projectKey || 'PROJ', description: req.description });
    return of(proj);
  }

  addProjectMember(memberReq: any): Observable<boolean> {
    return of(true);
  }

  getUsers(): Observable<UserDisplayInfo[]> {
    return of([
      { userId: 'user-01', displayName: 'Trần Văn Hoàng (Lead)' },
      { userId: 'user-02', displayName: 'Nguyễn Thanh Hà (Scrum Master)' },
      { userId: 'user-03', displayName: 'Phạm Đức Anh (Developer)' },
      { userId: 'user-04', displayName: 'Lê Minh Khiêm (QA Engineer)' }
    ]);
  }

  getProjectById(id: string): Observable<Project> {
    const p = this.allProjectsList().find(x => x.id === id) || this.currentProject();
    return of(p);
  }

  updateProject(id: string, req: UpdateProjectRequest): Observable<Project> {
    const current = this.currentProject();
    const updated = { ...current, name: req.name, description: req.description, leadUserId: req.leadUserId };
    this.currentProject.set(updated);
    this.allProjectsList.update(list => list.map(x => x.id === id ? updated : x));
    return of(updated);
  }

  deleteProject(id: string): Observable<boolean> {
    this.allProjectsList.update(list => list.filter(x => x.id !== id));
    return of(true);
  }

  // Custom Issue Type Creation
  addCustomIssueType(name: string, icon: string, color: string, description?: string): CustomIssueType {
    const newType: CustomIssueType = {
      id: `it-${Date.now()}`,
      name,
      icon,
      color,
      description,
      isCustom: true
    };
    this.customIssueTypes.update(types => [...types, newType]);
    return newType;
  }

  // Workflow Status Management
  getProjectStatuses(projectId: string): Observable<ProjectStatus[]> {
    if (projectId) { /* no-op */ }
    return of(this.projectStatuses());
  }

  addProjectStatus(name: string, category = 'In Progress'): ProjectStatus {
    const newStatus: ProjectStatus = {
      id: `st-${Date.now()}`,
      name,
      category,
      isInitial: false,
      orderIndex: this.projectStatuses().length + 1
    };
    this.projectStatuses.update(list => [...list, newStatus]);
    return newStatus;
  }

  deleteProjectStatus(id: string): void {
    this.projectStatuses.update(list => list.filter(s => s.id !== id));
  }

  applyWorkflowTemplateToProject(templateId: string): ProjectStatus[] {
    let newStatuses: ProjectStatus[] = [];
    if (templateId === 'scrum-std') {
      newStatuses = [
        { id: 'st-1', name: 'To Do', category: 'To Do', isInitial: true, orderIndex: 1 },
        { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 2 },
        { id: 'st-3', name: 'Code Review', category: 'In Progress', isInitial: false, orderIndex: 3 },
        { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 4 }
      ];
    } else if (templateId === 'agile-qa') {
      newStatuses = [
        { id: 'st-1', name: 'To Do', category: 'To Do', isInitial: true, orderIndex: 1 },
        { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 2 },
        { id: 'st-3', name: 'Code Review', category: 'In Progress', isInitial: false, orderIndex: 3 },
        { id: 'st-qa', name: 'QA Testing', category: 'In Progress', isInitial: false, orderIndex: 4 },
        { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 5 }
      ];
    } else if (templateId === 'kanban-simple') {
      newStatuses = [
        { id: 'st-1', name: 'To Do', category: 'To Do', isInitial: true, orderIndex: 1 },
        { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 2 },
        { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 3 }
      ];
    } else if (templateId === 'enterprise') {
      newStatuses = [
        { id: 'st-1', name: 'Backlog', category: 'To Do', isInitial: true, orderIndex: 1 },
        { id: 'st-spec', name: 'Spec Review', category: 'To Do', isInitial: false, orderIndex: 2 },
        { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 3 },
        { id: 'st-3', name: 'Code Review', category: 'In Progress', isInitial: false, orderIndex: 4 },
        { id: 'st-qa', name: 'QA Verification', category: 'In Progress', isInitial: false, orderIndex: 5 },
        { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 6 }
      ];
    } else {
      newStatuses = this.projectStatuses();
    }

    this.projectStatuses.set(newStatuses);
    return newStatuses;
  }

  getWorkflowTransitions(projectId: string): Observable<WorkflowTransition[]> {
    if (projectId) { /* no-op */ }
    return of([
      { id: 'tr-1', projectId, fromStatusId: 'st-1', fromStatusName: 'To Do', toStatusId: 'st-2', toStatusName: 'In Progress', name: 'Start Work' },
      { id: 'tr-2', projectId, fromStatusId: 'st-2', fromStatusName: 'In Progress', toStatusId: 'st-3', toStatusName: 'Code Review', name: 'Submit PR' },
      { id: 'tr-3', projectId, fromStatusId: 'st-3', fromStatusName: 'Code Review', toStatusId: 'st-4', toStatusName: 'Done', name: 'Approve & Merge' }
    ]);
  }

  createWorkflowTransition(projectId: string, req: CreateWorkflowTransitionRequest): Observable<WorkflowTransition> {
    const created: WorkflowTransition = {
      id: `tr-${Date.now()}`,
      projectId,
      fromStatusId: req.fromStatusId,
      fromStatusName: req.fromStatusId === 'st-1' ? 'To Do' : req.fromStatusId === 'st-2' ? 'In Progress' : 'Code Review',
      toStatusId: req.toStatusId,
      toStatusName: req.toStatusId === 'st-4' ? 'Done' : req.toStatusId === 'st-3' ? 'Code Review' : 'In Progress',
      name: req.name || 'Transition',
      requiredPermissionCode: req.requiredPermissionCode
    };
    return of(created);
  }

  deleteWorkflowTransition(id: string): Observable<boolean> {
    if (id) { /* no-op */ }
    return of(true);
  }

  getBoards(projectId: string): Observable<Board[]> {
    return of([
      { id: 'b-1', projectId, name: 'Default Scrum Board', type: 'Scrum', isDefault: true, createdAt: '2026-08-01' }
    ]);
  }

  createBoard(projectId: string, req: CreateBoardRequest): Observable<Board> {
    const created: Board = {
      id: `b-${Date.now()}`,
      projectId,
      name: req.name,
      type: req.type,
      isDefault: req.isDefault,
      createdAt: new Date().toISOString()
    };
    return of(created);
  }

  getBoardColumns(boardId: string): Observable<BoardColumn[]> {
    if (boardId) { /* no-op */ }
    return of([
      { id: 'col-1', boardId, statusId: 'st-1', statusName: 'To Do', name: 'Cần Làm (To Do)', orderIndex: 1, wipLimit: null },
      { id: 'col-2', boardId, statusId: 'st-2', statusName: 'In Progress', name: 'Đang Thực Hiện', orderIndex: 2, wipLimit: 5 },
      { id: 'col-3', boardId, statusId: 'st-3', statusName: 'Code Review', name: 'Code Review', orderIndex: 3, wipLimit: 3 },
      { id: 'col-4', boardId, statusId: 'st-4', statusName: 'Done', name: 'Hoàn Thành (Done)', orderIndex: 4, wipLimit: null }
    ]);
  }

  createBoardColumn(boardId: string, req: CreateBoardColumnRequest): Observable<BoardColumn> {
    const created: BoardColumn = {
      id: `col-${Date.now()}`,
      boardId,
      statusId: req.statusId,
      statusName: req.statusId === 'st-1' ? 'To Do' : req.statusId === 'st-2' ? 'In Progress' : req.statusId === 'st-3' ? 'Code Review' : 'Done',
      name: req.name || 'Cột mới',
      orderIndex: 5,
      wipLimit: req.wipLimit
    };
    return of(created);
  }

  reorderBoardColumns(boardId: string, orderedIds: string[]): Observable<boolean> {
    if (boardId || orderedIds) { /* no-op */ }
    return of(true);
  }

  deleteBoardColumn(id: string): Observable<boolean> {
    if (id) { /* no-op */ }
    return of(true);
  }
}
