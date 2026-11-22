CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125634_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'documents') THEN
            CREATE SCHEMA documents;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125634_InitialCreate') THEN
    CREATE TABLE documents."Documents" (
        "Id" uuid NOT NULL,
        "OwnerId" character varying(200) NOT NULL,
        "FileName" character varying(255) NOT NULL,
        "ContentType" character varying(100) NOT NULL,
        "SizeBytes" bigint NOT NULL,
        "Sha256" character varying(64) NOT NULL,
        "Kind" character varying(32) NOT NULL,
        "StorageKey" character varying(300) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Documents" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125634_InitialCreate') THEN
    CREATE INDEX "IX_Documents_OwnerId" ON documents."Documents" ("OwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125634_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122125634_InitialCreate', '10.0.15');
    END IF;
END $EF$;
COMMIT;

