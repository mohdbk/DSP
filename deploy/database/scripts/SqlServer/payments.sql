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
    WHERE [MigrationId] = N'20261122125620_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'payments') IS NULL EXEC(N'CREATE SCHEMA [payments];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125620_InitialCreate'
)
BEGIN
    CREATE TABLE [payments].[InboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [Type] nvarchar(200) NOT NULL,
        [ProcessedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125620_InitialCreate'
)
BEGIN
    CREATE TABLE [payments].[OutboxMessages] (
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
    WHERE [MigrationId] = N'20261122125620_InitialCreate'
)
BEGIN
    CREATE TABLE [payments].[Payments] (
        [Id] uniqueidentifier NOT NULL,
        [ReferenceId] uniqueidentifier NOT NULL,
        [PayerId] nvarchar(200) NOT NULL,
        [Description] nvarchar(300) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [PaidAt] datetimeoffset NULL,
        [Method] nvarchar(50) NULL,
        [TransactionReference] nvarchar(100) NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125620_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_PublishedAt_OccurredAt] ON [payments].[OutboxMessages] ([PublishedAt], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125620_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_PayerId] ON [payments].[Payments] ([PayerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125620_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_ReferenceId] ON [payments].[Payments] ([ReferenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125620_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122125620_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161223_MultilingualPlatform'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[payments].[Payments]') AND [c].[name] = N'Description');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [payments].[Payments] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [payments].[Payments] ALTER COLUMN [Description] nvarchar(2000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161223_MultilingualPlatform'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122161223_MultilingualPlatform', N'10.0.15');
END;

COMMIT;
GO

