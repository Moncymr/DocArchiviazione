-- ============================================================================
-- SQL Server Vector Index Optimizations for Massive Scale
-- Migration: 20260123100000_AddVectorIndexOptimizations
-- ============================================================================
-- 
-- This script creates optimized indexes for SQL Server 2025 VECTOR operations
-- on the Documents and DocumentChunks tables to improve performance at scale.
--
-- IMPORTANT: 
-- - Requires SQL Server 2025 or later (VECTOR type support)
-- - Run during low-traffic period (index creation can be resource intensive)
-- - Estimated time: 5-30 minutes depending on data volume
-- - Storage overhead: +10-20% of table size
--
-- ============================================================================

USE [DocNDb]; -- Change to your database name
GO

SET NOCOUNT ON;
GO

PRINT '========================================================================';
PRINT 'SQL Server Vector Index Optimizations';
PRINT 'Started at: ' + CONVERT(VARCHAR(30), GETDATE(), 121);
PRINT '========================================================================';
GO

-- ============================================================================
-- DOCUMENTS TABLE VECTOR OPTIMIZATIONS
-- ============================================================================

PRINT '';
PRINT 'Creating indexes on Documents table...';
GO

-- Columnstore index for Document vectors with 768 dimensions
-- Provides optimal compression (5-10x) and fast analytical scans
PRINT '  Creating IX_Documents_VectorColumnstore768...';
CREATE NONCLUSTERED COLUMNSTORE INDEX IX_Documents_VectorColumnstore768
ON Documents (EmbeddingVector768, Id, FileName, ActualCategory, OwnerId, TenantId)
WHERE EmbeddingVector768 IS NOT NULL;
GO

-- Columnstore index for Document vectors with 1536 dimensions
PRINT '  Creating IX_Documents_VectorColumnstore1536...';
CREATE NONCLUSTERED COLUMNSTORE INDEX IX_Documents_VectorColumnstore1536
ON Documents (EmbeddingVector1536, Id, FileName, ActualCategory, OwnerId, TenantId)
WHERE EmbeddingVector1536 IS NOT NULL;
GO

-- Composite filtered index for owner-based 768-dim vector searches
PRINT '  Creating IX_Documents_OwnerId_Vector768...';
CREATE NONCLUSTERED INDEX IX_Documents_OwnerId_Vector768
ON Documents (OwnerId, Id)
WHERE [OwnerId] IS NOT NULL AND [EmbeddingVector768] IS NOT NULL;
GO

-- Composite filtered index for owner-based 1536-dim vector searches
PRINT '  Creating IX_Documents_OwnerId_Vector1536...';
CREATE NONCLUSTERED INDEX IX_Documents_OwnerId_Vector1536
ON Documents (OwnerId, Id)
WHERE [OwnerId] IS NOT NULL AND [EmbeddingVector1536] IS NOT NULL;
GO

-- Composite filtered index for tenant-based 768-dim vector searches
PRINT '  Creating IX_Documents_TenantId_Vector768...';
CREATE NONCLUSTERED INDEX IX_Documents_TenantId_Vector768
ON Documents (TenantId, Id)
WHERE [TenantId] IS NOT NULL AND [EmbeddingVector768] IS NOT NULL;
GO

-- Composite filtered index for tenant-based 1536-dim vector searches
PRINT '  Creating IX_Documents_TenantId_Vector1536...';
CREATE NONCLUSTERED INDEX IX_Documents_TenantId_Vector1536
ON Documents (TenantId, Id)
WHERE [TenantId] IS NOT NULL AND [EmbeddingVector1536] IS NOT NULL;
GO

PRINT '  ✓ Documents table indexes created successfully';
GO

-- ============================================================================
-- DOCUMENT CHUNKS TABLE VECTOR OPTIMIZATIONS
-- ============================================================================

PRINT '';
PRINT 'Creating indexes on DocumentChunks table...';
GO

-- Columnstore index for DocumentChunk vectors with 768 dimensions
-- Critical for chunk-level search performance at scale
PRINT '  Creating IX_DocumentChunks_VectorColumnstore768...';
CREATE NONCLUSTERED COLUMNSTORE INDEX IX_DocumentChunks_VectorColumnstore768
ON DocumentChunks (ChunkEmbedding768, Id, DocumentId, ChunkIndex, ChunkText)
WHERE ChunkEmbedding768 IS NOT NULL;
GO

-- Columnstore index for DocumentChunk vectors with 1536 dimensions
PRINT '  Creating IX_DocumentChunks_VectorColumnstore1536...';
CREATE NONCLUSTERED COLUMNSTORE INDEX IX_DocumentChunks_VectorColumnstore1536
ON DocumentChunks (ChunkEmbedding1536, Id, DocumentId, ChunkIndex, ChunkText)
WHERE ChunkEmbedding1536 IS NOT NULL;
GO

-- Composite filtered index for document-filtered 768-dim chunk searches
PRINT '  Creating IX_DocumentChunks_DocumentId_Vector768...';
CREATE NONCLUSTERED INDEX IX_DocumentChunks_DocumentId_Vector768
ON DocumentChunks (DocumentId, Id, ChunkIndex)
WHERE [ChunkEmbedding768] IS NOT NULL;
GO

-- Composite filtered index for document-filtered 1536-dim chunk searches
PRINT '  Creating IX_DocumentChunks_DocumentId_Vector1536...';
CREATE NONCLUSTERED INDEX IX_DocumentChunks_DocumentId_Vector1536
ON DocumentChunks (DocumentId, Id, ChunkIndex)
WHERE [ChunkEmbedding1536] IS NOT NULL;
GO

-- Index to optimize finding chunks with embeddings (for batch processing)
PRINT '  Creating IX_DocumentChunks_EmbeddingDimension...';
CREATE NONCLUSTERED INDEX IX_DocumentChunks_EmbeddingDimension
ON DocumentChunks (EmbeddingDimension, DocumentId, ChunkIndex)
WHERE [EmbeddingDimension] IS NOT NULL;
GO

PRINT '  ✓ DocumentChunks table indexes created successfully';
GO

-- ============================================================================
-- VERIFICATION
-- ============================================================================

PRINT '';
PRINT 'Verifying index creation...';
GO

-- Count Documents indexes
DECLARE @DocsIndexCount INT;
SELECT @DocsIndexCount = COUNT(*)
FROM sys.indexes
WHERE object_id = OBJECT_ID('Documents')
  AND name LIKE 'IX_%Vector%';

PRINT '  Documents table: ' + CAST(@DocsIndexCount AS VARCHAR(10)) + ' vector indexes (expected: 6)';

-- Count DocumentChunks indexes
DECLARE @ChunksIndexCount INT;
SELECT @ChunksIndexCount = COUNT(*)
FROM sys.indexes
WHERE object_id = OBJECT_ID('DocumentChunks')
  AND name LIKE 'IX_%Vector%'
   OR (object_id = OBJECT_ID('DocumentChunks') AND name = 'IX_DocumentChunks_EmbeddingDimension');

PRINT '  DocumentChunks table: ' + CAST(@ChunksIndexCount AS VARCHAR(10)) + ' vector indexes (expected: 5)';
GO

-- ============================================================================
-- UPDATE STATISTICS
-- ============================================================================

PRINT '';
PRINT 'Updating statistics for optimal query plans...';
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
PRINT 'Vector Index Optimization completed successfully!';
PRINT 'Completed at: ' + CONVERT(VARCHAR(30), GETDATE(), 121);
PRINT '========================================================================';
PRINT '';
PRINT 'Next steps:';
PRINT '  1. Monitor query performance improvements';
PRINT '  2. Check index usage with sys.dm_db_index_usage_stats';
PRINT '  3. Schedule periodic index maintenance (see VECTOR_INDEX_OPTIMIZATION.md)';
PRINT '';
GO
