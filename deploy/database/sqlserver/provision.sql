/*
  FedNet SQL Server: one database and two logins per service.
    dsp_<svc>           runtime login  - read/write data only (used by the running service)
    dsp_<svc>_migrator  deploy login   - can change the schema (used by the pipeline to apply migrations)

  Run with sqlcmd as a sysadmin, passing passwords as variables, e.g.
    sqlcmd -S <server> -i provision.sql -v IdentityPwd="..." IdentityMigratorPwd="..." ...

  Idempotent: re-running keeps existing objects and resets passwords to the supplied values.
  Services then run with Database__MigrateOnStartup=false and their runtime login.
*/
SET NOCOUNT ON;

DECLARE @services TABLE (svc sysname, db sysname, pwd nvarchar(256), migratorPwd nvarchar(256));
INSERT INTO @services VALUES
  (N'identity',      N'DspIdentity',      N'$(IdentityPwd)',      N'$(IdentityMigratorPwd)'),
  (N'eservices',     N'DspEServices',     N'$(EServicesPwd)',     N'$(EServicesMigratorPwd)'),
  (N'eservices',     N'DspWorkflows',     N'$(EServicesPwd)',     N'$(EServicesMigratorPwd)'),
  (N'payments',      N'DspPayments',      N'$(PaymentsPwd)',      N'$(PaymentsMigratorPwd)'),
  (N'documents',     N'DspDocuments',     N'$(DocumentsPwd)',     N'$(DocumentsMigratorPwd)'),
  (N'notifications', N'DspNotifications', N'$(NotificationsPwd)', N'$(NotificationsMigratorPwd)');

DECLARE @svc sysname, @db sysname, @pwd nvarchar(256), @mpwd nvarchar(256), @sql nvarchar(max),
        @login sysname, @migrator sysname;

DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT svc, db, pwd, migratorPwd FROM @services;
OPEN c;
FETCH NEXT FROM c INTO @svc, @db, @pwd, @mpwd;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @login = N'dsp_' + @svc;
  SET @migrator = N'dsp_' + @svc + N'_migrator';

  IF DB_ID(@db) IS NULL
    EXEC (N'CREATE DATABASE ' + QUOTENAME(@db));

  IF SUSER_ID(@login) IS NULL
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@pwd, '''') + N', CHECK_POLICY = ON';
  ELSE
    SET @sql = N'ALTER LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@pwd, '''');
  EXEC (@sql);

  IF SUSER_ID(@migrator) IS NULL
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@migrator) + N' WITH PASSWORD = ' + QUOTENAME(@mpwd, '''') + N', CHECK_POLICY = ON';
  ELSE
    SET @sql = N'ALTER LOGIN ' + QUOTENAME(@migrator) + N' WITH PASSWORD = ' + QUOTENAME(@mpwd, '''');
  EXEC (@sql);

  SET @sql = N'USE ' + QUOTENAME(@db) + N';
    IF USER_ID(' + QUOTENAME(@login, '''') + N') IS NULL CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login) + N';
    IF USER_ID(' + QUOTENAME(@migrator, '''') + N') IS NULL CREATE USER ' + QUOTENAME(@migrator) + N' FOR LOGIN ' + QUOTENAME(@migrator) + N';
    ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@login) + N';
    ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@login) + N';
    ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@migrator) + N';';
  EXEC (@sql);

  PRINT N'Provisioned ' + @db + N' for ' + @login + N' / ' + @migrator;
  FETCH NEXT FROM c INTO @svc, @db, @pwd, @mpwd;
END
CLOSE c; DEALLOCATE c;
