-- =============================================================================
-- SQL Seeding Script: Test Accounts hoangteam01@gmail.com .. hoangteam07@gmail.com
-- Default Password: @123456xX (BCrypt WorkFactor 12)
-- Note: Also automatically seeded on startup by ProjectMgmt.Solution/Services/TestAccountSeeder.cs
-- =============================================================================

SET @ProjectId = (SELECT Id FROM Project WHERE IsDeleted = 0 AND (Name LIKE '%Quản lý dự án%' OR ProjectKey = 'QLDA') ORDER BY CreatedAt LIMIT 1);
SET @OrgId = (SELECT OrgId FROM Project WHERE Id = @ProjectId LIMIT 1);
SET @LeadUserId = (SELECT LeadUserId FROM Project WHERE Id = @ProjectId LIMIT 1);

-- Ensure Project Roles exist
INSERT IGNORE INTO Role (Id, Name, Scope, Description, IsSystem)
VALUES
  (UUID(), 'ProjectManager', 'Project', 'Giám đốc dự án / Quản lý dự án', 1),
  (UUID(), 'ScrumMaster', 'Project', 'Điều phối Scrum & Sprint', 1),
  (UUID(), 'Developer', 'Project', 'Kỹ sư phát triển phần mềm', 1),
  (UUID(), 'QA', 'Project', 'Kỹ sư kiểm thử chất lượng', 1),
  (UUID(), 'Viewer', 'Project', 'Người quan sát chỉ đọc', 1);

