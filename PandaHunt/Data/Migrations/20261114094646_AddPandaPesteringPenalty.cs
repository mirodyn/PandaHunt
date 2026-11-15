using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandaHunt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPandaPesteringPenalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PandaPesteringPenaltyPoints",
                table: "GameSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1); // the existing settings row starts with the default 1-point deduction
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PandaPesteringPenaltyPoints",
                table: "GameSettings");
        }
    }
}
