using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseholdFinances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Household",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            // Existing households have no creator recorded. A household with exactly one member can
            // only have been created by that member, so it is safe to record them. A household with
            // several members cannot be attributed without guessing, so it stays without a creator
            // and is therefore delete-locked: its members can leave it, but nobody can delete it.
            migrationBuilder.Sql(
                """
                UPDATE `Household` AS household
                INNER JOIN (
                    SELECT `HouseholdId`, MIN(`UserId`) AS `UserId`, COUNT(*) AS `MemberCount`
                    FROM `UserHousehold`
                    GROUP BY `HouseholdId`
                ) AS membership ON membership.`HouseholdId` = household.`Id`
                SET household.`CreatedByUserId` = membership.`UserId`
                WHERE membership.`MemberCount` = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Household_CreatedByUserId",
                table: "Household",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Household_Users_CreatedByUserId",
                table: "Household",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Household_Users_CreatedByUserId",
                table: "Household");

            migrationBuilder.DropIndex(
                name: "IX_Household_CreatedByUserId",
                table: "Household");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Household");
        }
    }
}
