using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Data.SqliteMigrations
{
    public partial class actionablereminders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActionable",
                table: "Reminders",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActionable",
                table: "Reminders");
        }
    }
}
