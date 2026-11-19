using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arco.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HabitDayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnchorHabitId",
                table: "Habits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnchorText",
                table: "Habits",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Habits",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TimeOfDay",
                table: "Habits",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Anytime");

            migrationBuilder.CreateIndex(
                name: "IX_Habits_AnchorHabitId",
                table: "Habits",
                column: "AnchorHabitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Habits_Habits_AnchorHabitId",
                table: "Habits",
                column: "AnchorHabitId",
                principalTable: "Habits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Habits_Habits_AnchorHabitId",
                table: "Habits");

            migrationBuilder.DropIndex(
                name: "IX_Habits_AnchorHabitId",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "AnchorHabitId",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "AnchorText",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "TimeOfDay",
                table: "Habits");
        }
    }
}
