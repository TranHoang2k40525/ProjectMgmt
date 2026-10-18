using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityExperience.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityExperienceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiModel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(100)", nullable: false, comment: "qwen2.5:3b-instruct, qwen2.5:3b-scrum-lora-v1", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(150)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Provider = table.Column<string>(type: "varchar(50)", nullable: false, defaultValue: "Ollama", comment: "Ollama / OpenAI / Local", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaskType = table.Column<string>(type: "varchar(30)", nullable: false, comment: "Breakdown / Assignment / Embedding", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BaseModelCode = table.Column<string>(type: "varchar(100)", nullable: true, comment: "Model gốc nếu đây là bản fine-tune", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdapterPath = table.Column<string>(type: "varchar(500)", nullable: true, comment: "Đường dẫn LoRA adapter / Modelfile", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TrainingRunId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> AiTrainingRun.Id nếu sinh từ fine-tune", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "Model đang được dùng ở production"),
                    ContextWindow = table.Column<int>(type: "int", nullable: true),
                    DefaultParams = table.Column<string>(type: "json", nullable: true, comment: "temperature, top_p, num_predict, seed...", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiModel_TaskType", "`TaskType` IN ('Breakdown','Assignment','Embedding')");
                },
                comment: "Registry model - BẮT BUỘC có nếu định fine-tune: phải biết kết quả nào sinh bởi model nào để so sánh A/B")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiPromptTemplate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(80)", nullable: false, comment: "breakdown.system.v1", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    TaskType = table.Column<string>(type: "varchar(30)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Language = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: "vi", comment: "vi / en / auto", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SystemPrompt = table.Column<string>(type: "mediumtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserTemplate = table.Column<string>(type: "mediumtext", nullable: true, comment: "Template có placeholder {{Title}}, {{Description}}, {{IssueTypes}}", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JsonSchema = table.Column<string>(type: "json", nullable: true, comment: "QUAN TRỌNG: schema gửi vào tham số `format` của Ollama để ÉP định dạng ở mức decoder. Giải quyết bài toán \"AI sinh task thừa/sai format\" mà KHÔNG cần fine-tune", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Prompt có version - không hard-code prompt trong C#, nếu không sẽ không A/B test được")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id (người nhận)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<string>(type: "varchar(50)", nullable: false, comment: "IssueAssigned / NewComment / Mention / SprintEnding / AiBreakdownCompleted / AiBreakdownFailed / AiAssignmentReady", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityType = table.Column<string>(type: "varchar(50)", nullable: true, comment: "BỔ SUNG: Issue / Sprint / Project / AiGenerationLog", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "BỔ SUNG: v2.0 thiếu cột này -> bấm vào thông báo không biết đi đâu", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD: lọc thông báo theo project", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id. NULL = do hệ thống/AI", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsRead = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Thông báo, kết hợp SignalR đẩy realtime")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Permission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(80)", nullable: false, comment: "issue.create, sprint.close, ai.breakdown.request...", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(255)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Grouping = table.Column<string>(type: "varchar(50)", nullable: true, comment: "Nhóm hiển thị trên UI phân quyền", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Danh mục hành động nhỏ nhất có thể cấp phép")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(80)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Scope = table.Column<string>(type: "varchar(20)", nullable: false, comment: "Phạm vi mặc định: System / Organization / Project", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSystem = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "Vai trò hệ thống, không cho xóa"),
                    Description = table.Column<string>(type: "varchar(255)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_Role_Scope", "`Scope` IN ('System','Organization','Project')");
                },
                comment: "Danh mục vai trò RBAC")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "SkillCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(60)", nullable: false, comment: "dotnet, angular, sql, devops, testing...", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(120)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(50)", nullable: true, comment: "Backend / Frontend / Database / QA / DevOps / Design", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Danh mục kỹ năng chuẩn hóa, dùng chung cho user và issue")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "GUID - PK toàn hệ thống", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(256)", nullable: false, comment: "Định danh đăng nhập chính", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedEmail = table.Column<string>(type: "varchar(256)", nullable: false, comment: "Email viết HOA để so sánh không phân biệt hoa/thường", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordHash = table.Column<string>(type: "varchar(255)", nullable: true, comment: "BCrypt/Argon2. NULL nếu tài khoản thuần OAuth", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsEmailVerified = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true, comment: "Soft-disable, không xóa dữ liệu"),
                    SecurityStamp = table.Column<Guid>(type: "char(36)", nullable: false, comment: "Đổi giá trị này để vô hiệu toàn bộ token cũ", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastLoginAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                },
                comment: "Tài khoản đăng nhập cốt lõi")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiGenerationLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Issue.Id (Story gốc được yêu cầu chia nhỏ)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Project.Id. Denormalize để rate-limit theo project không phải JOIN", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> User.Id (PO/Tech Lead kích hoạt)", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModelId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PromptTemplateId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InputText = table.Column<string>(type: "text", nullable: true, comment: "Mô tả bổ sung người dùng nhập", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RenderedPrompt = table.Column<string>(type: "mediumtext", nullable: true, comment: "Prompt cuối cùng đã ghép - BẮT BUỘC lưu để tái lập kết quả", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawResponseJson = table.Column<string>(type: "mediumtext", nullable: true, comment: "Phản hồi thô trước khi parse, phục vụ audit & đổi logic parse", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParsedJson = table.Column<string>(type: "json", nullable: true, comment: "Sau khi parse & validate schema", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Pending", comment: "Pending / Processing / Completed / Failed / Cancelled", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(1000)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorCode = table.Column<string>(type: "varchar(50)", nullable: true, comment: "Timeout / InvalidJson / SchemaMismatch / OllamaUnavailable", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: true, comment: "Theo dõi hiệu năng model cục bộ"),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    HangfireJobId = table.Column<string>(type: "varchar(64)", nullable: true, comment: "Truy vết ngược sang Hangfire dashboard khi debug", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AppliedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, comment: "Thời điểm user bấm \"Áp dụng\". NULL = chưa từng áp dụng"),
                    AppliedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "Số sub-task thực sự được tạo (có thể < số gợi ý)"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiGenerationLog_Status", "`Status` IN ('Pending','Processing','Completed','Failed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_AiGenerationLog_Model",
                        column: x => x.ModelId,
                        principalTable: "AiModel",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiGenerationLog_Prompt",
                        column: x => x.PromptTemplateId,
                        principalTable: "AiPromptTemplate",
                        principalColumn: "Id");
                },
                comment: "Mỗi lần yêu cầu AI chia nhỏ Story")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "RolePermission",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PermissionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermission_Permission",
                        column: x => x.PermissionId,
                        principalTable: "Permission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermission_Role",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "N-N Role <-> Permission")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "ExternalLogin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Provider = table.Column<string>(type: "varchar(50)", nullable: false, comment: "Google / Microsoft / Facebook", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderKey = table.Column<string>(type: "varchar(255)", nullable: false, comment: "claim \"sub\" - KHÔNG dùng email vì email đổi được", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LinkedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLogin_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Liên kết đăng nhập OAuth2")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "OtpCode",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CodeHash = table.Column<string>(type: "varchar(255)", nullable: false, comment: "HASH của OTP, không lưu plain-text", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<string>(type: "varchar(30)", nullable: false, comment: "VerifyEmail / ResetPassword / Login2FA", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsUsed = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "Chống replay"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "Chống brute-force, khóa sau N lần"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_OtpCode_Purpose", "`Purpose` IN ('VerifyEmail','ResetPassword','Login2FA')");
                    table.ForeignKey(
                        name: "FK_OtpCode_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Mã OTP đa mục đích")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokenHash = table.Column<string>(type: "char(64)", nullable: false, comment: "SHA-256 hex của refresh token", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsRevoked = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    ReplacedByTokenId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "Chuỗi rotation - phát hiện token reuse attack", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByIp = table.Column<string>(type: "varchar(45)", nullable: true, comment: "IPv6-safe", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAgent = table.Column<string>(type: "varchar(400)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshToken_Replaced",
                        column: x => x.ReplacedByTokenId,
                        principalTable: "RefreshToken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RefreshToken_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Chỉ RefreshToken lưu DB; Access Token JWT tự-chứa nên không lưu")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "UserProfile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(150)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AvatarUrl = table.Column<string>(type: "varchar(500)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "varchar(30)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timezone = table.Column<string>(type: "varchar(64)", nullable: false, defaultValue: "Asia/Ho_Chi_Minh", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JobTitle = table.Column<string>(type: "varchar(150)", nullable: true, comment: "AI ASSIGNMENT: dùng cho cold-start người mới", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeniorityLevel = table.Column<string>(type: "varchar(20)", nullable: true, comment: "Intern/Junior/Middle/Senior/Lead - cold-start", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    YearsOfExperience = table.Column<decimal>(type: "decimal(4,1)", nullable: true, comment: "Cold-start khi chưa có log task"),
                    Bio = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_UserProfile_Seniority", "`SeniorityLevel` IS NULL OR `SeniorityLevel` IN ('Intern','Junior','Middle','Senior','Lead','Principal')");
                    table.ForeignKey(
                        name: "FK_UserProfile_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Thông tin cá nhân, tách khỏi bảng bảo mật User (1-1)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "UserRole",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RoleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScopeType = table.Column<string>(type: "varchar(20)", nullable: false, comment: "System / Organization / Project", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScopeId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD: Id của Organization hoặc Project. NULL nếu ScopeType=System", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GrantedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "Ai là người gán vai trò này", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    ScopeKey = table.Column<Guid>(type: "char(36)", nullable: true, computedColumnSql: "IFNULL(`ScopeId`, '00000000-0000-0000-0000-000000000000')", stored: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_UserRole_ScopeType", "`ScopeType` IN ('System','Organization','Project')");
                    table.ForeignKey(
                        name: "FK_UserRole_Role",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRole_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Gán vai trò có phạm vi, thay cho OrgMember/ProjectMember riêng")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "UserSkill",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SkillId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProficiencyLevel = table.Column<sbyte>(type: "tinyint", nullable: false, defaultValue: (sbyte)3, comment: "1..5 - do user tự khai hoặc lead đánh giá"),
                    YearsOfExperience = table.Column<decimal>(type: "decimal(4,1)", nullable: true),
                    IsSelfDeclared = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true, comment: "0 = đã được lead xác nhận, tin cậy hơn"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true, defaultValueSql: "NULL ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_UserSkill_Level", "`ProficiencyLevel` BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_UserSkill_Skill",
                        column: x => x.SkillId,
                        principalTable: "SkillCatalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSkill_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "AI ASSIGNMENT: nguồn dữ liệu chính cho cold-start người mới vào project")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "AiSuggestedTask",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiGenerationLogId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderIndex = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OriginalSummary = table.Column<string>(type: "varchar(500)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalDescription = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalAcceptanceCriteria = table.Column<string>(type: "json", nullable: true, comment: "Mảng chuỗi AC do AI sinh", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalEstimatePoints = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    OriginalSuggestedSkills = table.Column<string>(type: "json", nullable: true, comment: "AI gợi ý skill cần có -> nạp vào IssueRequiredSkill", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FinalSummary = table.Column<string>(type: "varchar(500)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FinalDescription = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FinalAcceptanceCriteria = table.Column<string>(type: "json", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FinalEstimatePoints = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    UserAction = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Pending", comment: "Pending / Kept / Edited / Rejected — ĐÂY LÀ NHÃN HUẤN LUYỆN QUAN TRỌNG NHẤT", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EditDistanceRatio = table.Column<decimal>(type: "decimal(5,4)", nullable: true, comment: "0.0 = giữ nguyên, 1.0 = viết lại hoàn toàn. Tính bằng Levenshtein chuẩn hóa"),
                    RejectReason = table.Column<string>(type: "varchar(500)", nullable: true, comment: "Vì sao bỏ gợi ý này - dữ liệu vàng để cải thiện prompt", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedIssueId = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> Issue.Id nếu đã được tạo thành Sub-task thật", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedBy = table.Column<Guid>(type: "char(36)", nullable: true, comment: "XMOD -> User.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiSuggestedTask_Action", "`UserAction` IN ('Pending','Kept','Edited','Rejected')");
                    table.ForeignKey(
                        name: "FK_AiSuggestedTask_Log",
                        column: x => x.AiGenerationLogId,
                        principalTable: "AiGenerationLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "BẢNG CỐT LÕI CHO FINE-TUNE. Chỉ lưu RawResponseJson như thiết kế v2.0 thì KHÔNG BAO GIỜ biết người dùng đã sửa gì -> mất sạch tín hiệu huấn luyện")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationLog_Error",
                table: "AiGenerationLog",
                columns: new[] { "ErrorCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationLog_HangfireJobId",
                table: "AiGenerationLog",
                column: "HangfireJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationLog_Issue",
                table: "AiGenerationLog",
                columns: new[] { "IssueId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationLog_RateLimit",
                table: "AiGenerationLog",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationLog_Status",
                table: "AiGenerationLog",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationLog_User",
                table: "AiGenerationLog",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiModel_DisplayName",
                table: "AiModel",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_AiModel_Task_Active",
                table: "AiModel",
                columns: new[] { "TaskType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UQ_AiModel_Code",
                table: "AiModel",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiPromptTemplate_Active",
                table: "AiPromptTemplate",
                columns: new[] { "TaskType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UQ_AiPromptTemplate",
                table: "AiPromptTemplate",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "FT_AiSuggestedTask_Search",
                table: "AiSuggestedTask",
                columns: new[] { "OriginalSummary", "OriginalDescription", "FinalSummary", "FinalDescription" })
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "IX_AiSuggestedTask_Action",
                table: "AiSuggestedTask",
                columns: new[] { "UserAction", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiSuggestedTask_CreatedIssue",
                table: "AiSuggestedTask",
                column: "CreatedIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSuggestedTask_Log",
                table: "AiSuggestedTask",
                columns: new[] { "AiGenerationLogId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogin_UserId",
                table: "ExternalLogin",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_ExternalLogin_Provider",
                table: "ExternalLogin",
                columns: new[] { "Provider", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "FT_Notification_Search",
                table: "Notification",
                columns: new[] { "Title", "Content" })
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Entity",
                table: "Notification",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Inbox",
                table: "Notification",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Project_Created",
                table: "Notification",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Type_Created",
                table: "Notification",
                columns: new[] { "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpCode_ExpiresAt",
                table: "OtpCode",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCode_User_Purpose",
                table: "OtpCode",
                columns: new[] { "UserId", "Purpose", "IsUsed" });

            migrationBuilder.CreateIndex(
                name: "UQ_Permission_Code",
                table: "Permission",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_ExpiresAt",
                table: "RefreshToken",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshToken",
                columns: new[] { "UserId", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "UQ_RefreshToken_Hash",
                table: "RefreshToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Role_Name",
                table: "Role",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionId",
                table: "RolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillCatalog_Category_Name",
                table: "SkillCatalog",
                columns: new[] { "Category", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SkillCatalog_Name",
                table: "SkillCatalog",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UQ_SkillCatalog_Code",
                table: "SkillCatalog",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_Active_Created",
                table: "User",
                columns: new[] { "IsActive", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_User_LastLoginAt",
                table: "User",
                column: "LastLoginAt");

            migrationBuilder.CreateIndex(
                name: "UQ_User_NormalizedEmail",
                table: "User",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "FT_UserProfile_Search",
                table: "UserProfile",
                columns: new[] { "DisplayName", "JobTitle", "Bio" })
                .Annotation("MySql:FullTextIndex", true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_DisplayName",
                table: "UserProfile",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_JobTitle",
                table: "UserProfile",
                column: "JobTitle");

            migrationBuilder.CreateIndex(
                name: "UQ_UserProfile_UserId",
                table: "UserProfile",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_Lookup",
                table: "UserRole",
                columns: new[] { "UserId", "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_Scope",
                table: "UserRole",
                columns: new[] { "ScopeType", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "UQ_UserRole_Scoped",
                table: "UserRole",
                columns: new[] { "UserId", "RoleId", "ScopeType", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSkill_SkillId",
                table: "UserSkill",
                columns: new[] { "SkillId", "ProficiencyLevel" });

            migrationBuilder.CreateIndex(
                name: "UQ_UserSkill",
                table: "UserSkill",
                columns: new[] { "UserId", "SkillId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSuggestedTask");

            migrationBuilder.DropTable(
                name: "ExternalLogin");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "OtpCode");

            migrationBuilder.DropTable(
                name: "RefreshToken");

            migrationBuilder.DropTable(
                name: "RolePermission");

            migrationBuilder.DropTable(
                name: "UserProfile");

            migrationBuilder.DropTable(
                name: "UserRole");

            migrationBuilder.DropTable(
                name: "UserSkill");

            migrationBuilder.DropTable(
                name: "AiGenerationLog");

            migrationBuilder.DropTable(
                name: "Permission");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "SkillCatalog");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "AiModel");

            migrationBuilder.DropTable(
                name: "AiPromptTemplate");
        }
    }
}
