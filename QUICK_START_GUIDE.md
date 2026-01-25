# Enterprise Implementation Quick Start Guide

## Overview

This guide provides step-by-step instructions to begin implementing the enterprise features outlined in `ENTERPRISE_ROADMAP.md`.

## Before You Start

### Prerequisites

- [x] Review `ENTERPRISE_ROADMAP.md`
- [x] Review `WHATS_MISSING.md`
- [ ] Stakeholder approval for roadmap
- [ ] Team capacity confirmed (3-4 developers)
- [ ] Budget approved (~$205K-325K)
- [ ] Infrastructure provisioned (Azure/AWS)

### Tools Required

- Visual Studio 2022 or VS Code
- SQL Server 2025 (or 2022 with vector support)
- Docker Desktop
- .NET 8 SDK
- Git

## Phase 1: Foundation (Weeks 1-4)

### Week 1: SQL Server Optimization

#### Day 1-2: Backup and Baseline

```bash
# 1. Backup current database
sqlcmd -S localhost -U sa -Q "BACKUP DATABASE DocNDb TO DISK='/var/opt/mssql/backup/DocNDb_Pre_Optimization.bak'"

# 2. Measure baseline performance
sqlcmd -S localhost -U sa -d DocNDb -Q "
SELECT 
    COUNT(*) as TotalChunks,
    COUNT(DISTINCT DocumentId) as TotalDocuments
FROM DocumentChunks;
"

# 3. Run baseline query test
# Time this query and record the result
sqlcmd -S localhost -U sa -d DocNDb -Q "
SELECT TOP 10 Id, DocumentId, Content 
FROM DocumentChunks 
ORDER BY Id DESC;
" -o baseline_results.txt
```

#### Day 3-4: Implement Vector Indexes

```bash
# Apply SQL Server optimizations
cd /home/runner/work/DocArchiviazione/DocArchiviazione
sqlcmd -S localhost -U sa -d DocNDb -i docs/SQLServerVectorOptimization.md

# Or step by step:
sqlcmd -S localhost -U sa -d DocNDb -Q "
-- Create columnstore index
CREATE COLUMNSTORE INDEX IX_DocumentChunks_Embedding_Columnstore
ON DocumentChunks (Embedding)
WITH (DROP_EXISTING = OFF, COMPRESSION_DELAY = 0, MAXDOP = 4);

-- Create covering index
CREATE NONCLUSTERED INDEX IX_DocumentChunks_DocumentId_Covering
ON DocumentChunks (DocumentId)
INCLUDE (ChunkIndex, Content, Embedding, EmbeddingModel)
WITH (FILLFACTOR = 90, PAD_INDEX = ON);
"

# Test performance improvement
# This should be faster than baseline
sqlcmd -S localhost -U sa -d DocNDb -Q "
SELECT TOP 10 Id, DocumentId, Content 
FROM DocumentChunks 
ORDER BY Id DESC;
" -o optimized_results.txt
```

#### Day 5: Verify and Document

```bash
# Check index usage
sqlcmd -S localhost -U sa -d DocNDb -Q "
SELECT 
    OBJECT_NAME(s.object_id) AS TableName,
    i.name AS IndexName,
    s.user_seeks + s.user_scans + s.user_lookups AS TotalReads
FROM sys.dm_db_index_usage_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
WHERE OBJECT_NAME(s.object_id) = 'DocumentChunks'
ORDER BY TotalReads DESC;
"

# Document results
echo "Baseline: [TIME]ms" > performance_report.txt
echo "Optimized: [TIME]ms" >> performance_report.txt
echo "Improvement: [%]" >> performance_report.txt
```

### Week 2: Monitoring Setup

#### Day 1: Install Monitoring Stack

```bash
# 1. Create monitoring directory
mkdir -p monitoring/{prometheus,grafana/dashboards,grafana/datasources,logstash}

# 2. Copy config files from docs
cp docs/MonitoringSetup.md monitoring/SETUP.md

# 3. Create docker-compose file (see MonitoringSetup.md)
cat > docker-compose.monitoring.yml << 'EOF'
# Copy content from docs/MonitoringSetup.md
EOF

# 4. Start monitoring stack
docker-compose -f docker-compose.monitoring.yml up -d

# 5. Verify services
docker-compose -f docker-compose.monitoring.yml ps
```

#### Day 2-3: Configure Prometheus & Grafana

```bash
# 1. Install Prometheus packages in DocN.Server
cd DocN.Server
dotnet add package prometheus-net.AspNetCore
dotnet add package prometheus-net.AspNetCore.HealthChecks

# 2. Update Program.cs
cat >> Program.cs << 'EOF'
using Prometheus;

// After app.UseRouting()
app.UseHttpMetrics();
app.MapHealthChecks("/health");
app.MapMetrics();
EOF

# 3. Test metrics endpoint
curl http://localhost:5210/metrics

# 4. Access Grafana
# http://localhost:3000 (admin/admin)
# Add Prometheus datasource
# Import dashboard from monitoring/grafana/dashboards/
```

#### Day 4-5: Create Custom Dashboards

```bash
# 1. Create RAG metrics file
cat > DocN.Server/Metrics/RAGMetrics.cs << 'EOF'
// Copy from docs/MonitoringSetup.md
EOF

# 2. Build and run
dotnet build
dotnet run --project DocN.Server

# 3. Generate some traffic
curl http://localhost:5210/api/search?q=test

# 4. Verify metrics in Grafana
# Dashboard should show query latency, cache hits, etc.
```

### Week 3: SSO Integration

#### Day 1-2: Azure AD Setup

```bash
# 1. Install packages
cd DocN.Server
dotnet add package Microsoft.Identity.Web
dotnet add package Microsoft.Identity.Web.UI

# 2. Configure Azure AD app (see docs/SSOConfiguration.md)
# - Register app in Azure Portal
# - Note Client ID and Tenant ID
# - Create client secret

# 3. Update appsettings.json
cat >> appsettings.json << 'EOF'
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-domain.com",
    "TenantId": "YOUR-TENANT-ID",
    "ClientId": "YOUR-CLIENT-ID",
    "ClientSecret": "YOUR-CLIENT-SECRET"
  }
}
EOF
```

#### Day 3-4: Implement SSO

```bash
# 1. Update Program.cs
# Add authentication (see docs/SSOConfiguration.md for code)

# 2. Create role mapping service
cat > DocN.Server/Services/AzureADRoleMappingService.cs << 'EOF'
// Copy from docs/SSOConfiguration.md
EOF

# 3. Test SSO locally
dotnet run --project DocN.Server
# Navigate to https://localhost:5211
# Should redirect to Azure AD login
```

#### Day 5: Verify and Document

```bash
# Test claims endpoint
curl https://localhost:5211/debug/claims \
  -H "Authorization: Bearer YOUR_TOKEN"

# Document SSO configuration in README
```

### Week 4: RabbitMQ Integration

#### Day 1: Setup RabbitMQ

```bash
# 1. Start RabbitMQ
docker run -d \
  --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=docn \
  -e RABBITMQ_DEFAULT_PASS=your_password \
  rabbitmq:3.12-management

# 2. Verify
docker logs rabbitmq
# Access: http://localhost:15672 (docn/your_password)
```

#### Day 2-3: Implement Message Queue

```bash
# 1. Install package
cd DocN.Core
dotnet add package RabbitMQ.Client

# 2. Create message models
mkdir -p Messages
cat > Messages/DocumentUploadMessage.cs << 'EOF'
// Copy from docs/RabbitMQIntegration.md
EOF

# 3. Create RabbitMQ service
cat > Services/RabbitMQService.cs << 'EOF'
// Copy from docs/RabbitMQIntegration.md
EOF

# 4. Register service in Program.cs
# builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();
```

#### Day 4-5: Create Worker Service

```bash
# 1. Create worker
cat > DocN.Server/Workers/DocumentProcessingWorker.cs << 'EOF'
// Copy from docs/RabbitMQIntegration.md
EOF

# 2. Register worker
# builder.Services.AddHostedService<DocumentProcessingWorker>();

# 3. Test message publishing
curl -X POST http://localhost:5210/api/documents/upload \
  -F "file=@test.pdf"

# 4. Verify in RabbitMQ console
# Check queue: document.upload.queue has messages
```

## Phase 2: Enhancements (Weeks 5-8)

### Week 5: Feedback System

```bash
# 1. Create database migration
dotnet ef migrations add AddFeedbackSystem --project DocN.Data

# 2. Apply migration
dotnet ef database update --project DocN.Data

# 3. Create feedback component
cat > DocN.Client/Components/Shared/FeedbackWidget.razor << 'EOF'
@* Thumbs up/down component *@
<div class="feedback-widget">
    <button @onclick="() => SubmitFeedback(true)">👍</button>
    <button @onclick="() => SubmitFeedback(false)">👎</button>
</div>
EOF
```

### Week 6: Retrieval Visualization

```bash
# 1. Install D3.js
cd DocN.Client/wwwroot
npm install d3

# 2. Create document graph component
cat > ../Components/Visualization/DocumentGraph.razor << 'EOF'
@* Document graph visualization *@
<div id="document-graph"></div>
<script src="js/document-graph.js"></script>
EOF
```

### Week 7: Collaboration Features

```bash
# 1. Create workspace models
cat > DocN.Data/Models/Workspace.cs << 'EOF'
// Copy from WHATS_MISSING.md
EOF

# 2. Create migration
dotnet ef migrations add AddWorkspaces --project DocN.Data
dotnet ef database update --project DocN.Data

# 3. Create workspace service
# See WHATS_MISSING.md for implementation
```

### Week 8: Testing & Documentation

```bash
# 1. Run full test suite
dotnet test

# 2. Performance testing
# - Load test with 1000 concurrent users
# - Verify latency < 1s p95
# - Check cache hit rate > 60%

# 3. Update documentation
# - Create user guide
# - Update API documentation
# - Record demo videos
```

## Validation Checklist

### FASE 1 Success Criteria

- [ ] SQL Server: Query latency p95 < 1s
- [ ] Monitoring: Grafana dashboards accessible
- [ ] SSO: Users can login with Azure AD
- [ ] RabbitMQ: Async processing working
- [ ] Cache: Hit rate > 60%
- [ ] Alerts: Configured and tested

### FASE 2 Success Criteria

- [ ] Feedback: 500+ feedbacks collected
- [ ] Visualization: Document graph rendered
- [ ] Collaboration: Comments working
- [ ] Workspace: Team spaces created
- [ ] Accessibility: WCAG 2.1 AA compliant
- [ ] User Satisfaction: > 4.0/5.0

## Common Issues & Solutions

### Issue: SQL Server indexes not being used

```sql
-- Force index usage
SELECT * FROM DocumentChunks WITH (INDEX(IX_DocumentChunks_Embedding_Columnstore))
WHERE DocumentId = 123;

-- Update statistics
UPDATE STATISTICS DocumentChunks WITH FULLSCAN;
```

### Issue: RabbitMQ connection failed

```bash
# Check connection
docker exec rabbitmq rabbitmq-diagnostics ping

# Restart RabbitMQ
docker restart rabbitmq

# Check logs
docker logs rabbitmq
```

### Issue: Metrics not appearing in Prometheus

```bash
# Check if app is exposing metrics
curl http://localhost:5210/metrics | grep rag_query

# Check Prometheus config
docker exec prometheus cat /etc/prometheus/prometheus.yml

# Restart Prometheus
docker restart prometheus
```

## Daily Standup Template

**What did I complete yesterday?**
- [Task 1]
- [Task 2]

**What am I working on today?**
- [Task 3]
- [Task 4]

**Any blockers?**
- [Blocker 1]
- [Blocker 2]

**Metrics:**
- Lines of code: X
- Tests added: Y
- Documentation pages: Z

## Weekly Review Template

**Week X Summary:**

**Completed:**
- ✅ Feature 1
- ✅ Feature 2

**In Progress:**
- 🔄 Feature 3 (50% complete)

**Blocked:**
- ❌ Feature 4 (waiting for Azure AD approval)

**Metrics:**
- Velocity: X story points
- Bug count: Y
- Test coverage: Z%

**Next Week Focus:**
- Priority 1
- Priority 2

## Resources

- **Roadmap:** `ENTERPRISE_ROADMAP.md`
- **Gap Analysis:** `WHATS_MISSING.md`
- **SQL Optimization:** `docs/SQLServerVectorOptimization.md`
- **SSO Setup:** `docs/SSOConfiguration.md`
- **RabbitMQ:** `docs/RabbitMQIntegration.md`
- **Monitoring:** `docs/MonitoringSetup.md`
- **Runbooks:** `docs/runbooks/`

## Support

**Questions?**
- Slack: #docn-enterprise-dev
- Email: docn-dev@your-company.com
- Wiki: https://wiki.your-company.com/docn

**Office Hours:**
- Monday 2-3 PM: Architecture review
- Wednesday 10-11 AM: Q&A session
- Friday 4-5 PM: Demo & retrospective

---

**Version:** 1.0  
**Last Updated:** 2026-01-25  
**Owner:** Engineering Team
