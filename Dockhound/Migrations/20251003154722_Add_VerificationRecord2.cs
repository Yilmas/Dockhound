using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dockhound.Migrations
{
    /// <inheritdoc />
    public partial class Add_VerificationRecord2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_VerificationRecord",
                table: "VerificationRecord");

            migrationBuilder.RenameTable(
                name: "VerificationRecord",
                newName: "VerificationRecords");

            migrationBuilder.RenameIndex(
                name: "IX_VerificationRecord_UserId_ApprovedAtUtc",
                table: "VerificationRecords",
                newName: "IX_VerificationRecords_UserId_ApprovedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_VerificationRecord_GuildId_ApprovedAtUtc",
                table: "VerificationRecords",
                newName: "IX_VerificationRecords_GuildId_ApprovedAtUtc");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VerificationRecords",
                table: "VerificationRecords",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_VerificationRecords",
                table: "VerificationRecords");

            migrationBuilder.RenameTable(
                name: "VerificationRecords",
                newName: "VerificationRecord");

            migrationBuilder.RenameIndex(
                name: "IX_VerificationRecords_UserId_ApprovedAtUtc",
                table: "VerificationRecord",
                newName: "IX_VerificationRecord_UserId_ApprovedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_VerificationRecords_GuildId_ApprovedAtUtc",
                table: "VerificationRecord",
                newName: "IX_VerificationRecord_GuildId_ApprovedAtUtc");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VerificationRecord",
                table: "VerificationRecord",
                column: "Id");
        }
    }
}
