using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arco.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeeklyReviewId",
                table: "XpEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyReviewReminderDay",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "WeeklyReviewReminderEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WeeklyReviewReminderTime",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<DateOnly>(
                name: "LevelUpSnoozedUntil",
                table: "Habits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReminderTime",
                table: "Habits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNext",
                table: "Cycles",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewObstacles",
                table: "Cycles",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewWins",
                table: "Cycles",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PushDispatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDispatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    P256dh = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSuccessAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailureCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WeekStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Wins = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Obstacles = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Next = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyReviews", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "WeeklyReviewReminderDay", "WeeklyReviewReminderEnabled", "WeeklyReviewReminderTime" },
                values: new object[] { 0, true, new TimeOnly(18, 0, 0) });

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_WeeklyReviewId",
                table: "XpEvents",
                column: "WeeklyReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDispatches_Key",
                table: "PushDispatches",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReviews_WeekStart",
                table: "WeeklyReviews",
                column: "WeekStart",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_XpEvents_WeeklyReviews_WeeklyReviewId",
                table: "XpEvents",
                column: "WeeklyReviewId",
                principalTable: "WeeklyReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_XpEvents_WeeklyReviews_WeeklyReviewId",
                table: "XpEvents");

            migrationBuilder.DropTable(
                name: "PushDispatches");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "WeeklyReviews");

            migrationBuilder.DropIndex(
                name: "IX_XpEvents_WeeklyReviewId",
                table: "XpEvents");

            migrationBuilder.DropColumn(
                name: "WeeklyReviewId",
                table: "XpEvents");

            migrationBuilder.DropColumn(
                name: "WeeklyReviewReminderDay",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "WeeklyReviewReminderEnabled",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "WeeklyReviewReminderTime",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "LevelUpSnoozedUntil",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ReminderTime",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "ReviewNext",
                table: "Cycles");

            migrationBuilder.DropColumn(
                name: "ReviewObstacles",
                table: "Cycles");

            migrationBuilder.DropColumn(
                name: "ReviewWins",
                table: "Cycles");
        }
    }
}
