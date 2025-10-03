using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dockhound.Migrations
{
    /// <inheritdoc />
    public partial class Add_VerificationRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VerificationRecord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuildId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Faction = table.Column<int>(type: "int", maxLength: 32, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedByUserId = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationRecord", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRecord_GuildId_ApprovedAtUtc",
                table: "VerificationRecord",
                columns: new[] { "GuildId", "ApprovedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationRecord_UserId_ApprovedAtUtc",
                table: "VerificationRecord",
                columns: new[] { "UserId", "ApprovedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificationRecord");
        }
    }
}
