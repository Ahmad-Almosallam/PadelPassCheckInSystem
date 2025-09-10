using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddIsStoppedToEndUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStopped",
                schema: AppConstant.Schema,
                table: "EndUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StopReason",
                schema: AppConstant.Schema,
                table: "EndUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoppedDate",
                schema: AppConstant.Schema,
                table: "EndUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStopped",
                schema: AppConstant.Schema,
                table: "EndUsers");

            migrationBuilder.DropColumn(
                name: "StopReason",
                schema: AppConstant.Schema,
                table: "EndUsers");

            migrationBuilder.DropColumn(
                name: "StoppedDate",
                schema: AppConstant.Schema,
                table: "EndUsers");
        }
    }
}
