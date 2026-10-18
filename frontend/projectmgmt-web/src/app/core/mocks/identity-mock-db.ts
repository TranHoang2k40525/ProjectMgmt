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

  static users: UserProfileModel[] = [
    {
      id: '11111111-0000-0000-0000-000000000001',
      email: 'admin@scrumai.internal',
      displayName: 'Trần Hoàng Admin',
      avatarUrl: 'https://lh3.googleusercontent.com/aida/AEtjO1XtHjbcVDz-UBjctM0LfVUGmqUeR39WQIoExjpXXhoH4Wu_I9_Qpk42NbzSi5dIoyYQuxC6PmMSr__bvB8G1iRv51zwwYHyLI9epDcSeaSJ_23pgMSoDqCavTtJMnh_VUr1XiaX_dfIJVrOpVNtqTGgwFT5FuXj3i9tQVUICLqvROYKaCksEl97aHgdr2n-KIVSDs0bZzIZ5hDOpx0AeyayjF5Y_iybOHjJnpBXyyQz40C4Rx5A_y32U-v-DZxAw_gb18RoyaUMMA',
      jobTitle: 'Principal Lead Architect',
      roleId: 'role-1',
      roleName: 'System Administrator',
      isActive: true,
      twoFactorEnabled: true,
      createdAt: '2026-01-15T08:00:00Z',
      lastLoginAt: '2026-09-19T20:45:00Z'
    },
    {
      id: '11111111-0000-0000-0000-000000000002',
      email: 'dev.nguyen@scrumai.io',
      displayName: 'Nguyễn Văn Dev',
      avatarUrl: null,
      jobTitle: 'Senior Fullstack Engineer',
      roleId: 'role-5',
      roleName: 'Developer Engineer',
      isActive: true,
      twoFactorEnabled: false,
      createdAt: '2026-02-10T09:30:00Z',
      lastLoginAt: '2026-09-19T18:12:00Z'
    },
    {
      id: '11111111-0000-0000-0000-000000000003',
      email: 'scrum.tran@scrumai.io',
      displayName: 'Trần Thị Scrum',
      avatarUrl: null,
      jobTitle: 'Agile Coach & Scrum Master',
      roleId: 'role-4',
      roleName: 'Scrum Master',
      isActive: true,
      twoFactorEnabled: true,
      createdAt: '2026-03-01T10:15:00Z',
      lastLoginAt: '2026-09-18T14:20:00Z'
    }
  ];

  static skills: SkillModel[] = [
    { id: 'sk-1', name: 'Angular 21', category: 'Frontend Framework', description: 'Angular Standalone, Signals, RxJS & Performance Optimization' },
    { id: 'sk-2', name: '.NET 10 & C#', category: 'Backend Framework', description: 'Modular Monolith, ASP.NET Core Web API, EF Core' },
    { id: 'sk-3', name: 'TypeScript', category: 'Language', description: 'Strict typing, Generics, Type Guard & ESLint' },
    { id: 'sk-4', name: 'GSAP Animation', category: 'UI / UX', description: 'Physics timeline, Canvas interactive particles, ScrollTrigger' },
    { id: 'sk-5', name: 'MySQL & Database Optimization', category: 'Database', description: 'InnoDB indexing, Partitioning, Execution Plan analysis' }
  ];

  static userSkills: UserSkillModel[] = [
    { id: 'usk-1', userId: '11111111-0000-0000-0000-000000000001', skillId: 'sk-1', skillName: 'Angular 21', proficiencyLevel: 95 },
    { id: 'usk-2', userId: '11111111-0000-0000-0000-000000000001', skillId: 'sk-2', skillName: '.NET 10 & C#', proficiencyLevel: 90 },
    { id: 'usk-3', userId: '11111111-0000-0000-0000-000000000001', skillId: 'sk-4', skillName: 'GSAP Animation', proficiencyLevel: 85 }
  ];

  static notifications: NotificationModel[] = [
    {
      id: 'notif-1',
      userId: '11111111-0000-0000-0000-000000000001',
      title: 'Hệ thống đã cập nhật thành công!',
      message: 'Phiên bản Module IdentityExperience v2.4 đã sẵn sàng với mã hóa 2FA & ma trận RBAC.',
      type: 'SYSTEM',
      category: 'System',
      isRead: false,
      createdAt: '2026-09-19T22:30:00Z'
    },
    {
      id: 'notif-2',
      userId: '11111111-0000-0000-0000-000000000001',
      title: 'Phân công Nhiệm vụ Mới',
      message: 'Bạn được gán làm Lead cho Sprint 42: "Xây dựng GSAP dynamic background & OTP flow".',
      type: 'TASK',
      category: 'Assignment',
      isRead: false,
      createdAt: '2026-09-19T21:15:00Z',
      actionUrl: '/projects'
    },
    {
      id: 'notif-3',
      userId: '11111111-0000-0000-0000-000000000001',
      title: 'Cảnh báo Bảo mật Tài khoản',
      message: 'Tài khoản của bạn vừa đăng nhập thành công từ vị trí Windows Desktop (IP: 127.0.0.1).',
      type: 'SECURITY',
      category: 'Security',
      isRead: true,
      createdAt: '2026-09-19T18:00:00Z'
    }
  ];

  static aiModels: AiModelConfig[] = [
    { id: 'aim-1', name: 'GPT-4o Omnimodal', modelName: 'GPT-4o Omnimodal', provider: 'OpenAI', maxTokens: 8192, temperature: 0.3, isActive: true, isEnabled: true, isDefault: true, monthlyTokenQuota: 500000 },
    { id: 'aim-2', name: 'Claude 3.5 Sonnet', modelName: 'Claude 3.5 Sonnet', provider: 'Anthropic', maxTokens: 4096, temperature: 0.2, isActive: true, isEnabled: true, isDefault: false, monthlyTokenQuota: 300000 },
    { id: 'aim-3', name: 'DeepSeek R1 Reasoning', modelName: 'DeepSeek R1 Reasoning', provider: 'DeepSeek', maxTokens: 8192, temperature: 0.1, isActive: true, isEnabled: true, isDefault: false, monthlyTokenQuota: 1000000 }
  ];

  static aiLogs: AiGenLogModel[] = [
    { id: 'log-1', userDisplayName: 'Trần Hoàng Admin', modelName: 'GPT-4o Omnimodal', promptSnippet: 'Tự động phân tích điểm nghẽn Sprint 41 và gợi ý phân bổ thành viên', tokensUsed: 1420, latencyMs: 840, costEstimate: 0.0142, timestamp: '2026-09-19T22:10:00Z', status: 'Success' },
    { id: 'log-2', userDisplayName: 'Nguyễn Văn Dev', modelName: 'Claude 3.5 Sonnet', promptSnippet: 'Tạo tự động Acceptance Criteria cho Story #PROJ-104', tokensUsed: 890, latencyMs: 620, costEstimate: 0.0089, timestamp: '2026-09-19T21:40:00Z', status: 'Success' }
  ];

  static activeSessions: ActiveSessionModel[] = [
    { id: 'sess-1', device: 'Windows PC Desktop', deviceName: 'Windows PC Desktop', browser: 'Chrome 128.0 (Windows 11)', ipAddress: '127.0.0.1', location: 'Hà Nội, Việt Nam', lastActive: 'Hiện tại', isCurrent: true },
    { id: 'sess-2', device: 'MacBook Pro 16"', deviceName: 'MacBook Pro 16"', browser: 'Safari 17.4 (macOS)', ipAddress: '118.69.182.10', location: 'Hồ Chí Minh, Việt Nam', lastActive: '2 giờ trước', isCurrent: false }
  ];

  static otpStorage = new Map<string, { code: string; expiresAt: number; purpose: string }>();
}
