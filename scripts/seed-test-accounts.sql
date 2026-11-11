-- =============================================================================
-- SQL Seeding Script:
--   1. Danh mục Kỹ năng (SkillCatalog)
--   2. Hồ sơ cá nhân (UserProfile - chỉ Họ tên, Chức danh, Cấp bậc, Kinh nghiệm, Bio) & Kỹ năng (UserSkill)
--   3. Tài khoản thử nghiệm hoangteam01@gmail.com .. hoangteam07@gmail.com (Mật khẩu: @123456xX)
--   4. Lịch sử Sprint trước (Completed), Sprint hiện tại (Active) & Product Backlog
-- Note: Được tự động khởi tạo khi chạy ứng dụng bởi ProjectMgmt.Solution/Services/TestAccountSeeder.cs
-- =============================================================================

SET @ProjectId = (SELECT Id FROM Project WHERE IsDeleted = 0 AND (Name LIKE '%Quản lý dự án%' OR ProjectKey = 'QLDA') ORDER BY CreatedAt LIMIT 1);
SET @OrgId = (SELECT OrgId FROM Project WHERE Id = @ProjectId LIMIT 1);
SET @LeadUserId = (SELECT LeadUserId FROM Project WHERE Id = @ProjectId LIMIT 1);

-- 1. Ensure Project Roles exist
INSERT IGNORE INTO Role (Id, Name, Scope, Description, IsSystem)
VALUES
  (UUID(), 'ProjectManager', 'Project', 'Giám đốc dự án / Quản lý dự án', 1),
  (UUID(), 'ScrumMaster', 'Project', 'Điều phối Scrum & Sprint', 1),
  (UUID(), 'Developer', 'Project', 'Kỹ sư phát triển phần mềm', 1),
  (UUID(), 'QA', 'Project', 'Kỹ sư kiểm thử chất lượng', 1),
  (UUID(), 'Viewer', 'Project', 'Người quan sát chỉ đọc', 1);

-- 2. Ensure SkillCatalog entries exist
INSERT INTO SkillCatalog (Id, Code, Name, Category, IsActive)
VALUES
  (UUID(), 'CSHARP_DOTNET', 'C# / .NET 9 Web API', 'Backend', 1),
  (UUID(), 'EF_CORE', 'Entity Framework Core & LINQ', 'Backend', 1),
  (UUID(), 'MYSQL_DB', 'MySQL & Tối ưu Truy vấn SQL', 'Database', 1),
  (UUID(), 'REDIS_CACHE', 'Redis Caching & Distributed State', 'Infrastructure', 1),
  (UUID(), 'ANGULAR_TS', 'Angular 19, TypeScript & Signals', 'Frontend', 1),
  (UUID(), 'UI_TAILWIND', 'TailwindCSS, Responsive UI & GSAP', 'Frontend', 1),
  (UUID(), 'SIGNALR_SSE', 'SignalR & Server-Sent Events (SSE)', 'Realtime', 1),
  (UUID(), 'PLAYWRIGHT_QA', 'Playwright E2E & Kiểm thử Tự động', 'QA', 1),
  (UUID(), 'API_SECURITY', 'Bảo mật OWASP, JWT & Phân quyền RBAC', 'Security', 1),
  (UUID(), 'SCRUM_AGILE', 'Điều phối Scrum, Quản trị Sprint & Backlog', 'Management', 1),
  (UUID(), 'SYS_ARCH', 'Kiến trúc Hệ thống, Microservices & MCP', 'Architecture', 1)
ON DUPLICATE KEY UPDATE
  Name = VALUES(Name),
  Category = VALUES(Category),
  IsActive = 1;
