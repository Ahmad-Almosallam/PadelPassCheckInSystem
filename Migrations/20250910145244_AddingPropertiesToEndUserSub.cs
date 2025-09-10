using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddingPropertiesToEndUserSub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModificationDate",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransferredDate",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferredToId",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions");

            migrationBuilder.DropColumn(
                name: "LastModificationDate",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions");

            migrationBuilder.DropColumn(
                name: "TransferredDate",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions");

            migrationBuilder.DropColumn(
                name: "TransferredToId",
                schema: AppConstant.Schema,
                table: "EndUserSubscriptions");
        }
    }
}
