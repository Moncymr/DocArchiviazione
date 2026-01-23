-- ============================================================================
-- SQL Server Vector Index Optimizations - ROLLBACK
-- Migration: 20260123100000_AddVectorIndexOptimizations
-- ============================================================================
-- 
-- This script removes the vector optimization indexes created by the
-- 20260123100000_AddVectorIndexOptimizations migration.
--
-- IMPORTANT: 
-- - Run during low-traffic period
-- - Removing indexes will degrade vector search performance
-- - Only run if you need to revert the optimization changes
--
-- ============================================================================

USE [DocNDb]; -- Change to your database name
GO

SET NOCOUNT ON;
GO

PRINT '========================================================================';
PRINT 'SQL Server Vector Index Optimizations - ROLLBACK';
PRINT 'Started at: ' + CONVERT(VARCHAR(30), GETDATE(), 121);
PRINT '========================================================================';
GO

-- ============================================================================
-- DOCUMENT CHUNKS TABLE - DROP INDEXES
-- ============================================================================

PRINT '';
PRINT 'Dropping indexes from DocumentChunks table...';
GO

-- Drop embedding dimension index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'IX_DocumentChunks_EmbeddingDimension')
BEGIN
    PRINT '  Dropping IX_DocumentChunks_EmbeddingDimension...';
    DROP INDEX IX_DocumentChunks_EmbeddingDimension ON DocumentChunks;
END
GO

-- Drop document-filtered 1536-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'IX_DocumentChunks_DocumentId_Vector1536')
BEGIN
    PRINT '  Dropping IX_DocumentChunks_DocumentId_Vector1536...';
    DROP INDEX IX_DocumentChunks_DocumentId_Vector1536 ON DocumentChunks;
END
GO

-- Drop document-filtered 768-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'IX_DocumentChunks_DocumentId_Vector768')
BEGIN
    PRINT '  Dropping IX_DocumentChunks_DocumentId_Vector768...';
    DROP INDEX IX_DocumentChunks_DocumentId_Vector768 ON DocumentChunks;
END
GO

-- Drop columnstore 1536-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'IX_DocumentChunks_VectorColumnstore1536')
BEGIN
    PRINT '  Dropping IX_DocumentChunks_VectorColumnstore1536...';
    DROP INDEX IX_DocumentChunks_VectorColumnstore1536 ON DocumentChunks;
END
GO

-- Drop columnstore 768-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'IX_DocumentChunks_VectorColumnstore768')
BEGIN
    PRINT '  Dropping IX_DocumentChunks_VectorColumnstore768...';
    DROP INDEX IX_DocumentChunks_VectorColumnstore768 ON DocumentChunks;
END
GO

PRINT '  ✓ DocumentChunks table indexes dropped successfully';
GO

-- ============================================================================
-- DOCUMENTS TABLE - DROP INDEXES
-- ============================================================================

PRINT '';
PRINT 'Dropping indexes from Documents table...';
GO

-- Drop tenant-filtered 1536-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Documents') AND name = 'IX_Documents_TenantId_Vector1536')
BEGIN
    PRINT '  Dropping IX_Documents_TenantId_Vector1536...';
    DROP INDEX IX_Documents_TenantId_Vector1536 ON Documents;
END
GO

-- Drop tenant-filtered 768-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Documents') AND name = 'IX_Documents_TenantId_Vector768')
BEGIN
    PRINT '  Dropping IX_Documents_TenantId_Vector768...';
    DROP INDEX IX_Documents_TenantId_Vector768 ON Documents;
END
GO

-- Drop owner-filtered 1536-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Documents') AND name = 'IX_Documents_OwnerId_Vector1536')
BEGIN
    PRINT '  Dropping IX_Documents_OwnerId_Vector1536...';
    DROP INDEX IX_Documents_OwnerId_Vector1536 ON Documents;
END
GO

-- Drop owner-filtered 768-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Documents') AND name = 'IX_Documents_OwnerId_Vector768')
BEGIN
    PRINT '  Dropping IX_Documents_OwnerId_Vector768...';
    DROP INDEX IX_Documents_OwnerId_Vector768 ON Documents;
END
GO

-- Drop columnstore 1536-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Documents') AND name = 'IX_Documents_VectorColumnstore1536')
BEGIN
    PRINT '  Dropping IX_Documents_VectorColumnstore1536...';
    DROP INDEX IX_Documents_VectorColumnstore1536 ON Documents;
END
GO

-- Drop columnstore 768-dim index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Documents') AND name = 'IX_Documents_VectorColumnstore768')
BEGIN
    PRINT '  Dropping IX_Documents_VectorColumnstore768...';
    DROP INDEX IX_Documents_VectorColumnstore768 ON Documents;
END
GO

PRINT '  ✓ Documents table indexes dropped successfully';
GO

-- ============================================================================
-- VERIFICATION
-- ============================================================================

PRINT '';
PRINT 'Verifying index removal...';
GO

-- Count remaining Documents indexes
DECLARE @DocsIndexCount INT;
SELECT @DocsIndexCount = COUNT(*)
FROM sys.indexes
WHERE object_id = OBJECT_ID('Documents')
  AND name LIKE 'IX_%Vector%';

PRINT '  Documents table: ' + CAST(@DocsIndexCount AS VARCHAR(10)) + ' vector indexes remaining (expected: 0)';

-- Count remaining DocumentChunks indexes
DECLARE @ChunksIndexCount INT;
SELECT @ChunksIndexCount = COUNT(*)
FROM sys.indexes
WHERE object_id = OBJECT_ID('DocumentChunks')
  AND name LIKE 'IX_%Vector%'
   OR (object_id = OBJECT_ID('DocumentChunks') AND name = 'IX_DocumentChunks_EmbeddingDimension');

PRINT '  DocumentChunks table: ' + CAST(@ChunksIndexCount AS VARCHAR(10)) + ' vector indexes remaining (expected: 0)';
GO

-- ============================================================================
-- UPDATE STATISTICS
-- ============================================================================

PRINT '';
PRINT 'Updating statistics...';
GO

UPDATE STATISTICS Documents WITH FULLSCAN;
UPDATE STATISTICS DocumentChunks WITH FULLSCAN;
GO

PRINT '  ✓ Statistics updated';
GO

-- ============================================================================
-- COMPLETION
-- ============================================================================

PRINT '';
PRINT '========================================================================';
PRINT 'Vector Index Optimization rollback completed successfully!';
PRINT 'Completed at: ' + CONVERT(VARCHAR(30), GETDATE(), 121);
PRINT '========================================================================';
PRINT '';
PRINT 'WARNING: Vector search performance will be degraded until indexes are recreated.';
PRINT '';
GO
