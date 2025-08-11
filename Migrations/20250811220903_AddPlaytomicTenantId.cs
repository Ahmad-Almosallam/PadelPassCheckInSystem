using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaytomicTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlaytomicTenantId",
                schema: "access",
                table: "Branches",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaytomicTenantId",
                schema: "access",
                table: "Branches");
        }
    }
}
