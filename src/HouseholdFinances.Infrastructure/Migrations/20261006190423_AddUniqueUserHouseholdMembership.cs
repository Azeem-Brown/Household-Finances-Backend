using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseholdFinances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueUserHouseholdMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserHousehold_UserId",
                table: "UserHousehold");

            migrationBuilder.CreateIndex(
                name: "IX_UserHousehold_UserId_HouseholdId",
                table: "UserHousehold",
                columns: new[] { "UserId", "HouseholdId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserHousehold_UserId_HouseholdId",
                table: "UserHousehold");

            migrationBuilder.CreateIndex(
                name: "IX_UserHousehold_UserId",
                table: "UserHousehold",
                column: "UserId");
        }
    }
}
