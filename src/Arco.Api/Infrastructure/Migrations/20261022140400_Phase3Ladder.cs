using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arco.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Ladder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LevelUpDaysPerWeek",
                table: "Habits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LevelUpWeeks",
                table: "Habits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HabitPlannedLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HabitId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    MinimumDescription = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HabitPlannedLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HabitPlannedLevels_Habits_HabitId",
                        column: x => x.HabitId,
                        principalTable: "Habits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HabitPlannedLevels_HabitId_Position",
                table: "HabitPlannedLevels",
                columns: new[] { "HabitId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HabitPlannedLevels");

            migrationBuilder.DropColumn(
                name: "LevelUpDaysPerWeek",
                table: "Habits");

            migrationBuilder.DropColumn(
                name: "LevelUpWeeks",
                table: "Habits");
        }
    }
}
