using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcces.Migrations
{
    /// <inheritdoc />
    public partial class AddJornadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jornadas",
                columns: table => new
                {
                    JornadaId = table.Column<string>(type: "text", maxLength: 26, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: false),
                    ClockSn = table.Column<string>(type: "text", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BreakInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BreakOutAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatusCheck = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    StatusBreak = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jornadas", x => x.JornadaId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jornadas_ClockSn",
                table: "Jornadas",
                column: "ClockSn");

            migrationBuilder.CreateIndex(
                name: "IX_Jornadas_StartAt",
                table: "Jornadas",
                column: "StartAt");

            migrationBuilder.CreateIndex(
                name: "IX_Jornadas_UpdatedAt",
                table: "Jornadas",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Jornadas_EmployeeNumber_ClockSn_StatusCheck",
                table: "Jornadas",
                columns: new[] { "EmployeeNumber", "ClockSn", "StatusCheck" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jornadas");
        }
    }
}
