using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddEndUserSubscriptionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EndUserSubscriptionHistories",
                schema: AppConstant.Schema,
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EndUserSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    RekazId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    PausedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndUserSubscriptionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EndUserSubscriptionHistories_EndUserSubscriptions_EndUserSubscriptionId",
                        column: x => x.EndUserSubscriptionId,
                        principalSchema: AppConstant.Schema,
                        principalTable: "EndUserSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionHistory_Rekaz_Event_Created",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptionHistories",
                columns: new[] { "RekazId", "EventName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionHistory_Subscription_Created",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptionHistories",
                columns: new[] { "EndUserSubscriptionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EndUserSubscriptionHistories",
                schema: AppConstant.Schema);
        }
    }
}
