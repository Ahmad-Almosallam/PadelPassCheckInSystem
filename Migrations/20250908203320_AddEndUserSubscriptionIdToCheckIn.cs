using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddEndUserSubscriptionIdToCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EndUserSubscriptionId",
                schema: AppConstant.Schema,
                table: "CheckIns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_EndUserSubscriptionId",
                schema: AppConstant.Schema,
                table: "CheckIns",
                column: "EndUserSubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIns_EndUserSubscriptions_EndUserSubscriptionId",
                schema: AppConstant.Schema,
                table: "CheckIns",
                column: "EndUserSubscriptionId",
                principalSchema: AppConstant.Schema,
                principalTable: "EndUserSubscriptions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckIns_EndUserSubscriptions_EndUserSubscriptionId",
                schema: AppConstant.Schema,
                table: "CheckIns");

            migrationBuilder.DropIndex(
                name: "IX_CheckIns_EndUserSubscriptionId",
                schema: AppConstant.Schema,
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "EndUserSubscriptionId",
                schema: AppConstant.Schema,
                table: "CheckIns");
        }
    }
}
