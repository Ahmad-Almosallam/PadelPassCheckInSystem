using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class ColumnsRelatedToWarning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStoppedByWarning",
                schema: AppConstant.Schema,
                table: "EndUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WarningCount",
                schema: AppConstant.Schema,
                table: "EndUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PlayerAttended",
                schema: AppConstant.Schema,
                table: "CheckIns",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStoppedByWarning",
                schema: AppConstant.Schema,
                table: "EndUsers");

            migrationBuilder.DropColumn(
                name: "WarningCount",
                schema: AppConstant.Schema,
                table: "EndUsers");

            migrationBuilder.DropColumn(
                name: "PlayerAttended",
                schema: AppConstant.Schema,
                table: "CheckIns");
        }
    }
}
