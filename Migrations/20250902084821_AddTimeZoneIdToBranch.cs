using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeZoneIdToBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                schema: "access",
                table: "Branches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
            
            migrationBuilder.Sql(
                @"UPDATE [access].[Branches] 
                  SET [TimeZoneId] = 'Asia/Riyadh' 
                  WHERE [TimeZoneId] IS NULL;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                schema: "access",
                table: "Branches");
        }
    }
}
