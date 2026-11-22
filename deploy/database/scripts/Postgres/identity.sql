CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125554_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'identity') THEN
            CREATE SCHEMA identity;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125554_InitialCreate') THEN
    CREATE TABLE identity."StaffAccounts" (
        "Id" uuid NOT NULL,
        "Username" character varying(100) NOT NULL,
        "FullName" character varying(200) NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Email" character varying(256) NOT NULL,
        "PasswordHash" character varying(512) NOT NULL,
        "IsActive" boolean NOT NULL,
        "FailedAttempts" integer NOT NULL,
        "LockedUntil" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "LastSignInAt" timestamp with time zone,
        CONSTRAINT "PK_StaffAccounts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125554_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_StaffAccounts_Username" ON identity."StaffAccounts" ("Username");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125554_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122125554_InitialCreate', '10.0.15');
    END IF;
END $EF$;
COMMIT;

