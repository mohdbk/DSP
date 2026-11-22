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
    WHERE [MigrationId] = N'20261122161302_MultilingualPlatform'
)
BEGIN
    IF SCHEMA_ID(N'platform') IS NULL EXEC(N'CREATE SCHEMA [platform];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161302_MultilingualPlatform'
)
BEGIN
    CREATE TABLE [platform].[AiSettings] (
        [Id] uniqueidentifier NOT NULL,
        [ProviderName] nvarchar(100) NOT NULL,
        [BaseUrl] nvarchar(500) NOT NULL,
        [Model] nvarchar(200) NOT NULL,
        [ProtectedApiKey] nvarchar(2000) NULL,
        [Temperature] float NOT NULL,
        [TimeoutSeconds] int NOT NULL,
        [ChatEnabled] bit NOT NULL,
        [ReviewEnabled] bit NOT NULL,
        [MaxToolIterations] int NOT NULL,
        [MaxHistoryMessages] int NOT NULL,
        [ChatInstructions] nvarchar(max) NULL,
        [ReviewInstructions] nvarchar(max) NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_AiSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161302_MultilingualPlatform'
)
BEGIN
    CREATE TABLE [platform].[Languages] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(35) NOT NULL,
        [Name] nvarchar(4000) NOT NULL,
        [Direction] nvarchar(3) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [IsDefault] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Languages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161302_MultilingualPlatform'
)
BEGIN
    CREATE TABLE [platform].[Translations] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(200) NOT NULL,
        [LanguageCode] nvarchar(35) NOT NULL,
        [Text] nvarchar(4000) NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Translations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161302_MultilingualPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Languages_Code] ON [platform].[Languages] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161302_MultilingualPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Translations_Key_LanguageCode] ON [platform].[Translations] ([Key], [LanguageCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161302_MultilingualPlatform'
)
BEGIN
    CREATE INDEX [IX_Translations_LanguageCode] ON [platform].[Translations] ([LanguageCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122161302_MultilingualPlatform'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122161302_MultilingualPlatform', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122164206_PromptsPauseLanguageVisibility'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[platform].[AiSettings]') AND [c].[name] = N'ChatEnabled');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [platform].[AiSettings] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [platform].[AiSettings] DROP COLUMN [ChatEnabled];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122164206_PromptsPauseLanguageVisibility'
)
BEGIN
    EXEC sp_rename N'[platform].[AiSettings].[ReviewEnabled]', N'Paused', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122164206_PromptsPauseLanguageVisibility'
)
BEGIN
    UPDATE [platform].[AiSettings] SET [Paused] = 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122164206_PromptsPauseLanguageVisibility'
)
BEGIN
    ALTER TABLE [platform].[Languages] ADD [HiddenInBackoffice] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122164206_PromptsPauseLanguageVisibility'
)
BEGIN
    ALTER TABLE [platform].[Languages] ADD [HiddenInPortal] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122164206_PromptsPauseLanguageVisibility'
)
BEGIN
    ALTER TABLE [platform].[AiSettings] ADD [ChatPrompt] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122164206_PromptsPauseLanguageVisibility'
)
BEGIN
    ALTER TABLE [platform].[AiSettings] ADD [ReviewPrompt] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122164206_PromptsPauseLanguageVisibility'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122164206_PromptsPauseLanguageVisibility', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122170558_AiAuditSample'
)
BEGIN
    ALTER TABLE [platform].[AiSettings] ADD [AuditSamplePercent] int NOT NULL DEFAULT 10;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122170558_AiAuditSample'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122170558_AiAuditSample', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122172009_Currencies'
)
BEGIN
    CREATE TABLE [platform].[Currencies] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(3) NOT NULL,
        [Name] nvarchar(2000) NOT NULL,
        [Symbol] nvarchar(10) NOT NULL,
        [SymbolPosition] nvarchar(6) NOT NULL,
        [Decimals] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        [IsDefault] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_Currencies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122172009_Currencies'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Currencies_Code] ON [platform].[Currencies] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122172009_Currencies'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122172009_Currencies', N'10.0.15');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211216_ServerListsSavedViews'
)
BEGIN
    CREATE TABLE [platform].[SavedViews] (
        [Id] uniqueidentifier NOT NULL,
        [Owner] nvarchar(200) NOT NULL,
        [ListKey] nvarchar(100) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [State] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SavedViews] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211216_ServerListsSavedViews'
)
BEGIN
    CREATE INDEX [IX_SavedViews_Owner_ListKey] ON [platform].[SavedViews] ([Owner], [ListKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122211216_ServerListsSavedViews'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122211216_ServerListsSavedViews', N'10.0.15');
END;

COMMIT;
GO

