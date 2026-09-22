const fs = require('fs');
const path = require('path');
let docx;
try {
  docx = require('docx');
} catch (e) {
  docx = require(path.join(__dirname, '../frontend/projectmgmt-web/node_modules/docx'));
}
const { 
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, 
  HeadingLevel, AlignmentType, BorderStyle, WidthType, ShadingType,
  Header, Footer, PageNumber, NumberFormat
} = docx;

console.log('Generating Precision 128 API Specifications (HTML & DOCX)...');

const docsDir = path.join(__dirname);
const htmlPath = path.join(docsDir, 'Bao-cao-Dac-ta-API-He-thong-ProjectMgmt.html');
const docxPath = path.join(docsDir, 'Bao-cao-Dac-ta-API-He-thong-ProjectMgmt.docx');

// Complete Spec Meta
const specMeta = {
  title: "BÁO CÁO ĐẶC TẢ API CHUẨN MỰC TOÀN DIỆN HỆ THỐNG - DỰ ÁN PROJECTMGMT (HUCE SCRUM PLATFORM)",
  author: "Nhóm 7 — Tran Hoang (IdentityExperience), Thế Hoài (Planning), Huy Hoàng (DeliveryIntelligence)",
  version: "2.1.0 (Strict Frontend-Backend Alignment Release)",
  date: "2026-09-22",
  totalEndpoints: 128,
  totalTables: 55,
  totalViews: 4,
  conventions: {
    baseUrl: "https://api.projectmgmt.huce.edu.vn/api/v1",
    authHeader: "Authorization: Bearer <jwt_access_token>",
    naming: [
      { rule: "URL Endpoint", convention: "kebab-case danh từ số nhiều (plural)", example: "/api/v1/work-items, /api/v1/workflow-transitions" },
      { rule: "Query Parameter", convention: "camelCase", example: "pageSize=20&isRead=false&searchQuery=CRM" },
      { rule: "JSON Body Request/Response", convention: "camelCase đồng nhất 100% 3 module", example: "issueKey, storyPoints, createdById, acceptanceCriteria" },
      { rule: "Database Mapping", convention: "snake_case (MySQL) / PascalCase (.NET EF Core)", example: "issue_key -> issueKey, user_id -> userId" },
      { rule: "Datetime", convention: "ISO 8601 UTC string format", example: "2026-09-22T19:54:00.000Z" },
      { rule: "UUID / GUID", convention: "UUID v4 string (36 ký tự lowercase)", example: "01a09072-8caa-7743-8a5f-be64f3d5c3b8" }
    ],
    standardResponse: {
      single: `{
  "success": true,
  "code": "200",
  "message": "Thao tác thành công",
  "data": { ... }
}`,
      paged: `{
  "success": true,
  "code": "200",
  "message": "Thao tác thành công",
  "data": {
    "items": [ ... ],
    "totalCount": 128,
    "page": 1,
    "pageSize": 20,
    "totalPages": 7
  }
}`
    },
    errorStandard: `{
  "type": "https://api.projectmgmt.huce.edu.vn/errors/invalid-workflow-transition",
  "title": "Chuyển đổi trạng thái Workflow không hợp lệ",
  "status": 400,
  "detail": "Không thể chuyển trạng thái Issue CRM-102 từ 'In Progress' trực tiếp sang 'Done' khi chưa hoàn thành 2 Acceptance Criteria bắt buộc.",
  "instance": "/api/v1/issues/CRM-102/status",
  "code": "INVALID_WORKFLOW_TRANSITION",
  "errors": {
    "targetStatusId": ["Chuyển đổi không nằm trong danh sách WorkflowTransition được cấu hình cho dự án."],
    "acceptanceCriteria": ["Vẫn còn 2 tiêu chí AC chưa được xác nhận IsMet = true."]
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00"
}`
  }
};

// Build Modules Data with 1-to-1 Frontend Screen & DB Mapping
const modules = [
  {
    id: "module-1",
    name: "Module 1: IdentityExperience (Tài Khoản, Bảo Mật, Hồ Sơ, Phân Quyền Vai Trò Dự Án RBAC, Kỹ Năng, Thông Báo & AI 1 Core)",
    owner: "Trần Văn Hoàng (A)",
    dbTables: "16 Bảng DB (`User`, `UserProfile`, `ExternalLogin`, `OtpCode`, `RefreshToken`, `Role`, `Permission`, `RolePermission`, `UserRole`, `SkillCatalog`, `UserSkill`, `AiModel`, `AiPromptTemplate`, `AiGenerationLog`, `AiSuggestedTask`, `Notification`)",
    endpoints: [
      // 1.1 Auth & Credential Management
      { method: "POST", path: "/api/v1/auth/register", summary: "Đăng ký tài khoản mới", requestBody: { email: "string (email format)", password: "string (min 8 chars)", fullName: "string", phoneNumber: "string" }, responseBody: { userId: "UUID", email: "string", status: "PendingVerification" }, dbMapping: "Bảng `User`, `UserProfile`, `OtpCode`" },
      { method: "POST", path: "/api/v1/auth/login", summary: "Đăng nhập & Cấp JWT Token", requestBody: { email: "string", password: "string" }, responseBody: { accessToken: "JWT string", refreshToken: "UUID v4", expiresInSeconds: 900, user: { userId: "UUID", fullName: "string", roles: ["string"] } }, dbMapping: "Bảng `User`, `RefreshToken`" },
      { method: "POST", path: "/api/v1/auth/refresh-token", summary: "Làm mới Access Token (Rotation)", requestBody: { refreshToken: "string (UUID v4)" }, responseBody: { accessToken: "JWT string", refreshToken: "New rotated UUID", expiresInSeconds: 900 }, dbMapping: "Bảng `RefreshToken`" },
      { method: "POST", path: "/api/v1/auth/logout", summary: "Đăng xuất & Thu hồi Refresh Token", requestBody: { refreshToken: "string" }, responseBody: { message: "Đã thu hồi token an toàn" }, dbMapping: "Bảng `RefreshToken` (IsRevoked = 1)" },
      { method: "POST", path: "/api/v1/auth/forgot-password", summary: "Gửi mã OTP Quên Mật Khẩu qua email", requestBody: { email: "string" }, responseBody: { otpId: "UUID", expiresAt: "ISO 8601" }, dbMapping: "Bảng `OtpCode` (Purpose = ResetPassword)" },
      { method: "POST", path: "/api/v1/auth/reset-password", summary: "Đặt lại Mật Khẩu mới bằng mã OTP", requestBody: { email: "string", code: "string (6 digits)", newPassword: "string (min 8 chars)" }, responseBody: { passwordReset: true }, dbMapping: "Bảng `User` (PasswordHash), `OtpCode` (IsUsed = 1)" },
      { method: "PUT", path: "/api/v1/users/me/password", summary: "Đổi Mật Khẩu cá nhân", requestBody: { currentPassword: "string", newPassword: "string" }, responseBody: { passwordChanged: true }, dbMapping: "Bảng `User` (PasswordHash, SecurityStamp)" },
      { method: "POST", path: "/api/v1/auth/otp/send", summary: "Gửi mã OTP (VerifyEmail / ResetPassword / Login2FA)", requestBody: { email: "string", purpose: "VerifyEmail | ResetPassword | Login2FA" }, responseBody: { otpId: "UUID", expiresAt: "ISO 8601" }, dbMapping: "Bảng `OtpCode`" },
      { method: "POST", path: "/api/v1/auth/otp/verify", summary: "Xác minh mã OTP", requestBody: { email: "string", code: "string", purpose: "string" }, responseBody: { verified: true }, dbMapping: "Bảng `OtpCode`" },
      { method: "POST", path: "/api/v1/auth/external-login", summary: "Đăng nhập OAuth2 (Google/Microsoft)", requestBody: { provider: "Google | Microsoft", providerKey: "string", email: "string", fullName: "string" }, responseBody: { accessToken: "JWT", refreshToken: "UUID", user: "Object" }, dbMapping: "Bảng `ExternalLogin`, `User`" },

      // 1.2 Profile & Skill Matrix
      { method: "GET", path: "/api/v1/users/me", summary: "Lấy hồ sơ cá nhân hiện tại", requestBody: null, responseBody: { userId: "UUID", email: "string", fullName: "string", avatarUrl: "string", bio: "string", timezone: "Asia/Ho_Chi_Minh", jobTitle: "string", seniorityLevel: "Senior", yearsOfExperience: 4.5, skills: [{ skillId: "UUID", skillName: "Angular", proficiencyLevel: 4, verified: true }] }, dbMapping: "Bảng `User`, `UserProfile`, `UserSkill`" },
      { method: "PUT", path: "/api/v1/users/me/profile", summary: "Cập nhật hồ sơ cá nhân", requestBody: { fullName: "string", phoneNumber: "string", bio: "string", timezone: "string", jobTitle: "string", seniorityLevel: "Intern | Junior | Middle | Senior | Lead", yearsOfExperience: 3.5 }, responseBody: { userId: "UUID", updatedAt: "ISO 8601" }, dbMapping: "Bảng `UserProfile`" },
      { method: "POST", path: "/api/v1/users/me/avatar", summary: "Tải lên ảnh đại diện Avatar", requestBody: "Multipart FormData (avatar: Binary file)", responseBody: { avatarUrl: "https://storage.../avatar.jpg" }, dbMapping: "Bảng `UserProfile` (AvatarUrl)" },
      { method: "GET", path: "/api/v1/users/for-you", summary: "Dữ liệu trang cá nhân For You (Assigned & Recent Work)", requestBody: null, responseBody: { assignedIssues: [{ issueKey: "CRM-102", title: "string" }], recentIssues: [{ issueKey: "CRM-99", title: "string" }], attentionFocus: ["string"] }, dbMapping: "Gộp dữ liệu từ Delivery & Identity (`User`, `Issue`)" },
      { method: "GET", path: "/api/v1/skills", summary: "Tra cứu Danh mục Kỹ năng (SkillCatalog)", queryParams: "category=Backend|Frontend|Database|QA|DevOps&searchQuery=string", requestBody: null, responseBody: { items: [{ skillId: "UUID", code: "dotnet", name: ".NET / C#", category: "Backend", isActive: true }] }, dbMapping: "Bảng `SkillCatalog`" },
      { method: "POST", path: "/api/v1/skills", summary: "Thêm mới kỹ năng vào Danh mục chuẩn", requestBody: { code: "string", name: "string", category: "string" }, responseBody: { skillId: "UUID", code: "string" }, dbMapping: "Bảng `SkillCatalog`" },
      { method: "GET", path: "/api/v1/users/{userId}/skills", summary: "Lấy ma trận kỹ năng của thành viên", requestBody: null, responseBody: { userId: "UUID", skills: [{ skillId: "UUID", code: "sql", name: "SQL", proficiencyLevel: 5, yearsOfExperience: 3.0, isSelfDeclared: false }] }, dbMapping: "Bảng `UserSkill`, `SkillCatalog`" },
      { method: "PUT", path: "/api/v1/users/me/skills", summary: "Cập nhật danh mục kỹ năng cá nhân", requestBody: { skills: [{ skillId: "UUID", proficiencyLevel: "1-5", yearsOfExperience: 2.0 }] }, responseBody: { updatedCount: 3 }, dbMapping: "Bảng `UserSkill`" },
      { method: "PUT", path: "/api/v1/users/{userId}/skills/{skillId}/verify", summary: "Tech Lead xác nhận độ tin cậy kỹ năng", requestBody: { verified: true, level: "1-5" }, responseBody: { isSelfDeclared: false }, dbMapping: "Bảng `UserSkill` (`IsSelfDeclared = 0`)" },

      // 1.3 RBAC Roles & Scoped Project Permissions (UserRole)
      { method: "GET", path: "/api/v1/roles", summary: "Lấy danh sách Vai trò (Roles)", queryParams: "scope=System|Organization|Project", requestBody: null, responseBody: { items: [{ roleId: "UUID", name: "ScrumMaster", scope: "Project", isSystem: true, description: "string" }] }, dbMapping: "Bảng `Role`" },
      { method: "POST", path: "/api/v1/roles", summary: "Tạo Vai trò phân quyền mới", requestBody: { name: "string", scope: "Organization | Project", description: "string", permissionIds: ["UUID"] }, responseBody: { roleId: "UUID", name: "string" }, dbMapping: "Bảng `Role`, `RolePermission`" },
      { method: "GET", path: "/api/v1/permissions", summary: "Lấy danh mục Quyền hạn (Permissions)", requestBody: null, responseBody: { items: [{ permissionId: "UUID", code: "issue.create", grouping: "Issue", description: "string" }] }, dbMapping: "Bảng `Permission`" },
      { method: "PUT", path: "/api/v1/roles/{roleId}/permissions", summary: "Cập nhật ma trận quyền cho Role", requestBody: { permissionIds: ["array of UUID"] }, responseBody: { roleId: "UUID", updatedPermissionCount: 15 }, dbMapping: "Bảng `RolePermission`" },
      { method: "GET", path: "/api/v1/users/{userId}/roles", summary: "Lấy danh sách vai trò có phạm vi của User", queryParams: "scopeType=Project|Organization&scopeId=UUID", requestBody: null, responseBody: { roles: [{ userRoleId: "UUID", roleName: "ProjectManager", scopeType: "Project", scopeId: "UUID" }] }, dbMapping: "Bảng `UserRole`, `Role`" },
      { method: "POST", path: "/api/v1/users/{userId}/roles", summary: "Gán Vai trò theo phạm vi cho User", requestBody: { roleId: "UUID", scopeType: "Project | Organization", scopeId: "UUID" }, responseBody: { userRoleId: "UUID", grantedAt: "ISO 8601" }, dbMapping: "Bảng `UserRole`" },
      { method: "DELETE", path: "/api/v1/users/{userId}/roles/{userRoleId}", summary: "Gỡ Vai trò khỏi User", requestBody: null, responseBody: { success: true }, dbMapping: "Bảng `UserRole`" },

      // 1.4 Project Member Role Assignment (Product Owner, Scrum Master, Dev, Tech Lead, Viewer, PM)
      { method: "GET", path: "/api/v1/projects/{projectKey}/members", summary: "Danh sách thành viên & Vai trò trong Dự án", requestBody: null, responseBody: { members: [{ userId: "UUID", fullName: "Trần Văn Hoàng", email: "string", avatarUrl: "string", roleId: "UUID", roleName: "ScrumMaster", grantedAt: "ISO 8601" }] }, dbMapping: "Bảng `UserRole` (`ScopeType = 'Project'`, `ScopeId = ProjectId`), `User`, `Role`" },
      { method: "POST", path: "/api/v1/projects/{projectKey}/members", summary: "Mời / Phân công thành viên mới vào Dự án", requestBody: { userId: "UUID", roleId: "UUID (e.g. ProductOwner, ScrumMaster, Developer)" }, responseBody: { userRoleId: "UUID", roleName: "ProductOwner" }, dbMapping: "Bảng `UserRole`" },
      { method: "PUT", path: "/api/v1/projects/{projectKey}/members/{userId}/role", summary: "Thay đổi Vai trò chức danh của thành viên trong Dự án", requestBody: { newRoleId: "UUID (Switch Developer -> ScrumMaster)" }, responseBody: { userId: "UUID", newRoleName: "ScrumMaster" }, dbMapping: "Bảng `UserRole`" },
      { method: "DELETE", path: "/api/v1/projects/{projectKey}/members/{userId}", summary: "Gỡ thành viên khỏi Dự án", requestBody: null, responseBody: { removed: true }, dbMapping: "Bảng `UserRole`" },

      // 1.5 Notifications & Realtime
      { method: "GET", path: "/api/v1/notifications", summary: "Hộp thư thông báo Inbox (Paged)", queryParams: "isRead=boolean&page=1&pageSize=20", requestBody: null, responseBody: { items: [{ notificationId: "UUID", type: "IssueAssigned", title: "string", content: "string", entityType: "Issue", entityId: "UUID", isRead: false, createdAt: "ISO 8601" }], unreadCount: 5 }, dbMapping: "Bảng `Notification`" },
      { method: "GET", path: "/api/v1/notifications/unread-count", summary: "Lấy số lượng thông báo chưa đọc", requestBody: null, responseBody: { unreadCount: 5 }, dbMapping: "Bảng `Notification`" },
      { method: "PUT", path: "/api/v1/notifications/{id}/read", summary: "Đánh dấu 1 thông báo đã đọc", requestBody: null, responseBody: { notificationId: "UUID", isRead: true }, dbMapping: "Bảng `Notification`" },
      { method: "PUT", path: "/api/v1/notifications/read-all", summary: "Đánh dấu tất cả thông báo đã đọc", requestBody: null, responseBody: { readCount: 5 }, dbMapping: "Bảng `Notification`" },
      { method: "WEBSOCKET", path: "/hubs/notifications", summary: "SignalR WebSocket Hub Push Realtime", requestBody: "Header Bearer JWT Token", responseBody: "Event push: ReceiveNotification(NotificationDTO)", dbMapping: "Lớp Transport Realtime SignalR" },

      // 1.6 AI 1 Sub-task Breakdown Engine & Governance
      { method: "POST", path: "/api/v1/ai/breakdown/generate", summary: "AI 1: Đề xuất phân rã User Story thành Sub-tasks", requestBody: { issueId: "UUID", storyTitle: "string", storyDescription: "string", projectContext: "string" }, responseBody: { generationId: "UUID", status: "Completed | Processing", suggestedSubTasks: [{ tempId: "temp_1", title: "string", description: "string", estimatedHours: 3, acceptanceCriteria: ["string"] }] }, dbMapping: "Bảng `AiGenerationLog`, `AiSuggestedTask`" },
      { method: "GET", path: "/api/v1/ai/breakdown/{generationId}", summary: "Lấy chi tiết kết quả đề xuất AI 1", requestBody: null, responseBody: { generationId: "UUID", modelVersion: "qwen2.5:3b-instruct", promptVersion: "v1.0", suggestedSubTasks: ["array of TaskDTO"] }, dbMapping: "Bảng `AiGenerationLog`, `AiSuggestedTask`" },
      { method: "POST", path: "/api/v1/ai/breakdown/apply", summary: "AI 1: Chấp nhận & tạo Sub-tasks thật", requestBody: { generationId: "UUID", parentIssueId: "UUID", selectedSubTasks: [{ title: "string", description: "string", estimatedHours: 4, acceptanceCriteria: ["string"] }] }, responseBody: { createdSubTasks: [{ issueId: "UUID", issueKey: "CRM-105", title: "string" }] }, dbMapping: "Gửi UseCase sang DeliveryIntelligence (`Issue`, `AcceptanceCriteria`)" },
      { method: "POST", path: "/api/v1/ai/breakdown/feedback", summary: "AI 1: Ghi nhận phản hồi đánh giá gợi ý", requestBody: { generationId: "UUID", suggestedTaskId: "UUID", userAction: "Kept | Edited | Rejected", finalSummary: "string", editDistanceRatio: 0.15 }, responseBody: { feedbackLogged: true }, dbMapping: "Bảng `AiSuggestedTask` (UserAction, EditDistanceRatio)" },
      { method: "GET", path: "/api/v1/ai/prompt-templates", summary: "Danh sách Prompt Templates", queryParams: "taskType=Breakdown|Assignment", requestBody: null, responseBody: { items: [{ promptId: "UUID", code: "breakdown.system", version: 1, isActive: true, jsonSchema: "Object" }] }, dbMapping: "Bảng `AiPromptTemplate`" },
      { method: "POST", path: "/api/v1/ai/prompt-templates", summary: "Tạo mới Prompt Template kèm JSON Schema", requestBody: { code: "string", taskType: "Breakdown", systemPrompt: "string", userTemplate: "string", jsonSchema: "Object" }, responseBody: { promptId: "UUID", version: 1 }, dbMapping: "Bảng `AiPromptTemplate`" },
      { method: "PUT", path: "/api/v1/ai/prompt-templates/{id}/activate", summary: "Kích hoạt phiên bản Prompt Template", requestBody: null, responseBody: { isActive: true }, dbMapping: "Bảng `AiPromptTemplate`" }
    ]
  },
  {
    id: "module-2",
    name: "Module 2: Planning (Không Gian Dự Án, Cấu Hình Workflow & Board, Sprint, Capacity & AI 2 Phân Công)",
    owner: "Nguyễn Thế Hoài (B)",
    dbTables: "18 Bảng DB (`Organization`, `Project`, `ProjectComponent`, `ProjectVersion`, `WorkflowStatus`, `WorkflowTransition`, `IssueType`, `Priority`, `Board`, `BoardColumn`, `Sprint`, `SprintSnapshot`, `SprintMemberCapacity`, `UserWorkloadSnapshot`, `UserPerformanceMetric`, `AiAssignmentRun`, `AiAssignmentCandidate`, `AiAssignmentDecision`)",
    endpoints: [
      // 2.1 Workspace & Organization
      { method: "GET", path: "/api/v1/organizations", summary: "Danh sách Tổ chức (Workspace)", queryParams: "searchQuery=string", requestBody: null, responseBody: { items: [{ orgId: "UUID", name: "HUCE University", slug: "huce-edu", ownerId: "UUID" }] }, dbMapping: "Bảng `Organization`" },
      { method: "POST", path: "/api/v1/organizations", summary: "Tạo Tổ chức mới", requestBody: { name: "string", slug: "string (unique)" }, responseBody: { orgId: "UUID", slug: "string" }, dbMapping: "Bảng `Organization`" },
      { method: "GET", path: "/api/v1/organizations/{orgId}/members", summary: "Danh sách thành viên Tổ chức", requestBody: null, responseBody: { members: [{ userId: "UUID", fullName: "string", roleName: "OrgOwner" }] }, dbMapping: "Bảng `UserRole`, `User`" },
      { method: "POST", path: "/api/v1/organizations/{orgId}/members/invite", summary: "Mời thành viên vào Tổ chức", requestBody: { email: "string", roleId: "UUID" }, responseBody: { inviteSent: true }, dbMapping: "Bảng `UserRole`" },

      // 2.2 Project Management
      { method: "GET", path: "/api/v1/projects", summary: "Danh sách Không gian Dự án", queryParams: "orgId=UUID&searchQuery=string", requestBody: null, responseBody: { items: [{ projectId: "UUID", projectKey: "CRM", name: "Hệ thống CRM", leadUserName: "Trần Văn Hoàng", activeSprintName: "Sprint 12" }] }, dbMapping: "Bảng `Project`" },
      { method: "POST", path: "/api/v1/projects", summary: "Tạo Không gian Dự án mới", requestBody: { orgId: "UUID", projectKey: "PROJ", name: "string", description: "string", leadUserId: "UUID" }, responseBody: { projectId: "UUID", projectKey: "PROJ", defaultBoardId: "UUID" }, dbMapping: "Bảng `Project`, `WorkflowStatus`, `Board`, `BoardColumn`" },
      { method: "GET", path: "/api/v1/projects/{projectKey}", summary: "Chi tiết Không gian Dự án", requestBody: null, responseBody: { projectId: "UUID", projectKey: "CRM", name: "string", leadUserId: "UUID", issueCounter: 105 }, dbMapping: "Bảng `Project`" },
      { method: "PUT", path: "/api/v1/projects/{projectKey}", summary: "Cập nhật thông tin Dự án", requestBody: { name: "string", description: "string", leadUserId: "UUID" }, responseBody: { projectKey: "CRM", updatedAt: "ISO 8601" }, dbMapping: "Bảng `Project`" },
      { method: "DELETE", path: "/api/v1/projects/{projectKey}", summary: "Lưu trữ / Xóa mềm Dự án", requestBody: null, responseBody: { isDeleted: true }, dbMapping: "Bảng `Project` (`IsDeleted = 1`)" },
      { method: "GET", path: "/api/v1/projects/{projectKey}/summary", summary: "Tổng quan thống kê Dashboard Dự án", requestBody: null, responseBody: { projectKey: "CRM", activeSprint: { sprintId: "UUID", name: "Sprint 12", completedPoints: 24, totalPoints: 40 }, statusCounts: { toDo: 10, inProgress: 8, done: 24 } }, dbMapping: "View `vw_SprintVelocity` và bảng `Project`" },

      // 2.3 Project Components & Versions
      { method: "GET", path: "/api/v1/projects/{projectKey}/components", summary: "Danh sách Component Dự án", requestBody: null, responseBody: { items: [{ componentId: "UUID", name: "Backend API", leadUserId: "UUID", defaultSkillName: "ASP.NET Core" }] }, dbMapping: "Bảng `ProjectComponent`" },
      { method: "POST", path: "/api/v1/projects/{projectKey}/components", summary: "Tạo Component mới", requestBody: { name: "string", description: "string", leadUserId: "UUID", defaultSkillId: "UUID" }, responseBody: { componentId: "UUID", name: "string" }, dbMapping: "Bảng `ProjectComponent`" },
      { method: "PUT", path: "/api/v1/components/{componentId}", summary: "Cập nhật Component", requestBody: { name: "string", description: "string", leadUserId: "UUID", defaultSkillId: "UUID" }, responseBody: { componentId: "UUID" }, dbMapping: "Bảng `ProjectComponent`" },
      { method: "DELETE", path: "/api/v1/components/{componentId}", summary: "Xóa Component", requestBody: null, responseBody: { success: true }, dbMapping: "Bảng `ProjectComponent`" },
      { method: "GET", path: "/api/v1/projects/{projectKey}/versions", summary: "Danh sách Fix Versions / Releases", requestBody: null, responseBody: { items: [{ versionId: "UUID", name: "v1.2.0", startDate: "2026-09-01", releaseDate: "2026-09-30", isReleased: false }] }, dbMapping: "Bảng `ProjectVersion`" },
      { method: "POST", path: "/api/v1/projects/{projectKey}/versions", summary: "Tạo Version Release mới", requestBody: { name: "v1.3.0", description: "string", startDate: "2026-10-01", releaseDate: "2026-10-15" }, responseBody: { versionId: "UUID" }, dbMapping: "Bảng `ProjectVersion`" },
      { method: "PUT", path: "/api/v1/versions/{versionId}/release", summary: "Chốt phát hành Release Version", requestBody: { isReleased: true, releaseDate: "2026-09-22" }, responseBody: { versionId: "UUID", isReleased: true }, dbMapping: "Bảng `ProjectVersion`" },

      // 2.4 Workflow Statuses & Transitions
      { method: "GET", path: "/api/v1/projects/{projectKey}/statuses", summary: "Danh sách Trạng thái Workflow", requestBody: null, responseBody: { items: [{ statusId: "UUID", name: "In Progress", category: "InProgress", colorHex: "#0052CC", orderIndex: 2, isInitial: false }] }, dbMapping: "Bảng `WorkflowStatus`" },
      { method: "POST", path: "/api/v1/projects/{projectKey}/statuses", summary: "Tạo trạng thái Workflow mới", requestBody: { name: "In QA Review", category: "InProgress", colorHex: "#FFAB00", orderIndex: 3 }, responseBody: { statusId: "UUID" }, dbMapping: "Bảng `WorkflowStatus`" },
      { method: "PUT", path: "/api/v1/statuses/{statusId}", summary: "Sửa trạng thái Workflow", requestBody: { name: "string", colorHex: "string", orderIndex: 4 }, responseBody: { statusId: "UUID" }, dbMapping: "Bảng `WorkflowStatus`" },
      { method: "GET", path: "/api/v1/projects/{projectKey}/transitions", summary: "Danh sách luật chuyển trạng thái", requestBody: null, responseBody: { items: [{ transitionId: "UUID", fromStatusId: "UUID", toStatusId: "UUID", name: "Gửi QA", requiredPermissionCode: "issue.transition" }] }, dbMapping: "Bảng `WorkflowTransition`" },
      { method: "POST", path: "/api/v1/projects/{projectKey}/transitions", summary: "Tạo luật chuyển trạng thái hợp lệ", requestBody: { fromStatusId: "UUID", toStatusId: "UUID", name: "string", requiredPermissionCode: "string" }, responseBody: { transitionId: "UUID" }, dbMapping: "Bảng `WorkflowTransition`" },
      { method: "DELETE", path: "/api/v1/transitions/{transitionId}", summary: "Xóa luật chuyển trạng thái", requestBody: null, responseBody: { success: true }, dbMapping: "Bảng `WorkflowTransition`" },

      // 2.5 Boards & Board Columns Settings
      { method: "GET", path: "/api/v1/projects/{projectKey}/boards", summary: "Danh sách Bảng công việc (Board)", requestBody: null, responseBody: { items: [{ boardId: "UUID", name: "Main Scrum Board", type: "Scrum", isDefault: true }] }, dbMapping: "Bảng `Board`" },
      { method: "POST", path: "/api/v1/projects/{projectKey}/boards", summary: "Tạo Bảng công việc mới", requestBody: { name: "Kanban Support Board", type: "Kanban" }, responseBody: { boardId: "UUID" }, dbMapping: "Bảng `Board`" },
      { method: "GET", path: "/api/v1/boards/{boardId}/columns", summary: "Cấu hình cột của Bảng", requestBody: null, responseBody: { columns: [{ columnId: "UUID", statusId: "UUID", name: "Đang Làm", orderIndex: 2, wipLimit: 5 }] }, dbMapping: "Bảng `BoardColumn`, `WorkflowStatus`" },
      { method: "PUT", path: "/api/v1/boards/{boardId}/columns", summary: "Cập nhật thứ tự cột & Giới hạn WIP", requestBody: { columns: [{ columnId: "UUID", statusId: "UUID", name: "string", orderIndex: 1, wipLimit: 4 }] }, responseBody: { updatedCount: 4 }, dbMapping: "Bảng `BoardColumn`" },

      // 2.6 Sprint Lifecycle & Burndown/Velocity
      { method: "GET", path: "/api/v1/projects/{projectKey}/sprints", summary: "Danh sách Sprints", queryParams: "status=Planned|Active|Completed", requestBody: null, responseBody: { items: [{ sprintId: "UUID", name: "Sprint 12", status: "Active", startDate: "2026-09-15", endDate: "2026-09-29" }] }, dbMapping: "Bảng `Sprint`" },
      { method: "POST", path: "/api/v1/projects/{projectKey}/sprints", summary: "Tạo Sprint mới", requestBody: { name: "Sprint 13", goal: "Hoàn thiện API backend" }, responseBody: { sprintId: "UUID", status: "Planned" }, dbMapping: "Bảng `Sprint`" },
      { method: "PUT", path: "/api/v1/sprints/{sprintId}", summary: "Cập nhật Sprint", requestBody: { name: "string", goal: "string", startDate: "date", endDate: "date" }, responseBody: { sprintId: "UUID" }, dbMapping: "Bảng `Sprint`" },
      { method: "POST", path: "/api/v1/sprints/{sprintId}/start", summary: "Bắt đầu Sprint (Active Guard)", requestBody: { startDate: "date", endDate: "date", goal: "string" }, responseBody: { sprintId: "UUID", status: "Active", committedPoints: 35 }, dbMapping: "Bảng `Sprint` (ActiveGuard constraint), `SprintSnapshot`" },
      { method: "POST", path: "/api/v1/sprints/{sprintId}/complete", summary: "Hoàn thành & Đóng Sprint", requestBody: { moveUnfinishedToSprintId: "UUID (or null for backlog)" }, responseBody: { sprintId: "UUID", status: "Completed", movedIssuesCount: 3 }, dbMapping: "Bảng `Sprint`, `SprintSnapshot`" },
      { method: "GET", path: "/api/v1/sprints/{sprintId}/burndown", summary: "Dữ liệu biểu đồ Burndown Chart", requestBody: null, responseBody: { sprintId: "UUID", snapshots: [{ snapshotDate: "2026-09-16", totalPoints: 40, remainingPoints: 32, completedPoints: 8, addedPoints: 2 }] }, dbMapping: "Bảng `SprintSnapshot`" },
      { method: "GET", path: "/api/v1/projects/{projectKey}/velocity", summary: "Dữ liệu biểu đồ Velocity Chart", requestBody: null, responseBody: { sprints: [{ sprintName: "Sprint 10", committedPoints: 30, completedPoints: 28 }, { sprintName: "Sprint 11", committedPoints: 35, completedPoints: 35 }] }, dbMapping: "View `vw_SprintVelocity`" },

      // 2.7 Capacity & Workload
      { method: "GET", path: "/api/v1/sprints/{sprintId}/capacity", summary: "Năng lực thành viên trong Sprint", requestBody: null, responseBody: { members: [{ userId: "UUID", fullName: "Trần Văn Hoàng", capacityPoints: 10, availableHours: 35, note: "On-call 2 ngày" }] }, dbMapping: "Bảng `SprintMemberCapacity`" },
      { method: "PUT", path: "/api/v1/sprints/{sprintId}/capacity/{userId}", summary: "Cập nhật Capacity từng người", requestBody: { capacityPoints: 12, availableHours: 40, note: "string" }, responseBody: { userId: "UUID", updated: true }, dbMapping: "Bảng `SprintMemberCapacity`" },
      { method: "GET", path: "/api/v1/projects/{projectKey}/workload-snapshots", summary: "Lấy snapshot tải công việc hiện tại", requestBody: null, responseBody: { workloads: [{ userId: "UUID", openPoints: 15, inProgressCount: 2, utilizationRatio: 1.25 }] }, dbMapping: "View `vw_UserActiveWorkload` & bảng `UserWorkloadSnapshot`" },

      // 2.8 AI 2 Engine: Smart Member Assignment
      { method: "POST", path: "/api/v1/ai/smart-assign/recommend", summary: "AI 2: Đề xuất xếp hạng phân công thành viên", requestBody: { issueId: "UUID", projectKey: "CRM", requiredSkillIds: ["UUID"], estimatedHours: 8 }, responseBody: { runId: "UUID", issueId: "UUID", asOfTime: "ISO 8601", candidates: [{ rank: 1, userId: "UUID", fullName: "Trần Văn Hoàng", totalScore: 0.885, breakdown: { loadBalanceScore: 0.9, skillMatchScore: 0.95, historyScore: 0.8, capacityScore: 0.75 }, isColdStart: false, explanation: "Khớp 95% kỹ năng Angular, tải làm việc hiện tại 60%" }] }, dbMapping: "Bảng `AiAssignmentRun`, `AiAssignmentCandidate`" },
      { method: "GET", path: "/api/v1/ai/smart-assign/runs/{runId}", summary: "Lấy kết quả chi tiết lượt chạy AI 2", requestBody: null, responseBody: { runId: "UUID", strategy: "WeightedScore", weights: { load: 0.4, skill: 0.3, history: 0.2, capacity: 0.1 }, candidates: ["array of CandidateDTO"] }, dbMapping: "Bảng `AiAssignmentRun`, `AiAssignmentCandidate`" },
      { method: "POST", path: "/api/v1/ai/smart-assign/confirm", summary: "AI 2: PM/SM xác nhận phân công", requestBody: { runId: "UUID", issueId: "UUID", selectedUserId: "UUID", isOverride: false, overrideReason: "string (if override)" }, responseBody: { decisionId: "UUID", status: "Confirmed" }, dbMapping: "Bảng `AiAssignmentDecision`, chuyển event gán người sang Delivery" }
    ]
  },
  {
    id: "module-3",
    name: "Module 3: DeliveryIntelligence (Thực Thi Công Việc, AC, Cộng Tác, DataOps & Analytics)",
    owner: "Hoàng Trần Huy Hoàng (C)",
    dbTables: "21 Bảng DB (`Issue`, `IssueLink`, `IssueWatcher`, `Comment`, `Attachment`, `ActivityLog`, `IssueStatusHistory`, `IssueAssignmentHistory`, `Label`, `IssueLabel`, `IssueComponentLink`, `IssueVersionLink`, `IssueRequiredSkill`, `AcceptanceCriteria`, `AiDataset`, `AiDatasetVersion`, `AiDatasetSample`, `AiDataCleaningRule`, `AiDataQualityFlag`, `AiTrainingRun`, `AiEvaluationResult`)",
    endpoints: [
      // 3.1 Issue Core CRUD
      { method: "GET", path: "/api/v1/projects/{projectKey}/issues", summary: "Danh sách Issue / Work Items (Paged & Filtered)", queryParams: "sprintId=UUID&statusId=UUID&assigneeId=UUID&issueType=Story|Task|Bug&searchQuery=string&page=1&pageSize=20", requestBody: null, responseBody: { items: [{ issueId: "UUID", issueKey: "CRM-102", title: "string", statusName: "In Progress", assigneeName: "Trần Văn Hoàng", storyPoints: 5, updatedAt: "ISO 8601" }], totalCount: 105 }, dbMapping: "Bảng `Issue`, `IssueType`, `Priority`" },
      { method: "POST", path: "/api/v1/issues", summary: "Tạo Issue mới (Cấp IssueNumber nguyên tử)", requestBody: { projectKey: "CRM", title: "string", description: "string", issueType: "Story | Task | Bug | Sub-task | Epic", priority: "High", sprintId: "UUID", parentIssueId: "UUID", assigneeId: "UUID", storyPoints: 3, acceptanceCriteria: ["string"] }, responseBody: { issueId: "UUID", issueKey: "CRM-106", title: "string", statusName: "To Do" }, dbMapping: "Bảng `Issue`, `AcceptanceCriteria`, `ActivityLog`" },
      { method: "GET", path: "/api/v1/issues/{issueKey}", summary: "Xem chi tiết đầy đủ của Issue", requestBody: null, responseBody: { issueId: "UUID", issueKey: "CRM-102", title: "string", description: "string", storyPoints: 5, acceptanceCriteria: [{ criteriaId: "UUID", content: "string", isMet: true }], subTasks: [{ issueKey: "CRM-103", title: "string" }], commentsCount: 4, attachmentsCount: 2 }, dbMapping: "Bảng `Issue`, `AcceptanceCriteria`, `Comment`, `Attachment`" },
      { method: "PUT", path: "/api/v1/issues/{issueKey}", summary: "Cập nhật thông tin chi tiết Issue", requestBody: { title: "string", description: "string", storyPoints: 5, originalEstimateMinutes: 480, dueDate: "date" }, responseBody: { issueKey: "CRM-102", updatedAt: "ISO 8601" }, dbMapping: "Bảng `Issue`, `ActivityLog`" },
      { method: "DELETE", path: "/api/v1/issues/{issueKey}", summary: "Xóa mềm Issue", requestBody: null, responseBody: { isDeleted: true }, dbMapping: "Bảng `Issue` (`IsDeleted = 1`)" },

      // 3.2 Move & Rank Batch Operations
      { method: "PUT", path: "/api/v1/issues/{issueKey}/status", summary: "Chuyển trạng thái Workflow Transition", requestBody: { targetStatusId: "UUID", comment: "string" }, responseBody: { issueKey: "CRM-102", previousStatusId: "UUID", newStatusId: "UUID" }, dbMapping: "Bảng `Issue`, `IssueStatusHistory`, `ActivityLog`" },
      { method: "PUT", path: "/api/v1/issues/{issueKey}/move-sprint", summary: "Chuyển Issue giữa các Sprint / Backlog", requestBody: { targetSprintId: "UUID (null for Backlog)" }, responseBody: { issueKey: "CRM-102", newSprintId: "UUID" }, dbMapping: "Bảng `Issue` (SprintId)" },
      { method: "PUT", path: "/api/v1/issues/{issueKey}/rank", summary: "Sắp xếp lại vị trí ưu tiên (RankOrder)", requestBody: { previousIssueKey: "CRM-101", nextIssueKey: "CRM-103" }, responseBody: { issueKey: "CRM-102", newRankOrder: 15.5 }, dbMapping: "Bảng `Issue` (RankOrder interpolation)" },
      { method: "POST", path: "/api/v1/issues/batch-update", summary: "Cập nhật hàng loạt nhiều Issue", requestBody: { issueKeys: ["CRM-101", "CRM-102"], statusId: "UUID", assigneeId: "UUID", sprintId: "UUID" }, responseBody: { updatedCount: 2 }, dbMapping: "Bảng `Issue`, `ActivityLog`" },

      // 3.3 Hierarchy & Issue Links
      { method: "GET", path: "/api/v1/issues/{issueKey}/hierarchy", summary: "Xem cây phân cấp Epic -> Story -> Sub-task", requestBody: null, responseBody: { epic: { issueKey: "CRM-1", title: "Epic 1" }, stories: [{ issueKey: "CRM-102", subTasks: [{ issueKey: "CRM-103" }] }] }, dbMapping: "Bảng `Issue` (ParentId, EpicId)" },
      { method: "POST", path: "/api/v1/issues/{issueKey}/links", summary: "Tạo liên kết giữa 2 Issue", requestBody: { targetIssueKey: "CRM-105", linkType: "Blocks | IsBlockedBy | Relates | Duplicates" }, responseBody: { linkId: "UUID", linkType: "Blocks" }, dbMapping: "Bảng `IssueLink`" },
      { method: "DELETE", path: "/api/v1/issue-links/{linkId}", summary: "Xóa liên kết Issue", requestBody: null, responseBody: { success: true }, dbMapping: "Bảng `IssueLink`" },

      // 3.4 Acceptance Criteria (AC)
      { method: "GET", path: "/api/v1/issues/{issueKey}/acceptance-criteria", summary: "Danh sách Tiêu chí Nghiệm thu (AC)", requestBody: null, responseBody: { criteria: [{ criteriaId: "UUID", content: "string", isMet: false, source: "AI | Manual" }] }, dbMapping: "Bảng `AcceptanceCriteria`" },
      { method: "POST", path: "/api/v1/issues/{issueKey}/acceptance-criteria", summary: "Thêm mới Tiêu chí Nghiệm thu (AC)", requestBody: { content: "string", source: "Manual" }, responseBody: { criteriaId: "UUID", content: "string" }, dbMapping: "Bảng `AcceptanceCriteria`" },
      { method: "PUT", path: "/api/v1/acceptance-criteria/{criteriaId}", summary: "Sửa nội dung AC / Đánh dấu hoàn thành", requestBody: { content: "string", isMet: true }, responseBody: { criteriaId: "UUID", isMet: true, metAt: "ISO 8601" }, dbMapping: "Bảng `AcceptanceCriteria`" },
      { method: "DELETE", path: "/api/v1/acceptance-criteria/{criteriaId}", summary: "Xóa Tiêu chí Nghiệm thu (AC)", requestBody: null, responseBody: { success: true }, dbMapping: "Bảng `AcceptanceCriteria`" },

      // 3.5 Required Skills
      { method: "GET", path: "/api/v1/issues/{issueKey}/required-skills", summary: "Danh sách kỹ năng yêu cầu của Task", requestBody: null, responseBody: { skills: [{ id: "UUID", skillName: "Angular", minLevel: 3, weight: 1.0 }] }, dbMapping: "Bảng `IssueRequiredSkill`" },
      { method: "POST", path: "/api/v1/issues/{issueKey}/required-skills", summary: "Thêm kỹ năng yêu cầu cho Task", requestBody: { skillId: "UUID", minLevel: 3, weight: 1.0 }, responseBody: { id: "UUID" }, dbMapping: "Bảng `IssueRequiredSkill`" },
      { method: "DELETE", path: "/api/v1/issue-required-skills/{id}", summary: "Xóa kỹ năng yêu cầu khỏi Task", requestBody: null, responseBody: { success: true }, dbMapping: "Bảng `IssueRequiredSkill`" },

      // 3.6 Collaboration: Comments, Attachments & Watchers
      { method: "GET", path: "/api/v1/issues/{issueKey}/comments", summary: "Danh sách Bình luận (Hỗ trợ reply lồng)", requestBody: null, responseBody: { comments: [{ commentId: "UUID", authorName: "Trần Văn Hoàng", content: "string", parentCommentId: "UUID", createdAt: "ISO 8601" }] }, dbMapping: "Bảng `Comment`" },
      { method: "POST", path: "/api/v1/issues/{issueKey}/comments", summary: "Tạo Bình luận mới (Quét `@mention`)", requestBody: { content: "string", parentCommentId: "UUID", mentionedUserIds: ["UUID"] }, responseBody: { commentId: "UUID", content: "string" }, dbMapping: "Bảng `Comment`, gửi event thông báo sang IdentityExperience" },
      { method: "PUT", path: "/api/v1/comments/{commentId}", summary: "Sửa nội dung Bình luận", requestBody: { content: "string" }, responseBody: { commentId: "UUID", isEdited: true }, dbMapping: "Bảng `Comment`" },
      { method: "DELETE", path: "/api/v1/comments/{commentId}", summary: "Xóa mềm Bình luận", requestBody: null, responseBody: { isDeleted: true }, dbMapping: "Bảng `Comment`" },
      { method: "POST", path: "/api/v1/issues/{issueKey}/attachments", summary: "Tải lên Tệp đính kèm", requestBody: "Multipart FormData (file: Binary)", responseBody: { attachmentId: "UUID", fileName: "spec.pdf", fileUrl: "https://storage...", fileSizeBytes: 102400 }, dbMapping: "Bảng `Attachment`" },
      { method: "GET", path: "/api/v1/attachments/{attachmentId}/download", summary: "Tải xuống Tệp đính kèm", requestBody: null, responseBody: "File Binary Stream with Content-Disposition header", dbMapping: "Bảng `Attachment`" },
      { method: "DELETE", path: "/api/v1/attachments/{attachmentId}", summary: "Xóa Tệp đính kèm", requestBody: null, responseBody: { success: true }, dbMapping: "Bảng `Attachment`" },
      { method: "POST", path: "/api/v1/issues/{issueKey}/watchers", summary: "Đăng ký theo dõi Issue", requestBody: null, responseBody: { isWatching: true }, dbMapping: "Bảng `IssueWatcher`" },
      { method: "DELETE", path: "/api/v1/issues/{issueKey}/watchers/me", summary: "Hủy theo dõi Issue", requestBody: null, responseBody: { isWatching: false }, dbMapping: "Bảng `IssueWatcher`" },

      // 3.7 Audit Log & Cycle Time Analytics
      { method: "GET", path: "/api/v1/issues/{issueKey}/activity-log", summary: "Nhật ký lịch sử thay đổi chi tiết", queryParams: "page=1&pageSize=20", requestBody: null, responseBody: { logs: [{ logId: "UUID", actorName: "Trần Văn Hoàng", action: "StatusChanged", fieldName: "Status", oldValue: "To Do", newValue: "In Progress", createdAt: "ISO 8601" }] }, dbMapping: "Bảng `ActivityLog`" },
      { method: "GET", path: "/api/v1/issues/{issueKey}/status-history", summary: "Lịch sử thời gian chuyển trạng thái (Cycle time)", requestBody: null, responseBody: { history: [{ fromStatus: "In Progress", toStatus: "Done", durationSeconds: 172800, changedAt: "ISO 8601" }] }, dbMapping: "Bảng `IssueStatusHistory`" },
      { method: "GET", path: "/api/v1/issues/{issueKey}/assignment-history", summary: "Lịch sử giao việc & nguồn gán", requestBody: null, responseBody: { history: [{ fromAssigneeName: "Hoài", toAssigneeName: "Hoàng", assignmentSource: "AiSuggested", reason: "Khớp kỹ năng Angular" }] }, dbMapping: "Bảng `IssueAssignmentHistory`" },

      // 3.8 DataOps Engine & AI Fine-Tuning Governance
      { method: "GET", path: "/api/v1/ai/models", summary: "Danh sách AI Models", requestBody: null, responseBody: { items: [{ modelId: "UUID", code: "qwen2.5:3b-instruct", provider: "Ollama", taskType: "Breakdown", isActive: true }] }, dbMapping: "Bảng `AiModel`" },
      { method: "POST", path: "/api/v1/ai/models", summary: "Khai báo AI Model mới", requestBody: { code: "qwen2.5:3b-scrum-lora-v1", displayName: "Qwen 3B Fine-tuned", provider: "Ollama", taskType: "Breakdown", adapterPath: "/models/lora.gguf" }, responseBody: { modelId: "UUID" }, dbMapping: "Bảng `AiModel`" },
      { method: "PUT", path: "/api/v1/ai/models/{modelId}/activate", summary: "Kích hoạt Model chạy Production", requestBody: null, responseBody: { modelId: "UUID", isActive: true }, dbMapping: "Bảng `AiModel` (`IsActive = 1`)" },
      { method: "GET", path: "/api/v1/ai/datasets", summary: "Danh sách Dataset huấn luyện", requestBody: null, responseBody: { items: [{ datasetId: "UUID", code: "breakdown-vi-v1", name: "Bộ dữ liệu phân rã tiếng Việt", taskType: "Breakdown" }] }, dbMapping: "Bảng `AiDataset`" },
      { method: "POST", path: "/api/v1/ai/datasets", summary: "Khai báo Dataset mới", requestBody: { code: "string", name: "string", taskType: "Breakdown | Assignment" }, responseBody: { datasetId: "UUID" }, dbMapping: "Bảng `AiDataset`" },
      { method: "POST", path: "/api/v1/ai/datasets/{datasetId}/versions", summary: "Khởi tạo Dataset Version mới", requestBody: { versionTag: "v1.0.0", splitSeed: 42, notes: "string" }, responseBody: { datasetVersionId: "UUID", versionTag: "v1.0.0" }, dbMapping: "Bảng `AiDatasetVersion`" },
      { method: "PUT", path: "/api/v1/ai/dataset-versions/{versionId}/freeze", summary: "Đóng băng Dataset Version (IsFrozen = 1)", requestBody: null, responseBody: { versionId: "UUID", isFrozen: true, checksum: "SHA256 hex" }, dbMapping: "Bảng `AiDatasetVersion`" },
      { method: "GET", path: "/api/v1/ai/dataset-versions/{versionId}/samples", summary: "Danh sách mẫu dữ liệu huấn luyện", queryParams: "splitType=Train|Validation|Test&qualityStatus=Approved", requestBody: null, responseBody: { items: [{ sampleId: "UUID", instruction: "string", inputJson: "Object", outputJson: "Object", qualityStatus: "Approved", isPiiRedacted: true }] }, dbMapping: "Bảng `AiDatasetSample`" },
      { method: "POST", path: "/api/v1/ai/dataset-versions/{versionId}/samples", summary: "Thêm mẫu dữ liệu mới", requestBody: { instruction: "string", inputJson: "Object", outputJson: "Object", splitType: "Train", qualityStatus: "Raw" }, responseBody: { sampleId: "UUID" }, dbMapping: "Bảng `AiDatasetSample`" },
      { method: "PUT", path: "/api/v1/ai/dataset-samples/{sampleId}/review", summary: "Duyệt chất lượng mẫu dữ liệu", requestBody: { reviewStatus: "Approved | Rejected", qualityScore: 0.95, isPiiRedacted: true, reviewNote: "string" }, responseBody: { sampleId: "UUID", reviewStatus: "Approved" }, dbMapping: "Bảng `AiDatasetSample`" },
      { method: "GET", path: "/api/v1/ai/data-cleaning-rules", summary: "Danh sách Quy tắc làm sạch dữ liệu DataOps", requestBody: null, responseBody: { items: [{ ruleId: "UUID", code: "pii.email", name: "Khử email rò rỉ", ruleType: "PiiDetection", severity: "Error" }] }, dbMapping: "Bảng `AiDataCleaningRule`" },
      { method: "POST", path: "/api/v1/ai/data-cleaning-rules", summary: "Thêm quy tắc làm sạch dữ liệu mới", requestBody: { code: "string", name: "string", ruleType: "PiiDetection", severity: "Error", config: "Object" }, responseBody: { ruleId: "UUID" }, dbMapping: "Bảng `AiDataCleaningRule`" },
      { method: "GET", path: "/api/v1/ai/dataset-samples/{sampleId}/flags", summary: "Danh sách cờ vi phạm quy tắc dữ liệu", requestBody: null, responseBody: { flags: [{ flagId: "UUID", ruleCode: "pii.email", severity: "Error", message: "Phát hiện rò rỉ email" }] }, dbMapping: "Bảng `AiDataQualityFlag`" },
      { method: "POST", path: "/api/v1/ai/training-runs", summary: "Khởi chạy tiến trình Fine-tuning AI Model", requestBody: { datasetVersionId: "UUID", name: "LoRA Run 1", baseModelCode: "qwen2.5:3b-instruct", method: "LoRA", hyperparameters: { lora_r: 16, lr: 0.0002, epochs: 3 } }, responseBody: { runId: "UUID", status: "Queued" }, dbMapping: "Bảng `AiTrainingRun`" },
      { method: "GET", path: "/api/v1/ai/training-runs/{runId}", summary: "Xem trạng thái & Log tiến trình Fine-tuning", requestBody: null, responseBody: { runId: "UUID", status: "Running | Completed", trainLoss: 0.045, artifactPath: "/models/lora.gguf" }, dbMapping: "Bảng `AiTrainingRun`" },
      { method: "POST", path: "/api/v1/ai/evaluations", summary: "Ghi nhận chỉ số kết quả đánh giá Model", requestBody: { trainingRunId: "UUID", modelId: "UUID", datasetVersionId: "UUID", splitType: "Test", metricName: "json_valid_rate", metricValue: 0.985 }, responseBody: { evalId: "UUID" }, dbMapping: "Bảng `AiEvaluationResult`" },
      { method: "GET", path: "/api/v1/analytics/quality-health", summary: "Báo cáo tổng hợp sức khỏe DataOps & AI Accuracy", requestBody: null, responseBody: { totalSamples: 5000, cleanPercentage: 98.5, ai1AcceptanceRate: 84.2, ai2AgreementRate: 79.5 }, dbMapping: "View `vw_AiBreakdownQuality`, `vw_AiAssignmentAccuracy`" }
    ]
  }
];

// BUILD HTML REPORT
function buildHtml() {
  const css = `
    :root {
      --primary: #0052cc;
      --primary-dark: #003d9b;
      --bg: #090d16;
      --surface: #0f172a;
      --card-bg: #1e293b;
      --text: #f8fafc;
      --text-muted: #94a3b8;
      --border: rgba(255, 255, 255, 0.12);
      --get-bg: #10b981;
      --post-bg: #3b82f6;
      --put-bg: #f59e0b;
      --delete-bg: #ef4444;
      --ws-bg: #8b5cf6;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: 'Inter', system-ui, -apple-system, sans-serif; background: var(--bg); color: var(--text); line-height: 1.6; }
    .layout { display: flex; min-height: 100vh; }
    .sidebar { width: 350px; background: var(--surface); border-right: 1px solid var(--border); position: sticky; top: 0; height: 100vh; overflow-y: auto; padding: 2rem 1.25rem; }
    .sidebar h2 { font-size: 1.05rem; color: #38bdf8; margin-bottom: 1rem; text-transform: uppercase; letter-spacing: 0.05em; }
    .sidebar ul { list-style: none; }
    .sidebar li { margin-bottom: 0.4rem; }
    .sidebar a { color: var(--text-muted); text-decoration: none; font-size: 0.88rem; transition: color 0.2s; display: block; padding: 0.35rem 0.5rem; border-radius: 6px; }
    .sidebar a:hover { color: #ffffff; background: rgba(255, 255, 255, 0.06); }
    .main-content { flex: 1; padding: 3rem 4rem; max-width: 1450px; }
    .header-banner { background: linear-gradient(135deg, #0052cc 0%, #003d9b 100%); border-radius: 1rem; padding: 2.5rem; margin-bottom: 3rem; box-shadow: 0 20px 40px rgba(0,0,0,0.5); }
    .header-banner h1 { font-size: 1.9rem; font-weight: 800; margin-bottom: 0.75rem; color: #ffffff; }
    .header-banner p { color: #cbd5e1; font-size: 0.98rem; }
    .meta-pills { display: flex; gap: 0.75rem; margin-top: 1.5rem; flex-wrap: wrap; }
    .pill { background: rgba(255,255,255,0.18); backdrop-filter: blur(10px); padding: 0.4rem 0.9rem; border-radius: 9999px; font-size: 0.82rem; color: #ffffff; font-weight: 600; }
    .section-card { background: var(--surface); border: 1px solid var(--border); border-radius: 1rem; padding: 2.25rem; margin-bottom: 2.5rem; }
    .section-title { font-size: 1.45rem; font-weight: 700; color: #38bdf8; margin-bottom: 1rem; border-bottom: 1px solid var(--border); padding-bottom: 0.75rem; }
    .endpoint-card { background: var(--card-bg); border: 1px solid rgba(255,255,255,0.08); border-radius: 0.75rem; padding: 1.5rem; margin-bottom: 1.75rem; }
    .endpoint-header { display: flex; align-items: center; gap: 1rem; margin-bottom: 0.75rem; flex-wrap: wrap; }
    .method-badge { font-weight: 800; font-size: 0.78rem; padding: 0.35rem 0.75rem; border-radius: 6px; color: #ffffff; text-transform: uppercase; }
    .method-GET { background: var(--get-bg); }
    .method-POST { background: var(--post-bg); }
    .method-PUT { background: var(--put-bg); }
    .method-DELETE { background: var(--delete-bg); }
    .method-WEBSOCKET { background: var(--ws-bg); }
    .endpoint-path { font-family: monospace; font-size: 1.05rem; font-weight: 700; color: #f1f5f9; }
    .endpoint-summary { font-size: 0.98rem; font-weight: 600; color: #e2e8f0; margin-bottom: 0.5rem; }
    .code-block { background: #090d16; border: 1px solid var(--border); border-radius: 6px; padding: 0.85rem; font-family: monospace; font-size: 0.83rem; color: #38bdf8; overflow-x: auto; white-space: pre-wrap; margin-top: 0.4rem; }
    table { width: 100%; border-collapse: collapse; margin-top: 1rem; margin-bottom: 1rem; font-size: 0.88rem; }
    th, td { border: 1px solid var(--border); padding: 0.65rem 0.85rem; text-align: left; }
    th { background: rgba(255,255,255,0.05); color: #38bdf8; font-weight: 700; }
    .db-tag { display: inline-block; background: rgba(56, 189, 248, 0.15); color: #38bdf8; padding: 0.25rem 0.65rem; border-radius: 4px; font-size: 0.8rem; font-weight: 600; margin-top: 0.5rem; }
  `;

  let html = `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${specMeta.title}</title>
  <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap">
  <style>${css}</style>
</head>
<body>
  <div class="layout">
    <div class="sidebar">
      <h2>Mục Lục API (${specMeta.totalEndpoints} Endpoints)</h2>
      <ul>
        <li><a href="#conventions">1. Quy Tắc Chuẩn Tắc API</a></li>
        ${modules.map(m => `<li><a href="#${m.id}">${m.name}</a></li>`).join('')}
      </ul>
    </div>
    <div class="main-content">
      <div class="header-banner">
        <h1>${specMeta.title}</h1>
        <p>Báo cáo đặc tả kỹ thuật chi tiết chuyên sâu toàn bộ 128 API Endpoints bao phủ 100% các mục nhỏ trên giao diện Frontend và 55 bảng MySQL trong Cẩm nang kỹ thuật.</p>
        <div class="meta-pills">
          <span class="pill">Tác giả: ${specMeta.author}</span>
          <span class="pill">Phiên bản: ${specMeta.version}</span>
          <span class="pill">Tổng Endpoints: ${specMeta.totalEndpoints}</span>
          <span class="pill">Số bảng DB: ${specMeta.totalTables}</span>
        </div>
      </div>

      <!-- CONVENTIONS -->
      <div class="section-card" id="conventions">
        <h2 class="section-title">1. Quy Tắc Chuẩn Tắc & Thiết Kế API Toàn Hệ Thống</h2>
        <p><strong>Base URL:</strong> <code>${specMeta.conventions.baseUrl}</code></p>
        <p><strong>Auth Header:</strong> <code>${specMeta.conventions.authHeader}</code></p>
        
        <h3 style="margin-top:1.5rem; color:#e2e8f0;">1.1 Ma Trận Quy Tắc Đặt Tên (Naming Rules)</h3>
        <table>
          <thead>
            <tr><th>Đối Tượng</th><th>Quy Tắc Đặt Tên</th><th>Ví Dụ Cụ Thể</th></tr>
          </thead>
          <tbody>
            ${specMeta.conventions.naming.map(n => `
              <tr>
                <td><strong>${n.rule}</strong></td>
                <td>${n.convention}</td>
                <td><code>${n.example}</code></td>
              </tr>
            `).join('')}
          </tbody>
        </table>

        <h3 style="margin-top:1.5rem; color:#e2e8f0;">1.2 Cấu Trúc Phản Hồi Chuẩn (Standard Response Wrapper)</h3>
        <div style="display:grid; grid-template-columns: 1fr 1fr; gap:1rem;">
          <div>
            <strong>Single Object Response:</strong>
            <pre class="code-block">${specMeta.conventions.standardResponse.single}</pre>
          </div>
          <div>
            <strong>Paged List Response:</strong>
            <pre class="code-block">${specMeta.conventions.standardResponse.paged}</pre>
          </div>
        </div>

        <h3 style="margin-top:1.5rem; color:#e2e8f0;">1.3 Cấu Trúc Báo Lỗi RFC 7807 (ProblemDetails)</h3>
        <pre class="code-block">${specMeta.conventions.errorStandard}</pre>
      </div>

      <!-- MODULE SECTIONS -->
      ${modules.map(mod => `
        <div class="section-card" id="${mod.id}">
          <h2 class="section-title">${mod.name}</h2>
          <p><strong>Phụ trách Module:</strong> ${mod.owner}</p>
          <p style="margin-bottom:1.5rem;"><strong>Bảng DB sở hữu:</strong> ${mod.dbTables}</p>

          ${mod.endpoints.map(ep => `
            <div class="endpoint-card">
              <div class="endpoint-header">
                <span class="method-badge method-${ep.method}">${ep.method}</span>
                <span class="endpoint-path">${ep.path}</span>
              </div>
              <div class="endpoint-summary">${ep.summary}</div>
              ${ep.queryParams ? `<p style="font-size:0.85rem; color:#38bdf8; margin-top:0.4rem;"><strong>Query Params:</strong> <code>${ep.queryParams}</code></p>` : ''}
              
              <div style="display:grid; grid-template-columns: 1fr 1fr; gap:1rem; margin-top:0.8rem;">
                <div>
                  <strong>Dữ liệu truyền từ FE (Request):</strong>
                  ${ep.requestBody ? `<pre class="code-block">${typeof ep.requestBody === 'string' ? ep.requestBody : JSON.stringify(ep.requestBody, null, 2)}</pre>` : '<p style="color:#94a3b8; font-size:0.85rem; margin-top:0.4rem;">None (Không cần Request Body)</p>'}
                </div>
                <div>
                  <strong>Dữ liệu trả về cho FE (Response Data):</strong>
                  <pre class="code-block">${JSON.stringify(ep.responseBody, null, 2)}</pre>
                </div>
              </div>
              <div class="db-tag">Bảng / View MySQL tương ứng: ${ep.dbMapping}</div>
            </div>
          `).join('')}
        </div>
      `).join('')}

    </div>
  </div>
</body>
</html>`;

  fs.writeFileSync(htmlPath, html, 'utf8');
  console.log('✔ Exhaustive HTML report written to:', htmlPath);
}

// BUILD DOCX REPORT
async function buildDocx() {
  const doc = new Document({
    styles: {
      default: {
        heading1: { run: { font: "Calibri", size: 30, bold: true, color: "0052CC" } },
        heading2: { run: { font: "Calibri", size: 24, bold: true, color: "003D9B" } },
        heading3: { run: { font: "Calibri", size: 20, bold: true, color: "1E293B" } },
        body: { run: { font: "Calibri", size: 21, color: "0F172A" } }
      }
    },
    sections: [
      {
        properties: {
          page: { margin: { top: 1200, bottom: 1200, left: 1200, right: 1200 } }
        },
        headers: {
          default: new Header({
            children: [
              new Paragraph({
                alignment: AlignmentType.RIGHT,
                children: [new TextRun({ text: "PROJECTMGMT - BÁO CÁO ĐẶC TẢ API CHUẨN MỰC (128 ENDPOINTS)", size: 18, color: "94A3B8", italic: true })]
              })
            ]
          })
        },
        footers: {
          default: new Footer({
            children: [
              new Paragraph({
                alignment: AlignmentType.CENTER,
                children: [
                  new TextRun({ text: "Trang ", size: 18, color: "94A3B8" }),
                  new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "94A3B8" }),
                  new TextRun({ text: " / ", size: 18, color: "94A3B8" }),
                  new TextRun({ children: [PageNumber.TOTAL_PAGES], size: 18, color: "94A3B8" })
                ]
              })
            ]
          })
        },
        children: [
          // Banner
          new Paragraph({
            heading: HeadingLevel.HEADING_1,
            alignment: AlignmentType.CENTER,
            children: [new TextRun({ text: specMeta.title, bold: true })]
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            children: [
              new TextRun({ text: `Tác giả: ${specMeta.author} | Phiên bản: ${specMeta.version} | ${specMeta.totalEndpoints} Endpoints | ${specMeta.totalTables} DB Tables`, italic: true, color: "64748B" })
            ]
          }),
          new Paragraph({ text: "", spacing: { after: 250 } }),

          // Overview Callout Box
          new Table({
            width: { size: 100, type: WidthType.PERCENTAGE },
            rows: [
              new TableRow({
                children: [
                  new TableCell({
                    shading: { fill: "F1F5F9", type: ShadingType.CLEAR },
                    margins: { top: 180, bottom: 180, left: 180, right: 180 },
                    children: [
                      new Paragraph({
                        children: [
                          new TextRun({ text: "MỤC TIÊU BÁO CÁO KỸ THUẬT: ", bold: true, color: "0052CC" }),
                          new TextRun({ text: "Tài liệu này là bản đặc tả API chuẩn mực dành cho đội ngũ phát triển Backend, bao phủ 100% 128 API Endpoints khớp với mọi mục nhỏ trên giao diện Frontend và 55 bảng MySQL trong Cẩm nang kỹ thuật. Mọi API quy định camelCase, cấu trúc lỗi RFC 7807, gộp DTO tránh N+1 call HTTP, và phân quyền phạm vi dự án RBAC (Product Owner, Scrum Master, Developer, Tech Lead) thuộc Module 1 sở hữu." })
                        ]
                      })
                    ]
                  })
                ]
              })
            ]
          }),
          new Paragraph({ text: "", spacing: { after: 250 } }),

          // SECTION 1: CONVENTIONS
          new Paragraph({
            heading: HeadingLevel.HEADING_2,
            children: [new TextRun("1. Quy Tắc Chuẩn Tắc & Thiết Kế API Toàn Hệ Thống")]
          }),
          new Paragraph({ children: [new TextRun({ text: "• Base URL: ", bold: true }), new TextRun(specMeta.conventions.baseUrl)] }),
          new Paragraph({ children: [new TextRun({ text: "• Auth Header: ", bold: true }), new TextRun(specMeta.conventions.authHeader)] }),
          new Paragraph({ text: "", spacing: { after: 150 } }),

          new Paragraph({
            heading: HeadingLevel.HEADING_3,
            children: [new TextRun("1.1 Ma trận quy tắc đặt tên (Naming Rules)")]
          }),
          new Table({
            width: { size: 100, type: WidthType.PERCENTAGE },
            rows: [
              new TableRow({
                children: [
                  new TableCell({ shading: { fill: "0052CC" }, children: [new Paragraph({ children: [new TextRun({ text: "Đối Tượng", bold: true, color: "FFFFFF" })] })] }),
                  new TableCell({ shading: { fill: "0052CC" }, children: [new Paragraph({ children: [new TextRun({ text: "Quy Tắc Đặt Tên", bold: true, color: "FFFFFF" })] })] }),
                  new TableCell({ shading: { fill: "0052CC" }, children: [new Paragraph({ children: [new TextRun({ text: "Ví Dụ Cụ Thể", bold: true, color: "FFFFFF" })] })] })
                ]
              }),
              ...specMeta.conventions.naming.map(n => 
                new TableRow({
                  children: [
                    new TableCell({ children: [new Paragraph({ children: [new TextRun({ text: n.rule, bold: true })] })] }),
                    new TableCell({ children: [new Paragraph(n.convention)] }),
                    new TableCell({ children: [new Paragraph(n.example)] })
                  ]
                })
              )
            ]
          }),
          new Paragraph({ text: "", spacing: { after: 250 } }),

          // MODULE SECTIONS
          ...modules.flatMap(mod => [
            new Paragraph({
              heading: HeadingLevel.HEADING_2,
              children: [new TextRun(mod.name)]
            }),
            new Paragraph({
              children: [
                new TextRun({ text: "Phụ trách: ", bold: true }),
                new TextRun(mod.owner),
                new TextRun({ text: " | DB sở hữu: ", bold: true }),
                new TextRun(mod.dbTables)
              ],
              spacing: { after: 200 }
            }),

            ...mod.endpoints.flatMap(ep => [
              new Paragraph({
                heading: HeadingLevel.HEADING_3,
                children: [
                  new TextRun({ text: `[${ep.method}] `, bold: true, color: ep.method === 'GET' ? '10B981' : ep.method === 'POST' ? '3B82F6' : ep.method === 'PUT' ? 'F59E0B' : ep.method === 'DELETE' ? 'EF4444' : '8B5CF6' }),
                  new TextRun({ text: `${ep.path} - ${ep.summary}`, bold: true })
                ]
              }),
              new Table({
                width: { size: 100, type: WidthType.PERCENTAGE },
                rows: [
                  new TableRow({
                    children: [
                      new TableCell({ shading: { fill: "E2E8F0" }, children: [new Paragraph({ children: [new TextRun({ text: "Dữ Liệu Đầu Vào FE (Request)", bold: true })] })] }),
                      new TableCell({ shading: { fill: "E2E8F0" }, children: [new Paragraph({ children: [new TextRun({ text: "Dữ Liệu Trả Về FE (Response Data)", bold: true })] })] })
                    ]
                  }),
                  new TableRow({
                    children: [
                      new TableCell({ children: [new Paragraph(ep.requestBody ? (typeof ep.requestBody === 'string' ? ep.requestBody : JSON.stringify(ep.requestBody, null, 2)) : "Không có Request Body")] }),
                      new TableCell({ children: [new Paragraph(JSON.stringify(ep.responseBody, null, 2))] })
                    ]
                  })
                ]
              }),
              new Paragraph({
                children: [
                  new TextRun({ text: "Bảng MySQL tương ứng: ", bold: true, color: "0052CC" }),
                  new TextRun(ep.dbMapping)
                ],
                spacing: { after: 200 }
              })
            ])
          ])
        ]
      }
    ]
  });

  const buffer = await Packer.toBuffer(doc);
  try {
    fs.writeFileSync(docxPath, buffer);
    console.log('✔ Precision DOCX report written to:', docxPath);
  } catch (e) {
    const fallbackPath = path.join(docsDir, 'Bao-cao-Dac-ta-API-He-thong-ProjectMgmt-v2.docx');
    fs.writeFileSync(fallbackPath, buffer);
    console.log(`⚠ ${docxPath} is open in Word. Written fallback to:`, fallbackPath);
  }
}

async function main() {
  buildHtml();
  await buildDocx();
  console.log('🎉 Precision 128 API reports generated successfully!');
}

main().catch(err => console.error('Error generating precision reports:', err));
