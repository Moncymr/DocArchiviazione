# Distributed Vector Store and Caching - Guida all'Implementazione

## Panoramica

Questa implementazione risolve i limiti identificati:
- ✅ **Vector Store Distribuito**: Supporto per distribuzione su più istanze
- ✅ **Caching Distribuito**: Redis per caching condiviso tra istanze

## Architettura

### 1. Database Principale: SQL Server 2025
- **Ruolo**: Storage primario per tutti i dati dell'applicazione
- **Mantiene**: Documenti, metadati, chunks, embeddings
- **Non Cambia**: L'implementazione esistente rimane invariata

### 2. Redis - Distributed Caching
- **Ruolo**: Cache distribuita condivisa tra tutte le istanze
- **Caching di**:
  - Embedding vectors (24 ore)
  - Risultati di ricerca (15 minuti)
  - Risultati MMR (15 minuti)
  - Metadati e indici

### 3. Vector Store Distribuito
- **Primary Storage**: SQL Server 2025 (EnhancedVectorStoreService)
- **Distributed Layer**: DistributedVectorStoreService
- **Coordinamento**: Redis per sincronizzazione tra istanze

## Configurazione

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DocArc": "Server=YOUR_SERVER;Database=DocNDb;...",
    "Redis": "localhost:6379,abortConnect=false,connectTimeout=5000"
  },
  "DistributedCache": {
    "Provider": "Redis",
    "Redis": {
      "InstanceName": "DocN:",
      "ConnectionString": "localhost:6379,abortConnect=false",
      "Enabled": true
    },
    "DefaultExpirationMinutes": 60
  },
  "VectorStore": {
    "Provider": "SqlServer",
    "Distributed": {
      "Enabled": true,
      "ReplicationFactor": 3,
      "SyncIntervalSeconds": 60
    }
  }
}
```

## Setup Redis

### Installazione Locale (Windows)

1. **Download Redis per Windows**:
   ```powershell
   # Via Chocolatey
   choco install redis-64
   
   # O scarica da: https://github.com/microsoftarchive/redis/releases
   ```

2. **Avvio Redis**:
   ```powershell
   redis-server
   ```

3. **Verifica**:
   ```powershell
   redis-cli ping
   # Dovrebbe rispondere: PONG
   ```

### Installazione Docker

```bash
# Avvia Redis con Docker
docker run -d -p 6379:6379 --name docn-redis redis:latest

# Verifica
docker exec -it docn-redis redis-cli ping
```

### Configurazione Produzione

Per produzione, considera:

1. **Redis Cluster**: Per alta disponibilità
   ```
   redis://node1:6379,node2:6379,node3:6379
   ```

2. **Azure Redis Cache**:
   ```
   your-cache.redis.cache.windows.net:6380,password=YOUR_KEY,ssl=True
   ```

3. **AWS ElastiCache**:
   ```
   your-cluster.cache.amazonaws.com:6379
   ```

## Funzionalità Distribuite

### 1. Caching Distribuito

```csharp
// Automatico tramite IDistributedCacheService
// - Embeddings cachati per 24 ore
// - Ricerche cachate per 15 minuti
// - Cache condivisa tra tutte le istanze
```

### 2. Vector Store Distribuito

```csharp
// Configurazione automatica in Program.cs
// - Store primario: SQL Server 2025
// - Coordinamento: Redis
// - Replica automatica tra istanze
```

### 3. Sincronizzazione

- **Automatica**: Sync ogni 60 secondi (configurabile)
- **Event-driven**: Update immediati via Redis pub/sub
- **Failover**: Fallback a SQL Server se Redis non disponibile

## Scalabilità

### Deployment Multi-Istanza

1. **Load Balancer**: Distribuisci traffico su più istanze
2. **Shared Redis**: Tutte le istanze condividono la stessa cache
3. **Shared SQL Server**: Database centrale su SQL Server 2025
4. **Coordinamento Automatico**: Vector store si sincronizza via Redis

### Esempio Configurazione

```
┌─────────────┐
│ Load Balancer│
└──────┬──────┘
       │
       ├──────────┬──────────┬──────────┐
       │          │          │          │
   ┌───▼───┐  ┌──▼────┐  ┌──▼────┐  ┌──▼────┐
   │App #1 │  │App #2 │  │App #3 │  │App #N │
   └───┬───┘  └───┬───┘  └───┬───┘  └───┬───┘
       │          │          │          │
       └──────────┴──────────┴──────────┘
                  │          │
         ┌────────▼─┐    ┌──▼───────┐
         │  Redis   │    │SQL Server│
         │  Cache   │    │   2025   │
         └──────────┘    └──────────┘
```

## Monitoring

### Health Checks

L'applicazione espone health checks per:
- SQL Server 2025 database
- Redis cache
- Vector store status

Endpoint: `GET /health`

### Metriche

- **Cache Hit Rate**: Percentuale di hit nella cache Redis
- **Vector Sync Status**: Stato sincronizzazione tra istanze
- **Query Performance**: Latenza query con/senza cache

## Troubleshooting

### Redis Non Disponibile

L'applicazione funziona anche senza Redis:
- Fallback automatico a memory cache
- Vector store usa solo SQL Server
- Logging: "Redis not configured, using in-memory cache"

### Performance Tuning

```json
{
  "VectorStore": {
    "Distributed": {
      "ReplicationFactor": 3,      // Più alto = più ridondanza
      "SyncIntervalSeconds": 60     // Più basso = sync più frequente
    }
  },
  "DistributedCache": {
    "DefaultExpirationMinutes": 60  // Regola durata cache
  }
}
```

## Vantaggi

1. **Scalabilità Orizzontale**: Aggiungi istanze per gestire più carico
2. **Alta Disponibilità**: Ridondanza su più nodi
3. **Performance**: Cache distribuita riduce carico DB
4. **Failover**: Resilienza a guasti di singole istanze
5. **SQL Server 2025**: Database principale invariato

## Best Practices

1. **Usa Redis Cluster** in produzione per HA
2. **Monitora Cache Hit Rate** per ottimizzare expiration
3. **Configura Connection Pooling** per SQL Server
4. **Usa Load Balancer** con health checks
5. **Backup Regolari** di SQL Server 2025

## Migrazione

### Da Single Instance

Nessuna migrazione necessaria:
1. Installa Redis
2. Aggiorna appsettings.json
3. Riavvia applicazione
4. Deploy istanze aggiuntive (opzionale)

### Rollback

Per disabilitare funzionalità distribuite:

```json
{
  "VectorStore": {
    "Distributed": {
      "Enabled": false
    }
  },
  "ConnectionStrings": {
    "Redis": ""
  }
}
```

## Supporto

Per problemi o domande:
- Verifica logs in `logs/docn-*.log`
- Controlla health endpoint: `/health`
- Monitora Redis: `redis-cli monitor`
