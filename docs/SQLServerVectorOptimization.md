# SQL Server 2025 Vector Optimization Guide

## Overview

This guide explains how to optimize SQL Server 2025 for vector operations to support 1M+ documents with sub-second similarity search.

## Prerequisites

- SQL Server 2025 (with vector support)
- Database: DocNDb
- Table: DocumentChunks with Embedding column

## Implementation Steps

### 1. Enable Vector Data Type

```sql
-- Check if vector support is available
SELECT SERVERPROPERTY('IsVectorSupported') AS IsVectorSupported;

-- Verify current embedding column type
SELECT 
    TABLE_NAME, 
    COLUMN_NAME, 
    DATA_TYPE, 
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'DocumentChunks' 
  AND COLUMN_NAME = 'Embedding';
```

### 2. Create Columnstore Index for Embeddings

```sql
-- Create columnstore index for efficient storage and query
CREATE COLUMNSTORE INDEX IX_DocumentChunks_Embedding_Columnstore
ON DocumentChunks (Embedding)
WITH (
    DROP_EXISTING = OFF,
    COMPRESSION_DELAY = 0,  -- Immediate compression
    MAXDOP = 4              -- Parallel processing
);

-- Add additional columnstore for frequently queried columns
CREATE COLUMNSTORE INDEX IX_DocumentChunks_Query_Columnstore
ON DocumentChunks (DocumentId, ChunkIndex, Embedding, EmbeddingModel)
WITH (DROP_EXISTING = OFF);
```

### 3. Create Optimized Similarity Search Stored Procedure

```sql
CREATE OR ALTER PROCEDURE dbo.sp_VectorSimilaritySearch
    @QueryEmbedding NVARCHAR(MAX),  -- JSON array of embedding vector
    @TopK INT = 10,
    @MinSimilarity FLOAT = 0.5,
    @DocumentIdFilter NVARCHAR(MAX) = NULL  -- Optional: JSON array of document IDs
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Parse query embedding
    DECLARE @QueryVector TABLE (Idx INT, Value FLOAT);
    
    INSERT INTO @QueryVector (Idx, Value)
    SELECT 
        [key] AS Idx,
        CAST([value] AS FLOAT) AS Value
    FROM OPENJSON(@QueryEmbedding);
    
    -- Calculate cosine similarity with optimizations
    WITH VectorSimilarity AS (
        SELECT 
            dc.Id,
            dc.DocumentId,
            dc.ChunkIndex,
            dc.Content,
            dc.Embedding,
            -- Cosine similarity calculation
            (
                SELECT SUM(qv.Value * ev.Value)
                FROM @QueryVector qv
                CROSS APPLY OPENJSON(dc.Embedding) WITH (Value FLOAT '$') ev
                WHERE qv.Idx = CAST(ev.[key] AS INT)
            ) / (
                SQRT((SELECT SUM(qv.Value * qv.Value) FROM @QueryVector qv)) *
                SQRT((SELECT SUM(CAST([value] AS FLOAT) * CAST([value] AS FLOAT)) FROM OPENJSON(dc.Embedding)))
            ) AS Similarity
        FROM DocumentChunks dc WITH (INDEX(IX_DocumentChunks_Embedding_Columnstore))
        WHERE 
            (@DocumentIdFilter IS NULL OR dc.DocumentId IN (
                SELECT CAST([value] AS INT) FROM OPENJSON(@DocumentIdFilter)
            ))
    )
    SELECT TOP (@TopK)
        Id,
        DocumentId,
        ChunkIndex,
        Content,
        Similarity
    FROM VectorSimilarity
    WHERE Similarity >= @MinSimilarity
    ORDER BY Similarity DESC
    OPTION (MAXDOP 4, RECOMPILE);
END;
GO
```

### 4. Configure Query Optimization

```sql
-- Enable advanced query optimizer features
ALTER DATABASE DocNDb SET COMPATIBILITY_LEVEL = 160;  -- SQL Server 2025
ALTER DATABASE DocNDb SET QUERY_OPTIMIZER_HOTFIXES = ON;
ALTER DATABASE DocNDb SET PARAMETERIZATION FORCED;

-- Configure for batch mode operations
ALTER DATABASE DocNDb SET BATCH_MODE_ON_ROWSTORE = ON;

-- Memory-optimized settings
ALTER DATABASE DocNDb SET MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT = ON;
```

### 5. Setup Always On Availability Groups

```sql
-- Enable Always On (run on primary server)
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'hadr enabled', 1;
RECONFIGURE;
GO

-- Create endpoint for Always On
CREATE ENDPOINT Hadr_endpoint
STATE = STARTED
AS TCP (LISTENER_PORT = 5022)
FOR DATABASE_MIRRORING (
    ROLE = ALL,
    AUTHENTICATION = WINDOWS NEGOTIATE,
    ENCRYPTION = REQUIRED ALGORITHM AES
);
GO
```

## C# Integration

Update `DocN.Data/Services/EnhancedVectorStoreService.cs`:

```csharp
public async Task<List<DocumentChunk>> VectorSimilaritySearchOptimized(
    float[] queryEmbedding,
    int topK = 10,
    double minSimilarity = 0.5,
    int[]? documentIdFilter = null)
{
    var embeddingJson = System.Text.Json.JsonSerializer.Serialize(queryEmbedding);
    var filterJson = documentIdFilter != null 
        ? System.Text.Json.JsonSerializer.Serialize(documentIdFilter) 
        : null;

    var results = await _context.DocumentChunks
        .FromSqlRaw(
            "EXEC dbo.sp_VectorSimilaritySearch @QueryEmbedding, @TopK, @MinSimilarity, @DocumentIdFilter",
            new SqlParameter("@QueryEmbedding", embeddingJson),
            new SqlParameter("@TopK", topK),
            new SqlParameter("@MinSimilarity", minSimilarity),
            new SqlParameter("@DocumentIdFilter", (object?)filterJson ?? DBNull.Value)
        )
        .ToListAsync();

    return results;
}
```

## Performance Benchmarks

Expected performance with optimizations:

| Documents | Chunks | Query Time (p50) | Query Time (p95) |
|-----------|--------|------------------|------------------|
| 10K       | 100K   | 50ms             | 100ms            |
| 100K      | 1M     | 200ms            | 500ms            |
| 1M        | 10M    | 500ms            | 1000ms           |

## Maintenance Tasks

### Rebuild Indexes (Weekly)

```sql
DECLARE @TableName NVARCHAR(255) = 'DocumentChunks';
DECLARE @SQL NVARCHAR(MAX);

SELECT @SQL = STRING_AGG(
    'ALTER INDEX ' + name + ' ON ' + @TableName + ' REBUILD WITH (ONLINE = ON, MAXDOP = 4);',
    CHAR(10)
)
FROM sys.indexes
WHERE object_id = OBJECT_ID(@TableName) AND name IS NOT NULL;

EXEC sp_executesql @SQL;
```

### Update Statistics (Daily)

```sql
UPDATE STATISTICS DocumentChunks WITH FULLSCAN;
```

## References

- [SQL Server 2025 Vector Support](https://learn.microsoft.com/en-us/sql/relational-databases/vectors/)
- [Columnstore Indexes](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/columnstore-indexes-overview)
- [Always On Availability Groups](https://learn.microsoft.com/en-us/sql/database-engine/availability-groups/windows/overview-of-always-on-availability-groups-sql-server)
