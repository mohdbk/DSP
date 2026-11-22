CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125622_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'payments') THEN
            CREATE SCHEMA payments;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125622_InitialCreate') THEN
    CREATE TABLE payments."InboxMessages" (
        "Id" uuid NOT NULL,
        "Type" character varying(200) NOT NULL,
        "ProcessedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_InboxMessages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125622_InitialCreate') THEN
    CREATE TABLE payments."OutboxMessages" (
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
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125622_InitialCreate') THEN
    CREATE TABLE payments."Payments" (
        "Id" uuid NOT NULL,
        "ReferenceId" uuid NOT NULL,
        "PayerId" character varying(200) NOT NULL,
        "Description" character varying(300) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "Status" character varying(16) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "PaidAt" timestamp with time zone,
        "Method" character varying(50),
        "TransactionReference" character varying(100),
        CONSTRAINT "PK_Payments" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125622_InitialCreate') THEN
    CREATE INDEX "IX_OutboxMessages_PublishedAt_OccurredAt" ON payments."OutboxMessages" ("PublishedAt", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125622_InitialCreate') THEN
    CREATE INDEX "IX_Payments_PayerId" ON payments."Payments" ("PayerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125622_InitialCreate') THEN
    CREATE INDEX "IX_Payments_ReferenceId" ON payments."Payments" ("ReferenceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125622_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122125622_InitialCreate', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161226_MultilingualPlatform') THEN
    ALTER TABLE payments."Payments" ALTER COLUMN "Description" TYPE character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122161226_MultilingualPlatform') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122161226_MultilingualPlatform', '10.0.15');
    END IF;
END $EF$;
COMMIT;

