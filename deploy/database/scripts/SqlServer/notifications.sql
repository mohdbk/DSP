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
    WHERE [MigrationId] = N'20261122125642_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'notifications') IS NULL EXEC(N'CREATE SCHEMA [notifications];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125642_InitialCreate'
)
BEGIN
    CREATE TABLE [notifications].[InboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [Type] nvarchar(200) NOT NULL,
        [ProcessedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125642_InitialCreate'
)
BEGIN
    CREATE TABLE [notifications].[Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [Recipient] nvarchar(200) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [Link] nvarchar(300) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ReadAt] datetimeoffset NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125642_InitialCreate'
)
BEGIN
    CREATE TABLE [notifications].[OutboxMessages] (
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
    WHERE [MigrationId] = N'20261122125642_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_Recipient_CreatedAt] ON [notifications].[Notifications] ([Recipient], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125642_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_PublishedAt_OccurredAt] ON [notifications].[OutboxMessages] ([PublishedAt], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122125642_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122125642_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140111_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    ALTER TABLE [notifications].[Notifications] ADD [BodyAr] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140111_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    ALTER TABLE [notifications].[Notifications] ADD [TitleAr] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122140111_ServiceBuilderAgenticReviewArabic'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122140111_ServiceBuilderAgenticReviewArabic', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161245_MultilingualPlatform'
)
BEGIN
    ALTER TABLE [notifications].[Notifications] ADD [BodyMessage] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161245_MultilingualPlatform'
)
BEGIN
    ALTER TABLE [notifications].[Notifications] ADD [TitleMessage] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161245_MultilingualPlatform'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122161245_MultilingualPlatform', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211157_ServerListsSavedViews'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[notifications].[Notifications]') AND [c].[name] = N'Body');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [notifications].[Notifications] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [notifications].[Notifications] DROP COLUMN [Body];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211157_ServerListsSavedViews'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[notifications].[Notifications]') AND [c].[name] = N'BodyAr');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [notifications].[Notifications] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [notifications].[Notifications] DROP COLUMN [BodyAr];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211157_ServerListsSavedViews'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[notifications].[Notifications]') AND [c].[name] = N'Title');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [notifications].[Notifications] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [notifications].[Notifications] DROP COLUMN [Title];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211157_ServerListsSavedViews'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[notifications].[Notifications]') AND [c].[name] = N'TitleAr');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [notifications].[Notifications] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [notifications].[Notifications] DROP COLUMN [TitleAr];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211157_ServerListsSavedViews'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[notifications].[Notifications]') AND [c].[name] = N'TitleMessage');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [notifications].[Notifications] DROP CONSTRAINT ' + @var4 + ';');
    EXEC(N'UPDATE [notifications].[Notifications] SET [TitleMessage] = N'''' WHERE [TitleMessage] IS NULL');
    ALTER TABLE [notifications].[Notifications] ALTER COLUMN [TitleMessage] nvarchar(4000) NOT NULL;
    ALTER TABLE [notifications].[Notifications] ADD DEFAULT N'' FOR [TitleMessage];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211157_ServerListsSavedViews'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[notifications].[Notifications]') AND [c].[name] = N'BodyMessage');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [notifications].[Notifications] DROP CONSTRAINT ' + @var5 + ';');
    EXEC(N'UPDATE [notifications].[Notifications] SET [BodyMessage] = N'''' WHERE [BodyMessage] IS NULL');
    ALTER TABLE [notifications].[Notifications] ALTER COLUMN [BodyMessage] nvarchar(max) NOT NULL;
    ALTER TABLE [notifications].[Notifications] ADD DEFAULT N'' FOR [BodyMessage];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211157_ServerListsSavedViews'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122211157_ServerListsSavedViews', N'10.0.15');
END;

COMMIT;
GO

