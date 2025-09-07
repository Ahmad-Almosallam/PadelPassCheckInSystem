using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtInCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: "test",
                table: "CheckIns");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "test",
                table: "CheckIns",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: "test",
                table: "CheckIns",
                column: "BranchCourtId",
                principalSchema: "test",
                principalTable: "BranchCourts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: "test",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "test",
                table: "CheckIns");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: "test",
                table: "CheckIns",
                column: "BranchCourtId",
                principalSchema: "test",
                principalTable: "BranchCourts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
