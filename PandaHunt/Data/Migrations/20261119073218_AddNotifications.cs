using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PandaHunt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyByEmail",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyBySms",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "NotificationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpHost = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpSecurity = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpUser = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SmtpPasswordProtected = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpFromAddress = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SmtpFromName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    VapidPublicKey = table.Column<string>(type: "TEXT", nullable: true),
                    VapidPrivateKeyProtected = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SentNotifications",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentNotifications", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "WebPushDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    P256dh = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebPushDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebPushDevices_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebPushDevices_Endpoint",
                table: "WebPushDevices",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebPushDevices_UserId",
                table: "WebPushDevices",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationSettings");

            migrationBuilder.DropTable(
                name: "SentNotifications");

            migrationBuilder.DropTable(
                name: "WebPushDevices");

            migrationBuilder.DropColumn(
                name: "NotifyByEmail",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyBySms",
                table: "AspNetUsers");
        }
    }
}
