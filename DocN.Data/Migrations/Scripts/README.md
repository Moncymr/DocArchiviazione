# SQL Migration Scripts

This directory contains standalone SQL scripts for database migrations that can be run directly in SQL Server Management Studio (SSMS) or via `sqlcmd`.

## Purpose

While Entity Framework Core migrations are the recommended approach, these SQL scripts provide:

1. **Manual Control**: DBAs can review and apply changes manually
2. **Production Environments**: Run scripts during maintenance windows
3. **Troubleshooting**: Easier to debug and modify if needed
4. **CI/CD Integration**: Can be integrated into database deployment pipelines

## Available Scripts

### Vector Index Optimizations

**20260123100000_AddVectorIndexOptimizations.sql**
- Creates 12 optimized indexes for SQL Server 2025 VECTOR operations
- Includes columnstore indexes for compression and performance
- Includes filtered indexes for common query patterns
- **Estimated time**: 5-30 minutes depending on data volume
- **Storage overhead**: +10-20% of table size

**20260123100000_AddVectorIndexOptimizations_Rollback.sql**
- Removes all indexes created by the optimization migration
- Use only if you need to revert changes
- **Warning**: Will degrade vector search performance

## Usage

### Method 1: SQL Server Management Studio (SSMS)

1. Open SQL Server Management Studio
2. Connect to your database server
3. Open the SQL script file
4. **IMPORTANT**: Edit the `USE [DocNDb];` line to match your database name
5. Execute the script (F5 or click Execute)
6. Review the output messages for success confirmation

### Method 2: sqlcmd Command Line

```bash
# Apply the migration
sqlcmd -S localhost -d DocNDb -i 20260123100000_AddVectorIndexOptimizations.sql -o output.log

# Rollback if needed
sqlcmd -S localhost -d DocNDb -i 20260123100000_AddVectorIndexOptimizations_Rollback.sql -o rollback_output.log
```

### Method 3: PowerShell

```powershell
# Apply the migration
Invoke-Sqlcmd -ServerInstance "localhost" -Database "DocNDb" -InputFile "20260123100000_AddVectorIndexOptimizations.sql" -Verbose

# Rollback if needed
Invoke-Sqlcmd -ServerInstance "localhost" -Database "DocNDb" -InputFile "20260123100000_AddVectorIndexOptimizations_Rollback.sql" -Verbose
```

### Method 4: Azure Data Studio

1. Open Azure Data Studio
2. Connect to your database
3. Open the SQL script file
4. Edit the database name if needed
5. Click "Run" or press F5
6. Check the Messages pane for results

## Pre-Execution Checklist

Before running any migration script:

- [ ] **Backup your database** (critical!)
- [ ] Edit the `USE [DatabaseName];` statement to match your database
- [ ] Review the script contents and understand what it does
- [ ] Check that you have sufficient permissions (CREATE INDEX, ALTER)
- [ ] Verify you have sufficient disk space (+10-20% of table size)
- [ ] Plan to run during low-traffic period (index creation is resource intensive)
- [ ] Notify users of potential brief performance impact

## Verification

After running the migration script, verify success:

```sql
-- Check Documents table indexes
SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_disabled AS IsDisabled
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('Documents')
  AND i.name LIKE 'IX_%Vector%'
ORDER BY i.name;

-- Check DocumentChunks table indexes
SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_disabled AS IsDisabled
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('DocumentChunks')
  AND (i.name LIKE 'IX_%Vector%' OR i.name = 'IX_DocumentChunks_EmbeddingDimension')
ORDER BY i.name;
```

Expected results:
- **Documents**: 6 indexes (2 columnstore + 4 filtered)
- **DocumentChunks**: 5 indexes (2 columnstore + 3 filtered)

## Monitoring Performance

After applying the optimization:

```sql
-- Monitor index usage
SELECT 
    OBJECT_NAME(s.object_id) AS TableName,
    i.name AS IndexName,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.last_user_seek,
    s.last_user_scan
FROM sys.dm_db_index_usage_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
WHERE s.database_id = DB_ID()
  AND OBJECT_NAME(s.object_id) IN ('Documents', 'DocumentChunks')
  AND i.name LIKE 'IX_%Vector%'
ORDER BY TableName, IndexName;

-- Check index fragmentation
SELECT 
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent,
    ips.page_count
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE OBJECT_NAME(ips.object_id) IN ('Documents', 'DocumentChunks')
  AND i.name LIKE 'IX_%Vector%'
ORDER BY ips.avg_fragmentation_in_percent DESC;
```

## EF Core Migration Equivalent

These SQL scripts are equivalent to running:

```bash
# Using EF Core migrations (recommended for development)
dotnet ef database update 20260123100000_AddVectorIndexOptimizations --project DocN.Data --startup-project DocN.Server
```

The SQL scripts provide the same functionality but with manual control and better visibility into the exact SQL being executed.

## Troubleshooting

### Error: "Database name not found"
- Edit the `USE [DatabaseName];` statement to match your actual database name

### Error: "Insufficient permissions"
- Ensure your SQL user has `CREATE INDEX` and `ALTER` permissions

### Error: "Index already exists"
- The indexes may have been created by a previous run
- Check existing indexes or run the rollback script first

### Error: "Out of disk space"
- Free up disk space or consider running on smaller batches
- Columnstore indexes require temporary space during creation

### Performance Impact
- Index creation may cause brief locks on tables
- Consider running during maintenance window for large tables
- Monitor tempdb usage during execution

## Support

For more information, see:
- [VECTOR_INDEX_OPTIMIZATION.md](../../VECTOR_INDEX_OPTIMIZATION.md) - Detailed optimization guide
- [Migration file](../20260123100000_AddVectorIndexOptimizations.cs) - EF Core migration source

## Notes

- These scripts are idempotent for index creation (uses `IF NOT EXISTS` checks in rollback)
- The rollback script is safe to run even if indexes don't exist
- Always test in a non-production environment first
- Keep a backup before running any migration
