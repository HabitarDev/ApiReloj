using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcces.Migrations
{
    /// <inheritdoc />
    public partial class inclucionDeIndicesPorPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_AttendanceStatus",
                table: "AccessEvents",
                column: "AttendanceStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_Major_Minor_EventTimeUtc",
                table: "AccessEvents",
                columns: new[] { "Major", "Minor", "EventTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_Minor",
                table: "AccessEvents",
                column: "Minor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessEvents_AttendanceStatus",
                table: "AccessEvents");

            migrationBuilder.DropIndex(
                name: "IX_AccessEvents_Major_Minor_EventTimeUtc",
                table: "AccessEvents");

            migrationBuilder.DropIndex(
                name: "IX_AccessEvents_Minor",
                table: "AccessEvents");
        }
    }
}
