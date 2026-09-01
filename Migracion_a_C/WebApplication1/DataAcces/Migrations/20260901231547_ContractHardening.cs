using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcces.Migrations
{
    /// <inheritdoc />
    public partial class ContractHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_ResidentialId_EventTimeUtc_SerialNumber_Device~",
                table: "AccessEvents",
                columns: new[] { "ResidentialId", "EventTimeUtc", "SerialNumber", "DeviceSn" });

            migrationBuilder.Sql("""
                UPDATE "BackfillPollRuns"
                SET "Trigger" = 'scheduled'
                WHERE "Trigger" = 'startup';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessEvents_ResidentialId_EventTimeUtc_SerialNumber_Device~",
                table: "AccessEvents");
        }
    }
}
