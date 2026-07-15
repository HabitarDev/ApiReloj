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

            migrationBuilder.AlterColumn<string>(
                name: "IdResidential",
                table: "Residentials",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ResidentialId",
                table: "Relojes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "IdReloj",
                table: "Relojes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ResidentialId",
                table: "Devices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "Devices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

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

            migrationBuilder.AlterColumn<int>(
                name: "IdResidential",
                table: "Residentials",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<int>(
                name: "ResidentialId",
                table: "Relojes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<int>(
                name: "IdReloj",
                table: "Relojes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<int>(
                name: "ResidentialId",
                table: "Devices",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<int>(
                name: "DeviceId",
                table: "Devices",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

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
