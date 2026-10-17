using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BattleMapGenerator.Api.Migrations
{
    /// <inheritdoc />
    public partial class SavedMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Seed = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    RoomCount = table.Column<int>(type: "int", nullable: false),
                    Encounter = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BossSize = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Cells = table.Column<byte[]>(type: "varbinary(3600)", maxLength: 3600, nullable: false),
                    Rooms = table.Column<byte[]>(type: "varbinary(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedMaps_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedMaps_OwnerId_CreatedAt",
                table: "SavedMaps",
                columns: new[] { "OwnerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedMaps");
        }
    }
}
