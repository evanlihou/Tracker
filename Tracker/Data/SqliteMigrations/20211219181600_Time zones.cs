#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Tracker.Data.SqliteMigrations
{
    public partial class Timezones : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CronUtc",
                table: "Reminders",
                newName: "CronLocal");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "CronLocal",
                table: "Reminders",
                newName: "CronUtc");
        }
    }
}
