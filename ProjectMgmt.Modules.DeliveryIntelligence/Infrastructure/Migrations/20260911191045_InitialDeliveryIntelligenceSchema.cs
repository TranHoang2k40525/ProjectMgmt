using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDeliveryIntelligenceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiDataCleaningRule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(80)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RuleType = table.Column<string>(type: "varchar(30)", nullable: false, comment: "SchemaValidation / PiiDetection / Deduplication / LengthCheck / LanguageCheck / Heuristic", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Severity = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Warning", comment: "Info / Warning / Error (Error = tự động Reject)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Config = table.Column<string>(type: "json", nullable: true, comment: "Regex, ngưỡng min/max, danh sách từ cấm...", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AppliesTo = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "All", comment: "Breakdown / Assignment / All", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiDataCleaningRule_Severity", "`Severity` IN ('Info','Warning','Error')");
                },
                comment: "Rule làm sạch dạng cấu hình - không hard-code trong script Python")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiDataset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(80)", nullable: false, comment: "breakdown-vi-v1, assignment-ranking-v1", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaskType = table.Column<string>(type: "varchar(30)", nullable: false, comment: "Breakdown / Assignment", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Languages = table.Column<string>(type: "json", nullable: true, comment: "[\"vi\",\"en\"]", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiDataset_TaskType", "`TaskType` IN ('Breakdown','Assignment')");
                },
                comment: "Bộ dữ liệu huấn luyện")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Issue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Project.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueNumber = table.Column<int>(type: "int", nullable: false, comment: "BỔ SUNG: số thứ tự trong project, ghép Project.ProjectKey thành \"PROJ-123\""),
                    SprintId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> Sprint.Id. NULL = còn ở Backlog", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "Self-ref: Epic -> Story -> Sub-task", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EpicId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "BỔ SUNG: denormalize Epic gốc, tránh đệ quy khi gom nhóm Backlog", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssigneeId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReporterId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StatusId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> WorkflowStatus.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueTypeId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> IssueType.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PriorityId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> Priority.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(500)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "mediumtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoryPoints = table.Column<decimal>(type: "decimal(6,2)", nullable: true, comment: "DECIMAL thay INT để hỗ trợ 0.5 point"),
                    OriginalEstimateMinutes = table.Column<int>(type: "int", nullable: true),
                    TimeSpentMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "AI ASSIGNMENT: feature ước lượng độ chính xác"),
                    RankOrder = table.Column<decimal>(type: "decimal(30,15)", nullable: false, comment: "Chèn giữa = trung bình cộng 2 rank kề. Job rebalance khi khoảng cách < 1e-9"),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "AI ASSIGNMENT: lần đầu vào trạng thái InProgress"),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "AI ASSIGNMENT: thời điểm vào Done, dùng tính cycle time"),
                    IsAiGenerated = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "Sub-task do AI Breakdown sinh ra"),
                    AiGenerationLogId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> AiGenerationLog.Id. Truy vết sub-task đến từ request AI nào", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAiAssigned = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "BỔ SUNG: assignee hiện tại do AI gợi ý và được chấp nhận"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_Issue_AiLogConsistency", "`IsAiGenerated` = 0 OR `AiGenerationLogId` IS NOT NULL");
                    table.CheckConstraint("CK_Issue_Points", "`StoryPoints` IS NULL OR `StoryPoints` >= 0");
                    table.ForeignKey(
                        name: "FK_Issue_Parent",
                        column: x => x.ParentId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Entity trung tâm: Epic/Story/Task/Bug/Sub-task")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Label",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Project.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(60)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ColorHex = table.Column<string>(type: "char(7)", nullable: false, defaultValue: "#DFE1E6", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Nhãn tự do theo project")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiDatasetVersion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DatasetId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VersionTag = table.Column<string>(type: "varchar(40)", nullable: false, comment: "v1.0.0", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SampleCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TrainCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ValidationCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TestCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SplitSeed = table.Column<int>(type: "int", nullable: true, comment: "Seed chia split - BẮT BUỘC để tái lập được kết quả"),
                    Checksum = table.Column<string>(type: "char(64)", nullable: true, comment: "SHA-256 của file JSONL export", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExportPath = table.Column<string>(type: "varchar(500)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsFrozen = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "Đã đóng băng thì KHÔNG được sửa sample nữa"),
                    FrozenAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiDatasetVersion_Dataset",
                        column: x => x.DatasetId,
                        principalTable: "AiDataset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Version hóa dataset - so sánh model chỉ có ý nghĩa khi cùng dataset version")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AcceptanceCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "SỬA: cho phép gắn mọi loại issue, không chỉ Story (ví dụ JSON ở Mục 5.9.8 sinh AC cho Sub-task)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "text", nullable: false, comment: "Given-When-Then hoặc gạch đầu dòng", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsMet = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    MetBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OrderIndex = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Source = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Manual", comment: "AI / Manual - đánh giá độ tin cậy gợi ý AI", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiGenerationLogId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD: truy vết AC này do request AI nào sinh", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WasEditedAfterAi = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "BỔ SUNG: nhãn huấn luyện - AC do AI sinh có bị sửa không"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AcceptanceCriteria_Source", "`Source` IN ('AI','Manual')");
                    table.ForeignKey(
                        name: "FK_AcceptanceCriteria_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Tiêu chí nghiệm thu, tick được từng dòng cho Definition of Done")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "ActivityLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id. NULL = do hệ thống/AI thực hiện", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Action = table.Column<string>(type: "varchar(60)", nullable: false, comment: "Created / StatusChanged / AssigneeChanged / AiBreakdownApplied...", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FieldName = table.Column<string>(type: "varchar(80)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldValue = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewValue = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Detail = table.Column<string>(type: "json", nullable: true, comment: "Payload đầy đủ dạng JSON, query được bằng JSON_EXTRACT", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Source = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "User", comment: "User / System / AI - phân biệt để báo cáo AI", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_ActivityLog_Source", "`Source` IN ('User','System','AI')");
                    table.ForeignKey(
                        name: "FK_ActivityLog_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Nhật ký thay đổi tự động - sinh qua IActivityLogService")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Attachment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UploadedBy = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileName = table.Column<string>(type: "varchar(255)", nullable: false, comment: "Tên file gốc", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoredPath = table.Column<string>(type: "varchar(500)", nullable: false, comment: "Đường dẫn local / S3 key", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(120)", nullable: false, comment: "BỔ SUNG: cần cho Content-Disposition khi tải về", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false, comment: "BỔ SUNG: kiểm tra quota"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachment_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "File đính kèm")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Comment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentCommentId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "BỔ SUNG: reply lồng nhau", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "mediumtext", nullable: false, comment: "Markdown", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MentionedUserIds = table.Column<string>(type: "json", nullable: true, comment: "BỔ SUNG: mảng GUID được @mention, để module Notification xử lý", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsEdited = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comment_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comment_Parent",
                        column: x => x.ParentCommentId,
                        principalTable: "Comment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Bình luận do người dùng chủ động viết")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "IssueAssignmentHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromAssigneeId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id. NULL = chưa gán ai", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToAssigneeId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id. NULL = gỡ assignee", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id. NULL nếu do AI tự gán", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignmentSource = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Manual", comment: "Manual / AiSuggested / AiAuto / SelfPick", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiCandidateId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> AiAssignmentCandidate.Id nếu đến từ gợi ý AI", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoryPointsAtTime = table.Column<decimal>(type: "decimal(6,2)", nullable: true, comment: "Snapshot point tại thời điểm gán (point có thể bị đổi sau)"),
                    Reason = table.Column<string>(type: "varchar(500)", nullable: true, comment: "Lý do override nếu PM không theo gợi ý AI", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_IssueAssignHistory_Source", "`AssignmentSource` IN ('Manual','AiSuggested','AiAuto','SelfPick')");
                    table.ForeignKey(
                        name: "FK_IssueAssignHistory_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "BỔ SUNG - BẢNG QUAN TRỌNG NHẤT cho AI phân phối task: toàn bộ lịch sử giao việc + nguồn gán")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "IssueComponentLink",
                columns: table => new
                {
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ComponentId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> ProjectComponent.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => new { x.IssueId, x.ComponentId });
                    table.ForeignKey(
                        name: "FK_IssueComponent_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "BỔ SUNG: v2.0 yêu cầu báo cáo theo Component nhưng thiếu bảng nối này")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "IssueLink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceIssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetIssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LinkType = table.Column<string>(type: "varchar(30)", nullable: false, comment: "Blocks / IsBlockedBy / Relates / Duplicates / IsDuplicatedBy", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_IssueLink_NotSelf", "`SourceIssueId` <> `TargetIssueId`");
                    table.CheckConstraint("CK_IssueLink_Type", "`LinkType` IN ('Blocks','IsBlockedBy','Relates','Duplicates','IsDuplicatedBy')");
                    table.ForeignKey(
                        name: "FK_IssueLink_Source",
                        column: x => x.SourceIssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueLink_Target",
                        column: x => x.TargetIssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Quan hệ ngang giữa issue. Tạo Blocks thì tự sinh bản ghi IsBlockedBy ngược chiều")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "IssueRequiredSkill",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SkillId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> SkillCatalog.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MinLevel = table.Column<sbyte>(type: "tinyint", nullable: false, defaultValue: (sbyte)1, comment: "1..5"),
                    Weight = table.Column<decimal>(type: "decimal(4,3)", nullable: false, defaultValue: 1.000m, comment: "Trọng số khi tính skill-match score"),
                    Source = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Manual", comment: "Manual / AI / DerivedFromComponent", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_IssueRequiredSkill_Level", "`MinLevel` BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_IssueRequiredSkill_Source", "`Source` IN ('Manual','AI','DerivedFromComponent')");
                    table.ForeignKey(
                        name: "FK_IssueRequiredSkill_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "BỔ SUNG - AI ASSIGNMENT: kỹ năng yêu cầu của task, ghép với UserSkill để tính skill-match")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "IssueStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromStatusId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD. NULL = trạng thái đầu tiên khi tạo", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToStatusId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> WorkflowStatus.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromCategory = table.Column<string>(type: "varchar(20)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToCategory = table.Column<string>(type: "varchar(20)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DurationSeconds = table.Column<long>(type: "bigint", nullable: true, comment: "Thời gian ĐÃ Ở trạng thái trước đó - tính sẵn khi ghi"),
                    ChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueStatusHistory_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "BỔ SUNG - AI ASSIGNMENT: ActivityLog dạng text không tính được cycle time. Bảng này lưu có cấu trúc + duration tính sẵn")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "IssueVersionLink",
                columns: table => new
                {
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VersionId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> ProjectVersion.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LinkType = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "FixVersion", comment: "FixVersion / AffectsVersion", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => new { x.IssueId, x.VersionId, x.LinkType });
                    table.CheckConstraint("CK_IssueVersion_LinkType", "`LinkType` IN ('FixVersion','AffectsVersion')");
                    table.ForeignKey(
                        name: "FK_IssueVersion_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "BỔ SUNG: bảng nối Issue <-> Version cho release note")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "IssueWatcher",
                columns: table => new
                {
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => new { x.IssueId, x.UserId });
                    table.ForeignKey(
                        name: "FK_IssueWatcher_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "N-N theo dõi issue")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "IssueLabel",
                columns: table => new
                {
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LabelId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => new { x.IssueId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_IssueLabel_Issue",
                        column: x => x.IssueId,
                        principalTable: "Issue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueLabel_Label",
                        column: x => x.LabelId,
                        principalTable: "Label",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "N-N Issue <-> Label")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiDatasetSample",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DatasetVersionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceType = table.Column<string>(type: "varchar(30)", nullable: false, comment: "AiSuggestedTask / AiAssignmentDecision / ManualCurated / Synthetic", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceRefId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD: Id của bản ghi nguồn", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Instruction = table.Column<string>(type: "mediumtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InputJson = table.Column<string>(type: "json", nullable: false, comment: "Với Breakdown: {title, description, projectContext}. Với Assignment: {issue_features, candidates[]}", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OutputJson = table.Column<string>(type: "json", nullable: false, comment: "Đầu ra CHUẨN đã qua người duyệt", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Language = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: "vi", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokenCount = table.Column<int>(type: "int", nullable: true, comment: "Loại bỏ mẫu vượt context window"),
                    SplitType = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Train", comment: "Train / Validation / Test", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QualityStatus = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Raw", comment: "Raw -> Cleaned -> Approved / Rejected", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QualityScore = table.Column<decimal>(type: "decimal(4,3)", nullable: true, comment: "0..1 do người duyệt chấm"),
                    IsPiiRedacted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "BẮT BUỘC = 1 trước khi Approved. Story thật hay chứa tên KH, email, tên hệ thống nội bộ"),
                    ContentHash = table.Column<string>(type: "char(64)", nullable: false, comment: "SHA-256 của Instruction+Input+Output - khử trùng lặp", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewStatus = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "NotReviewed", comment: "NotReviewed / Approved / Rejected / NeedsFix", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReviewNote = table.Column<string>(type: "varchar(1000)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiDatasetSample_Quality", "`QualityStatus` IN ('Raw','Cleaned','Approved','Rejected')");
                    table.CheckConstraint("CK_AiDatasetSample_Split", "`SplitType` IN ('Train','Validation','Test')");
                    table.ForeignKey(
                        name: "FK_AiDatasetSample_Version",
                        column: x => x.DatasetVersionId,
                        principalTable: "AiDatasetVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Từng mẫu huấn luyện. Export ra JSONL từ bảng này")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiTrainingRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DatasetVersionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaskType = table.Column<string>(type: "varchar(30)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BaseModelCode = table.Column<string>(type: "varchar(100)", nullable: false, comment: "qwen2.5:3b-instruct", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Method = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "LoRA", comment: "LoRA / QLoRA / FullFineTune / GBDT (cho assignment)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Hyperparameters = table.Column<string>(type: "json", nullable: true, comment: "lora_r, lora_alpha, lr, epochs, batch_size, seed", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Queued", comment: "Queued / Running / Completed / Failed / Cancelled", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArtifactPath = table.Column<string>(type: "varchar(500)", nullable: true, comment: "Đường dẫn adapter / .gguf sau khi convert", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LogPath = table.Column<string>(type: "varchar(500)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TrainLoss = table.Column<decimal>(type: "decimal(10,6)", nullable: true),
                    ValidationLoss = table.Column<decimal>(type: "decimal(10,6)", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    HardwareInfo = table.Column<string>(type: "varchar(255)", nullable: true, comment: "GPU/CPU dùng để train, để so sánh công bằng", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiTrainingRun_Status", "`Status` IN ('Queued','Running','Completed','Failed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_AiTrainingRun_Dataset",
                        column: x => x.DatasetVersionId,
                        principalTable: "AiDatasetVersion",
                        principalColumn: "Id");
                },
                comment: "Mỗi lần fine-tune - lưu đủ để tái lập thí nghiệm")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiDataQualityFlag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SampleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RuleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Severity = table.Column<string>(type: "varchar(20)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "varchar(1000)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FieldPath = table.Column<string>(type: "varchar(200)", nullable: true, comment: "VD: $.subTasks[0].acceptanceCriteria", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsResolved = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    ResolvedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiDataQualityFlag_Rule",
                        column: x => x.RuleId,
                        principalTable: "AiDataCleaningRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiDataQualityFlag_Sample",
                        column: x => x.SampleId,
                        principalTable: "AiDatasetSample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Kết quả chạy rule làm sạch trên từng mẫu")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiEvaluationResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TrainingRunId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "NULL nếu đánh giá model gốc / prompt thuần (baseline)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModelId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DatasetVersionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SplitType = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Test", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetricName = table.Column<string>(type: "varchar(80)", nullable: false, comment: "Breakdown: json_valid_rate, schema_match_rate, avg_edit_distance, keep_rate, rouge_l. Assignment: precision@1, ndcg@3, mae_workload_gap", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetricValue = table.Column<decimal>(type: "decimal(12,6)", nullable: false),
                    SampleSize = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiEvaluationResult_Dataset",
                        column: x => x.DatasetVersionId,
                        principalTable: "AiDatasetVersion",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiEvaluationResult_TrainingRun",
                        column: x => x.TrainingRunId,
                        principalTable: "AiTrainingRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Không có bảng này thì không biết fine-tune có tốt hơn prompt thuần hay không")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "FT_AcceptanceCriteria_Content",
                table: "AcceptanceCriteria",
                column: "Content")
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "IX_AcceptanceCriteria_Issue",
                table: "AcceptanceCriteria",
                columns: new[] { "IssueId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptanceCriteria_Source",
                table: "AcceptanceCriteria",
                columns: new[] { "Source", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_Action",
                table: "ActivityLog",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_CreatedAt",
                table: "ActivityLog",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_Issue",
                table: "ActivityLog",
                columns: new[] { "IssueId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_Source_Created",
                table: "ActivityLog",
                columns: new[] { "Source", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_User_Created",
                table: "ActivityLog",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDataCleaningRule_Name",
                table: "AiDataCleaningRule",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UQ_AiDataCleaningRule_Code",
                table: "AiDataCleaningRule",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiDataQualityFlag_Queue",
                table: "AiDataQualityFlag",
                columns: new[] { "IsResolved", "Severity", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDataQualityFlag_Rule",
                table: "AiDataQualityFlag",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AiDataQualityFlag_Sample",
                table: "AiDataQualityFlag",
                columns: new[] { "SampleId", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDataset_Name",
                table: "AiDataset",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UQ_AiDataset_Code",
                table: "AiDataset",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "FT_AiDatasetSample_Instruction",
                table: "AiDatasetSample",
                column: "Instruction")
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "IX_AiDatasetSample_ReviewQueue",
                table: "AiDatasetSample",
                columns: new[] { "ReviewStatus", "QualityStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDatasetSample_Source",
                table: "AiDatasetSample",
                columns: new[] { "SourceType", "SourceRefId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDatasetSample_Split",
                table: "AiDatasetSample",
                columns: new[] { "DatasetVersionId", "SplitType", "QualityStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDatasetSample_TokenCount",
                table: "AiDatasetSample",
                columns: new[] { "DatasetVersionId", "TokenCount" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDatasetSample_Version_Review",
                table: "AiDatasetSample",
                columns: new[] { "DatasetVersionId", "ReviewStatus", "QualityStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_AiDatasetSample_Dedup",
                table: "AiDatasetSample",
                columns: new[] { "DatasetVersionId", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_AiDatasetVersion",
                table: "AiDatasetVersion",
                columns: new[] { "DatasetId", "VersionTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationResult_Dataset_Metric",
                table: "AiEvaluationResult",
                columns: new[] { "DatasetVersionId", "SplitType", "MetricName" });

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationResult_Model",
                table: "AiEvaluationResult",
                columns: new[] { "ModelId", "MetricName" });

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationResult_Run",
                table: "AiEvaluationResult",
                columns: new[] { "TrainingRunId", "MetricName" });

            migrationBuilder.CreateIndex(
                name: "IX_AiTrainingRun_Dataset",
                table: "AiTrainingRun",
                column: "DatasetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiTrainingRun_Name",
                table: "AiTrainingRun",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AiTrainingRun_Status_Created",
                table: "AiTrainingRun",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiTrainingRun_Task_Status",
                table: "AiTrainingRun",
                columns: new[] { "TaskType", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_CreatedAt",
                table: "Attachment",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_FileName",
                table: "Attachment",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_Issue",
                table: "Attachment",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "FT_Comment_Content",
                table: "Comment",
                column: "Content")
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Issue",
                table: "Comment",
                columns: new[] { "IssueId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Parent",
                table: "Comment",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comment_User_Created",
                table: "Comment",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "FT_Issue_Search",
                table: "Issue",
                columns: new[] { "Title", "Description" })
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "IX_Issue_AiLog",
                table: "Issue",
                column: "AiGenerationLogId");

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Assignee",
                table: "Issue",
                columns: new[] { "AssigneeId", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Assignee_Workload",
                table: "Issue",
                columns: new[] { "AssigneeId", "IsDeleted", "StatusId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Backlog",
                table: "Issue",
                columns: new[] { "ProjectId", "SprintId", "RankOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Board_Filter",
                table: "Issue",
                columns: new[] { "SprintId", "StatusId", "IsDeleted", "RankOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Epic",
                table: "Issue",
                column: "EpicId");

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Parent",
                table: "Issue",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Project_Due",
                table: "Issue",
                columns: new[] { "ProjectId", "IsDeleted", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Project_Recent",
                table: "Issue",
                columns: new[] { "ProjectId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Reporter_Created",
                table: "Issue",
                columns: new[] { "ReporterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Resolved",
                table: "Issue",
                columns: new[] { "ProjectId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Issue_Sprint_Status",
                table: "Issue",
                columns: new[] { "SprintId", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "UQ_Issue_Key",
                table: "Issue",
                columns: new[] { "ProjectId", "IssueNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueAssignHistory_AssignedAt",
                table: "IssueAssignmentHistory",
                column: "AssignedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IssueAssignHistory_Issue",
                table: "IssueAssignmentHistory",
                columns: new[] { "IssueId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueAssignHistory_Source",
                table: "IssueAssignmentHistory",
                columns: new[] { "AssignmentSource", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueAssignHistory_To",
                table: "IssueAssignmentHistory",
                columns: new[] { "ToAssigneeId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueComponent_Component",
                table: "IssueComponentLink",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueLabel_Label",
                table: "IssueLabel",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueLink_Target",
                table: "IssueLink",
                column: "TargetIssueId");

            migrationBuilder.CreateIndex(
                name: "UQ_IssueLink",
                table: "IssueLink",
                columns: new[] { "SourceIssueId", "TargetIssueId", "LinkType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueRequiredSkill_Skill",
                table: "IssueRequiredSkill",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "UQ_IssueRequiredSkill",
                table: "IssueRequiredSkill",
                columns: new[] { "IssueId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueStatusHistory_Category_Date",
                table: "IssueStatusHistory",
                columns: new[] { "ToCategory", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueStatusHistory_ChangedAt",
                table: "IssueStatusHistory",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IssueStatusHistory_Issue",
                table: "IssueStatusHistory",
                columns: new[] { "IssueId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueVersion_Version",
                table: "IssueVersionLink",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueWatcher_UserId",
                table: "IssueWatcher",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Label_Name",
                table: "Label",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UQ_Label_Name",
                table: "Label",
                columns: new[] { "ProjectId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptanceCriteria");

            migrationBuilder.DropTable(
                name: "ActivityLog");

            migrationBuilder.DropTable(
                name: "AiDataQualityFlag");

            migrationBuilder.DropTable(
                name: "AiEvaluationResult");

            migrationBuilder.DropTable(
                name: "Attachment");

            migrationBuilder.DropTable(
                name: "Comment");

            migrationBuilder.DropTable(
                name: "IssueAssignmentHistory");

            migrationBuilder.DropTable(
                name: "IssueComponentLink");

            migrationBuilder.DropTable(
                name: "IssueLabel");

            migrationBuilder.DropTable(
                name: "IssueLink");

            migrationBuilder.DropTable(
                name: "IssueRequiredSkill");

            migrationBuilder.DropTable(
                name: "IssueStatusHistory");

            migrationBuilder.DropTable(
                name: "IssueVersionLink");

            migrationBuilder.DropTable(
                name: "IssueWatcher");

            migrationBuilder.DropTable(
                name: "AiDataCleaningRule");

            migrationBuilder.DropTable(
                name: "AiDatasetSample");

            migrationBuilder.DropTable(
                name: "AiTrainingRun");

            migrationBuilder.DropTable(
                name: "Label");

            migrationBuilder.DropTable(
                name: "Issue");

            migrationBuilder.DropTable(
                name: "AiDatasetVersion");

            migrationBuilder.DropTable(
                name: "AiDataset");
        }
    }
}
