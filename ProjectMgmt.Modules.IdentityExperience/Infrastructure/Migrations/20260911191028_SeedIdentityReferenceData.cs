using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityExperience.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedIdentityReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.Sql(
            """
            ALTER TABLE `User` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `UserProfile` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `ExternalLogin` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `OtpCode` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `RefreshToken` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Role` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Permission` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `RolePermission` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `UserRole` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `SkillCatalog` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `UserSkill` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiModel` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiPromptTemplate` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiGenerationLog` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiSuggestedTask` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Notification` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `OtpCode` DROP INDEX `IX_OtpCode_ExpiresAt`, ADD KEY `IX_OtpCode_ExpiresAt` (`ExpiresAt`) COMMENT 'Cho job dọn OTP hết hạn';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `UserRole` DROP INDEX `IX_UserRole_Lookup`, ADD KEY `IX_UserRole_Lookup` (`UserId`, `ScopeType`, `ScopeId`) COMMENT 'Truy vấn chính của AuthorizationHandler';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `UserRole` DROP INDEX `IX_UserRole_Scope`, ADD KEY `IX_UserRole_Scope` (`ScopeType`, `ScopeId`) COMMENT 'Liệt kê thành viên của 1 project/org';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiGenerationLog` DROP INDEX `IX_AiGenerationLog_RateLimit`, ADD KEY `IX_AiGenerationLog_RateLimit` (`ProjectId`, `CreatedAt`) COMMENT 'Đếm số request gần đây để rate-limit';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiSuggestedTask` DROP INDEX `IX_AiSuggestedTask_Action`, ADD KEY `IX_AiSuggestedTask_Action` (`UserAction`, `CreatedAt`) COMMENT 'Truy vấn export dataset theo nhãn + thời gian';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Notification` DROP INDEX `IX_Notification_Inbox`, ADD KEY `IX_Notification_Inbox` (`UserId`, `IsRead`, `CreatedAt`) COMMENT 'Truy vấn Notification Center + badge đếm';
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `Role` (`Id`, `Name`, `Scope`, `IsSystem`, `Description`) VALUES
            ('11111111-0000-0000-0000-000000000001', 'Admin',           'System',       1, 'Quản trị toàn hệ thống'),
            ('11111111-0000-0000-0000-000000000002', 'OrgOwner',        'Organization', 1, 'Chủ sở hữu tổ chức'),
            ('11111111-0000-0000-0000-000000000003', 'ProjectManager',  'Project',      1, 'Quản lý dự án'),
            ('11111111-0000-0000-0000-000000000004', 'ScrumMaster',     'Project',      1, 'Điều phối quy trình Scrum'),
            ('11111111-0000-0000-0000-000000000005', 'ProductOwner',    'Project',      1, 'Chủ sở hữu sản phẩm, quản lý Backlog'),
            ('11111111-0000-0000-0000-000000000006', 'Developer',       'Project',      1, 'Thành viên phát triển'),
            ('11111111-0000-0000-0000-000000000007', 'Viewer',          'Project',      1, 'Chỉ xem');
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `Permission` (`Id`, `Code`, `Grouping`, `Description`) VALUES
            ('22222222-0000-0000-0000-000000000001', 'project.create',          'Project',  'Tạo dự án'),
            ('22222222-0000-0000-0000-000000000002', 'project.update',          'Project',  'Sửa thông tin dự án'),
            ('22222222-0000-0000-0000-000000000003', 'project.delete',          'Project',  'Xóa dự án'),
            ('22222222-0000-0000-0000-000000000004', 'project.settings.manage', 'Project',  'Cấu hình workflow, issue type, board'),
            ('22222222-0000-0000-0000-000000000005', 'member.invite',           'Member',   'Mời thành viên'),
            ('22222222-0000-0000-0000-000000000006', 'member.role.assign',      'Member',   'Gán vai trò'),
            ('22222222-0000-0000-0000-000000000007', 'issue.create',            'Issue',    'Tạo issue'),
            ('22222222-0000-0000-0000-000000000008', 'issue.update',            'Issue',    'Sửa issue'),
            ('22222222-0000-0000-0000-000000000009', 'issue.delete',            'Issue',    'Xóa issue'),
            ('22222222-0000-0000-0000-000000000010', 'issue.transition',        'Issue',    'Chuyển trạng thái issue'),
            ('22222222-0000-0000-0000-000000000011', 'issue.assign',            'Issue',    'Gán người xử lý'),
            ('22222222-0000-0000-0000-000000000012', 'backlog.rank',            'Backlog',  'Sắp xếp thứ tự backlog'),
            ('22222222-0000-0000-0000-000000000013', 'sprint.create',           'Sprint',   'Tạo sprint'),
            ('22222222-0000-0000-0000-000000000014', 'sprint.start',            'Sprint',   'Bắt đầu sprint'),
            ('22222222-0000-0000-0000-000000000015', 'sprint.close',            'Sprint',   'Kết thúc sprint'),
            ('22222222-0000-0000-0000-000000000016', 'report.view',             'Report',   'Xem báo cáo'),
            ('22222222-0000-0000-0000-000000000017', 'ai.breakdown.request',    'AI',       'Yêu cầu AI chia nhỏ task'),
            ('22222222-0000-0000-0000-000000000018', 'ai.breakdown.apply',      'AI',       'Áp dụng gợi ý AI thành issue thật'),
            ('22222222-0000-0000-0000-000000000019', 'ai.assignment.run',       'AI',       'Chạy AI phân phối task'),
            ('22222222-0000-0000-0000-000000000020', 'ai.assignment.apply',     'AI',       'Áp dụng kết quả phân phối'),
            ('22222222-0000-0000-0000-000000000021', 'ai.dataset.manage',       'AI',       'Quản lý dataset & training'),
            ('22222222-0000-0000-0000-000000000022', 'ai.model.manage',         'AI',       'Đăng ký/kích hoạt model');
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000001', `Id` FROM `Permission`;
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000003', `Id` FROM `Permission`
            WHERE `Code` NOT IN ('project.delete','ai.dataset.manage','ai.model.manage');
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000004', `Id` FROM `Permission`
            WHERE `Code` IN ('issue.create','issue.update','issue.transition','issue.assign','backlog.rank',
                             'sprint.create','sprint.start','sprint.close','report.view',
                             'ai.breakdown.request','ai.breakdown.apply','ai.assignment.run','ai.assignment.apply');
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000005', `Id` FROM `Permission`
            WHERE `Code` IN ('issue.create','issue.update','backlog.rank','report.view',
                             'ai.breakdown.request','ai.breakdown.apply');
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000006', `Id` FROM `Permission`
            WHERE `Code` IN ('issue.create','issue.update','issue.transition','report.view','ai.breakdown.request');
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000007', `Id` FROM `Permission`
            WHERE `Code` IN ('report.view');
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `SkillCatalog` (`Id`, `Code`, `Name`, `Category`) VALUES
            ('44444444-0000-0000-0000-000000000001', 'dotnet',     '.NET / C#',            'Backend'),
            ('44444444-0000-0000-0000-000000000002', 'angular',    'Angular / TypeScript', 'Frontend'),
            ('44444444-0000-0000-0000-000000000003', 'sql',        'SQL / Database',       'Database'),
            ('44444444-0000-0000-0000-000000000004', 'devops',     'CI/CD / Docker',       'DevOps'),
            ('44444444-0000-0000-0000-000000000005', 'testing',    'Kiểm thử / QA',        'QA'),
            ('44444444-0000-0000-0000-000000000006', 'uiux',       'UI/UX Design',         'Design'),
            ('44444444-0000-0000-0000-000000000007', 'ai-ml',      'AI / Machine Learning','AI'),
            ('44444444-0000-0000-0000-000000000008', 'signalr',    'Realtime / SignalR',   'Backend'),
            ('44444444-0000-0000-0000-000000000009', 'security',   'Bảo mật / Xác thực',   'Backend');
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `AiModel` (`Id`, `Code`, `DisplayName`, `Provider`, `TaskType`, `IsActive`, `ContextWindow`, `DefaultParams`) VALUES
            ('55555555-0000-0000-0000-000000000001', 'qwen2.5:3b-instruct', 'Qwen2.5 3B Instruct', 'Ollama', 'Breakdown', 1, 32768,
             JSON_OBJECT('temperature', 0.2, 'top_p', 0.9, 'num_predict', 2048, 'seed', 42)),
            ('55555555-0000-0000-0000-000000000002', 'rule:weighted-score-v1', 'Scoring function có trọng số (baseline)', 'Local', 'Assignment', 1, NULL,
             JSON_OBJECT('load', 0.40, 'skill', 0.30, 'history', 0.20, 'capacity', 0.10));
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `AiPromptTemplate`
                (`Id`, `Code`, `Version`, `TaskType`, `Language`, `IsActive`, `SystemPrompt`, `UserTemplate`, `JsonSchema`)
            VALUES
            ('66666666-0000-0000-0000-000000000001', 'breakdown.system', 1, 'Breakdown', 'vi', 1,
             'Bạn là Scrum Master kiêm Technical Lead giàu kinh nghiệm. Phân tích User Story được cung cấp thành các Sub-task KỸ THUẬT có thể thực thi được.
            QUY TẮC:
            - Sinh tối đa 8 sub-task. Chỉ sinh những task THỰC SỰ cần thiết để hoàn thành story, không thêm task chung chung như "họp", "nghiên cứu", "kiểm tra lại".
            - Mỗi sub-task phải hoàn thành được trong 1 ngày làm việc.
            - Mỗi sub-task có 2 đến 5 tiêu chí nghiệm thu, viết cụ thể, kiểm chứng được (có số liệu, mã lỗi HTTP, điều kiện rõ ràng).
            - Ước lượng story point theo thang Fibonacci: 1, 2, 3, 5, 8.
            - Trả lời bằng cùng ngôn ngữ với User Story.
            - CHỈ trả về JSON đúng schema, không thêm bất kỳ lời giải thích nào.',
             'User Story: {{Title}}
            
            Mô tả: {{Description}}
            
            Loại issue có sẵn trong dự án: {{IssueTypes}}
            Component liên quan: {{Components}}
            Ghi chú bổ sung từ người dùng: {{InputText}}',
             CAST('{
               "type": "object",
               "required": ["subTasks"],
               "additionalProperties": false,
               "properties": {
                 "subTasks": {
                   "type": "array", "minItems": 1, "maxItems": 8,
                   "items": {
                     "type": "object",
                     "required": ["summary", "description", "acceptanceCriteria", "estimatePoints"],
                     "additionalProperties": false,
                     "properties": {
                       "summary": { "type": "string", "minLength": 5, "maxLength": 200 },
                       "description": { "type": "string", "minLength": 10 },
                       "acceptanceCriteria": {
                         "type": "array", "minItems": 2, "maxItems": 5,
                         "items": { "type": "string", "minLength": 10 }
                       },
                       "estimatePoints": { "type": "number", "enum": [1, 2, 3, 5, 8] },
                       "suggestedSkills": { "type": "array", "items": { "type": "string" } }
                     }
                   }
                 }
               }
             }' AS JSON));
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.Sql(
            """
            DELETE FROM `RolePermission` WHERE `RoleId` LIKE '11111111-0000-0000-0000-%' OR `PermissionId` LIKE '22222222-0000-0000-0000-%';
            """);

        migrationBuilder.Sql(
            """
            DELETE FROM `AiPromptTemplate` WHERE `Id` = '66666666-0000-0000-0000-000000000001';
            """);

        migrationBuilder.Sql(
            """
            DELETE FROM `AiModel` WHERE `Id` IN ('55555555-0000-0000-0000-000000000001', '55555555-0000-0000-0000-000000000002');
            """);

        migrationBuilder.Sql(
            """
            DELETE FROM `SkillCatalog` WHERE `Id` LIKE '44444444-0000-0000-0000-%';
            """);

        migrationBuilder.Sql(
            """
            DELETE FROM `Permission` WHERE `Id` LIKE '22222222-0000-0000-0000-%';
            """);

        migrationBuilder.Sql(
            """
            DELETE FROM `Role` WHERE `Id` LIKE '11111111-0000-0000-0000-%';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `OtpCode` DROP INDEX `IX_OtpCode_ExpiresAt`, ADD KEY `IX_OtpCode_ExpiresAt` (`ExpiresAt`);
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `UserRole` DROP INDEX `IX_UserRole_Lookup`, ADD KEY `IX_UserRole_Lookup` (`UserId`, `ScopeType`, `ScopeId`);
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `UserRole` DROP INDEX `IX_UserRole_Scope`, ADD KEY `IX_UserRole_Scope` (`ScopeType`, `ScopeId`);
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiGenerationLog` DROP INDEX `IX_AiGenerationLog_RateLimit`, ADD KEY `IX_AiGenerationLog_RateLimit` (`ProjectId`, `CreatedAt`);
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiSuggestedTask` DROP INDEX `IX_AiSuggestedTask_Action`, ADD KEY `IX_AiSuggestedTask_Action` (`UserAction`, `CreatedAt`);
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Notification` DROP INDEX `IX_Notification_Inbox`, ADD KEY `IX_Notification_Inbox` (`UserId`, `IsRead`, `CreatedAt`);
            """);
        }
    }
}
