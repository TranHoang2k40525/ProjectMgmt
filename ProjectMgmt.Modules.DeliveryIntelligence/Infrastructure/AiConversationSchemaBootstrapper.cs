using Microsoft.EntityFrameworkCore;

namespace DeliveryIntelligence.Infrastructure;

/// <summary>
/// Bảo đảm các bảng lưu phiên hội thoại và chỉ mục trí nhớ AI tồn tại trên MySQL nếu ứng dụng khởi chạy trước khi chạy EF migration thủ công.
/// </summary>
public static class AiConversationSchemaBootstrapper
{
    private static volatile bool _ensured;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task EnsureTablesExistAsync(DeliveryIntelligenceDbContext db, CancellationToken ct = default)
    {
        if (_ensured || !db.Database.IsRelational())
        {
            return;
        }

        await Gate.WaitAsync(ct);
        try
        {
            if (_ensured)
            {
                return;
            }

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS `AiConversationSession` (
                    `Id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `SessionId` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `OrganizationId` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `ProjectId` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `UserId` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `CreatedByName` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `Title` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `Model` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `LastIntent` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `RecommendedTarget` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `TargetSprintId` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
                    `TurnCount` int NOT NULL DEFAULT 0,
                    `TaskCount` int NOT NULL DEFAULT 0,
                    `TotalStoryPoints` decimal(8,2) NOT NULL DEFAULT 0.00,
                    `MemoryCount` int NOT NULL DEFAULT 0,
                    `ConversationHistoryJson` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `ProposalJson` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
                    `CreatedAtUtc` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                    `UpdatedAtUtc` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                    `DeletedAtUtc` datetime(6) NULL,
                    CONSTRAINT `PRIMARY` PRIMARY KEY (`Id`),
                    CONSTRAINT `UQ_AiConversationSession_SessionId` UNIQUE (`SessionId`),
                    INDEX `IX_AiConversationSession_TenantUserProject` (`OrganizationId`, `ProjectId`, `UserId`, `IsDeleted`, `UpdatedAtUtc`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Phiên làm việc hội thoại đa lượt của Trợ Lý AI, khóa cứng theo (OrganizationId, ProjectId, UserId)';
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS `AiConversationMemoryEntry` (
                    `Id` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `ConversationSessionId` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `SessionId` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `OrganizationId` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `ProjectId` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `UserId` char(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `AnchorKey` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `TurnIndex` int NOT NULL,
                    `Category` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `Title` varchar(220) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `Keywords` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `Summary` varchar(600) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `ContentJson` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                    CONSTRAINT `PRIMARY` PRIMARY KEY (`Id`),
                    INDEX `IX_AiConversationMemoryEntry_SessionAnchor` (`ConversationSessionId`, `AnchorKey`),
                    INDEX `IX_AiConversationMemoryEntry_TenantLock` (`OrganizationId`, `ProjectId`, `UserId`, `SessionId`),
                    CONSTRAINT `FK_AiConvMemory_Session`
                        FOREIGN KEY (`ConversationSessionId`) REFERENCES `AiConversationSession` (`Id`) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Chỉ mục trí nhớ đánh mã trỏ (#mem-X) cho từng phiên Trợ Lý AI';
                """,
                ct);

            _ensured = true;
        }
        finally
        {
            Gate.Release();
        }
    }
}
