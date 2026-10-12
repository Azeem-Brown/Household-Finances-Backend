using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseholdFinances.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeGoalIntervalNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Interval",
                table: "Goals",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Data step. A non-recurring goal previously stored the zero-valued Daily because the
            // column was required and the Interval enum has no "None" member. Map exactly those rows
            // to NULL so a one-off goal is distinguishable in storage from a daily one. Rows where
            // Recurring is true (1) hold the cadence the user supplied and are left untouched. This
            // mirrors 20261008185248_MakeIncomeAndBillIntervalNullable.
            migrationBuilder.Sql("UPDATE `Goals` SET `Interval` = NULL WHERE `Recurring` = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the data step first. The column is about to become required, so any one-off row
            // (Interval is NULL) is restored to the previous zero-valued Daily before the NOT NULL
            // alteration runs.
            migrationBuilder.Sql("UPDATE `Goals` SET `Interval` = 0 WHERE `Interval` IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "Interval",
                table: "Goals",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
