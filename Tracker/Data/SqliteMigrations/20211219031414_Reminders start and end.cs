#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Tracker.Data.SqliteMigrations
{
    public partial class Remindersstartandend : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CronUtc",
                table: "Reminders",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Reminders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Reminders",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Reminders");

            migrationBuilder.AlterColumn<string>(
                name: "CronUtc",
                table: "Reminders",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
