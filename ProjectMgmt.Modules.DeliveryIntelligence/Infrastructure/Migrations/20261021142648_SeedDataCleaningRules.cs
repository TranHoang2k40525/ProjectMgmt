using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryIntelligence.Infrastructure.Migrations
{
    /// <summary>
    /// Dữ liệu tham chiếu (SCRUMAI-212): 5 rule làm sạch mặc định. Chỉ thêm dòng, không đổi schema.
    /// INSERT IGNORE theo UQ_AiDataCleaningRule_Code nên chạy lại an toàn và không ghi đè rule đã được chỉnh.
    /// </summary>
    public partial class SeedDataCleaningRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT IGNORE INTO `AiDataCleaningRule` (`Id`, `Code`, `Name`, `RuleType`, `Severity`, `Config`, `AppliesTo`, `IsActive`) VALUES
                ('55555555-0000-0000-0000-000000000001', 'empty-or-too-short', 'Instruction rỗng hoặc quá ngắn', 'LengthCheck', 'Error',
                 '{"field":"Instruction","min":15,"max":20000}', 'All', 1),
                ('55555555-0000-0000-0000-000000000002', 'generic-task', 'Sub-task chung chung', 'Heuristic', 'Warning',
                 '{"arrayPath":"subTasks","field":"title","minWords":3,"phrases":["làm task","xử lý","hoàn thành","todo","task","fix bug","làm tiếp","khác"]}', 'Breakdown', 1),
                ('55555555-0000-0000-0000-000000000003', 'invalid-output-schema', 'Output không đúng schema', 'SchemaValidation', 'Error',
                 '{"requiredArray":"subTasks"}', 'Breakdown', 1),
                ('55555555-0000-0000-0000-000000000004', 'duplicate-instruction', 'Trùng instruction trong version', 'Deduplication', 'Warning',
                 '{"field":"Instruction"}', 'All', 1),
                ('55555555-0000-0000-0000-000000000005', 'pii-candidate', 'Nghi chứa dữ liệu cá nhân / bí mật', 'PiiDetection', 'Warning',
                 '{"detectors":["email","phone","secret"]}', 'All', 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // FK_AiDataQualityFlag_Rule là ON DELETE CASCADE: chỉ gỡ rule seed chưa sinh flag nào để rollback không làm mất kết quả kiểm tra.
            migrationBuilder.Sql(
                """
                DELETE r FROM `AiDataCleaningRule` r
                WHERE r.`Id` IN ('55555555-0000-0000-0000-000000000001', '55555555-0000-0000-0000-000000000002',
                                 '55555555-0000-0000-0000-000000000003', '55555555-0000-0000-0000-000000000004',
                                 '55555555-0000-0000-0000-000000000005')
                  AND NOT EXISTS (SELECT 1 FROM `AiDataQualityFlag` f WHERE f.`RuleId` = r.`Id`);
                """);
        }
    }
}
