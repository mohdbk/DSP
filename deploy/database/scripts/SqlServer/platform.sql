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

