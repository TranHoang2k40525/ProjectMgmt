import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal, computed } from '@angular/core';
import { Observable, catchError, map, of, tap, throwError } from 'rxjs';
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
  memberUserIds?: string[];
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
  memberUserIds?: string[];
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

export interface UpdateBoardColumnRequest {
  name: string | null;
  wipLimit: number | null;
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
  readonly allProjectsList = signal<Project[]>([]);
  // An empty selection keeps existing feature consumers safe until real projects load.
  private readonly emptyProject: Project = {
    id: '', orgId: '', projectKey: '', name: 'Chưa chọn dự án', leadUserId: '',
    issueCounter: 0, isArchived: false, isDeleted: false, createdAt: ''
  };
  readonly currentProject = signal<Project>(this.emptyProject);
  readonly hasActiveProject = computed(() => !!this.currentProject().id && this.currentProject().id.trim() !== '');

  // Issue Types Signal (Standard Scrum Types with library icon keys)
  readonly customIssueTypes = signal<CustomIssueType[]>([
    { id: 'it-1', name: 'Task', icon: 'check_box', color: 'blue', description: 'Công việc phát triển' },
    { id: 'it-2', name: 'Story', icon: 'bookmark', color: 'emerald', description: 'Yêu cầu tính năng người dùng' },
    { id: 'it-3', name: 'Bug', icon: 'bug_report', color: 'red', description: 'Lỗi phát sinh cần sửa' },
    { id: 'it-4', name: 'Use-Case', icon: 'fact_check', color: 'cyan', description: 'Kịch bản kiểm thử / sử dụng' },
    { id: 'it-5', name: 'Epic', icon: 'bolt', color: 'purple', description: 'Hạng mục dự án lớn' },
    { id: 'it-6', name: 'Sub-task', icon: 'subdirectory_arrow_right', color: 'amber', description: 'Nhiệm vụ con phân rã' }
  ]);

  // Active Project Statuses Signal (Default Scrum workflow)
  readonly projectStatuses = signal<ProjectStatus[]>([
    { id: 'st-1', name: 'To Do', category: 'To Do', isInitial: true, orderIndex: 1 },
    { id: 'st-2', name: 'In Progress', category: 'In Progress', isInitial: false, orderIndex: 2 },
    { id: 'st-3', name: 'Code Review', category: 'In Progress', isInitial: false, orderIndex: 3 },
    { id: 'st-4', name: 'Done', category: 'Done', isInitial: false, orderIndex: 4 }
  ]);

  // Epics Signal List (real software dev epics)
  readonly epics = signal<EpicItem[]>([
    { id: 'epic-1', key: 'E-1', name: 'Kiến trúc Nền tảng & Xác thực (Auth & RBAC)', color: 'purple', description: 'Hạ tầng Microservices và bảo mật', status: 'Done' },
    { id: 'epic-2', key: 'E-2', name: 'Bộ máy Điều phối Scrum Agile (Scrum Engine)', color: 'blue', description: 'Quản lý Sprint, Board, Backlog và Workflow', status: 'In Progress' },
    { id: 'epic-3', key: 'E-3', name: 'Trí tuệ Nhân tạo Hỗ trợ Quản trị (AI Copilot)', color: 'emerald', description: 'Mô hình phân rã yêu cầu và xếp hạng phân công', status: 'To Do' }
  ]);

  // Sprints Signal List (real sprints cycle)
  readonly sprints = signal<Sprint[]>([
    {
      id: 'sp-1',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      name: 'Sprint 01 - Khởi tạo kiến trúc & RBAC',
      goal: 'Hoàn thiện nền tảng Micro-services, phân quyền RBAC và luồng bảo mật Token JWT.',
      status: 'completed',
      startDate: '2026-10-01',
      endDate: '2026-10-14',
      workItemsCount: 2,
      totalStoryPoints: 10
    },
    {
      id: 'sp-2',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      name: 'Sprint 02 - Bảng Scrum & Quản lý Backlog',
      goal: 'Hoàn thiện kéo thả Kanban Board, quản lý phân tầng User Stories và tối ưu UI/UX.',
      status: 'active',
      startDate: '2026-10-15',
      endDate: '2026-10-29',
      workItemsCount: 5,
      totalStoryPoints: 24
    },
    {
      id: 'sp-3',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      name: 'Sprint 03 - Trí tuệ Nhân tạo Phân rã & Đề xuất',
      goal: 'Tích hợp mô hình AI LLM phân rã yêu cầu thành Sub-tasks và xếp hạng ứng viên đa tiêu chí.',
      status: 'future',
      startDate: '2026-10-30',
      endDate: '2026-11-13',
      workItemsCount: 3,
      totalStoryPoints: 26
    }
  ]);

  // WorkItems Signal List (real agile work items)
  readonly workItems = signal<WorkItem[]>([
    {
      id: 'wi-1',
      issueKey: 'QLDA-1',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Thiết kế giao diện kéo thả trực quan chuẩn Jira cho Scrum Board',
      description: 'Hiện thực hóa các cột trạng thái Kanban, tích hợp CDK DragDrop và hiệu ứng FLIP animation mượt mà.',
      statusId: 'st-1',
      statusName: 'To Do',
      issueType: 'Story',
      priority: 'High',
      storyPoints: 5,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-2',
      sprintName: 'Sprint 02 - Bảng Scrum & Quản lý Backlog',
      epicName: 'Bộ máy Điều phối Scrum Agile (Scrum Engine)',
      epicColor: 'blue',
      labels: ['frontend', 'ui-ux'],
      createdAt: '2026-10-15',
      updatedAt: '2026-10-16'
    },
    {
      id: 'wi-2',
      issueKey: 'QLDA-2',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Tối ưu hóa hiệu năng đồng bộ dữ liệu Realtime qua WebSockets',
      description: 'Giảm thiểu độ trễ truyền gói tin thông báo và cập nhật thẻ việc tức thì giữa các thành viên đang hoạt động.',
      statusId: 'st-1',
      statusName: 'To Do',
      issueType: 'Task',
      priority: 'Medium',
      storyPoints: 3,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-2',
      sprintName: 'Sprint 02 - Bảng Scrum & Quản lý Backlog',
      epicName: 'Kiến trúc Nền tảng & Xác thực (Auth & RBAC)',
      epicColor: 'purple',
      labels: ['realtime', 'signalr'],
      createdAt: '2026-10-15',
      updatedAt: '2026-10-16'
    },
    {
      id: 'wi-3',
      issueKey: 'QLDA-3',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Xây dựng module tự động tính toán Sprint Velocity và Burndown Chart',
      description: 'Thu thập chỉ số Story Points hoàn thành theo từng ngày trong chu kỳ Sprint và vẽ đồ thị Ideal vs Actual burn rate.',
      statusId: 'st-2',
      statusName: 'In Progress',
      issueType: 'Story',
      priority: 'High',
      storyPoints: 8,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-2',
      sprintName: 'Sprint 02 - Bảng Scrum & Quản lý Backlog',
      epicName: 'Bộ máy Điều phối Scrum Agile (Scrum Engine)',
      epicColor: 'blue',
      labels: ['analytics', 'charts'],
      createdAt: '2026-10-15',
      updatedAt: '2026-10-17'
    },
    {
      id: 'wi-4',
      issueKey: 'QLDA-4',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Chuẩn hóa API RESTful và cơ chế phân quyền RBAC theo từng Project Workspace',
      description: 'Đảm bảo mỗi request đều được kiểm tra Scoped Permission và User Role trước khi cấp quyền truy xuất tài nguyên.',
      statusId: 'st-2',
      statusName: 'In Progress',
      issueType: 'Task',
      priority: 'Urgent',
      storyPoints: 5,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-2',
      sprintName: 'Sprint 02 - Bảng Scrum & Quản lý Backlog',
      epicName: 'Kiến trúc Nền tảng & Xác thực (Auth & RBAC)',
      epicColor: 'purple',
      labels: ['backend', 'security'],
      createdAt: '2026-10-15',
      updatedAt: '2026-10-17'
    },
    {
      id: 'wi-5',
      issueKey: 'QLDA-5',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Kiểm thử tự động E2E luồng tạo công việc và chuyển trạng thái Sprint',
      description: 'Viết kịch bản kiểm thử tích hợp đảm bảo tính toàn vẹn của dữ liệu trạng thái công việc qua các bước chuyển Workflow.',
      statusId: 'st-3',
      statusName: 'Code Review',
      issueType: 'Task',
      priority: 'High',
      storyPoints: 3,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-2',
      sprintName: 'Sprint 02 - Bảng Scrum & Quản lý Backlog',
      epicName: 'Bộ máy Điều phối Scrum Agile (Scrum Engine)',
      epicColor: 'blue',
      labels: ['testing', 'qa'],
      createdAt: '2026-10-15',
      updatedAt: '2026-10-18'
    },
    {
      id: 'wi-6',
      issueKey: 'QLDA-6',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Khởi tạo cơ sở dữ liệu và cấu hình hệ thống đa người dùng',
      description: 'Thiết kế lược đồ bảng MySQL với các ràng buộc khóa ngoại, chỉ mục hiệu năng cao và phân vùng lưu trữ tối ưu.',
      statusId: 'st-4',
      statusName: 'Done',
      issueType: 'Task',
      priority: 'High',
      storyPoints: 5,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-1',
      sprintName: 'Sprint 01 - Khởi tạo kiến trúc & RBAC',
      epicName: 'Kiến trúc Nền tảng & Xác thực (Auth & RBAC)',
      epicColor: 'purple',
      labels: ['database', 'mysql'],
      createdAt: '2026-10-02',
      updatedAt: '2026-10-12'
    },
    {
      id: 'wi-7',
      issueKey: 'QLDA-7',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Cài đặt JWT Authentication và bảo mật phiên đa thiết bị',
      description: 'Triển khai xác thực Token kép (Access Token + Refresh Token) tích hợp cơ chế thu hồi phiên và bảo vệ chống brute-force.',
      statusId: 'st-4',
      statusName: 'Done',
      issueType: 'Story',
      priority: 'High',
      storyPoints: 5,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-1',
      sprintName: 'Sprint 01 - Khởi tạo kiến trúc & RBAC',
      epicName: 'Kiến trúc Nền tảng & Xác thực (Auth & RBAC)',
      epicColor: 'purple',
      labels: ['security', 'auth'],
      createdAt: '2026-10-03',
      updatedAt: '2026-10-13'
    },
    {
      id: 'wi-8',
      issueKey: 'QLDA-8',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Huấn luyện mô hình phân rã tự động User Stories thành các Sub-tasks kỹ thuật',
      description: 'Xây dựng prompt template tối ưu và tích hợp LLM phân tích ngữ nghĩa để tự động đề xuất danh sách công việc con.',
      statusId: 'st-1',
      statusName: 'To Do',
      issueType: 'Story',
      priority: 'High',
      storyPoints: 13,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-3',
      sprintName: 'Sprint 03 - Trí tuệ Nhân tạo Phân rã & Đề xuất',
      epicName: 'Trí tuệ Nhân tạo Hỗ trợ Quản trị (AI Copilot)',
      epicColor: 'emerald',
      labels: ['ai', 'prompt-engineering'],
      createdAt: '2026-10-16',
      updatedAt: '2026-10-16'
    },
    {
      id: 'wi-9',
      issueKey: 'QLDA-9',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Thuật toán xếp hạng ứng viên phân bổ công việc theo kỹ năng và tải hiện tại',
      description: 'Tính toán trọng số phù hợp dựa trên 4 tiêu chí: Workload, Skill match, Lịch sử giải quyết và Capacity khả dụng.',
      statusId: 'st-1',
      statusName: 'To Do',
      issueType: 'Story',
      priority: 'Medium',
      storyPoints: 8,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-3',
      sprintName: 'Sprint 03 - Trí tuệ Nhân tạo Phân rã & Đề xuất',
      epicName: 'Trí tuệ Nhân tạo Hỗ trợ Quản trị (AI Copilot)',
      epicColor: 'emerald',
      labels: ['ai', 'algorithm'],
      createdAt: '2026-10-16',
      updatedAt: '2026-10-16'
    },
    {
      id: 'wi-10',
      issueKey: 'QLDA-10',
      projectId: 'f423e261-37af-44cd-9aa6-38f2030501f6',
      title: 'Giao diện giám sát AI Token Quota và chi phí sử dụng mô hình LLM',
      description: 'Bảng theo dõi hạn mức chi tiêu token, đo lường độ trễ API AI và thống kê số lần gọi generation theo thời gian thực.',
      statusId: 'st-1',
      statusName: 'To Do',
      issueType: 'Task',
      priority: 'Low',
      storyPoints: 5,
      assigneeName: 'Hoàng Trần Văn',
      reporterName: 'Hoàng Trần Văn',
      sprintId: 'sp-3',
      sprintName: 'Sprint 03 - Trí tuệ Nhân tạo Phân rã & Đề xuất',
      epicName: 'Trí tuệ Nhân tạo Hỗ trợ Quản trị (AI Copilot)',
      epicColor: 'emerald',
      labels: ['monitoring', 'cost-control'],
      createdAt: '2026-10-16',
      updatedAt: '2026-10-16'
    }
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
      projectId: this.currentProject()?.id ?? '',
      parentId,
      title: title || 'Sub-task mới',
      statusId: 'st-1',
      statusName: 'To Do',
      issueType: 'Sub-task',
      priority: 'Medium',
      storyPoints: 1,
      assigneeName: parent?.assigneeName || '',
      reporterName: parent?.reporterName || 'Tôi',
      sprintId: parent?.sprintId || '',
      sprintName: parent?.sprintName || '',
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

    const proj = this.currentProject();
    const counter = (proj?.issueCounter ?? 0) + 1;
    const issueKey = proj ? `${proj.projectKey}-${counter}` : `TASK-${counter}`;
    const cloned: WorkItem = {
      ...item,
      id: `wi-${Date.now()}`,
      issueKey,
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
    const proj = this.currentProject();
    const nextIdx = this.sprints().length + 1;
    const name = proj ? `${proj.projectKey} Sprint ${nextIdx}` : `Sprint ${nextIdx}`;
    const newSp: Sprint = {
      id: `sp-${Date.now()}`,
      projectId: proj?.id ?? '',
      name,
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
      orgId: '',
      projectKey: newProj.projectKey.toUpperCase(),
      name: newProj.name,
      type: newProj.type || 'Software space',
      description: newProj.description || '',
      leadUserId: '',
      leadName: '',
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
    const proj = this.currentProject();
    const counter = (proj?.issueCounter ?? 0) + 1;
    const key = proj ? `${proj.projectKey}-${counter}` : `TASK-${counter}`;
    
    const createdItem: WorkItem = {
      id: `wi-${Date.now()}`,
      issueKey: key,
      projectId: proj?.id ?? '',
      parentId: newItem.parentId,
      title: newItem.title || 'Công việc mới',
      description: newItem.description || '',
      statusId: 'st-1',
      statusName: newItem.statusName || 'To Do',
      issueType: newItem.issueType || 'Task',
      priority: newItem.priority || 'Medium',
      storyPoints: newItem.storyPoints || 3,
      assigneeName: newItem.assigneeName || '',
      reporterName: newItem.reporterName || 'Tôi',
      sprintId: newItem.sprintId || '',
      sprintName: newItem.sprintName || '',
      epicName: newItem.epicName || '',
      epicColor: newItem.epicColor || 'blue',
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
      authorName: 'Tôi',
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

  // HTTP API Operations
  getOrganizations(): Observable<Organization[]> {
    return this.http.get<Organization[]>(`${this.baseUrl}/organizations`);
  }

  createOrganization(req: CreateOrganizationRequest): Observable<Organization> {
    return this.http.post<Organization>(`${this.baseUrl}/organizations`, req);
  }

  selectProject(project: Project): void {
    this.currentProject.set(project);
    if (typeof localStorage !== 'undefined' && project.id) {
      try { localStorage.setItem('projectmgmt.active-project-id', project.id); } catch { /* ignore */ }
    }
  }

  getProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.baseUrl}/projects`).pipe(tap(projects => {
      this.allProjectsList.set(projects);
      const savedId = typeof localStorage !== 'undefined' ? localStorage.getItem('projectmgmt.active-project-id') : null;
      const matched = savedId ? projects.find(p => p.id === savedId) : null;
      const current = this.currentProject().id ? projects.find(p => p.id === this.currentProject().id) : null;
      const nextActive = current ?? matched ?? projects[0] ?? this.emptyProject;
      this.currentProject.set(nextActive);
      if (nextActive.id && typeof localStorage !== 'undefined') {
        try { localStorage.setItem('projectmgmt.active-project-id', nextActive.id); } catch { /* ignore */ }
      }
    }));
  }

  createProject(req: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>(`${this.baseUrl}/projects`, req).pipe(tap(project => {
      this.cacheProject(project);
      this.selectProject(project);
    }));
  }

  loadProjectMembers(projectId: string): Observable<ProjectMember[]> {
    return this.http.get<{ success?: boolean; members?: Array<{ userId?: string; roleId?: string; roleName?: string; fullName?: string; email?: string; avatarUrl?: string; grantedAt?: string }> }>(
      `${this.baseUrl}/projects/${projectId}/members`
    ).pipe(
      map(res => {
        const members: ProjectMember[] = (res.members ?? []).map(m => ({
          id: m.userId || `m-${Date.now()}`,
          userId: m.userId || '',
          displayName: m.fullName || m.email || 'Thành viên',
          email: m.email || '',
          avatarUrl: m.avatarUrl || undefined,
          role: this.mapBackendRoleToProjectMemberRole(m.roleName),
          joinedAt: m.grantedAt ? new Date(m.grantedAt).toLocaleDateString('vi-VN') : 'Vừa xong'
        }));
        const current = this.currentProject();
        if (current && current.id === projectId) {
          this.currentProject.set({ ...current, members });
        }
        this.allProjectsList.update(list => list.map(p => p.id === projectId ? { ...p, members } : p));
        return members;
      })
    );
  }

  inviteProjectMember(projectId: string, userId: string, roleId: string): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/projects/${projectId}/members`, { userId, roleId }).pipe(
      tap(() => {
        this.loadProjectMembers(projectId).subscribe({ error: () => undefined });
      })
    );
  }

  changeProjectMemberRole(projectId: string, userId: string, roleId: string): Observable<unknown> {
    return this.http.put(`${this.baseUrl}/projects/${projectId}/members/${userId}/role`, { roleId }).pipe(
      tap(() => {
        this.loadProjectMembers(projectId).subscribe({ error: () => undefined });
      })
    );
  }

  removeProjectMember(projectId: string, userId: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/projects/${projectId}/members/${userId}`).pipe(
      tap(() => {
        this.loadProjectMembers(projectId).subscribe({ error: () => undefined });
      })
    );
  }

  getRoles(scope = 'Project'): Observable<Array<{ id: string; name: string; code: string; description?: string }>> {
    return this.http.get<{ success?: boolean; items?: Array<{ roleId?: string; name?: string; description?: string }> }>(
      `${this.baseUrl}/v1/roles`,
      { params: { scope } }
    ).pipe(
      map(res => (res.items ?? []).map(r => ({
        id: r.roleId || '',
        name: r.name || '',
        code: (r.name || '').toUpperCase().replace(/\s+/g, '_'),
        description: r.description
      })))
    );
  }

  mapBackendRoleToProjectMemberRole(roleName?: string | null): ProjectMember['role'] {
    if (!roleName) return 'Developer';
    const lower = roleName.toLowerCase();
    if (lower.includes('manager') || lower.includes('lead') || lower.includes('owner') || lower.includes('admin')) {
      return 'Project Lead';
    }
    if (lower.includes('scrum')) return 'Scrum Master';
    if (lower.includes('qa') || lower.includes('test')) return 'QA Engineer';
    if (lower.includes('view')) return 'Viewer';
    return 'Developer';
  }

  addProjectMember(memberReq: Pick<ProjectMember, 'displayName' | 'email' | 'role'>): Observable<boolean> {
    void memberReq;
    return throwError(() => ({ title: 'Chức năng thêm thành viên chưa được tích hợp.' }));
  }

  getUsers(): Observable<UserDisplayInfo[]> {
    return this.http.get<UserDisplayInfo[]>(`${this.baseUrl}/users`).pipe(
      map(apiUsers => (apiUsers || []).filter(u => !u.userId.startsWith('11111111-0000-0000-0000'))),
      catchError(err => {
        console.error('Không thể lấy danh sách người dùng từ API:', err);
        return of([]);
      })
    );
  }

  getProjectById(id: string): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/projects/${id}`).pipe(tap(project => this.cacheProject(project)));
  }

  updateProject(id: string, req: UpdateProjectRequest): Observable<Project> {
    return this.http.put<Project>(`${this.baseUrl}/projects/${id}`, req).pipe(tap(project => this.cacheProject(project)));
  }

  deleteProject(id: string): Observable<boolean> {
    return this.http.delete<void>(`${this.baseUrl}/projects/${id}`).pipe(tap(() => {
      this.allProjectsList.update(projects => projects.filter(p => p.id !== id));
      if (this.currentProject().id === id) this.currentProject.set(this.allProjectsList()[0] ?? this.emptyProject);
    }), map(() => true));
  }

  private cacheProject(project: Project): void {
    this.allProjectsList.update(projects => {
      const exists = projects.some(p => p.id === project.id);
      return exists ? projects.map(p => p.id === project.id ? project : p) : [...projects, project];
    });
    if (this.currentProject().id === project.id) this.currentProject.set(project);
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
    return this.http.get<ProjectStatus[]>(`${this.baseUrl}/projects/${projectId}/statuses`);
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
    let newStatuses: ProjectStatus[];
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
    return this.http.get<WorkflowTransition[]>(`${this.baseUrl}/projects/${projectId}/workflow/transitions`);
  }

  createWorkflowTransition(projectId: string, req: CreateWorkflowTransitionRequest): Observable<WorkflowTransition> {
    return this.http.post<WorkflowTransition>(`${this.baseUrl}/projects/${projectId}/workflow/transitions`, req);
  }

  deleteWorkflowTransition(id: string): Observable<boolean> {
    return this.http.delete<void>(`${this.baseUrl}/workflow/transitions/${id}`).pipe(map(() => true));
  }

  getBoards(projectId: string): Observable<Board[]> {
    return this.http.get<Board[]>(`${this.baseUrl}/projects/${projectId}/boards`);
  }

  createBoard(projectId: string, req: CreateBoardRequest): Observable<Board> {
    return this.http.post<Board>(`${this.baseUrl}/projects/${projectId}/boards`, req);
  }

  getBoardColumns(boardId: string): Observable<BoardColumn[]> {
    return this.http.get<BoardColumn[]>(`${this.baseUrl}/boards/${boardId}/columns`);
  }

  createBoardColumn(boardId: string, req: CreateBoardColumnRequest): Observable<BoardColumn> {
    return this.http.post<BoardColumn>(`${this.baseUrl}/boards/${boardId}/columns`, req);
  }

  reorderBoardColumns(boardId: string, orderedIds: string[]): Observable<boolean> {
    return this.http.put<void>(`${this.baseUrl}/boards/${boardId}/columns/reorder`, { orderedColumnIds: orderedIds }).pipe(map(() => true));
  }

  deleteBoardColumn(id: string): Observable<boolean> {
    return this.http.delete<void>(`${this.baseUrl}/boards/columns/${id}`).pipe(map(() => true));
  }

  updateBoardColumn(id: string, req: UpdateBoardColumnRequest): Observable<BoardColumn> {
    return this.http.put<BoardColumn>(`${this.baseUrl}/boards/columns/${id}`, req);
  }
}
