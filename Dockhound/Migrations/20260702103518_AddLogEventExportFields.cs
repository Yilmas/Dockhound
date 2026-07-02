using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dockhound.Migrations
{
    /// <inheritdoc />
    public partial class AddLogEventExportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EventName",
                table: "LogEvents",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "LogEvents",
                type: "decimal(20,0)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogEvents_GuildId_EventName_Updated",
                table: "LogEvents",
                columns: new[] { "GuildId", "EventName", "Updated" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEvents_GuildId_MessageId_Updated",
                table: "LogEvents",
                columns: new[] { "GuildId", "MessageId", "Updated" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEvents_GuildId_Updated",
                table: "LogEvents",
                columns: new[] { "GuildId", "Updated" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEvents_GuildId_UserId_Updated",
                table: "LogEvents",
                columns: new[] { "GuildId", "UserId", "Updated" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogEvents_GuildId_EventName_Updated",
                table: "LogEvents");

            migrationBuilder.DropIndex(
                name: "IX_LogEvents_GuildId_MessageId_Updated",
                table: "LogEvents");

            migrationBuilder.DropIndex(
                name: "IX_LogEvents_GuildId_Updated",
                table: "LogEvents");

            migrationBuilder.DropIndex(
                name: "IX_LogEvents_GuildId_UserId_Updated",
                table: "LogEvents");

            migrationBuilder.DropColumn(
                name: "GuildId",
                table: "LogEvents");

            migrationBuilder.AlterColumn<string>(
                name: "EventName",
                table: "LogEvents",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
