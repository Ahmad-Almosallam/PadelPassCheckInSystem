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
                schema: "access",
                table: "EndUserSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModificationDate",
                schema: "access",
                table: "EndUserSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransferredDate",
                schema: "access",
                table: "EndUserSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferredToId",
                schema: "access",
                table: "EndUserSubscriptions",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "access",
                table: "EndUserSubscriptions");

            migrationBuilder.DropColumn(
                name: "LastModificationDate",
                schema: "access",
                table: "EndUserSubscriptions");

            migrationBuilder.DropColumn(
                name: "TransferredDate",
                schema: "access",
                table: "EndUserSubscriptions");

            migrationBuilder.DropColumn(
                name: "TransferredToId",
                schema: "access",
                table: "EndUserSubscriptions");
        }
    }
}
