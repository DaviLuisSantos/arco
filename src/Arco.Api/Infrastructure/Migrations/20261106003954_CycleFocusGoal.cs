using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arco.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CycleFocusGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeeklyFocusMinutes",
                table: "Cycles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 420);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeeklyFocusMinutes",
                table: "Cycles");
        }
    }
}
