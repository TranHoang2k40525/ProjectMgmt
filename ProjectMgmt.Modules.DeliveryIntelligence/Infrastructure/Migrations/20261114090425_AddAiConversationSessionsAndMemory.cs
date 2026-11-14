using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiConversationSessionsAndMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddIssueColumnIfMissing(migrationBuilder, "BlockReason", "varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL");
            AddIssueColumnIfMissing(migrationBuilder, "BlockedAt", "datetime(6) NULL");
            AddIssueColumnIfMissing(migrationBuilder, "EstimatedHours", "decimal(5,2) NULL");
            AddIssueColumnIfMissing(migrationBuilder, "IsBlocked", "tinyint(1) NOT NULL DEFAULT 0");
            AddIssueColumnIfMissing(migrationBuilder, "RemainingHours", "decimal(5,2) NULL");

            migrationBuilder.CreateTable(
                name: "AiConversationSession",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SessionId = table.Column<string>(type: "varchar(120)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByName = table.Column<string>(type: "varchar(150)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(300)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Model = table.Column<string>(type: "varchar(100)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastIntent = table.Column<string>(type: "varchar(50)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecommendedTarget = table.Column<string>(type: "varchar(30)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetSprintId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TurnCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TaskCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalStoryPoints = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MemoryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ConversationHistoryJson = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProposalJson = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Phiên làm việc hội thoại đa lượt của Trợ Lý AI, khóa cứng theo (OrganizationId, ProjectId, UserId)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiConversationMemoryEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConversationSessionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SessionId = table.Column<string>(type: "varchar(120)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnchorKey = table.Column<string>(type: "varchar(32)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TurnIndex = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "varchar(50)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(220)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Keywords = table.Column<string>(type: "varchar(500)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Summary = table.Column<string>(type: "varchar(600)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentJson = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiConversationMemoryEntry_AiConversationSession_Conversation~",
                        column: x => x.ConversationSessionId,
                        principalTable: "AiConversationSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Chỉ mục trí nhớ đánh mã trỏ (#mem-X) cho từng phiên Trợ Lý AI")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_AiConversationMemoryEntry_SessionAnchor",
                table: "AiConversationMemoryEntry",
                columns: new[] { "ConversationSessionId", "AnchorKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversationMemoryEntry_TenantLock",
                table: "AiConversationMemoryEntry",
                columns: new[] { "OrganizationId", "ProjectId", "UserId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversationSession_TenantUserProject",
                table: "AiConversationSession",
                columns: new[] { "OrganizationId", "ProjectId", "UserId", "IsDeleted", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_AiConversationSession_SessionId",
                table: "AiConversationSession",
                column: "SessionId",
                unique: true);
        }

        private static void AddIssueColumnIfMissing(MigrationBuilder migrationBuilder, string columnName, string columnDefinition)
        {
            migrationBuilder.Sql($"""
                SET @col_exists = (
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Issue' AND COLUMN_NAME = '{columnName}'
                );
                SET @ddl = IF(@col_exists = 0,
                    'ALTER TABLE `Issue` ADD COLUMN `{columnName}` {columnDefinition}',
                    'SELECT 1');
                PREPARE stmt FROM @ddl;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiConversationMemoryEntry");

            migrationBuilder.DropTable(
                name: "AiConversationSession");

            migrationBuilder.DropColumn(
                name: "BlockReason",
                table: "Issue");

            migrationBuilder.DropColumn(
                name: "BlockedAt",
                table: "Issue");

            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "Issue");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "Issue");

            migrationBuilder.DropColumn(
                name: "RemainingHours",
                table: "Issue");
        }
    }
}
