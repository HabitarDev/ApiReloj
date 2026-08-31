using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcces.Migrations
{
    /// <inheritdoc />
    public partial class MaestrosIdsString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Residentials_ResidentialId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_Relojes_Residentials_ResidentialId",
                table: "Relojes");

            migrationBuilder.Sql(
                """
                ALTER TABLE "Residentials"
                    ALTER COLUMN "IdResidential" TYPE character varying(128)
                    USING "IdResidential"::text;

                ALTER TABLE "Devices"
                    ALTER COLUMN "ResidentialId" TYPE character varying(128)
                    USING "ResidentialId"::text;

                ALTER TABLE "Relojes"
                    ALTER COLUMN "ResidentialId" TYPE character varying(128)
                    USING "ResidentialId"::text;

                ALTER TABLE "Devices"
                    ALTER COLUMN "DeviceId" TYPE character varying(128)
                    USING "DeviceId"::text;

                ALTER TABLE "Relojes"
                    ALTER COLUMN "IdReloj" TYPE character varying(128)
                    USING "IdReloj"::text;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Residentials_ResidentialId",
                table: "Devices",
                column: "ResidentialId",
                principalTable: "Residentials",
                principalColumn: "IdResidential",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Relojes_Residentials_ResidentialId",
                table: "Relojes",
                column: "ResidentialId",
                principalTable: "Residentials",
                principalColumn: "IdResidential",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Residentials_ResidentialId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_Relojes_Residentials_ResidentialId",
                table: "Relojes");

            migrationBuilder.Sql(
                """
                ALTER TABLE "Residentials"
                    ALTER COLUMN "IdResidential" TYPE integer
                    USING "IdResidential"::integer;

                ALTER TABLE "Devices"
                    ALTER COLUMN "ResidentialId" TYPE integer
                    USING "ResidentialId"::integer;

                ALTER TABLE "Relojes"
                    ALTER COLUMN "ResidentialId" TYPE integer
                    USING "ResidentialId"::integer;

                ALTER TABLE "Devices"
                    ALTER COLUMN "DeviceId" TYPE integer
                    USING "DeviceId"::integer;

                ALTER TABLE "Relojes"
                    ALTER COLUMN "IdReloj" TYPE integer
                    USING "IdReloj"::integer;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Residentials_ResidentialId",
                table: "Devices",
                column: "ResidentialId",
                principalTable: "Residentials",
                principalColumn: "IdResidential",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Relojes_Residentials_ResidentialId",
                table: "Relojes",
                column: "ResidentialId",
                principalTable: "Residentials",
                principalColumn: "IdResidential",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
