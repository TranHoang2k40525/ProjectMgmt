using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrackProjectCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Project",
                type: "char(36)",
                nullable: true,
                comment: "XMOD -> User.Id; người khởi tạo dự án, không đổi theo Lead",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                """
                UPDATE `Project`
                SET `CreatedByUserId` = `LeadUserId`
                WHERE `CreatedByUserId` IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                table: "Project",
                type: "char(36)",
                nullable: false,
                comment: "XMOD -> User.Id; người khởi tạo dự án, không đổi theo Lead",
                collation: "utf8mb4_0900_ai_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true,
                oldComment: "XMOD -> User.Id; người khởi tạo dự án, không đổi theo Lead",
                oldCollation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Project_CreatedByUserId",
                table: "Project",
                column: "CreatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Project_CreatedByUserId",
                table: "Project");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Project");
        }
    }
}
