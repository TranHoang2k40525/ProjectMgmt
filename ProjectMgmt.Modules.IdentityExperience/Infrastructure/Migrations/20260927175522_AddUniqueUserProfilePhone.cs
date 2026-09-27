using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityExperience.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueUserProfilePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UQ_UserProfile_PhoneNumber",
                table: "UserProfile",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_UserProfile_PhoneNumber",
                table: "UserProfile");
        }
    }
}
