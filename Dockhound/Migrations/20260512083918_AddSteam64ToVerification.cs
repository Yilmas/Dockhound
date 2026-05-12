using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dockhound.Migrations
{
    /// <inheritdoc />
    public partial class AddSteam64ToVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Steam64Id",
                table: "VerificationRecords",
                type: "decimal(20,0)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Steam64Id",
                table: "VerificationRecords");
        }
    }
}
