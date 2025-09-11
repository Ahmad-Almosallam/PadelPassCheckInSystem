using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelPassCheckInSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAllPhoneNumberToBe966 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // add +966 before the phonenumber
            migrationBuilder.Sql($"UPDATE {AppConstant.Schema}.EndUsers SET PhoneNumber = '+966' + RIGHT(PhoneNumber, LEN(PhoneNumber) - 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
