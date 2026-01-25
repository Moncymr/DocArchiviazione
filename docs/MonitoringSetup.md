# Monitoring Setup Guide - Grafana & Prometheus

## Overview

This guide sets up comprehensive monitoring for DocN using Grafana, Prometheus, and ELK stack.

## Architecture

```
┌──────────────┐    ┌─────────────┐    ┌──────────┐
│   DocN App   │───>│ Prometheus  │───>│ Grafana  │
│  (Metrics)   │    │  (Storage)  │    │   (UI)   │
└──────────────┘    └─────────────┘    └──────────┘
       │
       │ (Logs)
       ↓
┌──────────────┐    ┌─────────────┐    ┌──────────┐
│  Filebeat    │───>│ Logstash    │───>│  Kibana  │
└──────────────┘    └─────────────┘    └──────────┘
                           │
                           ↓
                    ┌─────────────┐
                    │Elasticsearch│
                    └─────────────┘
```

## Docker Compose Setup

Create `docker-compose.monitoring.yml`:

```yaml
version: '3.8'

services:
  prometheus:
    image: prom/prometheus:latest
    container_name: prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./monitoring/prometheus:/etc/prometheus
      - prometheus_data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=30d'
    restart: unless-stopped

  grafana:
    image: grafana/grafana:latest
    container_name: grafana
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    volumes:
      - grafana_data:/var/lib/grafana
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./monitoring/grafana/datasources:/etc/grafana/provisioning/datasources
    restart: unless-stopped
    depends_on:
      - prometheus

  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
    container_name: elasticsearch
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
      - "ES_JAVA_OPTS=-Xms512m -Xmx512m"
    ports:
      - "9200:9200"
    volumes:
      - elasticsearch_data:/usr/share/elasticsearch/data
    restart: unless-stopped

  logstash:
    image: docker.elastic.co/logstash/logstash:8.11.0
    container_name: logstash
    volumes:
      - ./monitoring/logstash:/usr/share/logstash/pipeline
    ports:
      - "5044:5044"
    environment:
      - "LS_JAVA_OPTS=-Xms256m -Xmx256m"
    depends_on:
      - elasticsearch
    restart: unless-stopped

  kibana:
    image: docker.elastic.co/kibana/kibana:8.11.0
    container_name: kibana
    ports:
      - "5601:5601"
    environment:
      - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
    depends_on:
      - elasticsearch
    restart: unless-stopped

volumes:
  prometheus_data:
  grafana_data:
  elasticsearch_data:
```

## Prometheus Configuration

Create `monitoring/prometheus/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  # DocN Application Metrics
  - job_name: 'docn-server'
    static_configs:
      - targets: ['host.docker.internal:5210']
    metrics_path: '/metrics'

  # RabbitMQ Metrics
  - job_name: 'rabbitmq'
    static_configs:
      - targets: ['host.docker.internal:15692']

  # SQL Server Metrics (requires exporter)
  - job_name: 'sqlserver'
    static_configs:
      - targets: ['host.docker.internal:4000']

  # Redis Metrics (requires exporter)
  - job_name: 'redis'
    static_configs:
      - targets: ['host.docker.internal:9121']

  # Node Exporter (system metrics)
  - job_name: 'node'
    static_configs:
      - targets: ['host.docker.internal:9100']
```

## Add Prometheus to DocN

### 1. Install NuGet Packages

```bash
cd DocN.Server
dotnet add package prometheus-net.AspNetCore
dotnet add package prometheus-net.AspNetCore.HealthChecks
```

### 2. Update Program.cs

```csharp
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    .AddRedis(builder.Configuration.GetConnectionString("Redis"));

var app = builder.Build();

// Enable metrics endpoint
app.UseRouting();
app.UseHttpMetrics(); // Track HTTP request metrics

app.MapHealthChecks("/health");
app.MapMetrics(); // Expose /metrics endpoint

app.Run();
```

### 3. Add Custom Metrics

Create `DocN.Server/Metrics/RAGMetrics.cs`:

```csharp
using Prometheus;

namespace DocN.Server.Metrics;

public static class RAGMetrics
{
    // Query latency histogram
    public static readonly Histogram QueryLatency = Prometheus.Metrics
        .CreateHistogram(
            "rag_query_duration_seconds",
            "Histogram of RAG query durations",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.01, 2, 10),
                LabelNames = new[] { "query_type", "status" }
            }
        );

    // Token usage counter
    public static readonly Counter TokensUsed = Prometheus.Metrics
        .CreateCounter(
            "rag_tokens_used_total",
            "Total tokens used in RAG operations",
            new CounterConfiguration
            {
                LabelNames = new[] { "model", "operation" }
            }
        );

    // Retrieval accuracy gauge
    public static readonly Gauge RetrievalPrecision = Prometheus.Metrics
        .CreateGauge(
            "rag_retrieval_precision",
            "Current retrieval precision",
            new GaugeConfiguration
            {
                LabelNames = new[] { "dataset" }
            }
        );

    // Cache hit rate
    public static readonly Gauge CacheHitRate = Prometheus.Metrics
        .CreateGauge(
            "cache_hit_rate",
            "Current cache hit rate",
            new GaugeConfiguration
            {
                LabelNames = new[] { "cache_type" }
            }
        );

    // Document processing rate
    public static readonly Counter DocumentsProcessed = Prometheus.Metrics
        .CreateCounter(
            "documents_processed_total",
            "Total documents processed",
            new CounterConfiguration
            {
                LabelNames = new[] { "status" }
            }
        );
}
```

### 4. Use Metrics in Code

```csharp
public async Task<RAGResponse> ExecuteRAGQueryAsync(string query)
{
    using (RAGMetrics.QueryLatency.WithLabels("hybrid", "success").NewTimer())
    {
        try
        {
            var response = await _ragService.QueryAsync(query);
            
            RAGMetrics.TokensUsed
                .WithLabels(response.Model, "query")
                .Inc(response.TokensUsed);
            
            return response;
        }
        catch (Exception ex)
        {
            RAGMetrics.QueryLatency.WithLabels("hybrid", "error");
            throw;
        }
    }
}
```

## Grafana Dashboards

### Create Dashboard Provider

Create `monitoring/grafana/datasources/prometheus.yml`:

```yaml
apiVersion: 1

datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: true
```

### Create Dashboard Config

Create `monitoring/grafana/dashboards/dashboard.yml`:

```yaml
apiVersion: 1

providers:
  - name: 'DocN Dashboards'
    orgId: 1
    folder: ''
    type: file
    disableDeletion: false
    updateIntervalSeconds: 10
    allowUiUpdates: true
    options:
      path: /etc/grafana/provisioning/dashboards
```

### RAG Metrics Dashboard

Create `monitoring/grafana/dashboards/rag-metrics.json`:

```json
{
  "dashboard": {
    "title": "DocN RAG Performance",
    "panels": [
      {
        "title": "Query Latency (p95)",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(rag_query_duration_seconds_bucket[5m]))",
            "legendFormat": "{{query_type}}"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Cache Hit Rate",
        "targets": [
          {
            "expr": "cache_hit_rate",
            "legendFormat": "{{cache_type}}"
          }
        ],
        "type": "gauge"
      },
      {
        "title": "Token Usage",
        "targets": [
          {
            "expr": "rate(rag_tokens_used_total[5m])",
            "legendFormat": "{{model}} - {{operation}}"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Documents Processed",
        "targets": [
          {
            "expr": "rate(documents_processed_total[1h])",
            "legendFormat": "{{status}}"
          }
        ],
        "type": "stat"
      }
    ]
  }
}
```

## Logstash Configuration

Create `monitoring/logstash/logstash.conf`:

```conf
input {
  beats {
    port => 5044
  }
}

filter {
  if [fields][service] == "docn" {
    json {
      source => "message"
    }
    
    date {
      match => [ "timestamp", "ISO8601" ]
    }
    
    mutate {
      add_field => { "service" => "docn" }
    }
  }
}

output {
  elasticsearch {
    hosts => ["elasticsearch:9200"]
    index => "docn-logs-%{+YYYY.MM.dd}"
  }
  
  stdout {
    codec => rubydebug
  }
}
```

## Filebeat Configuration

Create `monitoring/filebeat/filebeat.yml`:

```yaml
filebeat.inputs:
  - type: log
    enabled: true
    paths:
      - /var/log/docn/*.log
    fields:
      service: docn
    fields_under_root: true

output.logstash:
  hosts: ["localhost:5044"]
```

## Alert Rules

Create `monitoring/prometheus/alerts.yml`:

```yaml
groups:
  - name: docn_alerts
    interval: 30s
    rules:
      # High latency alert
      - alert: HighRAGLatency
        expr: histogram_quantile(0.95, rate(rag_query_duration_seconds_bucket[5m])) > 2
        for: 5m
        labels:
          severity: warning
          component: rag
        annotations:
          summary: "High RAG query latency detected"
          description: "p95 latency is {{ $value }}s (threshold: 2s)"

      # Low cache hit rate
      - alert: LowCacheHitRate
        expr: cache_hit_rate < 0.6
        for: 10m
        labels:
          severity: warning
          component: cache
        annotations:
          summary: "Low cache hit rate"
          description: "Cache hit rate is {{ $value }} (threshold: 0.6)"

      # High error rate
      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
          component: api
        annotations:
          summary: "High API error rate"
          description: "Error rate is {{ $value }}"

      # Database connection issues
      - alert: DatabaseDown
        expr: up{job="sqlserver"} == 0
        for: 1m
        labels:
          severity: critical
          component: database
        annotations:
          summary: "SQL Server is down"
          description: "Cannot connect to SQL Server"
```

Update `prometheus.yml` to include alerts:

```yaml
rule_files:
  - 'alerts.yml'

alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']
```

## Start Monitoring Stack

```bash
# Start all services
docker-compose -f docker-compose.monitoring.yml up -d

# Check status
docker-compose -f docker-compose.monitoring.yml ps

# View logs
docker-compose -f docker-compose.monitoring.yml logs -f grafana
```

## Access Dashboards

- **Grafana:** http://localhost:3000 (admin/admin)
- **Prometheus:** http://localhost:9090
- **Kibana:** http://localhost:5601

## Verify Metrics

```bash
# Check if metrics endpoint is working
curl http://localhost:5210/metrics

# Query Prometheus
curl 'http://localhost:9090/api/v1/query?query=rag_query_duration_seconds_count'
```

## Best Practices

1. **Retention:** Configure appropriate retention periods (30 days for Prometheus)
2. **Sampling:** Use histogram buckets appropriate for your latency distribution
3. **Labels:** Use consistent label names across metrics
4. **Aggregation:** Pre-aggregate metrics where possible
5. **Alerting:** Set meaningful thresholds with appropriate for durations

## Troubleshooting

### Metrics not appearing

```bash
# Check if app is exposing metrics
curl http://localhost:5210/metrics | grep rag_query

# Check Prometheus targets
curl http://localhost:9090/api/v1/targets

# Check Prometheus logs
docker logs prometheus
```

### Grafana connection issues

```bash
# Test Prometheus from Grafana container
docker exec grafana curl http://prometheus:9090/api/v1/query?query=up
```

## References

- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [ELK Stack Guide](https://www.elastic.co/guide/index.html)
