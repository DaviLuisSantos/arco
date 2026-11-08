using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arco.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GoogleSubject = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    EmailVerifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // The existing single-user data becomes user 1 (only if there is data). OwnerBootstrap later swaps
            // the placeholder e-mail for Auth:OwnerEmail and sets Auth:PasswordHash. Every UserId column below
            // is added with default 1, which assigns the existing rows to this user.
            migrationBuilder.Sql("""
                INSERT INTO "Users" ("Id", "Email", "PasswordHash", "GoogleSubject", "DisplayName", "EmailVerifiedAt", "IsDemo", "SecurityStamp", "CreatedAt")
                SELECT 1, 'owner@arco.local', NULL, NULL, 'Dono', NULL, 0,
                       upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6))),
                       strftime('%Y-%m-%d %H:%M:%S', 'now')
                WHERE EXISTS (SELECT 1 FROM "Habits") OR EXISTS (SELECT 1 FROM "Cycles") OR EXISTS (SELECT 1 FROM "XpEvents");
                """);

            // Fresh install (no user): drop what would be orphaned, including the old Settings singleton row.
            migrationBuilder.Sql("DELETE FROM \"Settings\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"Cycles\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"Goals\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"Milestones\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"GoalUpdates\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"Habits\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"HabitLevels\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"HabitPlannedLevels\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"CheckIns\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"FocusSessions\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"XpEvents\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"WeeklyReviews\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"PushSubscriptions\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"PushDispatches\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"Achievements\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");
            migrationBuilder.Sql("DELETE FROM \"ApiKeys\" WHERE NOT EXISTS (SELECT 1 FROM \"Users\");");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyReviews_WeekStart",
                table: "WeeklyReviews");

            migrationBuilder.DropIndex(
                name: "IX_PushDispatches_Key",
                table: "PushDispatches");

            migrationBuilder.DropIndex(
                name: "IX_Cycles_Status",
                table: "Cycles");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_Key",
                table: "Achievements");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "XpEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "WeeklyReviews",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "PushSubscriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "PushDispatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Milestones",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Habits",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "HabitPlannedLevels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "HabitLevels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "GoalUpdates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Goals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "FocusSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Cycles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "CheckIns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ApiKeys",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Achievements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_UserId",
                table: "XpEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReviews_UserId_WeekStart",
                table: "WeeklyReviews",
                columns: new[] { "UserId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settings_UserId",
                table: "Settings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId",
                table: "PushSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDispatches_UserId_Key",
                table: "PushDispatches",
                columns: new[] { "UserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Milestones_UserId",
                table: "Milestones",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Habits_UserId",
                table: "Habits",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HabitPlannedLevels_UserId",
                table: "HabitPlannedLevels",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HabitLevels_UserId",
                table: "HabitLevels",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalUpdates_UserId",
                table: "GoalUpdates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_UserId",
                table: "Goals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_UserId",
                table: "FocusSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Cycles_UserId_Status",
                table: "Cycles",
                columns: new[] { "UserId", "Status" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_UserId",
                table: "CheckIns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId",
                table: "ApiKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_UserId_Key",
                table: "Achievements",
                columns: new[] { "UserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleSubject",
                table: "Users",
                column: "GoogleSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_TokenHash",
                table: "UserTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_UserId",
                table: "UserTokens",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Achievements_Users_UserId",
                table: "Achievements",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeys_Users_UserId",
                table: "ApiKeys",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIns_Users_UserId",
                table: "CheckIns",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cycles_Users_UserId",
                table: "Cycles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FocusSessions_Users_UserId",
                table: "FocusSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Goals_Users_UserId",
                table: "Goals",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GoalUpdates_Users_UserId",
                table: "GoalUpdates",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HabitLevels_Users_UserId",
                table: "HabitLevels",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HabitPlannedLevels_Users_UserId",
                table: "HabitPlannedLevels",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Habits_Users_UserId",
                table: "Habits",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Milestones_Users_UserId",
                table: "Milestones",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PushDispatches_Users_UserId",
                table: "PushDispatches",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PushSubscriptions_Users_UserId",
                table: "PushSubscriptions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_Users_UserId",
                table: "Settings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyReviews_Users_UserId",
                table: "WeeklyReviews",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_XpEvents_Users_UserId",
                table: "XpEvents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // EF emits DROP TABLE "Users" before the table rebuilds; with foreign keys on, that implicit
            // DELETE would cascade to every owned table. Turn them off first (a PRAGMA outside a transaction).
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            // Back to single-user: keep only user 1's data, with its Settings row as the singleton (Id = 1).
            migrationBuilder.Sql("DELETE FROM \"Users\" WHERE \"Id\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"Settings\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"Cycles\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"Goals\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"Milestones\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"GoalUpdates\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"Habits\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"HabitLevels\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"HabitPlannedLevels\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"CheckIns\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"FocusSessions\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"XpEvents\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"WeeklyReviews\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"PushSubscriptions\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"PushDispatches\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"Achievements\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"ApiKeys\" WHERE \"UserId\" <> 1;");
            migrationBuilder.Sql("UPDATE \"Settings\" SET \"Id\" = 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_Achievements_Users_UserId",
                table: "Achievements");

            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeys_Users_UserId",
                table: "ApiKeys");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckIns_Users_UserId",
                table: "CheckIns");

            migrationBuilder.DropForeignKey(
                name: "FK_Cycles_Users_UserId",
                table: "Cycles");

            migrationBuilder.DropForeignKey(
                name: "FK_FocusSessions_Users_UserId",
                table: "FocusSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Goals_Users_UserId",
                table: "Goals");

            migrationBuilder.DropForeignKey(
                name: "FK_GoalUpdates_Users_UserId",
                table: "GoalUpdates");

            migrationBuilder.DropForeignKey(
                name: "FK_HabitLevels_Users_UserId",
                table: "HabitLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_HabitPlannedLevels_Users_UserId",
                table: "HabitPlannedLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_Habits_Users_UserId",
                table: "Habits");

            migrationBuilder.DropForeignKey(
                name: "FK_Milestones_Users_UserId",
                table: "Milestones");

            migrationBuilder.DropForeignKey(
                name: "FK_PushDispatches_Users_UserId",
                table: "PushDispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PushSubscriptions_Users_UserId",
                table: "PushSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Settings_Users_UserId",
                table: "Settings");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyReviews_Users_UserId",
                table: "WeeklyReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_XpEvents_Users_UserId",
                table: "XpEvents");

            migrationBuilder.DropIndex(
                name: "IX_XpEvents_UserId",
                table: "XpEvents");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyReviews_UserId_WeekStart",
                table: "WeeklyReviews");

            migrationBuilder.DropIndex(
                name: "IX_Settings_UserId",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_UserId",
                table: "PushSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_PushDispatches_UserId_Key",
                table: "PushDispatches");

            migrationBuilder.DropIndex(
                name: "IX_Milestones_UserId",
                table: "Milestones");

            migrationBuilder.DropIndex(
                name: "IX_Habits_UserId",
                table: "Habits");

            migrationBuilder.DropIndex(
                name: "IX_HabitPlannedLevels_UserId",
                table: "HabitPlannedLevels");

            migrationBuilder.DropIndex(
                name: "IX_HabitLevels_UserId",
                table: "HabitLevels");

            migrationBuilder.DropIndex(
                name: "IX_GoalUpdates_UserId",
                table: "GoalUpdates");

            migrationBuilder.DropIndex(
                name: "IX_Goals_UserId",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_FocusSessions_UserId",
                table: "FocusSessions");

            migrationBuilder.DropIndex(
                name: "IX_Cycles_UserId_Status",
                table: "Cycles");

            migrationBuilder.DropIndex(
                name: "IX_CheckIns_UserId",
                table: "CheckIns");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_UserId",
                table: "ApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_UserId_Key",
                table: "Achievements");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "XpEvents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WeeklyReviews");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PushDispatches");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "HabitPlannedLevels");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "HabitLevels");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "GoalUpdates");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "FocusSessions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Cycles");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Achievements");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO "Settings" ("Id", "Timezone", "WeeklyReviewReminderDay", "WeeklyReviewReminderEnabled", "WeeklyReviewReminderTime")
                VALUES (1, 'America/Sao_Paulo', 0, 1, '18:00:00');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReviews_WeekStart",
                table: "WeeklyReviews",
                column: "WeekStart",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushDispatches_Key",
                table: "PushDispatches",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cycles_Status",
                table: "Cycles",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Key",
                table: "Achievements",
                column: "Key",
                unique: true);

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
