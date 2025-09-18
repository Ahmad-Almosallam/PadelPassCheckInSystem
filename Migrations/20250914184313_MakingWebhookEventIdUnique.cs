using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class MakingWebhookEventIdUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@$"WITH DuplicateRows AS (SELECT Id,
                              WebhookEventId,
                              ReceivedAt,
                              ROW_NUMBER() OVER (
                                  PARTITION BY WebhookEventId
                                  ORDER BY ReceivedAt DESC
                                  ) AS rn
                        FROM {AppConstant.Schema}.WebhookEventLogs)
                        DELETE
                        FROM {AppConstant.Schema}.WebhookEventLogs
                        WHERE Id IN (SELECT Id
                                     FROM DuplicateRows
                                     WHERE rn > 1);");
            
            
            migrationBuilder.AlterColumn<Guid>(
                name: "WebhookEventId",
                schema: AppConstant.Schema,
                table: "WebhookEventLogs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEventLogs_WebhookEventId",
                schema: AppConstant.Schema,
                table: "WebhookEventLogs",
                column: "WebhookEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookEventLogs_WebhookEventId",
                schema: "access",
                table: "WebhookEventLogs");

            migrationBuilder.AlterColumn<string>(
                name: "WebhookEventId",
                schema: "access",
                table: "WebhookEventLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}
