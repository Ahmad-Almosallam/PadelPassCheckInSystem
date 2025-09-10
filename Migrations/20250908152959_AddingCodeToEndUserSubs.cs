using Microsoft.EntityFrameworkCore.Migrations;
using PadelPassCheckInSystem.Shared;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddingCodeToEndUserSubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions");
        }
    }
}
