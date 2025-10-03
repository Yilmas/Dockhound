using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dockhound.Migrations
{
    /// <inheritdoc />
    public partial class Add_Whiteboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Whiteboards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuildId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedById = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Whiteboards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhiteboardRoles",
                columns: table => new
                {
                    WhiteboardId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<decimal>(type: "decimal(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhiteboardRoles", x => new { x.WhiteboardId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_WhiteboardRoles_Whiteboards_WhiteboardId",
                        column: x => x.WhiteboardId,
                        principalTable: "Whiteboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhiteboardVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WhiteboardId = table.Column<long>(type: "bigint", nullable: false),
                    VersionIndex = table.Column<int>(type: "int", nullable: false),
                    EditorId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrevLength = table.Column<int>(type: "int", nullable: false),
                    NewLength = table.Column<int>(type: "int", nullable: false),
                    EditDistance = table.Column<int>(type: "int", nullable: false),
                    PercentChanged = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhiteboardVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhiteboardVersions_Whiteboards_WhiteboardId",
                        column: x => x.WhiteboardId,
                        principalTable: "Whiteboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhiteboardVersions_WhiteboardId_VersionIndex",
                table: "WhiteboardVersions",
                columns: new[] { "WhiteboardId", "VersionIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhiteboardRoles");

            migrationBuilder.DropTable(
                name: "WhiteboardVersions");

            migrationBuilder.DropTable(
                name: "Whiteboards");
        }
    }
}
