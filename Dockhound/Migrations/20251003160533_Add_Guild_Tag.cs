using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dockhound.Migrations
{
    /// <inheritdoc />
    public partial class Add_Guild_Tag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tag",
                table: "Guilds",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_Tag",
                table: "Guilds",
                column: "Tag",
                unique: true,
                filter: "[Tag] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guilds_Tag",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "Tag",
                table: "Guilds");
        }
    }
}
