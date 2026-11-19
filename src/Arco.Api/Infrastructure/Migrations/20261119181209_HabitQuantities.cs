using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arco.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HabitQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Habits",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumTenths",
                table: "HabitPlannedLevels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetTenths",
                table: "HabitPlannedLevels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumTenths",
                table: "HabitLevels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetTenths",
                table: "HabitLevels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HabitDayQuantities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    HabitId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalTenths = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HabitDayQuantities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HabitDayQuantities_Habits_HabitId",
                        column: x => x.HabitId,
                        principalTable: "Habits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HabitDayQuantities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HabitDayQuantities_Date",
                table: "HabitDayQuantities",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_HabitDayQuantities_HabitId_Date",
                table: "HabitDayQuantities",
                columns: new[] { "HabitId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HabitDayQuantities_UserId",
                table: "HabitDayQuantities",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HabitDayQuantities");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "MinimumTenths",
                table: "HabitPlannedLevels");

            migrationBuilder.DropColumn(
                name: "TargetTenths",
                table: "HabitPlannedLevels");

            migrationBuilder.DropColumn(
                name: "MinimumTenths",
                table: "HabitLevels");

            migrationBuilder.DropColumn(
                name: "TargetTenths",
                table: "HabitLevels");
        }
    }
}
