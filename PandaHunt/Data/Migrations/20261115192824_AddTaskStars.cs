using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandaHunt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskStars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskStars",
                columns: table => new
                {
                    TaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    SinceUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskStars", x => new { x.TaskId, x.Color });
                    table.ForeignKey(
                        name: "FK_TaskStars_AspNetUsers_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskStars_HuntTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "HuntTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskStars_PlayerId",
                table: "TaskStars",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskStars");
        }
    }
}
