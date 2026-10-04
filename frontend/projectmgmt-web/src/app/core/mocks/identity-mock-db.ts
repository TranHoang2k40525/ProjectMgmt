export interface UserProfileModel {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  jobTitle: string;
  roleId: string;
  roleName: string;
  isActive: boolean;
  twoFactorEnabled: boolean;
  createdAt: string;
  lastLoginAt: string;
  phoneNumber?: string | null;
  bio?: string | null;
  timezone?: string | null;
  seniorityLevel?: string | null;
  yearsOfExperience?: number | null;
}

export interface RoleModel {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystem: boolean;
  permissionCodes: string[];
}

export interface PermissionModel {
  id: string;
  code: string;
  name: string;
  category: 'Identity' | 'Planning' | 'Delivery' | 'System';
  module: string;
  description: string;
}

export interface SkillModel {
  id: string;
  name: string;
  category: string;
  description: string;
}

export interface UserSkillModel {
  id: string;
  userId: string;
  skillId: string;
  skillName: string;
  proficiencyLevel: number;
}

export interface NotificationModel {
  id: string;
  userId: string;
  title: string;
  message: string;
  type: 'SECURITY' | 'AI_SUGGESTION' | 'TASK' | 'SYSTEM';
  category: 'System' | 'Assignment' | 'Mention' | 'Security';
  isRead: boolean;
  createdAt: string;
  actionUrl?: string;
}

export interface AiModelConfig {
  id: string;
  name: string;
  modelName: string;
  provider: 'OpenAI' | 'Anthropic' | 'DeepSeek' | 'Self-Hosted';
  maxTokens: number;
  temperature: number;
  isActive: boolean;
  isEnabled: boolean;
  isDefault: boolean;
  monthlyTokenQuota: number;
}

export interface AiGenLogModel {
  id: string;
  userDisplayName: string;
  modelName: string;
  promptSnippet: string;
  tokensUsed: number;
  latencyMs: number;
  costEstimate: number;
  timestamp: string;
  status: 'Success' | 'Filtered' | 'Error';
}

export interface ActiveSessionModel {
  id: string;
  device: string;
  deviceName: string;
  browser: string;
  ipAddress: string;
  location: string;
  lastActive: string;
  isCurrent: boolean;
}

// Seed Data & State Manager
export class IdentityMockDb {
  static permissions: PermissionModel[] = [
    { id: 'perm-1', code: 'USER_READ', name: 'Xem thông tin người dùng', category: 'Identity', module: 'AUTHENTICATION', description: 'Cho phép xem danh sách và chi tiết người dùng' },
    { id: 'perm-2', code: 'USER_WRITE', name: 'Quản lý người dùng', category: 'Identity', module: 'AUTHENTICATION', description: 'Cho phép thêm, sửa, khóa tài khoản người dùng' },
    { id: 'perm-3', code: 'ROLE_MANAGE', name: 'Quản lý Vai trò & Phân quyền', category: 'Identity', module: 'RBAC_ADMIN', description: 'Cho phép chỉnh sửa ma trận RBAC Role & Permission' },
    { id: 'perm-4', code: 'SKILL_MANAGE', name: 'Quản lý Catalog Kỹ năng', category: 'Identity', module: 'AUTHENTICATION', description: 'Cho phép cập nhật danh mục kỹ năng hệ thống' },

    { id: 'perm-5', code: 'PROJECT_READ', name: 'Xem Dự án', category: 'Planning', module: 'PROJECT', description: 'Cho phép truy cập danh sách và chi tiết dự án' },
    { id: 'perm-6', code: 'PROJECT_CREATE', name: 'Tạo Dự án Mới', category: 'Planning', module: 'PROJECT', description: 'Cho phép khởi tạo dự án và cấu hình mặc định' },
    { id: 'perm-7', code: 'PROJECT_EDIT', name: 'Cài đặt Dự án & Workflow', category: 'Planning', module: 'PROJECT', description: 'Cho phép sửa thông tin, board và workflow' },
    { id: 'perm-8', code: 'PROJECT_DELETE', name: 'Xóa/Lưu trữ Dự án', category: 'Planning', module: 'PROJECT', description: 'Cho phép ẩn hoặc lưu trữ dự án' },
    { id: 'perm-9', code: 'SPRINT_MANAGE', name: 'Quản lý Sprint & Backlog', category: 'Planning', module: 'PROJECT', description: 'Tạo, bắt đầu và hoàn thành Sprint' },

    { id: 'perm-10', code: 'ISSUE_READ', name: 'Xem Issue / Task', category: 'Delivery', module: 'WORK_ITEM', description: 'Cho phép đọc thông tin chi tiết Issue' },
    { id: 'perm-11', code: 'ISSUE_CREATE', name: 'Tạo Task mới', category: 'Delivery', module: 'WORK_ITEM', description: 'Cho phép tạo mới Story, Task, Bug, Epic' },
    { id: 'perm-12', code: 'ISSUE_UPDATE', name: 'Cập nhật Task', category: 'Delivery', module: 'WORK_ITEM', description: 'Cho phép sửa mô tả, gán assignee, đổi priority' },
    { id: 'perm-13', code: 'ISSUE_TRANSITION', name: 'Chuyển Trạng thái Task', category: 'Delivery', module: 'WORK_ITEM', description: 'Kéo thả task qua các cột trạng thái trên Board' },
    { id: 'perm-14', code: 'ISSUE_DELETE', name: 'Xóa Task', category: 'Delivery', module: 'WORK_ITEM', description: 'Cho phép loại bỏ Task khỏi hệ thống' },

    { id: 'perm-15', code: 'SYSTEM_SETTINGS', name: 'Cấu hình Hệ thống', category: 'System', module: 'AI_GOVERNANCE', description: 'Truy cập thông tin hạ tầng và cấu hình chung' },
    { id: 'perm-16', code: 'AI_GOVERNANCE', name: 'Quản trị AI Models & Prompts', category: 'System', module: 'AI_GOVERNANCE', description: 'Quản lý mô hình AI, prompt template và log sinh AI' },
    { id: 'perm-17', code: 'AUDIT_LOGS_READ', name: 'Xem Nhật ký Hoạt động', category: 'System', module: 'AI_GOVERNANCE', description: 'Theo dõi toàn bộ Activity Logs trong hệ thống' }
  ];

  static roles: RoleModel[] = [
    {
      id: 'role-1',
      name: 'System Administrator',
      code: 'SYS_ADMIN',
      description: 'Quản trị viên toàn quyền hệ thống, quản lý tài khoản và phân quyền.',
      isSystem: true,
      permissionCodes: IdentityMockDb.permissions.map(p => p.code)
    },
    {
      id: 'role-2',
      name: 'Project Administrator',
      code: 'PROJ_ADMIN',
      description: 'Quản trị viên dự án, thiết lập workflow, board và thành viên dự án.',
      isSystem: true,
      permissionCodes: ['USER_READ', 'PROJECT_READ', 'PROJECT_CREATE', 'PROJECT_EDIT', 'SPRINT_MANAGE', 'ISSUE_READ', 'ISSUE_CREATE', 'ISSUE_UPDATE', 'ISSUE_TRANSITION', 'ISSUE_DELETE']
    },
    {
      id: 'role-3',
      name: 'Product Owner',
      code: 'PRODUCT_OWNER',
      description: 'Quản lý Yêu cầu sản phẩm, sắp xếp ưu tiên Backlog và tạo Epic/Story.',
      isSystem: false,
      permissionCodes: ['USER_READ', 'PROJECT_READ', 'SPRINT_MANAGE', 'ISSUE_READ', 'ISSUE_CREATE', 'ISSUE_UPDATE', 'ISSUE_TRANSITION']
    },
    {
      id: 'role-4',
      name: 'Scrum Master',
      code: 'SCRUM_MASTER',
      description: 'Điều phối Sprint, thúc đẩy quy trình Agile và loại bỏ điểm nghẽn.',
      isSystem: false,
      permissionCodes: ['USER_READ', 'PROJECT_READ', 'SPRINT_MANAGE', 'ISSUE_READ', 'ISSUE_CREATE', 'ISSUE_UPDATE', 'ISSUE_TRANSITION']
    },
    {
      id: 'role-5',
      name: 'Developer Engineer',
      code: 'DEVELOPER',
      description: 'Thành viên phát triển, cập nhật tiến độ công việc và kéo thả Kanban Board.',
      isSystem: false,
      permissionCodes: ['USER_READ', 'PROJECT_READ', 'ISSUE_READ', 'ISSUE_CREATE', 'ISSUE_UPDATE', 'ISSUE_TRANSITION']
    },
    {
      id: 'role-6',
      name: 'QA / Tester',
      code: 'QA_ENGINEER',
      description: 'Kiểm thử viên, tạo Bug, xác minh tiêu chuẩn nghiệm thu và review task.',
      isSystem: false,
      permissionCodes: ['USER_READ', 'PROJECT_READ', 'ISSUE_READ', 'ISSUE_CREATE', 'ISSUE_UPDATE', 'ISSUE_TRANSITION']
    },
    {
      id: 'role-7',
      name: 'Guest Viewer',
      code: 'VIEWER',
      description: 'Chỉ có quyền xem thông tin báo cáo và tiến độ dự án.',
      isSystem: true,
      permissionCodes: ['USER_READ', 'PROJECT_READ', 'ISSUE_READ']
    }
  ];

  // Data Stores (Default to empty, to be populated from backend APIs)
  static users: UserProfileModel[] = [];
  static skills: SkillModel[] = [];
  static userSkills: UserSkillModel[] = [];
  static notifications: NotificationModel[] = [];
  static aiModels: AiModelConfig[] = [];
  static aiLogs: AiGenLogModel[] = [];
  static activeSessions: ActiveSessionModel[] = [];

  static otpStorage = new Map<string, { code: string; expiresAt: number; purpose: string }>();
}
