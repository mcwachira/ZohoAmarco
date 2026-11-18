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
CREATE TABLE [fiscalization_requests] (
    [Id] uniqueidentifier NOT NULL,
    [ZohoInvoiceId] nvarchar(100) NOT NULL,
    [IdempotencyKey] nvarchar(200) NOT NULL,
    [Status] int NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [UpdatedAtUtc] datetimeoffset NOT NULL,
    [CompletedAtUtc] datetimeoffset NULL,
    [FiscalReceiptNumber] nvarchar(100) NULL,
    [FailureCode] nvarchar(100) NULL,
    [FailureReason] nvarchar(2000) NULL,
    CONSTRAINT [PK_fiscalization_requests] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_fiscalization_requests_IdempotencyKey] ON [fiscalization_requests] ([IdempotencyKey]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261021080126_InitialCreate', N'10.0.13');

COMMIT;
GO

