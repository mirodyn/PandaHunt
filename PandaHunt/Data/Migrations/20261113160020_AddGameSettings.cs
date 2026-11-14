using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandaHunt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Phase0Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Phase0Code = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    RegistrationOpensAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RegistrationClosesAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    HuntStartsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    HuntEndsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSettings");
        }
    }
}
