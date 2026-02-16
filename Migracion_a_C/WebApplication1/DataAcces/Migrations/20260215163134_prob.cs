using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcces.Migrations
{
    /// <inheritdoc />
    public partial class prob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceSn",
                table: "Relojes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPollEvent",
                table: "Relojes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPushEvent",
                table: "Relojes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccessEvents",
                columns: table => new
                {
                    DeviceSn = table.Column<string>(type: "text", nullable: false),
                    SerialNumber = table.Column<long>(type: "bigint", nullable: false),
                    EventTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeDevice = table.Column<string>(type: "text", nullable: true),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    Major = table.Column<int>(type: "integer", nullable: false),
                    Minor = table.Column<int>(type: "integer", nullable: false),
                    AttendanceStatus = table.Column<string>(type: "text", nullable: true),
                    Raw = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessEvents", x => new { x.DeviceSn, x.SerialNumber });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Relojes_DeviceSn",
                table: "Relojes",
                column: "DeviceSn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_DeviceSn_EventTimeUtc",
                table: "AccessEvents",
                columns: new[] { "DeviceSn", "EventTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_EmployeeNumber",
                table: "AccessEvents",
                column: "EmployeeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_EventTimeUtc",
                table: "AccessEvents",
                column: "EventTimeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessEvents");

            migrationBuilder.DropIndex(
                name: "IX_Relojes_DeviceSn",
                table: "Relojes");

            migrationBuilder.DropColumn(
                name: "DeviceSn",
                table: "Relojes");

            migrationBuilder.DropColumn(
                name: "LastPollEvent",
                table: "Relojes");

            migrationBuilder.DropColumn(
                name: "LastPushEvent",
                table: "Relojes");
        }
    }
}
