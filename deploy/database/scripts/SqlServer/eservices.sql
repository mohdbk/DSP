IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'eservices') IS NULL EXEC(N'CREATE SCHEMA [eservices];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE TABLE [eservices].[Applications] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(32) NOT NULL,
        [ServiceCode] nvarchar(64) NOT NULL,
        [ServiceName] nvarchar(200) NOT NULL,
        [WorkflowVersion] int NOT NULL,
        [ApplicantId] nvarchar(200) NOT NULL,
        [ApplicantName] nvarchar(200) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [DataJson] nvarchar(max) NOT NULL,
        [ReferenceValue] nvarchar(200) NULL,
        [Fee] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [SubmittedAt] datetimeoffset NULL,
        [CompletedAt] datetimeoffset NULL,
        [Approved] bit NULL,
        [DecisionComment] nvarchar(max) NULL,
        [DecidedBy] nvarchar(200) NULL,
        [PaymentId] uniqueidentifier NULL,
        [CertificateDocumentId] uniqueidentifier NULL,
        CONSTRAINT [PK_Applications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE TABLE [eservices].[InboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [Type] nvarchar(200) NOT NULL,
        [ProcessedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE TABLE [eservices].[OutboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [Type] nvarchar(200) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        [PublishedAt] datetimeoffset NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(2000) NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE TABLE [eservices].[ReviewTasks] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [ApplicationNumber] nvarchar(32) NOT NULL,
        [ServiceCode] nvarchar(64) NOT NULL,
        [ServiceName] nvarchar(200) NOT NULL,
        [ApplicantName] nvarchar(200) NOT NULL,
        [ReferenceValue] nvarchar(200) NULL,
        [Status] nvarchar(32) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [DueAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [CompletedBy] nvarchar(200) NULL,
        [Outcome] nvarchar(32) NULL,
        CONSTRAINT [PK_ReviewTasks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Applications_ApplicantId] ON [eservices].[Applications] ([ApplicantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Applications_Number] ON [eservices].[Applications] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Applications_ServiceCode_ReferenceValue] ON [eservices].[Applications] ([ServiceCode], [ReferenceValue]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_PublishedAt_OccurredAt] ON [eservices].[OutboxMessages] ([PublishedAt], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReviewTasks_ApplicationId] ON [eservices].[ReviewTasks] ([ApplicationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReviewTasks_Status_CreatedAt] ON [eservices].[ReviewTasks] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125611_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122125611_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    ALTER TABLE [eservices].[ReviewTasks] ADD [AiRecommendation] nvarchar(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    ALTER TABLE [eservices].[Applications] ADD [InformationRequestCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    ALTER TABLE [eservices].[Applications] ADD [InformationRequestJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    ALTER TABLE [eservices].[Applications] ADD [ServiceVersion] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    CREATE TABLE [eservices].[AiAssessments] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicationId] uniqueidentifier NOT NULL,
        [Round] int NOT NULL,
        [Recommendation] nvarchar(32) NOT NULL,
        [Summary] nvarchar(2000) NOT NULL,
        [FindingsJson] nvarchar(max) NOT NULL,
        [QuestionsJson] nvarchar(max) NOT NULL,
        [Mode] nvarchar(16) NOT NULL,
        [Model] nvarchar(100) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AiAssessments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    CREATE TABLE [eservices].[ServiceVersions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Version] int NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DefinitionJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(200) NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(200) NOT NULL,
        [PublishedAt] datetimeoffset NULL,
        [PublishedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_ServiceVersions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    CREATE INDEX [IX_AiAssessments_ApplicationId_Round] ON [eservices].[AiAssessments] ([ApplicationId], [Round]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    CREATE INDEX [IX_ServiceVersions_Code_Status] ON [eservices].[ServiceVersions] ([Code], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ServiceVersions_Code_Version] ON [eservices].[ServiceVersions] ([Code], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140039_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122140039_ServiceBuilderAgenticReviewArabic', N'10.0.15');
END;

COMMIT;
GO

