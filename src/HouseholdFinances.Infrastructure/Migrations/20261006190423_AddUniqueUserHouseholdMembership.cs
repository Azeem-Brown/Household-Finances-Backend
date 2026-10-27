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
            // The composite unique index must be created before the single-column index is dropped.
            // MySQL refuses to drop IX_UserHousehold_UserId while it is the only index supporting the
            // FK_UserHousehold_Users_UserId foreign key ("Cannot drop index ... needed in a foreign
            // key constraint"). The composite index's leftmost column is UserId, so once it exists
            // MySQL uses it for that foreign key and the single-column index can be removed.
            migrationBuilder.CreateIndex(
                name: "IX_UserHousehold_UserId_HouseholdId",
                table: "UserHousehold",
                columns: new[] { "UserId", "HouseholdId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_UserHousehold_UserId",
                table: "UserHousehold");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mirror of Up: recreate the single-column index the foreign key needs before dropping
            // the composite index that currently supports it, for the same MySQL restriction.
            migrationBuilder.CreateIndex(
                name: "IX_UserHousehold_UserId",
                table: "UserHousehold",
                column: "UserId");

            migrationBuilder.DropIndex(
                name: "IX_UserHousehold_UserId_HouseholdId",
                table: "UserHousehold");
        }
    }
}
