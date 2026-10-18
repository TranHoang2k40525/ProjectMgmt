using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeDeliveryDatabaseObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.Sql(
            """
            ALTER TABLE `Issue` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `IssueLink` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `IssueWatcher` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Comment` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Attachment` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `ActivityLog` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `IssueStatusHistory` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `IssueAssignmentHistory` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Label` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `IssueLabel` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `IssueComponentLink` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `IssueVersionLink` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `IssueRequiredSkill` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AcceptanceCriteria` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiDataset` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiDatasetVersion` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiDatasetSample` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiDataCleaningRule` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiDataQualityFlag` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiTrainingRun` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiEvaluationResult` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Issue` DROP INDEX `IX_Issue_Backlog`, ADD KEY `IX_Issue_Backlog` (`ProjectId`, `SprintId`, `RankOrder`) COMMENT 'Truy vấn Backlog & Board';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Issue` DROP INDEX `IX_Issue_Assignee`, ADD KEY `IX_Issue_Assignee` (`AssigneeId`, `StatusId`) COMMENT 'AI ASSIGNMENT: tính workload hiện tại';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Issue` DROP INDEX `IX_Issue_Resolved`, ADD KEY `IX_Issue_Resolved` (`ProjectId`, `ResolvedAt`) COMMENT 'Báo cáo velocity & cycle time';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `ActivityLog` DROP INDEX `IX_ActivityLog_Action`, ADD KEY `IX_ActivityLog_Action` (`Action`, `CreatedAt`) COMMENT 'Báo cáo tỉ lệ sửa nội dung AI sinh';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiDatasetSample` DROP INDEX `UQ_AiDatasetSample_Dedup`, ADD UNIQUE KEY `UQ_AiDatasetSample_Dedup` (`DatasetVersionId`, `ContentHash`) COMMENT 'Chặn mẫu trùng ngay ở tầng DB';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiEvaluationResult` ADD CONSTRAINT `FK_AiEvaluationResult_Model` FOREIGN KEY (`ModelId`) REFERENCES `AiModel`(`Id`) ON DELETE CASCADE;
            """);

        migrationBuilder.Sql(
            """
            CREATE TRIGGER `TRG_Issue_NoSelfParent_Insert`
            BEFORE INSERT ON `Issue`
            FOR EACH ROW
            BEGIN
                IF NEW.`ParentId` IS NOT NULL AND NEW.`ParentId` = NEW.`Id` THEN
                    SIGNAL SQLSTATE '45000'
                        SET MESSAGE_TEXT = 'Issue cannot be its own parent';
                END IF;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE TRIGGER `TRG_Issue_NoSelfParent_Update`
            BEFORE UPDATE ON `Issue`
            FOR EACH ROW
            BEGIN
                IF NEW.`ParentId` IS NOT NULL AND NEW.`ParentId` = NEW.`Id` THEN
                    SIGNAL SQLSTATE '45000'
                        SET MESSAGE_TEXT = 'Issue cannot be its own parent';
                END IF;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE VIEW `vw_UserActiveWorkload` AS
            SELECT
                i.`ProjectId`,
                i.`SprintId`,
                i.`AssigneeId`                                              AS `UserId`,
                COUNT(*)                                                    AS `OpenIssueCount`,
                SUM(CASE WHEN ws.`Category` = 'InProgress' THEN 1 ELSE 0 END) AS `InProgressCount`,
                COALESCE(SUM(i.`StoryPoints`), 0)                           AS `OpenPoints`,
                COALESCE(SUM(CASE WHEN ws.`Category` = 'InProgress' THEN i.`StoryPoints` ELSE 0 END), 0) AS `InProgressPoints`,
                SUM(CASE WHEN i.`DueDate` IS NOT NULL AND i.`DueDate` < CURDATE() THEN 1 ELSE 0 END) AS `OverdueCount`
            FROM `Issue` i
            JOIN `WorkflowStatus` ws ON ws.`Id` = i.`StatusId`
            WHERE i.`IsDeleted` = 0
              AND i.`AssigneeId` IS NOT NULL
              AND ws.`Category` <> 'Done'
            GROUP BY i.`ProjectId`, i.`SprintId`, i.`AssigneeId`;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE VIEW `vw_SprintVelocity` AS
            SELECT
                s.`ProjectId`,
                s.`Id`                            AS `SprintId`,
                s.`Name`                          AS `SprintName`,
                s.`StartDate`,
                s.`EndDate`,
                COALESCE(SUM(CASE WHEN ws.`Category` = 'Done' THEN i.`StoryPoints` ELSE 0 END), 0) AS `CompletedPoints`,
                COALESCE(SUM(i.`StoryPoints`), 0) AS `CommittedPoints`,
                COUNT(i.`Id`)                     AS `IssueCount`
            FROM `Sprint` s
            LEFT JOIN `Issue` i          ON i.`SprintId` = s.`Id` AND i.`IsDeleted` = 0
            LEFT JOIN `WorkflowStatus` ws ON ws.`Id` = i.`StatusId`
            WHERE s.`Status` = 'Completed'
            GROUP BY s.`ProjectId`, s.`Id`, s.`Name`, s.`StartDate`, s.`EndDate`;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE VIEW `vw_AiBreakdownQuality` AS
            SELECT
                l.`ProjectId`,
                l.`ModelId`,
                l.`PromptTemplateId`,
                DATE(l.`CreatedAt`)                                          AS `Day`,
                COUNT(DISTINCT l.`Id`)                                       AS `RequestCount`,
                COUNT(t.`Id`)                                                AS `SuggestedTaskCount`,
                SUM(CASE WHEN t.`UserAction` = 'Kept'     THEN 1 ELSE 0 END) AS `KeptCount`,
                SUM(CASE WHEN t.`UserAction` = 'Edited'   THEN 1 ELSE 0 END) AS `EditedCount`,
                SUM(CASE WHEN t.`UserAction` = 'Rejected' THEN 1 ELSE 0 END) AS `RejectedCount`,
                AVG(t.`EditDistanceRatio`)                                   AS `AvgEditDistance`,
                AVG(l.`LatencyMs`)                                           AS `AvgLatencyMs`
            FROM `AiGenerationLog` l
            LEFT JOIN `AiSuggestedTask` t ON t.`AiGenerationLogId` = l.`Id`
            WHERE l.`Status` = 'Completed'
            GROUP BY l.`ProjectId`, l.`ModelId`, l.`PromptTemplateId`, DATE(l.`CreatedAt`);
            """);

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE VIEW `vw_AiAssignmentAccuracy` AS
            SELECT
                r.`ProjectId`,
                r.`Strategy`,
                DATE(r.`CreatedAt`)                                             AS `Day`,
                COUNT(d.`Id`)                                                   AS `DecisionCount`,
                SUM(CASE WHEN d.`Outcome` = 'Accepted'   THEN 1 ELSE 0 END)     AS `AcceptedCount`,
                SUM(CASE WHEN d.`Outcome` = 'Overridden' THEN 1 ELSE 0 END)     AS `OverriddenCount`,
                SUM(CASE WHEN d.`WasReassignedLater` = 1 THEN 1 ELSE 0 END)     AS `ReassignedLaterCount`,
                AVG(d.`ActualCycleTimeHours`)                                   AS `AvgCycleTimeHours`
            FROM `AiAssignmentRun` r
            JOIN `AiAssignmentDecision` d ON d.`RunId` = r.`Id`
            GROUP BY r.`ProjectId`, r.`Strategy`, DATE(r.`CreatedAt`);
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `AiDataCleaningRule` (`Id`, `Code`, `Name`, `RuleType`, `Severity`, `AppliesTo`, `Config`) VALUES
            ('77777777-0000-0000-0000-000000000001', 'schema.valid',    'Output phải khớp JSON Schema',            'SchemaValidation', 'Error',   'Breakdown',
             JSON_OBJECT('schemaRef', 'breakdown.system.v1')),
            ('77777777-0000-0000-0000-000000000002', 'pii.email',       'Phát hiện địa chỉ email',                 'PiiDetection',     'Error',   'All',
             JSON_OBJECT('pattern', '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}', 'action', 'redact')),
            ('77777777-0000-0000-0000-000000000003', 'pii.phone.vn',    'Phát hiện số điện thoại Việt Nam',        'PiiDetection',     'Error',   'All',
             JSON_OBJECT('pattern', '(0|\\+84)[0-9]{9,10}', 'action', 'redact')),
            ('77777777-0000-0000-0000-000000000004', 'pii.secret',      'Phát hiện API key / token / mật khẩu',    'PiiDetection',     'Error',   'All',
             JSON_OBJECT('keywords', JSON_ARRAY('api_key','apikey','password','secret','token','connectionstring','bearer'))),
            ('77777777-0000-0000-0000-000000000005', 'dedup.hash',      'Loại bỏ mẫu trùng theo ContentHash',      'Deduplication',    'Error',   'All',
             JSON_OBJECT('hashField', 'ContentHash')),
            ('77777777-0000-0000-0000-000000000006', 'length.min',      'Story quá ngắn, không đủ ngữ cảnh',       'LengthCheck',      'Warning', 'Breakdown',
             JSON_OBJECT('minInputChars', 40, 'minOutputChars', 120)),
            ('77777777-0000-0000-0000-000000000007', 'length.token.max','Vượt quá context window của model',       'LengthCheck',      'Error',   'All',
             JSON_OBJECT('maxTokens', 8192)),
            ('77777777-0000-0000-0000-000000000008', 'lang.consistent', 'Ngôn ngữ input và output phải khớp',      'LanguageCheck',    'Warning', 'Breakdown',
             JSON_OBJECT('allowed', JSON_ARRAY('vi','en'))),
            ('77777777-0000-0000-0000-000000000009', 'quality.rejected','Mẫu bị người dùng Rejected - không dùng làm nhãn dương', 'Heuristic', 'Warning', 'Breakdown',
             JSON_OBJECT('excludeUserAction', JSON_ARRAY('Rejected'))),
            ('77777777-0000-0000-0000-000000000010','quality.generic', 'Sub-task chung chung, không có giá trị',   'Heuristic',        'Warning', 'Breakdown',
             JSON_OBJECT('banPhrases', JSON_ARRAY('nghiên cứu','tìm hiểu','họp bàn','kiểm tra lại','làm rõ yêu cầu','trao đổi với team')));
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.Sql(
            """
            DELETE FROM `AiDataCleaningRule` WHERE `Id` LIKE '77777777-0000-0000-0000-%';
            """);

        migrationBuilder.Sql(
            """
            DROP VIEW IF EXISTS `vw_AiAssignmentAccuracy`;
            """);

        migrationBuilder.Sql(
            """
            DROP VIEW IF EXISTS `vw_AiBreakdownQuality`;
            """);

        migrationBuilder.Sql(
            """
            DROP VIEW IF EXISTS `vw_SprintVelocity`;
            """);

        migrationBuilder.Sql(
            """
            DROP VIEW IF EXISTS `vw_UserActiveWorkload`;
            """);

        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS `TRG_Issue_NoSelfParent_Update`;
            """);

        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS `TRG_Issue_NoSelfParent_Insert`;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiEvaluationResult` DROP FOREIGN KEY `FK_AiEvaluationResult_Model`;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Issue` DROP INDEX `IX_Issue_Backlog`, ADD KEY `IX_Issue_Backlog` (`ProjectId`, `SprintId`, `RankOrder`);
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Issue` DROP INDEX `IX_Issue_Assignee`, ADD KEY `IX_Issue_Assignee` (`AssigneeId`, `StatusId`);
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Issue` DROP INDEX `IX_Issue_Resolved`, ADD KEY `IX_Issue_Resolved` (`ProjectId`, `ResolvedAt`);
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `ActivityLog` DROP INDEX `IX_ActivityLog_Action`, ADD KEY `IX_ActivityLog_Action` (`Action`, `CreatedAt`);
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiDatasetSample` DROP INDEX `UQ_AiDatasetSample_Dedup`, ADD UNIQUE KEY `UQ_AiDatasetSample_Dedup` (`DatasetVersionId`, `ContentHash`);
            """);
        }
    }
}
