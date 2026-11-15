using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandaHunt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrBaseUrl",
                table: "GameSettings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FloorCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Token = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloorCodeClaims",
                columns: table => new
                {
                    CodeId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorCodeClaims", x => new { x.CodeId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_FloorCodeClaims_AspNetUsers_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FloorCodeClaims_FloorCodes_CodeId",
                        column: x => x.CodeId,
                        principalTable: "FloorCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FloorCodeClaims_PlayerId",
                table: "FloorCodeClaims",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorCodes_Token",
                table: "FloorCodes",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FloorCodeClaims");

            migrationBuilder.DropTable(
                name: "FloorCodes");

            migrationBuilder.DropColumn(
                name: "QrBaseUrl",
                table: "GameSettings");
        }
    }
}
