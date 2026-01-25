-- =====================================================
-- Script SQL Manuale per Aggiornamento a V3
-- Manual SQL Script for Update to V3
-- =====================================================
-- Versione / Version: V3 (2026-01-25)
-- 
-- Questo script aggiorna manualmente il database controllando
-- l'esistenza di tabelle e campi prima di crearli/aggiungerli.
--
-- This script manually updates the database by checking
-- for existence of tables and fields before creating/adding them.
--
-- IMPORTANTE / IMPORTANT:
-- - Fai un BACKUP prima di eseguire! / Make a BACKUP before running!
-- - Questo script può essere eseguito più volte / This script can be run multiple times
-- - Controlla ogni sezione prima dell'esecuzione / Review each section before running
--
-- UTILIZZO / USAGE:
--   sqlcmd -S your_server -d DocN -i ManualUpdate_To_V3.sql
-- =====================================================

SET NOCOUNT ON;
PRINT '=====================================================';
PRINT 'DocN Database - Manual Update to V3';
PRINT 'Data/Date: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '=====================================================';
PRINT '';

BEGIN TRANSACTION;

BEGIN TRY

    -- =====================================================
    -- PARTE 1: CAMPI WORKFLOW SU TABELLA DOCUMENTS
    -- PART 1: WORKFLOW FIELDS ON DOCUMENTS TABLE
    -- =====================================================
    PRINT 'Parte 1/5: Aggiornamento tabella Documents...';
    
    -- ErrorDetailsJson
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'ErrorDetailsJson')
    BEGIN
        ALTER TABLE Documents ADD ErrorDetailsJson NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo ErrorDetailsJson';
    END
    ELSE
        PRINT '  - Campo ErrorDetailsJson già esistente';

    -- ErrorMessage
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'ErrorMessage')
    BEGIN
        ALTER TABLE Documents ADD ErrorMessage NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo ErrorMessage';
    END
    ELSE
        PRINT '  - Campo ErrorMessage già esistente';

    -- ErrorType
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'ErrorType')
    BEGIN
        ALTER TABLE Documents ADD ErrorType NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo ErrorType';
    END
    ELSE
        PRINT '  - Campo ErrorType già esistente';

    -- IsRetryable
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'IsRetryable')
    BEGIN
        ALTER TABLE Documents ADD IsRetryable BIT NOT NULL DEFAULT 0;
        PRINT '  ✓ Aggiunto campo IsRetryable';
    END
    ELSE
        PRINT '  - Campo IsRetryable già esistente';

    -- LastRetryAt
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'LastRetryAt')
    BEGIN
        ALTER TABLE Documents ADD LastRetryAt DATETIME2 NULL;
        PRINT '  ✓ Aggiunto campo LastRetryAt';
    END
    ELSE
        PRINT '  - Campo LastRetryAt già esistente';

    -- MaxRetries
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'MaxRetries')
    BEGIN
        ALTER TABLE Documents ADD MaxRetries INT NOT NULL DEFAULT 0;
        PRINT '  ✓ Aggiunto campo MaxRetries';
    END
    ELSE
        PRINT '  - Campo MaxRetries già esistente';

    -- NextRetryAt
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'NextRetryAt')
    BEGIN
        ALTER TABLE Documents ADD NextRetryAt DATETIME2 NULL;
        PRINT '  ✓ Aggiunto campo NextRetryAt';
    END
    ELSE
        PRINT '  - Campo NextRetryAt già esistente';

    -- PreviousWorkflowState
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'PreviousWorkflowState')
    BEGIN
        ALTER TABLE Documents ADD PreviousWorkflowState NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo PreviousWorkflowState';
    END
    ELSE
        PRINT '  - Campo PreviousWorkflowState già esistente';

    -- RetryCount
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'RetryCount')
    BEGIN
        ALTER TABLE Documents ADD RetryCount INT NOT NULL DEFAULT 0;
        PRINT '  ✓ Aggiunto campo RetryCount';
    END
    ELSE
        PRINT '  - Campo RetryCount già esistente';

    -- SourceConnectorId
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'SourceConnectorId')
    BEGIN
        ALTER TABLE Documents ADD SourceConnectorId INT NULL;
        PRINT '  ✓ Aggiunto campo SourceConnectorId';
    END
    ELSE
        PRINT '  - Campo SourceConnectorId già esistente';

    -- SourceFileHash
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'SourceFileHash')
    BEGIN
        ALTER TABLE Documents ADD SourceFileHash NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo SourceFileHash';
    END
    ELSE
        PRINT '  - Campo SourceFileHash già esistente';

    -- SourceFilePath
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'SourceFilePath')
    BEGIN
        ALTER TABLE Documents ADD SourceFilePath NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo SourceFilePath';
    END
    ELSE
        PRINT '  - Campo SourceFilePath già esistente';

    -- SourceLastModified
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'SourceLastModified')
    BEGIN
        ALTER TABLE Documents ADD SourceLastModified DATETIME2 NULL;
        PRINT '  ✓ Aggiunto campo SourceLastModified';
    END
    ELSE
        PRINT '  - Campo SourceLastModified già esistente';

    -- StateEnteredAt
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'StateEnteredAt')
    BEGIN
        ALTER TABLE Documents ADD StateEnteredAt DATETIME2 NULL;
        PRINT '  ✓ Aggiunto campo StateEnteredAt';
    END
    ELSE
        PRINT '  - Campo StateEnteredAt già esistente';

    -- WorkflowState
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'WorkflowState')
    BEGIN
        ALTER TABLE Documents ADD WorkflowState NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo WorkflowState';
    END
    ELSE
        PRINT '  - Campo WorkflowState già esistente';

    PRINT '';

    -- =====================================================
    -- PARTE 2: CAMPI SU TABELLA DOCUMENTCHUNKS
    -- PART 2: FIELDS ON DOCUMENTCHUNKS TABLE
    -- =====================================================
    PRINT 'Parte 2/5: Aggiornamento tabella DocumentChunks...';

    -- ChunkType
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'ChunkType')
    BEGIN
        ALTER TABLE DocumentChunks ADD ChunkType NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo ChunkType';
    END
    ELSE
        PRINT '  - Campo ChunkType già esistente';

    -- ImportanceScore
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'ImportanceScore')
    BEGIN
        ALTER TABLE DocumentChunks ADD ImportanceScore FLOAT NULL;
        PRINT '  ✓ Aggiunto campo ImportanceScore';
    END
    ELSE
        PRINT '  - Campo ImportanceScore già esistente';

    -- KeywordsJson
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'KeywordsJson')
    BEGIN
        ALTER TABLE DocumentChunks ADD KeywordsJson NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo KeywordsJson';
    END
    ELSE
        PRINT '  - Campo KeywordsJson già esistente';

    -- Section
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'Section')
    BEGIN
        ALTER TABLE DocumentChunks ADD Section NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo Section';
    END
    ELSE
        PRINT '  - Campo Section già esistente';

    -- Title
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'Title')
    BEGIN
        ALTER TABLE DocumentChunks ADD Title NVARCHAR(MAX) NULL;
        PRINT '  ✓ Aggiunto campo Title';
    END
    ELSE
        PRINT '  - Campo Title già esistente';

    PRINT '';

    -- =====================================================
    -- PARTE 3: NUOVE TABELLE DASHBOARD
    -- PART 3: NEW DASHBOARD TABLES
    -- =====================================================
    PRINT 'Parte 3/5: Creazione tabelle Dashboard...';

    -- Tabella DashboardWidgets
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DashboardWidgets')
    BEGIN
        CREATE TABLE DashboardWidgets (
            Id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
            UserId NVARCHAR(450) NOT NULL,
            WidgetType NVARCHAR(50) NOT NULL,
            Title NVARCHAR(200) NOT NULL,
            Position INT NOT NULL,
            Configuration NVARCHAR(MAX) NULL,
            IsVisible BIT NOT NULL,
            CreatedAt DATETIME2 NOT NULL,
            UpdatedAt DATETIME2 NULL,
            CONSTRAINT FK_DashboardWidgets_AspNetUsers_UserId 
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
        );
        
        CREATE INDEX IX_DashboardWidgets_UserId ON DashboardWidgets(UserId);
        CREATE INDEX IX_DashboardWidgets_UserId_Position ON DashboardWidgets(UserId, Position);
        
        PRINT '  ✓ Creata tabella DashboardWidgets';
    END
    ELSE
        PRINT '  - Tabella DashboardWidgets già esistente';

    -- Tabella SavedSearches
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SavedSearches')
    BEGIN
        CREATE TABLE SavedSearches (
            Id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
            UserId NVARCHAR(450) NOT NULL,
            Name NVARCHAR(200) NOT NULL,
            Query NVARCHAR(1000) NOT NULL,
            Filters NVARCHAR(MAX) NULL,
            SearchType NVARCHAR(20) NOT NULL,
            IsDefault BIT NOT NULL,
            CreatedAt DATETIME2 NOT NULL,
            LastUsedAt DATETIME2 NULL,
            UseCount INT NOT NULL,
            CONSTRAINT FK_SavedSearches_AspNetUsers_UserId 
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
        );
        
        CREATE INDEX IX_SavedSearches_UserId ON SavedSearches(UserId);
        CREATE INDEX IX_SavedSearches_UserId_LastUsedAt ON SavedSearches(UserId, LastUsedAt);
        
        PRINT '  ✓ Creata tabella SavedSearches';
    END
    ELSE
        PRINT '  - Tabella SavedSearches già esistente';

    -- Tabella UserActivities
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserActivities')
    BEGIN
        CREATE TABLE UserActivities (
            Id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
            UserId NVARCHAR(450) NOT NULL,
            ActivityType NVARCHAR(50) NOT NULL,
            Description NVARCHAR(500) NOT NULL,
            DocumentId INT NULL,
            Metadata NVARCHAR(MAX) NULL,
            CreatedAt DATETIME2 NOT NULL,
            CONSTRAINT FK_UserActivities_AspNetUsers_UserId 
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
            CONSTRAINT FK_UserActivities_Documents_DocumentId 
                FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE SET NULL
        );
        
        CREATE INDEX IX_UserActivities_UserId ON UserActivities(UserId);
        CREATE INDEX IX_UserActivities_UserId_CreatedAt ON UserActivities(UserId, CreatedAt);
        CREATE INDEX IX_UserActivities_DocumentId ON UserActivities(DocumentId);
        CREATE INDEX IX_UserActivities_CreatedAt ON UserActivities(CreatedAt);
        
        PRINT '  ✓ Creata tabella UserActivities';
    END
    ELSE
        PRINT '  - Tabella UserActivities già esistente';

    PRINT '';

    -- =====================================================
    -- PARTE 4: TABELLE GOLDEN DATASETS (se non esistono)
    -- PART 4: GOLDEN DATASETS TABLES (if not exist)
    -- =====================================================
    PRINT 'Parte 4/5: Verifica tabelle Golden Datasets...';

    -- Tabella GoldenDatasets
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GoldenDatasets')
    BEGIN
        CREATE TABLE GoldenDatasets (
            Id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
            DatasetId NVARCHAR(100) NOT NULL,
            Name NVARCHAR(200) NOT NULL,
            Description NVARCHAR(1000) NULL,
            Version NVARCHAR(20) NOT NULL,
            TenantId INT NULL,
            CreatedBy NVARCHAR(256) NULL,
            CreatedAt DATETIME2 NOT NULL,
            UpdatedAt DATETIME2 NULL,
            IsActive BIT NOT NULL,
            MetadataJson NVARCHAR(MAX) NULL,
            CONSTRAINT FK_GoldenDatasets_Tenants_TenantId 
                FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE SET NULL
        );
        
        CREATE UNIQUE INDEX IX_GoldenDatasets_DatasetId_Version ON GoldenDatasets(DatasetId, Version);
        CREATE INDEX IX_GoldenDatasets_TenantId ON GoldenDatasets(TenantId);
        
        PRINT '  ✓ Creata tabella GoldenDatasets';
    END
    ELSE
        PRINT '  - Tabella GoldenDatasets già esistente';

    -- Tabella GoldenDatasetSamples
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GoldenDatasetSamples')
    BEGIN
        CREATE TABLE GoldenDatasetSamples (
            Id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
            GoldenDatasetId INT NOT NULL,
            Query NVARCHAR(1000) NOT NULL,
            GroundTruth NVARCHAR(4000) NOT NULL,
            RelevantDocumentIdsJson NVARCHAR(MAX) NULL,
            ExpectedResponse NVARCHAR(4000) NULL,
            Category NVARCHAR(100) NULL,
            DifficultyLevel NVARCHAR(20) NOT NULL,
            ImportanceWeight INT NOT NULL,
            Notes NVARCHAR(1000) NULL,
            CreatedAt DATETIME2 NOT NULL,
            CreatedBy NVARCHAR(256) NULL,
            IsActive BIT NOT NULL,
            CONSTRAINT FK_GoldenDatasetSamples_GoldenDatasets_GoldenDatasetId 
                FOREIGN KEY (GoldenDatasetId) REFERENCES GoldenDatasets(Id) ON DELETE CASCADE
        );
        
        CREATE INDEX IX_GoldenDatasetSamples_GoldenDatasetId ON GoldenDatasetSamples(GoldenDatasetId);
        CREATE INDEX IX_GoldenDatasetSamples_Category ON GoldenDatasetSamples(Category);
        CREATE INDEX IX_GoldenDatasetSamples_DifficultyLevel ON GoldenDatasetSamples(DifficultyLevel);
        
        PRINT '  ✓ Creata tabella GoldenDatasetSamples';
    END
    ELSE
        PRINT '  - Tabella GoldenDatasetSamples già esistente';

    -- Tabella GoldenDatasetEvaluationRecords
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GoldenDatasetEvaluationRecords')
    BEGIN
        CREATE TABLE GoldenDatasetEvaluationRecords (
            Id INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
            GoldenDatasetId INT NOT NULL,
            EvaluatedAt DATETIME2 NOT NULL,
            ConfigurationId NVARCHAR(100) NULL,
            TotalSamples INT NOT NULL,
            EvaluatedSamples INT NOT NULL,
            FailedSamples INT NOT NULL,
            AverageFaithfulnessScore FLOAT NOT NULL,
            AverageAnswerRelevancyScore FLOAT NOT NULL,
            AverageContextPrecisionScore FLOAT NOT NULL,
            AverageContextRecallScore FLOAT NOT NULL,
            OverallRAGASScore FLOAT NOT NULL,
            AverageConfidenceScore FLOAT NOT NULL,
            LowConfidenceRate FLOAT NOT NULL,
            HallucinationRate FLOAT NOT NULL,
            CitationVerificationRate FLOAT NOT NULL,
            DetailedResultsJson NVARCHAR(MAX) NULL,
            FailedSampleIdsJson NVARCHAR(MAX) NULL,
            Status NVARCHAR(20) NOT NULL,
            Notes NVARCHAR(2000) NULL,
            DurationSeconds FLOAT NOT NULL,
            TenantId INT NULL,
            CONSTRAINT FK_GoldenDatasetEvaluationRecords_GoldenDatasets_GoldenDatasetId 
                FOREIGN KEY (GoldenDatasetId) REFERENCES GoldenDatasets(Id) ON DELETE CASCADE,
            CONSTRAINT FK_GoldenDatasetEvaluationRecords_Tenants_TenantId 
                FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE SET NULL
        );
        
        CREATE INDEX IX_GoldenDatasetEvaluationRecords_GoldenDatasetId ON GoldenDatasetEvaluationRecords(GoldenDatasetId);
        CREATE INDEX IX_GoldenDatasetEvaluationRecords_EvaluatedAt ON GoldenDatasetEvaluationRecords(EvaluatedAt);
        CREATE INDEX IX_GoldenDatasetEvaluationRecords_ConfigurationId ON GoldenDatasetEvaluationRecords(ConfigurationId);
        CREATE INDEX IX_GoldenDatasetEvaluationRecords_GoldenDatasetId_EvaluatedAt ON GoldenDatasetEvaluationRecords(GoldenDatasetId, EvaluatedAt);
        CREATE INDEX IX_GoldenDatasetEvaluationRecords_TenantId ON GoldenDatasetEvaluationRecords(TenantId);
        
        PRINT '  ✓ Creata tabella GoldenDatasetEvaluationRecords';
    END
    ELSE
        PRINT '  - Tabella GoldenDatasetEvaluationRecords già esistente';

    PRINT '';

    -- =====================================================
    -- PARTE 5: AGGIORNAMENTO MIGRATIONS HISTORY
    -- PART 5: UPDATE MIGRATIONS HISTORY
    -- =====================================================
    PRINT 'Parte 5/5: Aggiornamento __EFMigrationsHistory...';

    IF NOT EXISTS (SELECT * FROM __EFMigrationsHistory WHERE MigrationId = '20260124115302_AddDashboardAndRBACFeatures')
    BEGIN
        INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES ('20260124115302_AddDashboardAndRBACFeatures', '10.0.1');
        PRINT '  ✓ Aggiunta migrazione 20260124115302_AddDashboardAndRBACFeatures';
    END
    ELSE
        PRINT '  - Migrazione 20260124115302_AddDashboardAndRBACFeatures già registrata';

    PRINT '';

    -- =====================================================
    -- COMMIT TRANSAZIONE
    -- =====================================================
    COMMIT TRANSACTION;
    
    PRINT '=====================================================';
    PRINT '✅ AGGIORNAMENTO COMPLETATO CON SUCCESSO!';
    PRINT '✅ UPDATE COMPLETED SUCCESSFULLY!';
    PRINT '=====================================================';
    PRINT '';
    PRINT 'Database aggiornato alla versione V3';
    PRINT 'Database updated to version V3';
    PRINT '';
    PRINT 'Nuove funzionalità disponibili / New features available:';
    PRINT '  - Dashboard Widgets personalizzabili';
    PRINT '  - Saved Searches (ricerche salvate)';
    PRINT '  - User Activities (attività utente)';
    PRINT '  - Enhanced Workflow States (stati workflow avanzati)';
    PRINT '  - Golden Datasets per test qualità RAG';
    PRINT '';
    PRINT 'Verifica esecuzione / Verify execution:';
    PRINT '  sqlcmd -S your_server -d DocN -i Database/CheckVersion.sql';
    PRINT '';

END TRY
BEGIN CATCH
    -- =====================================================
    -- GESTIONE ERRORI
    -- ERROR HANDLING
    -- =====================================================
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    PRINT '';
    PRINT '=====================================================';
    PRINT '❌ ERRORE DURANTE L''AGGIORNAMENTO!';
    PRINT '❌ ERROR DURING UPDATE!';
    PRINT '=====================================================';
    PRINT '';
    PRINT 'Messaggio errore / Error message:';
    PRINT ERROR_MESSAGE();
    PRINT '';
    PRINT 'Riga / Line: ' + CAST(ERROR_LINE() AS VARCHAR);
    PRINT 'Procedura / Procedure: ' + ISNULL(ERROR_PROCEDURE(), 'N/A');
    PRINT '';
    PRINT 'La transazione è stata annullata.';
    PRINT 'The transaction has been rolled back.';
    PRINT '';
    PRINT 'Consulta la documentazione in Database/UPDATE_GUIDE.md';
    PRINT '';
    
    -- Rilancia l'errore / Re-throw error
    THROW;
END CATCH;

SET NOCOUNT OFF;
