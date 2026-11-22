CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125643_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'notifications') THEN
            CREATE SCHEMA notifications;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125643_InitialCreate') THEN
    CREATE TABLE notifications."InboxMessages" (
        "Id" uuid NOT NULL,
        "Type" character varying(200) NOT NULL,
        "ProcessedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_InboxMessages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125643_InitialCreate') THEN
    CREATE TABLE notifications."Notifications" (
        "Id" uuid NOT NULL,
        "Recipient" character varying(200) NOT NULL,
        "Title" character varying(300) NOT NULL,
        "Body" character varying(2000) NOT NULL,
        "Link" character varying(300),
        "CreatedAt" timestamp with time zone NOT NULL,
        "ReadAt" timestamp with time zone,
        CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125643_InitialCreate') THEN
    CREATE TABLE notifications."OutboxMessages" (
        "Id" uuid NOT NULL,
        "Type" character varying(200) NOT NULL,
        "Payload" text NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "PublishedAt" timestamp with time zone,
        "Attempts" integer NOT NULL,
        "LastError" character varying(2000),
        CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125643_InitialCreate') THEN
    CREATE INDEX "IX_Notifications_Recipient_CreatedAt" ON notifications."Notifications" ("Recipient", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125643_InitialCreate') THEN
    CREATE INDEX "IX_OutboxMessages_PublishedAt_OccurredAt" ON notifications."OutboxMessages" ("PublishedAt", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125643_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122125643_InitialCreate', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140114_ServiceBuilderAgenticReviewArabic') THEN
    ALTER TABLE notifications."Notifications" ADD "BodyAr" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140114_ServiceBuilderAgenticReviewArabic') THEN
    ALTER TABLE notifications."Notifications" ADD "TitleAr" character varying(300);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140114_ServiceBuilderAgenticReviewArabic') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122140114_ServiceBuilderAgenticReviewArabic', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161248_MultilingualPlatform') THEN
    ALTER TABLE notifications."Notifications" ADD "BodyMessage" character varying(8000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161248_MultilingualPlatform') THEN
    ALTER TABLE notifications."Notifications" ADD "TitleMessage" character varying(4000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161248_MultilingualPlatform') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122161248_MultilingualPlatform', '10.0.15');
    END IF;
END $EF$;
COMMIT;

