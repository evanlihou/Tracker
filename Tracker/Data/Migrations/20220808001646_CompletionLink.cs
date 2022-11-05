using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Data.Migrations
{
    public partial class CompletionLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompletionLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Guid = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletionLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompletionLinkReminder",
                columns: table => new
                {
                    CompletionLinksId = table.Column<int>(type: "INTEGER", nullable: false),
                    RemindersId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletionLinkReminder", x => new { x.CompletionLinksId, x.RemindersId });
                    table.ForeignKey(
                        name: "FK_CompletionLinkReminder_CompletionLinks_CompletionLinksId",
                        column: x => x.CompletionLinksId,
                        principalTable: "CompletionLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompletionLinkReminder_Reminders_RemindersId",
                        column: x => x.RemindersId,
                        principalTable: "Reminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompletionLinkReminder_RemindersId",
                table: "CompletionLinkReminder",
                column: "RemindersId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompletionLinkReminder");

            migrationBuilder.DropTable(
                name: "CompletionLinks");
        }
    }
}
