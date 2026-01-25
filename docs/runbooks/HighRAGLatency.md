# Runbook: High RAG Query Latency

## Alert Details

**Alert Name:** HighRAGLatency  
**Severity:** Warning  
**Threshold:** p95 latency > 2 seconds for 5 minutes  
**Component:** RAG Query Engine

## Symptoms

- Users experiencing slow search responses
- Dashboard showing high latency metrics
- Prometheus alert triggered

## Impact

- **User Experience:** Degraded search performance
- **Business Impact:** Reduced productivity, user frustration
- **System Impact:** Potential resource exhaustion

## Diagnosis Steps

### 1. Verify the Alert

```bash
# Check current latency metrics
curl 'http://localhost:9090/api/v1/query?query=histogram_quantile(0.95, rate(rag_query_duration_seconds_bucket[5m]))'

# View Grafana dashboard
# Navigate to: http://localhost:3000/d/rag-metrics
```

### 2. Check System Resources

```bash
# Check CPU usage
top -b -n 1 | head -20

# Check memory
free -h

# Check disk I/O
iostat -x 1 5

# Check SQL Server resource usage
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SA_PASSWORD} -Q "
SELECT 
    r.session_id,
    r.status,
    r.command,
    r.cpu_time,
    r.total_elapsed_time,
    r.wait_type,
    t.text AS query_text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.session_id > 50
ORDER BY r.cpu_time DESC;
"
```

### 3. Check Database Performance

```sql
-- Check for blocking queries
SELECT 
    blocking_session_id,
    wait_duration_ms,
    session_id,
    wait_type,
    resource_description
FROM sys.dm_os_waiting_tasks
WHERE blocking_session_id IS NOT NULL;

-- Check index fragmentation
SELECT 
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'DETAILED') ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 30
ORDER BY ips.avg_fragmentation_in_percent DESC;

-- Check missing indexes
SELECT 
    migs.avg_total_user_cost * (migs.avg_user_impact / 100.0) AS improvement_measure,
    mid.statement,
    mid.equality_columns,
    mid.inequality_columns,
    mid.included_columns
FROM sys.dm_db_missing_index_groups mig
INNER JOIN sys.dm_db_missing_index_group_stats migs ON migs.group_handle = mig.index_group_handle
INNER JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
ORDER BY improvement_measure DESC;
```

### 4. Check Cache Performance

```bash
# Check Redis status
redis-cli INFO stats | grep hit_rate

# Check cache metrics in Prometheus
curl 'http://localhost:9090/api/v1/query?query=cache_hit_rate'

# View cache size
redis-cli INFO memory
```

### 5. Check Vector Store Performance

```sql
-- Check DocumentChunks table size
SELECT 
    COUNT(*) AS total_chunks,
    AVG(LEN(Embedding)) AS avg_embedding_size,
    COUNT(DISTINCT DocumentId) AS total_documents
FROM DocumentChunks;

-- Check recent slow queries
SELECT TOP 10
    qt.query_sql_text,
    rs.avg_duration / 1000.0 AS avg_duration_ms,
    rs.count_executions,
    rs.last_execution_time
FROM sys.query_store_query q
INNER JOIN sys.query_store_query_text qt ON q.query_text_id = qt.query_text_id
INNER JOIN sys.query_store_plan p ON q.query_id = p.query_id
INNER JOIN sys.query_store_runtime_stats rs ON p.plan_id = rs.plan_id
WHERE qt.query_sql_text LIKE '%DocumentChunks%'
ORDER BY rs.avg_duration DESC;
```

### 6. Check Application Logs

```bash
# View recent error logs
tail -n 100 /var/log/docn/error.log | grep -i "rag\|query\|timeout"

# Check Kibana for errors
# Navigate to: http://localhost:5601
# Query: service:docn AND level:error AND message:*rag*
```

## Resolution Steps

### Quick Fixes (5-10 minutes)

#### 1. Clear Cache (if cache is stale)

```bash
# Clear Redis cache
redis-cli FLUSHDB

# Restart application to clear memory cache
docker restart docn-server
```

**When to use:** Cache corruption suspected, after database updates

#### 2. Restart Services

```bash
# Restart DocN Server
systemctl restart docn-server

# Or with Docker
docker restart docn-server

# Restart SQL Server (last resort)
systemctl restart mssql-server
```

**When to use:** Memory leaks suspected, after configuration changes

#### 3. Scale Resources (if using orchestrator)

```bash
# Scale out (Kubernetes)
kubectl scale deployment docn-server --replicas=5

# Scale out (Docker Swarm)
docker service scale docn-server=5
```

**When to use:** High traffic, resource exhaustion

### Medium-Term Fixes (30-60 minutes)

#### 4. Rebuild Fragmented Indexes

```sql
-- Rebuild indexes on DocumentChunks
ALTER INDEX ALL ON DocumentChunks REBUILD WITH (ONLINE = ON, MAXDOP = 4);

-- Update statistics
UPDATE STATISTICS DocumentChunks WITH FULLSCAN;
```

**When to use:** Index fragmentation > 30%

#### 5. Optimize Query Plans

```sql
-- Clear plan cache (forces recompilation)
DBCC FREEPROCCACHE;

-- Optimize specific procedure
EXEC sp_recompile 'dbo.sp_VectorSimilaritySearch';
```

**When to use:** After index changes, parameter sniffing issues

#### 6. Adjust Configuration

Edit `appsettings.json`:

```json
{
  "EnhancedRAG": {
    "Retrieval": {
      "DefaultTopK": 5,  // Reduce from 10
      "CandidateMultiplier": 1.5  // Reduce from 2
    },
    "Caching": {
      "EnableSemanticCache": true,  // Enable if not already
      "CacheExpirationHours": 4  // Increase cache time
    }
  }
}
```

**When to use:** Consistently high latency, known bottlenecks

### Long-Term Fixes (1-4 hours)

#### 7. Add Missing Indexes

```sql
-- Create covering index (example from missing index report)
CREATE NONCLUSTERED INDEX IX_DocumentChunks_DocumentId_Covering
ON DocumentChunks (DocumentId)
INCLUDE (ChunkIndex, Content, Embedding, EmbeddingModel)
WITH (FILLFACTOR = 90, PAD_INDEX = ON, ONLINE = ON);
```

**When to use:** Missing index reports show high improvement measure

#### 8. Enable Query Store

```sql
-- Enable query store if not already enabled
ALTER DATABASE DocNDb SET QUERY_STORE = ON (
    OPERATION_MODE = READ_WRITE,
    CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30),
    DATA_FLUSH_INTERVAL_SECONDS = 900,
    MAX_STORAGE_SIZE_MB = 1024,
    INTERVAL_LENGTH_MINUTES = 60
);
```

**When to use:** Need better query performance analysis

#### 9. Implement Caching Strategy

Update `DocN.Core/Services/EnhancedRAGService.cs`:

```csharp
public async Task<RAGResponse> QueryAsync(string query)
{
    // Check semantic cache
    var cacheKey = query.ToSearchCacheKey();
    var cached = await _cache.GetAsync<RAGResponse>(cacheKey);
    if (cached != null)
    {
        cached.FromCache = true;
        return cached;
    }

    // Execute query
    var response = await ExecuteQueryInternalAsync(query);

    // Cache result
    await _cache.SetAsync(cacheKey, response, TimeSpan.FromHours(4));

    return response;
}
```

**When to use:** Low cache hit rate, repeated queries

#### 10. Partition Large Tables

```sql
-- Partition DocumentChunks by date (requires table rebuild)
-- See SQLServerVectorOptimization.md for full implementation
```

**When to use:** Table > 10M rows, time-based access patterns

## Escalation

### When to Escalate

- Latency > 5 seconds for 15+ minutes
- Multiple mitigation attempts failed
- System resources at 90%+ utilization
- Database unresponsive

### Escalation Path

1. **Level 1:** On-call DevOps Engineer
2. **Level 2:** Senior Backend Engineer
3. **Level 3:** Database Administrator
4. **Level 4:** Engineering Manager

### Escalation Template

```
Subject: [URGENT] High RAG Query Latency - Production

Issue: RAG query latency p95 > Xs (threshold: 2s)
Duration: Started at YYYY-MM-DD HH:MM UTC
Impact: X users affected, Y% throughput reduction

Mitigation Attempts:
1. [Action taken] - [Result]
2. [Action taken] - [Result]

Current Status:
- CPU: X%
- Memory: Y%
- Database: [Status]
- Cache hit rate: Z%

Metrics:
- Grafana: http://localhost:3000/d/rag-metrics
- Prometheus: [Alert URL]

Need assistance with: [Specific help needed]
```

## Prevention

### Monitoring

```yaml
# Add to prometheus/alerts.yml
- alert: LatencyIncreasing
  expr: rate(rag_query_duration_seconds_sum[5m]) / rate(rag_query_duration_seconds_count[5m]) > 1
  for: 5m
  labels:
    severity: info
  annotations:
    summary: "RAG latency trending upward"
```

### Scheduled Maintenance

```bash
# Weekly index rebuild (run during low-traffic window)
0 2 * * SUN /opt/docn/scripts/rebuild-indexes.sh

# Daily statistics update
0 3 * * * /opt/docn/scripts/update-statistics.sh
```

### Capacity Planning

- Monitor query volume trends
- Plan for 2x capacity headroom
- Set up auto-scaling at 70% resource utilization

## Post-Incident

### Document the Incident

1. Update incident log with details
2. Record mitigation steps taken
3. Note duration and impact
4. Calculate downtime/degradation

### Root Cause Analysis

- What caused the latency spike?
- Why wasn't it caught earlier?
- What prevented faster resolution?

### Follow-up Actions

- Update monitoring thresholds
- Implement preventive measures
- Update runbook based on learnings
- Schedule review meeting

## Related Runbooks

- [Low Cache Hit Rate](./LowCacheHitRate.md)
- [Database Performance Issues](./DatabasePerformance.md)
- [High Memory Usage](./HighMemoryUsage.md)

## References

- [SQL Server Performance Tuning](https://learn.microsoft.com/en-us/sql/relational-databases/performance/performance-monitoring-and-tuning-tools)
- [RAG Performance Best Practices](../SQLServerVectorOptimization.md)
- [Monitoring Setup](../MonitoringSetup.md)

---

**Last Updated:** 2026-01-25  
**Owner:** DevOps Team  
**Review Schedule:** Quarterly
