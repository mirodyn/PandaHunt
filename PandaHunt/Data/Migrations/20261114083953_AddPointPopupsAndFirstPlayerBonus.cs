using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandaHunt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPointPopupsAndFirstPlayerBonus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Seen",
                table: "PointEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true); // points given before popups existed count as already seen

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "PointEntries",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstPlayerBonusPoints",
                table: "GameSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1); // the existing settings row starts with the default 1-point bonus
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Seen",
                table: "PointEntries");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "PointEntries");

            migrationBuilder.DropColumn(
                name: "FirstPlayerBonusPoints",
                table: "GameSettings");
        }
    }
}
