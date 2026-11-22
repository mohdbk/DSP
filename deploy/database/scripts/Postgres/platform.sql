CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161305_MultilingualPlatform') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'platform') THEN
            CREATE SCHEMA platform;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161305_MultilingualPlatform') THEN
    CREATE TABLE platform."AiSettings" (
        "Id" uuid NOT NULL,
        "ProviderName" character varying(100) NOT NULL,
        "BaseUrl" character varying(500) NOT NULL,
        "Model" character varying(200) NOT NULL,
        "ProtectedApiKey" character varying(2000),
        "Temperature" double precision NOT NULL,
        "TimeoutSeconds" integer NOT NULL,
        "ChatEnabled" boolean NOT NULL,
        "ReviewEnabled" boolean NOT NULL,
        "MaxToolIterations" integer NOT NULL,
        "MaxHistoryMessages" integer NOT NULL,
        "ChatInstructions" character varying(8000),
        "ReviewInstructions" character varying(8000),
        "UpdatedAt" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(200) NOT NULL,
        CONSTRAINT "PK_AiSettings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161305_MultilingualPlatform') THEN
    CREATE TABLE platform."Languages" (
        "Id" uuid NOT NULL,
        "Code" character varying(35) NOT NULL,
        "Name" character varying(4000) NOT NULL,
        "Direction" character varying(3) NOT NULL,
        "IsEnabled" boolean NOT NULL,
        "IsDefault" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(200) NOT NULL,
        CONSTRAINT "PK_Languages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161305_MultilingualPlatform') THEN
    CREATE TABLE platform."Translations" (
        "Id" uuid NOT NULL,
        "Key" character varying(200) NOT NULL,
        "LanguageCode" character varying(35) NOT NULL,
        "Text" character varying(4000) NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(200) NOT NULL,
        CONSTRAINT "PK_Translations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161305_MultilingualPlatform') THEN
    CREATE UNIQUE INDEX "IX_Languages_Code" ON platform."Languages" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161305_MultilingualPlatform') THEN
    CREATE UNIQUE INDEX "IX_Translations_Key_LanguageCode" ON platform."Translations" ("Key", "LanguageCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161305_MultilingualPlatform') THEN
    CREATE INDEX "IX_Translations_LanguageCode" ON platform."Translations" ("LanguageCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161305_MultilingualPlatform') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122161305_MultilingualPlatform', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122164209_PromptsPauseLanguageVisibility') THEN
    ALTER TABLE platform."AiSettings" DROP COLUMN "ChatEnabled";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122164209_PromptsPauseLanguageVisibility') THEN
    ALTER TABLE platform."AiSettings" RENAME COLUMN "ReviewEnabled" TO "Paused";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122164209_PromptsPauseLanguageVisibility') THEN
    UPDATE platform."AiSettings" SET "Paused" = FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122164209_PromptsPauseLanguageVisibility') THEN
    ALTER TABLE platform."Languages" ADD "HiddenInBackoffice" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122164209_PromptsPauseLanguageVisibility') THEN
    ALTER TABLE platform."Languages" ADD "HiddenInPortal" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122164209_PromptsPauseLanguageVisibility') THEN
    ALTER TABLE platform."AiSettings" ADD "ChatPrompt" character varying(8000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122164209_PromptsPauseLanguageVisibility') THEN
    ALTER TABLE platform."AiSettings" ADD "ReviewPrompt" character varying(8000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122164209_PromptsPauseLanguageVisibility') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122164209_PromptsPauseLanguageVisibility', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122170601_AiAuditSample') THEN
    ALTER TABLE platform."AiSettings" ADD "AuditSamplePercent" integer NOT NULL DEFAULT 10;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122170601_AiAuditSample') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122170601_AiAuditSample', '10.0.15');
    END IF;
END $EF$;
COMMIT;

