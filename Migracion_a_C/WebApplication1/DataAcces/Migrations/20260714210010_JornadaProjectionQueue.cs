using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcces.Migrations
{
    /// <inheritdoc />
    public partial class JornadaProjectionQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jornadas_EmployeeNumber_ClockSn_StatusCheck",
                table: "Jornadas");

            migrationBuilder.AddColumn<string>(
                name: "BreakInDeviceSn",
                table: "Jornadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BreakInSerialNumber",
                table: "Jornadas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BreakOutDeviceSn",
                table: "Jornadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BreakOutSerialNumber",
                table: "Jornadas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Jornadas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "EndDeviceSn",
                table: "Jornadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EndSerialNumber",
                table: "Jornadas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorsJson",
                table: "Jornadas",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "IdentityDeviceSn",
                table: "Jornadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IdentitySerialNumber",
                table: "Jornadas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Jornadas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProjectionStatus",
                table: "Jornadas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "READY");

            migrationBuilder.AddColumn<string>(
                name: "ResidentialId",
                table: "Jornadas",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "Jornadas",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "StartDeviceSn",
                table: "Jornadas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StartSerialNumber",
                table: "Jornadas",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarningsJson",
                table: "Jornadas",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ResidentialId",
                table: "AccessEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Las filas anteriores no contienen evidencia suficiente para inferir
            // el tenant a partir de la asociación actual del reloj. Se conservan
            // en cuarentena para impedir una atribución histórica incorrecta.
            migrationBuilder.Sql("""
                UPDATE "AccessEvents"
                SET "ResidentialId" = '__legacy__'
                WHERE "ResidentialId" IS NULL;

                UPDATE "Jornadas"
                SET "ResidentialId" = '__legacy__'
                WHERE "ResidentialId" IS NULL;

                UPDATE "Jornadas"
                SET "CreatedAt" = "UpdatedAt";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ResidentialId",
                table: "Jornadas",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ResidentialId",
                table: "AccessEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "JornadaProjectionStates",
                columns: table => new
                {
                    EmployeeNumber = table.Column<string>(type: "text", nullable: false),
                    ResidentialId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DirtyFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedRevision = table.Column<long>(type: "bigint", nullable: false),
                    AppliedRevision = table.Column<long>(type: "bigint", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JornadaProjectionStates", x => new { x.EmployeeNumber, x.ResidentialId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jornadas_EmployeeNumber_ResidentialId_IdentityDeviceSn_Iden~",
                table: "Jornadas",
                columns: new[] { "EmployeeNumber", "ResidentialId", "IdentityDeviceSn", "IdentitySerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jornadas_EmployeeNumber_ResidentialId_StatusCheck",
                table: "Jornadas",
                columns: new[] { "EmployeeNumber", "ResidentialId", "StatusCheck" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_EmployeeNumber_ResidentialId_EventTimeUtc",
                table: "AccessEvents",
                columns: new[] { "EmployeeNumber", "ResidentialId", "EventTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JornadaProjectionStates_DirtyFromUtc",
                table: "JornadaProjectionStates",
                column: "DirtyFromUtc");

            migrationBuilder.CreateIndex(
                name: "IX_JornadaProjectionStates_Status_NextAttemptAtUtc_UpdatedAtUtc",
                table: "JornadaProjectionStates",
                columns: new[] { "Status", "NextAttemptAtUtc", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JornadaProjectionStates");

            migrationBuilder.DropIndex(
                name: "IX_Jornadas_EmployeeNumber_ResidentialId_IdentityDeviceSn_Iden~",
                table: "Jornadas");

            migrationBuilder.DropIndex(
                name: "IX_Jornadas_EmployeeNumber_ResidentialId_StatusCheck",
                table: "Jornadas");

            migrationBuilder.DropIndex(
                name: "IX_AccessEvents_EmployeeNumber_ResidentialId_EventTimeUtc",
                table: "AccessEvents");

            migrationBuilder.DropColumn(
                name: "BreakInDeviceSn",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "BreakInSerialNumber",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "BreakOutDeviceSn",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "BreakOutSerialNumber",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "EndDeviceSn",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "EndSerialNumber",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "ErrorsJson",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "IdentityDeviceSn",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "IdentitySerialNumber",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "ProjectionStatus",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "ResidentialId",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "StartDeviceSn",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "StartSerialNumber",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "WarningsJson",
                table: "Jornadas");

            migrationBuilder.DropColumn(
                name: "ResidentialId",
                table: "AccessEvents");

            migrationBuilder.CreateIndex(
                name: "IX_Jornadas_EmployeeNumber_ClockSn_StatusCheck",
                table: "Jornadas",
                columns: new[] { "EmployeeNumber", "ClockSn", "StatusCheck" });
        }
    }
}
