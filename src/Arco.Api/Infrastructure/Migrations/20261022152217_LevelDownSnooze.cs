using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arco.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LevelDownSnooze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LevelDownSnoozedUntil",
                table: "Habits",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LevelDownSnoozedUntil",
                table: "Habits");
        }
    }
}
