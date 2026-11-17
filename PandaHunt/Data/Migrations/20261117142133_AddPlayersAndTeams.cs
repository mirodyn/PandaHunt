using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandaHunt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayersAndTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlayerId",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IsTeam = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });


            // Existing logins each become their own player, with the login's id as the player's id,
            // so all existing points, stars, completions and claims stay valid.
            migrationBuilder.Sql("INSERT INTO Players (Id, Name, IsTeam) SELECT Id, UserName, 0 FROM AspNetUsers");
            migrationBuilder.Sql("UPDATE AspNetUsers SET PlayerId = Id");
            migrationBuilder.DropForeignKey(
                name: "FK_BonusEventClaims_AspNetUsers_PlayerId",
                table: "BonusEventClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_FloorCodeClaims_AspNetUsers_PlayerId",
                table: "FloorCodeClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_PointEntries_AspNetUsers_PlayerId",
                table: "PointEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskCompletions_AspNetUsers_PlayerId",
                table: "TaskCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskStars_AspNetUsers_PlayerId",
                table: "TaskStars");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PlayerId",
                table: "AspNetUsers",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Players_PlayerId",
                table: "AspNetUsers",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BonusEventClaims_Players_PlayerId",
                table: "BonusEventClaims",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FloorCodeClaims_Players_PlayerId",
                table: "FloorCodeClaims",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PointEntries_Players_PlayerId",
                table: "PointEntries",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskCompletions_Players_PlayerId",
                table: "TaskCompletions",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskStars_Players_PlayerId",
                table: "TaskStars",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Players_PlayerId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_BonusEventClaims_Players_PlayerId",
                table: "BonusEventClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_FloorCodeClaims_Players_PlayerId",
                table: "FloorCodeClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_PointEntries_Players_PlayerId",
                table: "PointEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskCompletions_Players_PlayerId",
                table: "TaskCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskStars_Players_PlayerId",
                table: "TaskStars");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PlayerId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_BonusEventClaims_AspNetUsers_PlayerId",
                table: "BonusEventClaims",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FloorCodeClaims_AspNetUsers_PlayerId",
                table: "FloorCodeClaims",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PointEntries_AspNetUsers_PlayerId",
                table: "PointEntries",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskCompletions_AspNetUsers_PlayerId",
                table: "TaskCompletions",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskStars_AspNetUsers_PlayerId",
                table: "TaskStars",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
