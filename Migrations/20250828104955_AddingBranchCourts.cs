using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddingBranchCourts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BranchCourts",
                schema: AppConstant.Schema,
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourtName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchCourts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchCourts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: AppConstant.Schema,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns",
                column: "BranchCourtId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchCourt_Branch_Active",
                schema: AppConstant.Schema,
                table: "BranchCourts",
                columns: new[] { "BranchId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns",
                column: "BranchCourtId",
                principalSchema: AppConstant.Schema,
                principalTable: "BranchCourts",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIns_BranchCourts_BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns");

            migrationBuilder.DropTable(
                name: "BranchCourts",
                schema: AppConstant.Schema);

            migrationBuilder.DropIndex(
                name: "IX_CheckIns_BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "BranchCourtId",
                schema: AppConstant.Schema,
                table: "CheckIns");
        }
    }
}
