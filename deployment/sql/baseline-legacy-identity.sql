-- Run only on the original email-verification deployment after a database backup.
-- This records the existing Identity schema; it does not confirm email addresses.
-- Normal EF migrations can then create the new product/order/profile tables.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL
    THROW 50001, 'This helper is only for the original Identity-only database. Do not run it on the new platform database.', 1;
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
    OR OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
    OR OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
    OR OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
    OR OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
    OR OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
    OR OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
    OR COL_LENGTH(N'dbo.AspNetUsers', N'EmailConfirmed') IS NULL
    OR COL_LENGTH(N'dbo.AspNetUsers', N'SecurityStamp') IS NULL
    THROW 50002, 'Expected ASP.NET Identity schema is missing. Stop and inspect the database.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL PRIMARY KEY,
        [ProductVersion] nvarchar(32) NOT NULL
    );
END;
IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809133612_InitialIdentityBaseline')
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260809133612_InitialIdentityBaseline', N'8.0.22');
COMMIT;
