CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'eservices') THEN
            CREATE SCHEMA eservices;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE TABLE eservices."Applications" (
        "Id" uuid NOT NULL,
        "Number" character varying(32) NOT NULL,
        "ServiceCode" character varying(64) NOT NULL,
        "ServiceName" character varying(200) NOT NULL,
        "WorkflowVersion" integer NOT NULL,
        "ApplicantId" character varying(200) NOT NULL,
        "ApplicantName" character varying(200) NOT NULL,
        "Status" character varying(32) NOT NULL,
        "DataJson" text NOT NULL,
        "ReferenceValue" character varying(200),
        "Fee" numeric(18,2) NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "SubmittedAt" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "Approved" boolean,
        "DecisionComment" text,
        "DecidedBy" character varying(200),
        "PaymentId" uuid,
        "CertificateDocumentId" uuid,
        CONSTRAINT "PK_Applications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE TABLE eservices."InboxMessages" (
        "Id" uuid NOT NULL,
        "Type" character varying(200) NOT NULL,
        "ProcessedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_InboxMessages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE TABLE eservices."OutboxMessages" (
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
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE TABLE eservices."ReviewTasks" (
        "Id" uuid NOT NULL,
        "ApplicationId" uuid NOT NULL,
        "ApplicationNumber" character varying(32) NOT NULL,
        "ServiceCode" character varying(64) NOT NULL,
        "ServiceName" character varying(200) NOT NULL,
        "ApplicantName" character varying(200) NOT NULL,
        "ReferenceValue" character varying(200),
        "Status" character varying(32) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "DueAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone,
        "CompletedBy" character varying(200),
        "Outcome" character varying(32),
        CONSTRAINT "PK_ReviewTasks" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE INDEX "IX_Applications_ApplicantId" ON eservices."Applications" ("ApplicantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Applications_Number" ON eservices."Applications" ("Number");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE INDEX "IX_Applications_ServiceCode_ReferenceValue" ON eservices."Applications" ("ServiceCode", "ReferenceValue");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE INDEX "IX_OutboxMessages_PublishedAt_OccurredAt" ON eservices."OutboxMessages" ("PublishedAt", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE INDEX "IX_ReviewTasks_ApplicationId" ON eservices."ReviewTasks" ("ApplicationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    CREATE INDEX "IX_ReviewTasks_Status_CreatedAt" ON eservices."ReviewTasks" ("Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122125612_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122125612_InitialCreate', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    ALTER TABLE eservices."ReviewTasks" ADD "AiRecommendation" character varying(32);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    ALTER TABLE eservices."Applications" ADD "InformationRequestCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    ALTER TABLE eservices."Applications" ADD "InformationRequestJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    ALTER TABLE eservices."Applications" ADD "ServiceVersion" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    CREATE TABLE eservices."AiAssessments" (
        "Id" uuid NOT NULL,
        "ApplicationId" uuid NOT NULL,
        "Round" integer NOT NULL,
        "Recommendation" character varying(32) NOT NULL,
        "Summary" character varying(2000) NOT NULL,
        "FindingsJson" text NOT NULL,
        "QuestionsJson" text NOT NULL,
        "Mode" character varying(16) NOT NULL,
        "Model" character varying(100),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AiAssessments" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    CREATE TABLE eservices."ServiceVersions" (
        "Id" uuid NOT NULL,
        "Code" character varying(64) NOT NULL,
        "Version" integer NOT NULL,
        "Status" character varying(16) NOT NULL,
        "Name" character varying(200) NOT NULL,
        "DefinitionJson" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(200) NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(200) NOT NULL,
        "PublishedAt" timestamp with time zone,
        "PublishedBy" character varying(200),
        CONSTRAINT "PK_ServiceVersions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    CREATE INDEX "IX_AiAssessments_ApplicationId_Round" ON eservices."AiAssessments" ("ApplicationId", "Round");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    CREATE INDEX "IX_ServiceVersions_Code_Status" ON eservices."ServiceVersions" ("Code", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    CREATE UNIQUE INDEX "IX_ServiceVersions_Code_Version" ON eservices."ServiceVersions" ("Code", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261122140044_ServiceBuilderAgenticReviewArabic') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261122140044_ServiceBuilderAgenticReviewArabic', '10.0.15');
    END IF;
END $EF$;
COMMIT;

