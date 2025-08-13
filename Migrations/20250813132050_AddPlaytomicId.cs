using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaytomicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PlaytomicUserId",
                schema: "access",
                table: "EndUsers",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaytomicUserId",
                schema: "access",
                table: "EndUsers");
        }
    }
}
