using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliveryIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiDocumentChunks : Migration
    {
        // Chỉ tạo bảng AiDocumentChunk. Các cột Issue (IsBlocked, BlockReason, BlockedAt, EstimatedHours, RemainingHours)
        // có trong model/snapshot nhưng chưa có migration chính thức; nhiều DB đã vá tay bằng scripts/dev-setup.ps1,
        // nên không thêm ở đây để migration không lỗi trùng cột.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiDocumentChunk",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> Project.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "XMOD -> ProjectDocument.Id", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChunkIndex = table.Column<int>(type: "int", nullable: false),
                    DocumentName = table.Column<string>(type: "varchar(500)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Heading = table.Column<string>(type: "varchar(500)", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PageNumber = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "mediumtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceVersion = table.Column<string>(type: "char(64)", nullable: false, comment: "SHA-256 của phiên bản tài liệu + model + tham số chia đoạn", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmbeddingModel = table.Column<string>(type: "varchar(120)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Dimensions = table.Column<int>(type: "int", nullable: false),
                    Embedding = table.Column<byte[]>(type: "longblob", nullable: false, comment: "float32 little-endian, đã chuẩn hóa độ dài 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRIMARY", x => x.Id);
                    table.CheckConstraint("CK_AiDocumentChunk_ChunkIndex", "`ChunkIndex` >= 0");
                    table.CheckConstraint("CK_AiDocumentChunk_Dimensions", "`Dimensions` > 0");
                },
                comment: "Đoạn tài liệu Spec Hub kèm vector embedding cho tìm kiếm ngữ nghĩa (RAG)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_AiDocumentChunk_Project_Model",
                table: "AiDocumentChunk",
                columns: new[] { "ProjectId", "EmbeddingModel" });

            migrationBuilder.CreateIndex(
                name: "UQ_AiDocumentChunk_Document_Chunk",
                table: "AiDocumentChunk",
                columns: new[] { "DocumentId", "ChunkIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiDocumentChunk");
        }
    }
}
