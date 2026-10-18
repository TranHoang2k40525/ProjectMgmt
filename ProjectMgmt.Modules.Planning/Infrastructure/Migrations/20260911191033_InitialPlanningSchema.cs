using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlanningSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiAssignmentRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Project.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SprintId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> Sprint.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedBy = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TriggerSource = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "Manual", comment: "Manual / AfterAiBreakdown / SprintPlanning - luồng \"AI phân phối task VỪA ĐƯỢC AI bóc tách\" dùng AfterAiBreakdown", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceGenerationLogId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> AiGenerationLog.Id. Nối 2 AI với nhau", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Strategy = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "WeightedScore", comment: "WeightedScore (giai đoạn 1) / LearnedRanker (giai đoạn 2) / RoundRobin (baseline đối chứng)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModelId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "NULL nếu dùng scoring function thuần", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Weights = table.Column<string>(type: "json", nullable: true, comment: "Trọng số dùng cho lần chạy này, VD {\"load\":0.4,\"skill\":0.3,\"history\":0.2,\"capacity\":0.1} - lưu để tái lập", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CandidateUserIds = table.Column<string>(type: "json", nullable: true, comment: "Danh sách user được xét (thành viên project tại thời điểm chạy)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Pending", comment: "Pending / Processing / Completed / Failed", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(1000)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiAssignmentRun_Status", "`Status` IN ('Pending','Processing','Completed','Failed')");
                    table.CheckConstraint("CK_AiAssignmentRun_Strategy", "`Strategy` IN ('WeightedScore','LearnedRanker','RoundRobin','Hybrid')");
                },
                comment: "Một lần chạy phân phối cho một tập issue")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Slug = table.Column<string>(type: "varchar(80)", nullable: false, comment: "URL-friendly, dùng cho subdomain/route", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OwnerId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Workspace cấp cao nhất, tương đương Site trong Jira Cloud")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Sprint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Project.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Goal = table.Column<string>(type: "varchar(1000)", nullable: true, comment: "Sprint Goal", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualStartAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "Thời điểm bấm Start thật"),
                    ActualCompleteAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Planned", comment: "Planned / Active / Completed", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderIndex = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)"),
                    ActiveGuard = table.Column<Guid>(type: "char(36)", nullable: true, computedColumnSql: "IF(`Status` = 'Active', `ProjectId`, NULL)", stored: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_Sprint_Dates", "`EndDate` IS NULL OR `StartDate` IS NULL OR `EndDate` >= `StartDate`");
                    table.CheckConstraint("CK_Sprint_Status", "`Status` IN ('Planned','Active','Completed')");
                },
                comment: "Chu kỳ Scrum. UNIQUE trên ActiveGuard ép ràng buộc 1 Sprint Active/Project")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "UserPerformanceMetric",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Project.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SprintId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> Sprint.Id. NULL = số liệu tổng toàn project", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    AssignedIssueCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CompletedIssueCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CommittedPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: false, defaultValue: 0m),
                    CompletedPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: false, defaultValue: 0m),
                    AvgCycleTimeHours = table.Column<decimal>(type: "decimal(9,2)", nullable: true, comment: "Trung bình từ InProgress -> Done"),
                    MedianCycleTimeHours = table.Column<decimal>(type: "decimal(9,2)", nullable: true, comment: "Median chống ảnh hưởng của outlier tốt hơn Avg"),
                    OnTimeRatio = table.Column<decimal>(type: "decimal(5,4)", nullable: true, comment: "Tỉ lệ hoàn thành trước DueDate"),
                    ReopenedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "Proxy cho chất lượng công việc"),
                    EstimateAccuracyRatio = table.Column<decimal>(type: "decimal(6,3)", nullable: true, comment: "TimeSpent / OriginalEstimate"),
                    CalculatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Feature \"năng lực thực tế\" cho AI phân phối task")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "UserWorkloadSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Project.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SprintId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> Sprint.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenIssueCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    InProgressCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OpenPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: false, defaultValue: 0m, comment: "Tổng point chưa Done đang gánh"),
                    InProgressPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: false, defaultValue: 0m),
                    OverdueCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CapacityPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: true, comment: "Lấy từ SprintMemberCapacity"),
                    UtilizationRatio = table.Column<decimal>(type: "decimal(6,3)", nullable: true, comment: "OpenPoints / CapacityPoints. > 1.0 = quá tải"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Hangfire job hằng ngày. Đây là feature \"độ cân bằng khối lượng công việc\" mà đề xuất nhắc tới")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiAssignmentCandidate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RunId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Issue.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CandidateUserId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rank = table.Column<int>(type: "int", nullable: false, comment: "1 = gợi ý tốt nhất"),
                    TotalScore = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    LoadBalanceScore = table.Column<decimal>(type: "decimal(9,6)", nullable: true, comment: "Càng ít point đang gánh, điểm càng cao"),
                    SkillMatchScore = table.Column<decimal>(type: "decimal(9,6)", nullable: true, comment: "Khớp IssueRequiredSkill với UserSkill"),
                    HistoryScore = table.Column<decimal>(type: "decimal(9,6)", nullable: true, comment: "Từng làm task tương tự (component/label) và làm tốt"),
                    CapacityScore = table.Column<decimal>(type: "decimal(9,6)", nullable: true, comment: "Còn dư capacity trong sprint"),
                    IsColdStart = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "TRUE = user chưa có log task, điểm tính từ UserProfile/UserSkill thay vì lịch sử"),
                    FeatureSnapshot = table.Column<string>(type: "json", nullable: false, comment: "TOÀN BỘ feature thô tại thời điểm chạy: open_points, in_progress_count, capacity, skill_levels, avg_cycle_time... KHÔNG ĐƯỢC tính lại khi train, nếu không sẽ data leakage", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Explanation = table.Column<string>(type: "varchar(1000)", nullable: true, comment: "Câu giải thích hiển thị cho PM", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAssignmentCandidate_Run",
                        column: x => x.RunId,
                        principalTable: "AiAssignmentRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Mỗi (issue, user) một dòng có điểm - đây chính là format dữ liệu chuẩn cho learning-to-rank sau này")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Project",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectKey = table.Column<string>(type: "varchar(10)", nullable: false, comment: "Mã viết tắt sinh issue key, VD \"PROJ\". `Key` là từ khóa MySQL nên đổi tên cột", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LeadUserId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueCounter = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "BỔ SUNG: bộ đếm sinh IssueNumber (PROJ-1, PROJ-2...)"),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_Project_Key_Format", "`ProjectKey` REGEXP '^[A-Z][A-Z0-9]{1,9}$'");
                    table.ForeignKey(
                        name: "FK_Project_Organization",
                        column: x => x.OrgId,
                        principalTable: "Organization",
                        principalColumn: "Id");
                },
                comment: "Dự án - đơn vị chứa Sprint/Issue/Board/cấu hình workflow riêng")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "SprintMemberCapacity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SprintId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CapacityPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: true, comment: "Số point tối đa nhận được trong sprint này"),
                    AvailableHours = table.Column<decimal>(type: "decimal(7,2)", nullable: true, comment: "Trừ nghỉ phép, họp, on-call"),
                    Note = table.Column<string>(type: "varchar(255)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintCapacity_Sprint",
                        column: x => x.SprintId,
                        principalTable: "Sprint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "BỔ SUNG - AI ASSIGNMENT: trần công suất từng người, tránh gán quá tải")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "SprintSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SprintId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: false, defaultValue: 0m, comment: "BỔ SUNG: để vẽ được ideal line"),
                    RemainingPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: false, defaultValue: 0m),
                    CompletedPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: false, defaultValue: 0m, comment: "BỔ SUNG"),
                    AddedPoints = table.Column<decimal>(type: "decimal(9,2)", nullable: false, defaultValue: 0m, comment: "BỔ SUNG: scope change trong sprint"),
                    RemainingIssueCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalIssueCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintSnapshot_Sprint",
                        column: x => x.SprintId,
                        principalTable: "Sprint",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Ảnh chụp hằng ngày cho Burndown - không thể tính on-the-fly")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiAssignmentDecision",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RunId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Issue.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SuggestedUserId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id. Ứng viên Rank = 1", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SuggestedCandidateId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FinalUserId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id. Người thực sự được gán", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Outcome = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Pending", comment: "Pending / Accepted / Overridden / Rejected — NHÃN HUẤN LUYỆN", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OverrideReason = table.Column<string>(type: "varchar(500)", nullable: true, comment: "PM ghi lý do đổi người - dữ liệu quý nhất để cải thiện model", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DecidedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DecidedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ActualCycleTimeHours = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    WasCompletedOnTime = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    WasReassignedLater = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "Bị chuyển người sau đó = dấu hiệu gợi ý sai"),
                    OutcomeEvaluatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiAssignmentDecision_Outcome", "`Outcome` IN ('Pending','Accepted','Overridden','Rejected')");
                    table.ForeignKey(
                        name: "FK_AiAssignmentDecision_Candidate",
                        column: x => x.SuggestedCandidateId,
                        principalTable: "AiAssignmentCandidate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiAssignmentDecision_Run",
                        column: x => x.RunId,
                        principalTable: "AiAssignmentRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Nhãn cho AI phân phối: PM có theo gợi ý không, và kết quả thực tế ra sao")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Board",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<string>(type: "varchar(20)", nullable: false, comment: "Scrum (gắn Sprint) hoặc Kanban (liên tục)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDefault = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_Board_Type", "`Type` IN ('Scrum','Kanban')");
                    table.ForeignKey(
                        name: "FK_Board_Project",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Một project có thể có nhiều board")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "IssueType",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(60)", nullable: false, comment: "Epic / Story / Task / Bug / Sub-task", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconKey = table.Column<string>(type: "varchar(50)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ColorHex = table.Column<string>(type: "char(7)", nullable: false, defaultValue: "#0052CC", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSubtask = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "Validate khi gán ParentId"),
                    HierarchyLevel = table.Column<sbyte>(type: "tinyint", nullable: false, defaultValue: (sbyte)1, comment: "2=Epic, 1=Story/Task/Bug, 0=Sub-task"),
                    OrderIndex = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueType_Project",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Loại issue cấu hình theo từng project")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Priority",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "SỬA: NULL = mức ưu tiên dùng chung toàn hệ thống; có giá trị = riêng project (khớp Mục 5.2)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(40)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<int>(type: "int", nullable: false, comment: "1 = Highest ... 5 = Lowest"),
                    ColorHex = table.Column<string>(type: "char(7)", nullable: false, defaultValue: "#6B778C", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconKey = table.Column<string>(type: "varchar(50)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Priority_Project",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Mức độ ưu tiên")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "ProjectComponent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", nullable: false, comment: "Backend API, Mobile App, Database...", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LeadUserId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id. Dùng auto-suggest assignee", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultSkillId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> SkillCatalog.Id. AI ASSIGNMENT: skill mặc định của component", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectComponent_Project",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Tương đương Components của Jira")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "ProjectVersion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(60)", nullable: false, comment: "v1.2.0", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsReleased = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectVersion_Project",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Tương đương Fix Version / Release của Jira")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "WorkflowStatus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(80)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(20)", nullable: false, comment: "ToDo / InProgress / Done - chuẩn hóa để tính báo cáo", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ColorHex = table.Column<string>(type: "char(7)", nullable: false, defaultValue: "#8993A4", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderIndex = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsInitial = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "Trạng thái mặc định khi tạo issue mới")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_WorkflowStatus_Category", "`Category` IN ('ToDo','InProgress','Done')");
                    table.ForeignKey(
                        name: "FK_WorkflowStatus_Project",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Mỗi Project tự định nghĩa bộ trạng thái riêng")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "BoardColumn",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BoardId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StatusId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "Cột ánh xạ tới WorkflowStatus nào", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(80)", nullable: true, comment: "Ghi đè tên hiển thị, NULL = lấy theo status", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderIndex = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    WipLimit = table.Column<int>(type: "int", nullable: true, comment: "BỔ SUNG: Mục 5.5 yêu cầu WIP limit nhưng v2.0 không có chỗ lưu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_BoardColumn_Wip", "`WipLimit` IS NULL OR `WipLimit` > 0");
                    table.ForeignKey(
                        name: "FK_BoardColumn_Board",
                        column: x => x.BoardId,
                        principalTable: "Board",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardColumn_Status",
                        column: x => x.StatusId,
                        principalTable: "WorkflowStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "BỔ SUNG: cột board + giới hạn WIP")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "WorkflowTransition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "BỔ SUNG: thiếu trong bản v2.0. Không có cột này phải JOIN 2 lần mới validate được", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromStatusId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToStatusId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(80)", nullable: true, comment: "Tên nút bấm hiển thị, VD \"Gửi review\"", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequiredPermissionCode = table.Column<string>(type: "varchar(80)", nullable: true, comment: "Chỉ role có quyền này mới được chuyển", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_WorkflowTransition_NotSelf", "`FromStatusId` <> `ToStatusId`");
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_From",
                        column: x => x.FromStatusId,
                        principalTable: "WorkflowStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_Project",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_To",
                        column: x => x.ToStatusId,
                        principalTable: "WorkflowStatus",
                        principalColumn: "Id");
                },
                comment: "Luật chuyển trạng thái hợp lệ - chặn kéo thẳng ToDo -> Done")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentCandidate_Issue",
                table: "AiAssignmentCandidate",
                columns: new[] { "IssueId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentCandidate_Run_Issue_Rank",
                table: "AiAssignmentCandidate",
                columns: new[] { "RunId", "IssueId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "UQ_AiAssignmentCandidate",
                table: "AiAssignmentCandidate",
                columns: new[] { "RunId", "IssueId", "CandidateUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentDecision_Issue",
                table: "AiAssignmentDecision",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentDecision_Outcome",
                table: "AiAssignmentDecision",
                columns: new[] { "Outcome", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_AiAssignmentDecision",
                table: "AiAssignmentDecision",
                columns: new[] { "RunId", "IssueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentRun_Project",
                table: "AiAssignmentRun",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentRun_Source",
                table: "AiAssignmentRun",
                column: "SourceGenerationLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentRun_Status_Created",
                table: "AiAssignmentRun",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAssignmentRun_Strategy_Created",
                table: "AiAssignmentRun",
                columns: new[] { "Strategy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Board_Name",
                table: "Board",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UQ_Board_Name",
                table: "Board",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumn_Name",
                table: "BoardColumn",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumn_Order",
                table: "BoardColumn",
                columns: new[] { "BoardId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "UQ_BoardColumn_Status",
                table: "BoardColumn",
                columns: new[] { "BoardId", "StatusId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueType_Name",
                table: "IssueType",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_IssueType_Project_Order",
                table: "IssueType",
                columns: new[] { "ProjectId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "UQ_IssueType_Name",
                table: "IssueType",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organization_Name",
                table: "Organization",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Organization_OwnerId",
                table: "Organization",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "UQ_Organization_Slug",
                table: "Organization",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Priority_Name",
                table: "Priority",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Priority_Project",
                table: "Priority",
                columns: new[] { "ProjectId", "Level" });

            migrationBuilder.CreateIndex(
                name: "FT_Project_Search",
                table: "Project",
                columns: new[] { "Name", "Description" })
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "IX_Project_LeadUserId",
                table: "Project",
                column: "LeadUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Name",
                table: "Project",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Project_Org_Visible_Name",
                table: "Project",
                columns: new[] { "OrgId", "IsDeleted", "IsArchived", "Name" });

            migrationBuilder.CreateIndex(
                name: "UQ_Project_Org_Key",
                table: "Project",
                columns: new[] { "OrgId", "ProjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectComponent_Name",
                table: "ProjectComponent",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UQ_ProjectComponent_Name",
                table: "ProjectComponent",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersion_Name",
                table: "ProjectVersion",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersion_Release",
                table: "ProjectVersion",
                columns: new[] { "IsReleased", "ReleaseDate" });

            migrationBuilder.CreateIndex(
                name: "UQ_ProjectVersion_Name",
                table: "ProjectVersion",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sprint_Name",
                table: "Sprint",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Sprint_Project_Dates",
                table: "Sprint",
                columns: new[] { "ProjectId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sprint_Project_Order",
                table: "Sprint",
                columns: new[] { "ProjectId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Sprint_Project_Status",
                table: "Sprint",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sprint_Status_EndDate",
                table: "Sprint",
                columns: new[] { "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "UQ_Sprint_OneActivePerProject",
                table: "Sprint",
                column: "ActiveGuard",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SprintCapacity",
                table: "SprintMemberCapacity",
                columns: new[] { "SprintId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SprintSnapshot_Day",
                table: "SprintSnapshot",
                columns: new[] { "SprintId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPerfMetric_Project_Period",
                table: "UserPerformanceMetric",
                columns: new[] { "ProjectId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPerfMetric_Sprint",
                table: "UserPerformanceMetric",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "UQ_UserPerfMetric",
                table: "UserPerformanceMetric",
                columns: new[] { "UserId", "ProjectId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkload_Project_Date",
                table: "UserWorkloadSnapshot",
                columns: new[] { "ProjectId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkload_Sprint_Date",
                table: "UserWorkloadSnapshot",
                columns: new[] { "SprintId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "UQ_UserWorkloadSnapshot",
                table: "UserWorkloadSnapshot",
                columns: new[] { "UserId", "ProjectId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatus_Name",
                table: "WorkflowStatus",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatus_Project_Order",
                table: "WorkflowStatus",
                columns: new[] { "ProjectId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "UQ_WorkflowStatus_Name",
                table: "WorkflowStatus",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_From",
                table: "WorkflowTransition",
                column: "FromStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_Name",
                table: "WorkflowTransition",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_To",
                table: "WorkflowTransition",
                column: "ToStatusId");

            migrationBuilder.CreateIndex(
                name: "UQ_WorkflowTransition",
                table: "WorkflowTransition",
                columns: new[] { "ProjectId", "FromStatusId", "ToStatusId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAssignmentDecision");

            migrationBuilder.DropTable(
                name: "BoardColumn");

            migrationBuilder.DropTable(
                name: "IssueType");

            migrationBuilder.DropTable(
                name: "Priority");

            migrationBuilder.DropTable(
                name: "ProjectComponent");

            migrationBuilder.DropTable(
                name: "ProjectVersion");

            migrationBuilder.DropTable(
                name: "SprintMemberCapacity");

            migrationBuilder.DropTable(
                name: "SprintSnapshot");

            migrationBuilder.DropTable(
                name: "UserPerformanceMetric");

            migrationBuilder.DropTable(
                name: "UserWorkloadSnapshot");

            migrationBuilder.DropTable(
                name: "WorkflowTransition");

            migrationBuilder.DropTable(
                name: "AiAssignmentCandidate");

            migrationBuilder.DropTable(
                name: "Board");

            migrationBuilder.DropTable(
                name: "Sprint");

            migrationBuilder.DropTable(
                name: "WorkflowStatus");

            migrationBuilder.DropTable(
                name: "AiAssignmentRun");

            migrationBuilder.DropTable(
                name: "Project");

            migrationBuilder.DropTable(
                name: "Organization");
        }
    }
}
