CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260115222557_InitialCreate') THEN
    CREATE TABLE "Residentials" (
        "IdResidential" integer NOT NULL,
        "IpActual" text NOT NULL,
        CONSTRAINT "PK_Residentials" PRIMARY KEY ("IdResidential")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260115222557_InitialCreate') THEN
    CREATE TABLE "Devices" (
        "DeviceId" integer NOT NULL,
        "SecretKey" text NOT NULL,
        "LastSeen" timestamp with time zone,
        "ResidentialId" integer NOT NULL,
        CONSTRAINT "PK_Devices" PRIMARY KEY ("DeviceId"),
        CONSTRAINT "FK_Devices_Residentials_ResidentialId" FOREIGN KEY ("ResidentialId") REFERENCES "Residentials" ("IdResidential") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260115222557_InitialCreate') THEN
    CREATE TABLE "Relojes" (
        "IdReloj" integer NOT NULL,
        "Puerto" integer NOT NULL,
        "ResidentialId" integer NOT NULL,
        CONSTRAINT "PK_Relojes" PRIMARY KEY ("IdReloj"),
        CONSTRAINT "FK_Relojes_Residentials_ResidentialId" FOREIGN KEY ("ResidentialId") REFERENCES "Residentials" ("IdResidential") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260115222557_InitialCreate') THEN
    CREATE INDEX "IX_Devices_ResidentialId" ON "Devices" ("ResidentialId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260115222557_InitialCreate') THEN
    CREATE INDEX "IX_Relojes_ResidentialId" ON "Relojes" ("ResidentialId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260115222557_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260115222557_InitialCreate', '10.0.1');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260215163134_prob') THEN
    ALTER TABLE "Relojes" ADD "DeviceSn" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260215163134_prob') THEN
    ALTER TABLE "Relojes" ADD "LastPollEvent" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260215163134_prob') THEN
    ALTER TABLE "Relojes" ADD "LastPushEvent" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260215163134_prob') THEN
    CREATE TABLE "AccessEvents" (
        "DeviceSn" text NOT NULL,
        "SerialNumber" bigint NOT NULL,
        "EventTimeUtc" timestamp with time zone NOT NULL,
        "TimeDevice" text,
        "EmployeeNumber" text,
        "Major" integer NOT NULL,
        "Minor" integer NOT NULL,
        "AttendanceStatus" text,
        "Raw" jsonb NOT NULL,
        CONSTRAINT "PK_AccessEvents" PRIMARY KEY ("DeviceSn", "SerialNumber")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260215163134_prob') THEN
    CREATE UNIQUE INDEX "IX_Relojes_DeviceSn" ON "Relojes" ("DeviceSn");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260215163134_prob') THEN
    CREATE INDEX "IX_AccessEvents_DeviceSn_EventTimeUtc" ON "AccessEvents" ("DeviceSn", "EventTimeUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260215163134_prob') THEN
    CREATE INDEX "IX_AccessEvents_EmployeeNumber" ON "AccessEvents" ("EmployeeNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260215163134_prob') THEN
    CREATE INDEX "IX_AccessEvents_EventTimeUtc" ON "AccessEvents" ("EventTimeUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260215163134_prob') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260215163134_prob', '10.0.1');
    END IF;
END $EF$;
COMMIT;

