using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddRekazId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns");

            migrationBuilder.AddColumn<Guid>(
                name: "RekazId",
                schema: AppConstant.Schema,
                table: "EndUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns",
                column: "BranchCourtId",
                principalSchema: AppConstant.Schema,
                principalTable: "BranchCourts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "RekazId",
                schema: AppConstant.Schema,
                table: "EndUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns",
                column: "BranchCourtId",
                principalSchema: AppConstant.Schema,
                principalTable: "BranchCourts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
