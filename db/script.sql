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
    WHERE [MigrationId] = N'20260713135345_InitialCreate'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [InvoiceNumber] nvarchar(40) NOT NULL,
        [CustomerName] nvarchar(200) NOT NULL,
        [CustomerEmail] nvarchar(256) NOT NULL,
        [Status] int NOT NULL,
        [Currency] nchar(3) NOT NULL,
        [IssueDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [TaxTotal] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713135345_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceLineItem] (
        [Id] uniqueidentifier NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxRate] decimal(18,2) NOT NULL,
        [InvoiceId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_InvoiceLineItem] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoiceLineItem_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713135345_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceLineItem_InvoiceId] ON [InvoiceLineItem] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713135345_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_TenantId_InvoiceNumber] ON [Invoices] ([TenantId], [InvoiceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713135345_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_TenantId_IssueDate] ON [Invoices] ([TenantId], [IssueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713135345_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_TenantId_Status] ON [Invoices] ([TenantId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713135345_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260713135345_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

