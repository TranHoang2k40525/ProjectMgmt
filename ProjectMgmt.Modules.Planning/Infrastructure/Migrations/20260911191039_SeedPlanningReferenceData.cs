using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedPlanningReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.Sql(
            """
            ALTER TABLE `Organization` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Project` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `ProjectComponent` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `ProjectVersion` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `WorkflowStatus` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `WorkflowTransition` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `IssueType` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Priority` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Board` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `BoardColumn` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `Sprint` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `SprintSnapshot` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `SprintMemberCapacity` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `UserWorkloadSnapshot` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `UserPerformanceMetric` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiAssignmentRun` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiAssignmentCandidate` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `AiAssignmentDecision` ENGINE=InnoDB ROW_FORMAT=DYNAMIC;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `SprintSnapshot` DROP INDEX `UQ_SprintSnapshot_Day`, ADD UNIQUE KEY `UQ_SprintSnapshot_Day` (`SprintId`, `SnapshotDate`) COMMENT 'Job chạy lại trong ngày không tạo dòng trùng';
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO `Priority` (`Id`, `ProjectId`, `Name`, `Level`, `ColorHex`) VALUES
            ('33333333-0000-0000-0000-000000000001', NULL, 'Highest', 1, '#CD1317'),
            ('33333333-0000-0000-0000-000000000002', NULL, 'High',    2, '#E9494B'),
            ('33333333-0000-0000-0000-000000000003', NULL, 'Medium',  3, '#E97F33'),
            ('33333333-0000-0000-0000-000000000004', NULL, 'Low',     4, '#2A8735'),
            ('33333333-0000-0000-0000-000000000005', NULL, 'Lowest',  5, '#57A55A');
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.Sql(
            """
            DELETE FROM `Priority` WHERE `Id` LIKE '33333333-0000-0000-0000-%';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE `SprintSnapshot` DROP INDEX `UQ_SprintSnapshot_Day`, ADD UNIQUE KEY `UQ_SprintSnapshot_Day` (`SprintId`, `SnapshotDate`);
            """);
        }
    }
}
