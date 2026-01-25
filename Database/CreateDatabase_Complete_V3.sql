-- =====================================================
-- DocN Database Schema - Complete Creation Script V3
-- =====================================================
-- Version: V3
-- Date: 2026-01-25
-- Description: Complete idempotent database creation/update script
--
-- This script creates the entire DocN database schema including:
--   - ASP.NET Identity tables (users, roles)
--   - Document management tables
--   - RAG (Retrieval-Augmented Generation) tables
--   - AI configuration tables
--   - Dashboard widgets, saved searches, user activities
--   - Audit logs and golden datasets
--   - All indexes and constraints
--
-- FEATURES:
--   ✓ Idempotent - Can be run multiple times safely
--   ✓ Checks __EFMigrationsHistory table
--   ✓ Applies only missing migrations
--   ✓ Can be used for both new installations and updates
--
-- USAGE:
--   New installation:
--     sqlcmd -S your_server -d DocN -i CreateDatabase_Complete_V3.sql
--
--   Update existing database:
--     sqlcmd -S your_server -d DocN -i CreateDatabase_Complete_V3.sql
--
-- NOTES:
--   - This script only creates the database schema
--   - Initial data (admin user) is created by ApplicationSeeder at runtime
--   - See README.md for more information
--   - See UPDATE_GUIDE.md for detailed update instructions
--
-- LATEST MIGRATION: 20260124115302_AddDashboardAndRBACFeatures
-- =====================================================

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
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [AIConfigurations] (
        [Id] int NOT NULL IDENTITY,
        [ConfigurationName] nvarchar(100) NOT NULL,
        [AzureOpenAIEndpoint] nvarchar(max) NULL,
        [AzureOpenAIKey] nvarchar(500) NULL,
        [EmbeddingDeploymentName] nvarchar(max) NULL,
        [ChatDeploymentName] nvarchar(max) NULL,
        [MaxDocumentsToRetrieve] int NOT NULL,
        [SimilarityThreshold] float NOT NULL,
        [MaxTokensForContext] int NOT NULL,
        [SystemPrompt] nvarchar(2000) NULL,
        [EmbeddingDimensions] int NOT NULL,
        [EmbeddingModel] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_AIConfigurations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FirstName] nvarchar(max) NULL,
        [LastName] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [TenantId] int NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUsers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [Conversations] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastMessageAt] datetime2 NOT NULL,
        [IsArchived] bit NOT NULL,
        [IsStarred] bit NOT NULL,
        [Tags] nvarchar(500) NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversations_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [Documents] (
        [Id] int NOT NULL IDENTITY,
        [FileName] nvarchar(255) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(max) NOT NULL,
        [FileSize] bigint NOT NULL,
        [ExtractedText] nvarchar(max) NOT NULL,
        [SuggestedCategory] nvarchar(450) NULL,
        [CategoryReasoning] nvarchar(2000) NULL,
        [ActualCategory] nvarchar(450) NULL,
        [AITagsJson] nvarchar(max) NULL,
        [AIAnalysisDate] datetime2 NULL,
        [PageCount] int NULL,
        [DetectedLanguage] nvarchar(max) NULL,
        [ProcessingStatus] nvarchar(max) NULL,
        [ProcessingError] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [Visibility] int NOT NULL,
        [EmbeddingVector] nvarchar(max) NULL,
        [UploadedAt] datetime2 NOT NULL,
        [LastAccessedAt] datetime2 NULL,
        [AccessCount] int NOT NULL,
        [OwnerId] nvarchar(450) NULL,
        [TenantId] int NULL,
        CONSTRAINT [PK_Documents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Documents_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Documents_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [Messages] (
        [Id] int NOT NULL IDENTITY,
        [ConversationId] int NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ReferencedDocumentIds] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL,
        [IsError] bit NOT NULL,
        [Metadata] nvarchar(max) NULL,
        [UserRating] int NULL,
        [UserFeedback] nvarchar(max) NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [DocumentChunks] (
        [Id] int NOT NULL IDENTITY,
        [DocumentId] int NOT NULL,
        [ChunkIndex] int NOT NULL,
        [ChunkText] nvarchar(max) NOT NULL,
        [ChunkEmbedding] nvarchar(max) NULL,
        [TokenCount] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [StartPosition] int NOT NULL,
        [EndPosition] int NOT NULL,
        CONSTRAINT [PK_DocumentChunks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentChunks_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [DocumentShares] (
        [Id] int NOT NULL IDENTITY,
        [DocumentId] int NOT NULL,
        [SharedWithUserId] nvarchar(450) NOT NULL,
        [Permission] int NOT NULL,
        [SharedAt] datetime2 NOT NULL,
        [SharedByUserId] nvarchar(max) NULL,
        CONSTRAINT [PK_DocumentShares] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentShares_AspNetUsers_SharedWithUserId] FOREIGN KEY ([SharedWithUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DocumentShares_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE TABLE [DocumentTags] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [DocumentId] int NOT NULL,
        CONSTRAINT [PK_DocumentTags] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentTags_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_TenantId] ON [AspNetUsers] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Conversations_LastMessageAt] ON [Conversations] ([LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Conversations_UserId] ON [Conversations] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Conversations_UserId_IsArchived_LastMessageAt] ON [Conversations] ([UserId], [IsArchived], [LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_DocumentChunks_DocumentId] ON [DocumentChunks] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_DocumentChunks_DocumentId_ChunkIndex] ON [DocumentChunks] ([DocumentId], [ChunkIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Documents_ActualCategory] ON [Documents] ([ActualCategory]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Documents_OwnerId] ON [Documents] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Documents_SuggestedCategory] ON [Documents] ([SuggestedCategory]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Documents_TenantId] ON [Documents] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Documents_UploadedAt] ON [Documents] ([UploadedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Documents_Visibility] ON [Documents] ([Visibility]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_DocumentShares_DocumentId] ON [DocumentShares] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_DocumentShares_SharedWithUserId] ON [DocumentShares] ([SharedWithUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_DocumentTags_DocumentId] ON [DocumentTags] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_DocumentTags_Name] ON [DocumentTags] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Messages_ConversationId] ON [Messages] ([ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Messages_Timestamp] ON [Messages] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    CREATE INDEX [IX_Tenants_Name] ON [Tenants] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227115401_AddDocumentMetadataFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251227115401_AddDocumentMetadataFields', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [AzureOpenAIChatModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [AzureOpenAIEmbeddingModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [ChatModelName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [ChatProvider] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [ChunkOverlap] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [ChunkSize] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [EmbeddingModelName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [EmbeddingsProvider] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [EnableChunking] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [EnableFallback] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [GeminiApiKey] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [GeminiChatModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [GeminiEmbeddingModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [OpenAIApiKey] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [OpenAIChatModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [OpenAIEmbeddingModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [ProviderApiKey] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [ProviderEndpoint] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [ProviderType] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [RAGProvider] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [TagExtractionProvider] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228072726_AddMultiProviderAIConfiguration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251228072726_AddMultiProviderAIConfiguration', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228082855_AddSimilarDocumentsTable'
)
BEGIN
    CREATE TABLE [SimilarDocuments] (
        [Id] int NOT NULL IDENTITY,
        [SourceDocumentId] int NOT NULL,
        [SimilarDocumentId] int NOT NULL,
        [SimilarityScore] float NOT NULL,
        [RelevantChunk] nvarchar(1000) NULL,
        [ChunkIndex] int NULL,
        [AnalyzedAt] datetime2 NOT NULL,
        [Rank] int NOT NULL,
        CONSTRAINT [PK_SimilarDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SimilarDocuments_Documents_SimilarDocumentId] FOREIGN KEY ([SimilarDocumentId]) REFERENCES [Documents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SimilarDocuments_Documents_SourceDocumentId] FOREIGN KEY ([SourceDocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228082855_AddSimilarDocumentsTable'
)
BEGIN
    CREATE INDEX [IX_SimilarDocuments_SimilarDocumentId] ON [SimilarDocuments] ([SimilarDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228082855_AddSimilarDocumentsTable'
)
BEGIN
    CREATE INDEX [IX_SimilarDocuments_SourceDocumentId] ON [SimilarDocuments] ([SourceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228082855_AddSimilarDocumentsTable'
)
BEGIN
    CREATE INDEX [IX_SimilarDocuments_SourceDocumentId_Rank] ON [SimilarDocuments] ([SourceDocumentId], [Rank]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228082855_AddSimilarDocumentsTable'
)
BEGIN
    CREATE INDEX [IX_SimilarDocuments_SourceDocumentId_SimilarityScore] ON [SimilarDocuments] ([SourceDocumentId], [SimilarityScore]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228082855_AddSimilarDocumentsTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251228082855_AddSimilarDocumentsTable', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251229153527_AddEmbeddingDimensionTracking'
)
BEGIN
    ALTER TABLE [Documents] DROP CONSTRAINT [FK_Documents_AspNetUsers_OwnerId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251229153527_AddEmbeddingDimensionTracking'
)
BEGIN
    ALTER TABLE [Documents] ADD [EmbeddingDimension] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251229153527_AddEmbeddingDimensionTracking'
)
BEGIN
    ALTER TABLE [Documents] ADD [ExtractedMetadataJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251229153527_AddEmbeddingDimensionTracking'
)
BEGIN
    ALTER TABLE [DocumentChunks] ADD [EmbeddingDimension] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251229153527_AddEmbeddingDimensionTracking'
)
BEGIN
    ALTER TABLE [Documents] ADD CONSTRAINT [FK_Documents_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251229153527_AddEmbeddingDimensionTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251229153527_AddEmbeddingDimensionTracking', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    EXEC sp_rename N'[Documents].[EmbeddingVector]', N'EmbeddingVector768', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    EXEC sp_rename N'[DocumentChunks].[ChunkEmbedding]', N'ChunkEmbedding768', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    ALTER TABLE [Documents] ADD [ChunkEmbeddingStatus] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    ALTER TABLE [Documents] ADD [EmbeddingVector1536] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    ALTER TABLE [DocumentChunks] ADD [ChunkEmbedding1536] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE TABLE [AgentTemplates] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [Icon] nvarchar(max) NOT NULL,
        [AgentType] int NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [RecommendedProvider] int NOT NULL,
        [RecommendedModel] nvarchar(max) NULL,
        [DefaultSystemPrompt] nvarchar(max) NOT NULL,
        [DefaultParametersJson] nvarchar(4000) NOT NULL,
        [IsBuiltIn] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [UsageCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [OwnerId] nvarchar(450) NULL,
        [ExampleQuery] nvarchar(max) NULL,
        [ExampleResponse] nvarchar(max) NULL,
        [ConfigurationGuide] nvarchar(max) NULL,
        CONSTRAINT [PK_AgentTemplates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AgentTemplates_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] nvarchar(450) NULL,
        [Username] nvarchar(256) NULL,
        [Action] nvarchar(100) NOT NULL,
        [ResourceType] nvarchar(50) NOT NULL,
        [ResourceId] nvarchar(100) NULL,
        [Details] nvarchar(max) NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(500) NULL,
        [TenantId] int NULL,
        [Timestamp] datetime2 NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Success] bit NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AuditLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE TABLE [LogEntries] (
        [Id] int NOT NULL IDENTITY,
        [Timestamp] datetime2 NOT NULL,
        [Level] nvarchar(50) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [Details] nvarchar(max) NULL,
        [UserId] nvarchar(450) NULL,
        [FileName] nvarchar(500) NULL,
        [StackTrace] nvarchar(max) NULL,
        CONSTRAINT [PK_LogEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE TABLE [AgentConfigurations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [AgentType] int NOT NULL,
        [PrimaryProvider] int NOT NULL,
        [FallbackProvider] int NULL,
        [ModelName] nvarchar(max) NULL,
        [EmbeddingModelName] nvarchar(max) NULL,
        [MaxDocumentsToRetrieve] int NOT NULL,
        [SimilarityThreshold] float NOT NULL,
        [MaxTokensForContext] int NOT NULL,
        [MaxTokensForResponse] int NOT NULL,
        [Temperature] float NOT NULL,
        [SystemPrompt] nvarchar(max) NOT NULL,
        [CustomInstructions] nvarchar(2000) NULL,
        [CanRetrieveDocuments] bit NOT NULL,
        [CanClassifyDocuments] bit NOT NULL,
        [CanExtractTags] bit NOT NULL,
        [CanSummarize] bit NOT NULL,
        [CanAnswer] bit NOT NULL,
        [UseHybridSearch] bit NOT NULL,
        [HybridSearchAlpha] float NOT NULL,
        [EnableConversationHistory] bit NOT NULL,
        [MaxConversationHistoryMessages] int NOT NULL,
        [EnableCitation] bit NOT NULL,
        [EnableStreaming] bit NOT NULL,
        [CategoryFilter] nvarchar(1000) NULL,
        [TagFilter] nvarchar(1000) NULL,
        [VisibilityFilter] int NULL,
        [CacheTTLSeconds] int NULL,
        [EnableParallelRetrieval] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [IsPublic] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastUsedAt] datetime2 NULL,
        [UsageCount] int NOT NULL,
        [OwnerId] nvarchar(450) NULL,
        [TenantId] int NULL,
        [TemplateId] int NULL,
        CONSTRAINT [PK_AgentConfigurations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AgentConfigurations_AgentTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [AgentTemplates] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AgentConfigurations_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AgentConfigurations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE TABLE [AgentUsageLogs] (
        [Id] int NOT NULL IDENTITY,
        [AgentConfigurationId] int NOT NULL,
        [Query] nvarchar(max) NOT NULL,
        [Response] nvarchar(max) NULL,
        [DocumentsRetrieved] int NOT NULL,
        [RetrievalTimeTicks] bigint NOT NULL,
        [SynthesisTimeTicks] bigint NOT NULL,
        [TotalTimeTicks] bigint NOT NULL,
        [RetrievalTime] time NOT NULL,
        [SynthesisTime] time NOT NULL,
        [TotalTime] time NOT NULL,
        [PromptTokens] int NULL,
        [CompletionTokens] int NULL,
        [TotalTokens] int NULL,
        [ProviderUsed] int NOT NULL,
        [ModelUsed] nvarchar(max) NULL,
        [RelevanceScore] float NULL,
        [UserFeedbackPositive] bit NULL,
        [UserFeedbackComment] nvarchar(max) NULL,
        [IsError] bit NOT NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [UserId] nvarchar(450) NULL,
        [TenantId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AgentUsageLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AgentUsageLogs_AgentConfigurations_AgentConfigurationId] FOREIGN KEY ([AgentConfigurationId]) REFERENCES [AgentConfigurations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AgentUsageLogs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AgentUsageLogs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentConfigurations_AgentType] ON [AgentConfigurations] ([AgentType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentConfigurations_IsActive] ON [AgentConfigurations] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentConfigurations_OwnerId] ON [AgentConfigurations] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentConfigurations_TemplateId] ON [AgentConfigurations] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentConfigurations_TenantId] ON [AgentConfigurations] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentConfigurations_TenantId_IsActive] ON [AgentConfigurations] ([TenantId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentTemplates_AgentType] ON [AgentTemplates] ([AgentType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentTemplates_Category] ON [AgentTemplates] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentTemplates_IsActive] ON [AgentTemplates] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentTemplates_IsBuiltIn] ON [AgentTemplates] ([IsBuiltIn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentTemplates_OwnerId] ON [AgentTemplates] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentUsageLogs_AgentConfigurationId] ON [AgentUsageLogs] ([AgentConfigurationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentUsageLogs_AgentConfigurationId_CreatedAt] ON [AgentUsageLogs] ([AgentConfigurationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentUsageLogs_CreatedAt] ON [AgentUsageLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentUsageLogs_TenantId] ON [AgentUsageLogs] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AgentUsageLogs_UserId] ON [AgentUsageLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action_Timestamp] ON [AuditLogs] ([Action], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ResourceType] ON [AuditLogs] ([ResourceType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ResourceType_ResourceId] ON [AuditLogs] ([ResourceType], [ResourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TenantId] ON [AuditLogs] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId_Timestamp] ON [AuditLogs] ([UserId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_LogEntries_Category_Timestamp] ON [LogEntries] ([Category], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_LogEntries_Timestamp] ON [LogEntries] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    CREATE INDEX [IX_LogEntries_UserId_Timestamp] ON [LogEntries] ([UserId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260102100313_AddChunkEmbeddingStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260102100313_AddChunkEmbeddingStatus', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260103172002_MakeReferencedDocumentIdsNullable'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Messages]') AND [c].[name] = N'ReferencedDocumentIds');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Messages] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Messages] ALTER COLUMN [ReferencedDocumentIds] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260103172002_MakeReferencedDocumentIdsNullable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260103172002_MakeReferencedDocumentIdsNullable', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105103522_AddMetadataExtractionProvider'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [GroqApiKey] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105103522_AddMetadataExtractionProvider'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [GroqChatModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105103522_AddMetadataExtractionProvider'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [GroqEndpoint] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105103522_AddMetadataExtractionProvider'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [MetadataExtractionProvider] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105103522_AddMetadataExtractionProvider'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [OllamaChatModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105103522_AddMetadataExtractionProvider'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [OllamaEmbeddingModel] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105103522_AddMetadataExtractionProvider'
)
BEGIN
    ALTER TABLE [AIConfigurations] ADD [OllamaEndpoint] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260105103522_AddMetadataExtractionProvider'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260105103522_AddMetadataExtractionProvider', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE TABLE [UserGroups] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [OwnerId] nvarchar(450) NULL,
        [TenantId] int NULL,
        CONSTRAINT [PK_UserGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserGroups_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_UserGroups_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE TABLE [DocumentGroupShares] (
        [Id] int NOT NULL IDENTITY,
        [DocumentId] int NOT NULL,
        [GroupId] int NOT NULL,
        [Permission] int NOT NULL,
        [SharedAt] datetime2 NOT NULL,
        [SharedByUserId] nvarchar(max) NULL,
        CONSTRAINT [PK_DocumentGroupShares] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentGroupShares_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DocumentGroupShares_UserGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [UserGroups] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE TABLE [UserGroupMembers] (
        [Id] int NOT NULL IDENTITY,
        [GroupId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Role] int NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserGroupMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserGroupMembers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserGroupMembers_UserGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [UserGroups] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentGroupShares_DocumentId_GroupId] ON [DocumentGroupShares] ([DocumentId], [GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE INDEX [IX_DocumentGroupShares_GroupId] ON [DocumentGroupShares] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserGroupMembers_GroupId_UserId] ON [UserGroupMembers] ([GroupId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE INDEX [IX_UserGroupMembers_UserId] ON [UserGroupMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE INDEX [IX_UserGroups_Name] ON [UserGroups] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE INDEX [IX_UserGroups_OwnerId] ON [UserGroups] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    CREATE INDEX [IX_UserGroups_TenantId] ON [UserGroups] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260108043707_AddUserGroupsAndDocumentGroupShares'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260108043707_AddUserGroupsAndDocumentGroupShares', N'10.0.1');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [ErrorDetailsJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [ErrorMessage] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [ErrorType] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [IsRetryable] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [LastRetryAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [MaxRetries] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [NextRetryAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [PreviousWorkflowState] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [RetryCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [SourceConnectorId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [SourceFileHash] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [SourceFilePath] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [SourceLastModified] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [StateEnteredAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [Documents] ADD [WorkflowState] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [DocumentChunks] ADD [ChunkType] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [DocumentChunks] ADD [ImportanceScore] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [DocumentChunks] ADD [KeywordsJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [DocumentChunks] ADD [Section] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    ALTER TABLE [DocumentChunks] ADD [Title] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE TABLE [DashboardWidgets] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [WidgetType] nvarchar(50) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Position] int NOT NULL,
        [Configuration] nvarchar(max) NULL,
        [IsVisible] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_DashboardWidgets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DashboardWidgets_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE TABLE [GoldenDatasets] (
        [Id] int NOT NULL IDENTITY,
        [DatasetId] nvarchar(100) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Version] nvarchar(20) NOT NULL,
        [TenantId] int NULL,
        [CreatedBy] nvarchar(256) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [MetadataJson] nvarchar(max) NULL,
        CONSTRAINT [PK_GoldenDatasets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoldenDatasets_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE TABLE [SavedSearches] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Query] nvarchar(1000) NOT NULL,
        [Filters] nvarchar(max) NULL,
        [SearchType] nvarchar(20) NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastUsedAt] datetime2 NULL,
        [UseCount] int NOT NULL,
        CONSTRAINT [PK_SavedSearches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SavedSearches_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE TABLE [UserActivities] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ActivityType] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [DocumentId] int NULL,
        [Metadata] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserActivities_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserActivities_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE TABLE [GoldenDatasetEvaluationRecords] (
        [Id] int NOT NULL IDENTITY,
        [GoldenDatasetId] int NOT NULL,
        [EvaluatedAt] datetime2 NOT NULL,
        [ConfigurationId] nvarchar(100) NULL,
        [TotalSamples] int NOT NULL,
        [EvaluatedSamples] int NOT NULL,
        [FailedSamples] int NOT NULL,
        [AverageFaithfulnessScore] float NOT NULL,
        [AverageAnswerRelevancyScore] float NOT NULL,
        [AverageContextPrecisionScore] float NOT NULL,
        [AverageContextRecallScore] float NOT NULL,
        [OverallRAGASScore] float NOT NULL,
        [AverageConfidenceScore] float NOT NULL,
        [LowConfidenceRate] float NOT NULL,
        [HallucinationRate] float NOT NULL,
        [CitationVerificationRate] float NOT NULL,
        [DetailedResultsJson] nvarchar(max) NULL,
        [FailedSampleIdsJson] nvarchar(max) NULL,
        [Status] nvarchar(20) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [DurationSeconds] float NOT NULL,
        [TenantId] int NULL,
        CONSTRAINT [PK_GoldenDatasetEvaluationRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoldenDatasetEvaluationRecords_GoldenDatasets_GoldenDatasetId] FOREIGN KEY ([GoldenDatasetId]) REFERENCES [GoldenDatasets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GoldenDatasetEvaluationRecords_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE TABLE [GoldenDatasetSamples] (
        [Id] int NOT NULL IDENTITY,
        [GoldenDatasetId] int NOT NULL,
        [Query] nvarchar(1000) NOT NULL,
        [GroundTruth] nvarchar(4000) NOT NULL,
        [RelevantDocumentIdsJson] nvarchar(max) NULL,
        [ExpectedResponse] nvarchar(4000) NULL,
        [Category] nvarchar(100) NULL,
        [DifficultyLevel] nvarchar(20) NOT NULL,
        [ImportanceWeight] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_GoldenDatasetSamples] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoldenDatasetSamples_GoldenDatasets_GoldenDatasetId] FOREIGN KEY ([GoldenDatasetId]) REFERENCES [GoldenDatasets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_DashboardWidgets_UserId] ON [DashboardWidgets] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_DashboardWidgets_UserId_Position] ON [DashboardWidgets] ([UserId], [Position]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasetEvaluationRecords_ConfigurationId] ON [GoldenDatasetEvaluationRecords] ([ConfigurationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasetEvaluationRecords_EvaluatedAt] ON [GoldenDatasetEvaluationRecords] ([EvaluatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasetEvaluationRecords_GoldenDatasetId] ON [GoldenDatasetEvaluationRecords] ([GoldenDatasetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasetEvaluationRecords_GoldenDatasetId_EvaluatedAt] ON [GoldenDatasetEvaluationRecords] ([GoldenDatasetId], [EvaluatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasetEvaluationRecords_TenantId] ON [GoldenDatasetEvaluationRecords] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GoldenDatasets_DatasetId] ON [GoldenDatasets] ([DatasetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasets_IsActive] ON [GoldenDatasets] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasets_TenantId] ON [GoldenDatasets] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasets_TenantId_IsActive] ON [GoldenDatasets] ([TenantId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasetSamples_Category] ON [GoldenDatasetSamples] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasetSamples_GoldenDatasetId] ON [GoldenDatasetSamples] ([GoldenDatasetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasetSamples_GoldenDatasetId_IsActive] ON [GoldenDatasetSamples] ([GoldenDatasetId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_GoldenDatasetSamples_IsActive] ON [GoldenDatasetSamples] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_SavedSearches_UserId] ON [SavedSearches] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_SavedSearches_UserId_LastUsedAt] ON [SavedSearches] ([UserId], [LastUsedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_UserActivities_CreatedAt] ON [UserActivities] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_UserActivities_DocumentId] ON [UserActivities] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_UserActivities_UserId] ON [UserActivities] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    CREATE INDEX [IX_UserActivities_UserId_CreatedAt] ON [UserActivities] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260124115302_AddDashboardAndRBACFeatures'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260124115302_AddDashboardAndRBACFeatures', N'10.0.1');
END;

COMMIT;
GO

