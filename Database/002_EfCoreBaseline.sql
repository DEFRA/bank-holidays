-- Baseline for databases created by the previous EF6 model (tables already exist, EF6 __MigrationHistory present).
-- Marks the EF Core InitialCreate migration as applied so Database.Migrate() / the idempotent script skip it.
-- Do NOT run this against a new, empty database: let the migration create the schema instead.
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261127160242_InitialCreate')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261127160242_InitialCreate', N'10.0.15');
