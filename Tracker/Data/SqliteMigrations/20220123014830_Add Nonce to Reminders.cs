using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Data.SqliteMigrations
{
    public partial class AddNoncetoReminders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Nonce",
                table: "Reminders",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nonce",
                table: "Reminders");
        }
    }
}
