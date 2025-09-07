using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class EndUserSubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EndUserSubscriptions",
                schema: "test",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RekazId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndUserId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    PausedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResumedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndUserSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EndUserSubscriptions_EndUsers_EndUserId",
                        column: x => x.EndUserId,
                        principalSchema: "test",
                        principalTable: "EndUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EndUserSubscriptions_EndUserId",
                schema: "test",
                table: "EndUserSubscriptions",
                column: "EndUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EndUserSubscriptions",
                schema: "test");
        }
    }
}
