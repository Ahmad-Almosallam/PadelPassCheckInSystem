﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class NewTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CheckIns_BranchId",
                schema: "access",
                table: "CheckIns");

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPauseEndDate",
                schema: "access",
                table: "EndUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPauseStartDate",
                schema: "access",
                table: "EndUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                schema: "access",
                table: "EndUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CourtName",
                schema: "access",
                table: "CheckIns",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "access",
                table: "CheckIns",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "PlayDuration",
                schema: "access",
                table: "CheckIns",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlayStartTime",
                schema: "access",
                table: "CheckIns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BranchTimeSlots",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchTimeSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchTimeSlots_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "access",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPauses",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EndUserId = table.Column<int>(type: "int", nullable: false),
                    PauseStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PauseDays = table.Column<int>(type: "int", nullable: false),
                    PauseEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPauses_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "access",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPauses_EndUsers_EndUserId",
                        column: x => x.EndUserId,
                        principalSchema: "access",
                        principalTable: "EndUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIn_Branch_DateTime",
                schema: "access",
                table: "CheckIns",
                columns: new[] { "BranchId", "CheckInDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchTimeSlot_Branch_Day_Active",
                schema: "access",
                table: "BranchTimeSlots",
                columns: new[] { "BranchId", "DayOfWeek", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPause_EndUser_Active",
                schema: "access",
                table: "SubscriptionPauses",
                columns: new[] { "EndUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPauses_CreatedByUserId",
                schema: "access",
                table: "SubscriptionPauses",
                column: "CreatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchTimeSlots",
                schema: "access");

            migrationBuilder.DropTable(
                name: "SubscriptionPauses",
                schema: "access");

            migrationBuilder.DropIndex(
                name: "IX_CheckIn_Branch_DateTime",
                schema: "access",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "CurrentPauseEndDate",
                schema: "access",
                table: "EndUsers");

            migrationBuilder.DropColumn(
                name: "CurrentPauseStartDate",
                schema: "access",
                table: "EndUsers");

            migrationBuilder.DropColumn(
                name: "IsPaused",
                schema: "access",
                table: "EndUsers");

            migrationBuilder.DropColumn(
                name: "CourtName",
                schema: "access",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "access",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "PlayDuration",
                schema: "access",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "PlayStartTime",
                schema: "access",
                table: "CheckIns");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_BranchId",
                schema: "access",
                table: "CheckIns",
                column: "BranchId");
        }
    }
}
