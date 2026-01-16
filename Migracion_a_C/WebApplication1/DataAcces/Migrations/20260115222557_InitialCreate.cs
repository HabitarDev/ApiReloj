using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcces.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Residentials",
                columns: table => new
                {
                    IdResidential = table.Column<int>(type: "integer", nullable: false),
                    IpActual = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Residentials", x => x.IdResidential);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    DeviceId = table.Column<int>(type: "integer", nullable: false),
                    SecretKey = table.Column<string>(type: "text", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResidentialId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_Devices_Residentials_ResidentialId",
                        column: x => x.ResidentialId,
                        principalTable: "Residentials",
                        principalColumn: "IdResidential",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Relojes",
                columns: table => new
                {
                    IdReloj = table.Column<int>(type: "integer", nullable: false),
                    Puerto = table.Column<int>(type: "integer", nullable: false),
                    ResidentialId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relojes", x => x.IdReloj);
                    table.ForeignKey(
                        name: "FK_Relojes_Residentials_ResidentialId",
                        column: x => x.ResidentialId,
                        principalTable: "Residentials",
                        principalColumn: "IdResidential",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ResidentialId",
                table: "Devices",
                column: "ResidentialId");

            migrationBuilder.CreateIndex(
                name: "IX_Relojes_ResidentialId",
                table: "Relojes",
                column: "ResidentialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Relojes");

            migrationBuilder.DropTable(
                name: "Residentials");
        }
    }
}
