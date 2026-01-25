-- =====================================================
-- Script di Verifica Versione Database DocN
-- DocN Database Version Check Script
-- =====================================================
-- Questo script verifica quale versione del database è attualmente installata
-- This script checks which version of the database is currently installed
--
-- UTILIZZO / USAGE:
--   sqlcmd -S your_server -d DocN -i CheckVersion.sql
-- =====================================================

SET NOCOUNT ON;

PRINT '==================================================';
PRINT 'DocN Database Version Check';
PRINT 'Data/Date: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '==================================================';
PRINT '';

-- Verifica se la tabella delle migrazioni esiste
-- Check if migrations table exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    PRINT '❌ ERRORE: Tabella __EFMigrationsHistory non trovata!';
    PRINT '❌ ERROR: __EFMigrationsHistory table not found!';
    PRINT '';
    PRINT '   Il database non sembra essere stato inizializzato correttamente.';
    PRINT '   The database does not appear to be properly initialized.';
    PRINT '';
    PRINT '   AZIONE RICHIESTA / ACTION REQUIRED:';
    PRINT '   Esegui / Run: CreateDatabase_Complete_V3.sql';
    PRINT '';
END
ELSE
BEGIN
    PRINT '✓ Database inizializzato / Database initialized';
    PRINT '';
    
    -- Mostra l'ultima migrazione applicata
    -- Show last applied migration
    DECLARE @LastMigration NVARCHAR(150);
    SELECT TOP 1 @LastMigration = MigrationId 
    FROM __EFMigrationsHistory 
    ORDER BY MigrationId DESC;
    
    PRINT 'Ultima migrazione applicata / Last applied migration:';
    PRINT '  ' + ISNULL(@LastMigration, 'Nessuna / None');
    PRINT '';
    
    -- Conta il totale delle migrazioni
    -- Count total migrations
    DECLARE @TotalMigrations INT;
    SELECT @TotalMigrations = COUNT(*) FROM __EFMigrationsHistory;
    
    PRINT 'Totale migrazioni applicate / Total applied migrations: ' + CAST(@TotalMigrations AS VARCHAR);
    PRINT '';
    
    -- Verifica la versione corrente
    -- Check current version
    IF @LastMigration = '20260124115302_AddDashboardAndRBACFeatures'
    BEGIN
        PRINT '✅ DATABASE AGGIORNATO ALLA VERSIONE CORRENTE V3!';
        PRINT '✅ DATABASE IS UP TO DATE WITH VERSION V3!';
        PRINT '';
        PRINT '   Include / Includes:';
        PRINT '   - Dashboard Widgets';
        PRINT '   - Saved Searches';
        PRINT '   - User Activities';
        PRINT '   - RBAC Features';
        PRINT '   - Enhanced Workflow States';
    END
    ELSE IF @LastMigration >= '20260120000000'
    BEGIN
        PRINT '⚠️  DATABASE PARZIALMENTE AGGIORNATO';
        PRINT '⚠️  DATABASE PARTIALLY UPDATED';
        PRINT '';
        PRINT '   AZIONE RICHIESTA / ACTION REQUIRED:';
        PRINT '   Aggiorna alla V3 / Update to V3:';
        PRINT '   - Metodo 1: dotnet ef database update';
        PRINT '   - Metodo 2: Esegui / Run CreateDatabase_Complete_V3.sql';
        PRINT '';
        PRINT '   Consulta / See: Database/UPDATE_GUIDE.md';
    END
    ELSE
    BEGIN
        PRINT '⚠️  DATABASE NON AGGIORNATO';
        PRINT '⚠️  DATABASE NOT UP TO DATE';
        PRINT '';
        PRINT '   Versione rilevata / Detected version: V1 o V2 / V1 or V2';
        PRINT '';
        PRINT '   AZIONE RICHIESTA / ACTION REQUIRED:';
        PRINT '   Aggiorna alla V3 / Update to V3:';
        PRINT '   - Metodo 1: dotnet ef database update';
        PRINT '   - Metodo 2: Esegui / Run CreateDatabase_Complete_V3.sql';
        PRINT '';
        PRINT '   ⚠️  IMPORTANTE: Fai un backup prima! / IMPORTANT: Backup first!';
        PRINT '   Consulta / See: Database/UPDATE_GUIDE.md';
    END
    
    PRINT '';
    PRINT '--------------------------------------------------';
    PRINT 'Verifica tabelle Dashboard / Check Dashboard tables:';
    PRINT '--------------------------------------------------';
    
    -- Verifica presenza nuove tabelle
    -- Check for new tables
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DashboardWidgets')
        PRINT '✓ DashboardWidgets - OK'
    ELSE
        PRINT '❌ DashboardWidgets - MANCANTE / MISSING';
        
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SavedSearches')
        PRINT '✓ SavedSearches - OK'
    ELSE
        PRINT '❌ SavedSearches - MANCANTE / MISSING';
        
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'UserActivities')
        PRINT '✓ UserActivities - OK'
    ELSE
        PRINT '❌ UserActivities - MANCANTE / MISSING';
    
    PRINT '';
    PRINT '--------------------------------------------------';
    PRINT 'Verifica campi workflow / Check workflow fields:';
    PRINT '--------------------------------------------------';
    
    -- Verifica nuovi campi in Documents
    -- Check new fields in Documents
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'WorkflowState')
        PRINT '✓ Documents.WorkflowState - OK'
    ELSE
        PRINT '❌ Documents.WorkflowState - MANCANTE / MISSING';
        
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'ErrorMessage')
        PRINT '✓ Documents.ErrorMessage - OK'
    ELSE
        PRINT '❌ Documents.ErrorMessage - MANCANTE / MISSING';
    
    PRINT '';
    PRINT '--------------------------------------------------';
    PRINT 'Ultimi 5 migrazioni / Last 5 migrations:';
    PRINT '--------------------------------------------------';
    
    SELECT TOP 5 
        MigrationId AS [Migration],
        ProductVersion AS [EF Version]
    FROM __EFMigrationsHistory 
    ORDER BY MigrationId DESC;
END

PRINT '';
PRINT '==================================================';
PRINT 'Fine verifica / End of check';
PRINT '==================================================';

SET NOCOUNT OFF;
