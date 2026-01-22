# SPECIFICA TECNICA - PROGETTO C# BLAZOR CON RAG E INTELLIGENZA ARTIFICIALE

## DOCUMENTO PER PROMPT AI - GENERAZIONE PROGETTO

Questo documento fornisce tutte le caratteristiche tecniche per generare un progetto analogo in C# e Blazor con capacità avanzate di gestione documentale, RAG (Retrieval-Augmented Generation), ricerca semantica e intelligenza artificiale.

---

## 1. ARCHITETTURA GENERALE

### 1.1 Tipo di Architettura
- **Pattern**: Clean Architecture / Onion Architecture
- **Stile**: Multi-progetto con separazione delle responsabilità
- **Comunicazione**: Client-Server con API REST
- **Frontend**: Blazor Server (Razor Components Interactive)
- **Backend**: ASP.NET Core Web API

### 1.2 Struttura della Solution

La solution è composta da 4 progetti principali:

```
Doc_archiviazione.sln
├── DocN.Server (ASP.NET Core Web API)
│   └── Backend API, Controller, Middleware, Services
├── DocN.Client (Blazor Server Application)
│   └── Frontend UI, Razor Components, Pages
├── DocN.Data (Data Layer)
│   └── DbContext, Repositories, Data Services, Migrations
└── DocN.Core (Core/Domain Layer)
    └── Interfaces, AI Services, Semantic Kernel Integration
```

---

## 2. STACK TECNOLOGICO

### 2.1 Framework e Runtime
- **.NET Version**: .NET 10.0 (latest)
- **C# Language Version**: C# 13 (latest)
- **ASP.NET Core**: 10.0.0
- **Blazor**: Server-side rendering con Interactive Server Components

### 2.2 Database e Persistenza

#### Database Relazionale
- **ORM**: Entity Framework Core 10.0.1
- **Provider Principale**: SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`)
- **Provider Alternativo**: PostgreSQL 10.0.0-preview.1 (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Migration**: Code-First con EF Core Migrations
- **Design Patterns**: Repository Pattern, Unit of Work

#### Database Vettoriale
- **Estensione PostgreSQL**: pgvector (`Pgvector` 0.3.0)
- **Supporto Ricerca Vettoriale**: Vector similarity search con HNSW indexing
- **Dimensioni Embedding**: Configurabile (tipicamente 1536 per OpenAI)

#### Caching
- **In-Memory Cache**: `Microsoft.Extensions.Caching.Memory`
- **Distributed Cache**: Redis (`StackExchange.Redis` 2.8.16)
- **Cache Service**: Custom implementation per embedding e risultati ricerca

### 2.3 Intelligenza Artificiale e Machine Learning

#### Semantic Kernel
- **Microsoft.SemanticKernel**: 1.29.0
- **Microsoft.SemanticKernel.Agents.Core**: 1.29.0-alpha
- **Microsoft.SemanticKernel.Connectors.OpenAI**: 1.29.0
- **Funzionalità**: Orchestrazione AI, Plugin System, Memory Management

#### AI Providers (Multi-Provider)
1. **OpenAI**
   - Package: `OpenAI` 2.1.0
   - Modelli: GPT-4, GPT-3.5-turbo, text-embedding-3-small/large
   
2. **Azure OpenAI**
   - Package: `Azure.AI.OpenAI` 2.1.0
   - Deployment personalizzati
   
3. **Google Gemini**
   - Package: `Mscc.GenerativeAI` 2.1.0
   - Modelli: Gemini Pro, Gemini Pro Vision
   
4. **Ollama (Local LLM)**
   - Package: `OllamaSharp` 5.4.12
   - Supporto modelli locali

#### RAG (Retrieval-Augmented Generation)
- **Hybrid Search**: Vector search + BM25 keyword search
- **Chunking Strategies**: 
  - Recursive Character Text Splitter
  - Semantic Chunking (structure-aware)
  - Fixed size con overlap configurabile
- **Reranking**: Cross-encoder per migliorare risultati
- **Query Rewriting**: Espansione query con sinonimi
- **HyDE**: Hypothetical Document Embeddings
- **MMR**: Maximal Marginal Relevance per diversificazione
- **Contextual Compression**: Compressione contestuale dei documenti

### 2.4 Document Processing

#### OCR (Optical Character Recognition)
- **Tesseract**: `Tesseract` 5.2.0
- **Supporto**: Estrazione testo da immagini e PDF scansionati
- **Lingue**: Italiano, Inglese, configurabili

#### Document Parsing
- **PDF**: `itext7` 9.0.0
- **Office Documents**: `DocumentFormat.OpenXml` 3.2.0, `ClosedXML` 0.104.2
- **Image Processing**: `SixLabors.ImageSharp` 3.1.12

### 2.5 Connectors e Integrazione

#### Document Sources
- **SharePoint**: `PnP.Framework` 1.15.0
- **OneDrive/Microsoft Graph**: `Microsoft.Graph` 5.87.0
- **Google Drive**: `Google.Apis.Drive.v3` 1.70.0.3643
- **Protocolli**: FTP, SFTP, Local Folder

#### Ingestion Scheduler
- **Cron Scheduling**: `Cronos` 0.8.4
- **Pattern**: Background Service con scheduling configurabile

### 2.6 Background Jobs e Task Scheduling

#### Hangfire
- **Hangfire.AspNetCore**: 1.8.14
- **Hangfire.SqlServer**: 1.8.14
- **Hangfire.Console**: 1.4.3
- **Dashboard**: Web UI per monitoraggio job
- **Queue**: Multiple code (critical, default, low)

### 2.7 Monitoring, Logging e Observability

#### Logging
- **Serilog**: 8.0.3
- **Sinks**: Console, File (rolling)
- **Enrichers**: Environment, Machine Name, Thread ID
- **Format**: Structured logging con template personalizzati

#### OpenTelemetry (Distributed Tracing)
- **OpenTelemetry.Extensions.Hosting**: 1.10.0
- **OpenTelemetry.Instrumentation.AspNetCore**: 1.10.1
- **OpenTelemetry.Instrumentation.Http**: 1.11.0
- **OpenTelemetry.Instrumentation.SqlClient**: 1.10.0-beta.1
- **OpenTelemetry.Exporter.Prometheus**: 1.10.0-beta.1
- **OpenTelemetry.Exporter.OpenTelemetryProtocol**: 1.10.0 (OTLP per Jaeger/Zipkin)

#### Metrics
- **App.Metrics**: 4.3.0
- **Prometheus Format**: `App.Metrics.Formatters.Prometheus` 4.3.0
- **Endpoint**: `/metrics` per scraping Prometheus
- **Custom Metrics**: Alert system, RAG quality metrics

#### Health Checks
- **Microsoft.Extensions.Diagnostics.HealthChecks**: 10.0.0
- **AspNetCore.HealthChecks.UI.Client**: 9.0.0
- **AspNetCore.HealthChecks.Redis**: 9.0.0
- **Custom Health Checks**:
  - Database connectivity
  - AI Provider availability
  - OCR Service status
  - Semantic Kernel readiness
  - File Storage accessibility
  
- **Endpoints**:
  - `/health` - Overall health status
  - `/health/live` - Liveness probe (Kubernetes)
  - `/health/ready` - Readiness probe (Kubernetes)

### 2.8 Security e Authentication

#### Identity Framework
- **ASP.NET Core Identity**: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.0
- **Password Policy**: Configurabile (lunghezza minima, complessità)
- **Lockout**: Protezione contro brute force
- **User Management**: Registrazione, login, logout, recupero password

#### Security Headers
- Custom Middleware per security headers
- HSTS, CSP, X-Frame-Options, X-Content-Type-Options

#### Rate Limiting
- **ASP.NET Core Rate Limiting**: Built-in
- **Strategie**:
  - Fixed Window (API generali)
  - Sliding Window (upload documenti)
  - Concurrency Limiter (operazioni AI)

#### Audit Logging
- **Compliance**: GDPR, SOC2
- **Tracking**: User actions, data access, modifications
- **Storage**: Database con retention configurabile

### 2.9 API Documentation

#### Swagger/OpenAPI
- **Swashbuckle.AspNetCore**: 10.1.0
- **Microsoft.AspNetCore.OpenApi**: 10.0.0
- **Microsoft.OpenApi**: 2.3.0
- **Features**:
  - XML comments per documentazione automatica
  - API versioning
  - Response types documentation
  - Authentication schemes

---

## 3. MODELLI DI DATI E DATABASE SCHEMA

### 3.1 Entità Principali

#### Document (Documento)
```csharp
- Id: Guid (Primary Key)
- TenantId: int (Multi-tenancy)
- FileName: string
- FilePath: string
- FileSize: long
- ContentType: string
- UploadedAt: DateTime
- ProcessedAt: DateTime?
- ExtractedText: string
- ActualCategory: string
- PredictedCategory: string
- Confidence: double
- Status: DocumentStatus (enum)
- UserId: string (Owner)
```

#### DocumentChunk (Chunk con metadati arricchiti)
```csharp
- Id: Guid (Primary Key)
- DocumentId: Guid (Foreign Key)
- ChunkIndex: int
- Content: string
- EmbeddingVector: Vector (pgvector)
- PageNumber: int?
- SectionTitle: string?
- SectionPath: string? (es. "1.2.3")
- KeywordsJson: string? (JSON array)
- DocumentType: string?
- HeaderLevel: int (0-6)
- ChunkType: string (paragraph, section, sentence)
- IsListItem: bool
- CustomMetadataJson: string?
```

#### AIConfiguration (Configurazione Multi-Provider)
```csharp
- Id: int (Primary Key)
- TenantId: int
- ProviderName: string (OpenAI, AzureOpenAI, Gemini, Ollama)
- ModelName: string
- EmbeddingModel: string
- ApiKey: string (encrypted)
- Endpoint: string?
- IsActive: bool
- IsDefault: bool
- MaxTokens: int
- Temperature: double
- AdditionalSettingsJson: string?
```

#### GoldenDataset (Valutazione Qualità RAG)
```csharp
- Id: int (Primary Key)
- TenantId: int
- Name: string
- Description: string
- QueriesJson: string (JSON array)
- CreatedAt: DateTime
- UpdatedAt: DateTime
```

#### AuditLog (Audit Trail per Compliance)
```csharp
- Id: Guid (Primary Key)
- TenantId: int
- UserId: string
- Action: string
- EntityType: string
- EntityId: string
- Changes: string (JSON)
- IpAddress: string
- UserAgent: string
- Timestamp: DateTime
```

#### IngestionSchedule (Ingestion Automatica)
```csharp
- Id: int (Primary Key)
- TenantId: int
- Name: string
- ConnectorType: ConnectorType (enum)
- SourcePath: string
- Schedule: string (Cron expression)
- IsActive: bool
- LastRunAt: DateTime?
- NextRunAt: DateTime?
- ConfigurationJson: string
```

#### ApplicationUser (Identity User esteso)
```csharp
- Id: string (Primary Key)
- TenantId: int
- FirstName: string
- LastName: string
- Email: string
- PhoneNumber: string?
- IsActive: bool
- CreatedAt: DateTime
- LastLoginAt: DateTime?
- PreferencesJson: string?
```

#### Tenant (Multi-tenancy)
```csharp
- Id: int (Primary Key)
- Name: string
- SubDomain: string?
- IsActive: bool
- CreatedAt: DateTime
- SubscriptionTier: string
- MaxUsers: int
- MaxDocuments: int
```

### 3.2 DbContext

#### ApplicationDbContext
- **Gestisce**: User, Tenant, AIConfiguration, GoldenDataset, AuditLog, IngestionSchedule
- **Connection String**: DefaultConnection
- **Features**: 
  - Multi-tenancy con query filters
  - Soft delete
  - Audit automatico (CreatedAt, UpdatedAt)

#### DocArcContext
- **Gestisce**: Document, DocumentChunk, DocumentShare, DocumentTag, LogEntry
- **Connection String**: DocArc (può essere stesso database)
- **Features**:
  - Vector search support (pgvector)
  - Full-text search indexes
  - Ottimizzazioni performance

---

## 4. SERVIZI E BUSINESS LOGIC

### 4.1 Core Services (DocN.Core)

#### ISemanticKernelFactory
- Creazione dinamica Semantic Kernel da configurazione database
- Supporto multi-provider
- Plugin management

#### IKernelProvider
- Fornisce istanze Kernel configurate
- Scoped lifecycle
- Lazy loading

### 4.2 AI Services (DocN.Data/Services)

#### IMultiProviderAIService
- Gestione multi-provider (OpenAI, Azure, Gemini, Ollama)
- Fallback automatico tra provider
- Retry logic con exponential backoff
- Cost tracking

#### IEmbeddingService
- Generazione embeddings da testo
- Batch processing ottimizzato
- Caching embeddings
- Supporto modelli diversi (ada-002, text-embedding-3-small/large)

#### ISemanticRAGService / EnhancedAgentRAGService
- RAG orchestration
- Retrieval strategy selection
- Context building
- Answer generation
- Citation tracking
- **Implementations**:
  - **MultiProviderSemanticRAGService**: Classic RAG
  - **EnhancedAgentRAGService**: Microsoft Agent Framework con agenti specializzati

#### Agent Framework
- **IRetrievalAgent**: Specializzato nel retrieval documenti
- **ISynthesisAgent**: Sintesi e generazione risposte
- **IClassificationAgent**: Classificazione intent query
- **IAgentOrchestrator**: Coordinamento multi-agente

#### ISemanticCacheService
- Caching semantico query simili
- Threshold-based similarity matching
- Cache statistics (hit rate)
- Eviction policy

#### IBM25Service
- BM25 scoring algorithm (keyword search)
- TF-IDF calculation
- Document statistics management
- Tunable parameters (K1, B)

#### IMultiHopSearchService
- Query decomposition
- Multi-step reasoning
- Result aggregation
- Tracing intermediate steps

### 4.3 Document Processing Services

#### IDocumentService
- Upload e storage documenti
- Metadata extraction
- File type detection
- Virus scanning integration point

#### IChunkingService / ISemanticChunkingService
- **Standard Chunking**: Fixed size con overlap
- **Semantic Chunking**: Structure-aware (headers, paragraphs, sections)
- Metadata extraction (keywords, section titles)
- Multiple strategies

#### IOCRService (TesseractOCRService)
- Text extraction da immagini
- PDF scansionati
- Language detection
- Confidence scoring

#### IFileProcessingService
- Orchestrazione pipeline processing
- Text extraction
- Chunking
- Embedding generation
- Indicizzazione

### 4.4 Search Services

#### IHybridSearchService
- Vector search + keyword search
- Weighted fusion (configurable)
- RRF (Reciprocal Rank Fusion)
- Filtering (date, type, author)
- MMR diversification

#### IQueryIntentClassifier
- Intent classification (semantic, statistical, factual)
- Query routing
- Statistical query detection

#### IDocumentStatisticsService
- Document statistics calculation
- Aggregazioni (count, avg, sum, grouping)
- Performance ottimizzata

#### IStatisticalAnswerGenerator
- Risposta automatica query statistiche
- Natural language generation
- Chart data generation

### 4.5 RAG Quality & Metrics Services

#### IRAGQualityService
- Quality assessment
- Failure detection
- Auto-remediation

#### IRAGASMetricsService
- RAGAS metrics (Faithfulness, Answer Relevance, Context Relevance)
- Evaluation framework
- Benchmark tracking

#### IRetrievalMetricsService
- **MRR** (Mean Reciprocal Rank)
- **NDCG** (Normalized Discounted Cumulative Gain)
- **Precision@K**
- **Recall@K**
- **Hit Rate**
- A/B testing configurations

#### IGoldenDatasetService
- Dataset management
- Query-document relevance pairs
- Evaluation orchestration

#### IEmbeddingFineTuningService
- Training data preparation
- Contrastive pair generation
- Model evaluation
- Domain adaptation

### 4.6 Connector Services

#### IConnectorService
- Connector registry
- Source configuration
- Connection testing

#### Connector Handlers (BaseConnectorHandler)
- **SharePointConnectorHandler**: SharePoint Online/OnPrem
- **OneDriveConnectorHandler**: OneDrive for Business
- **GoogleDriveConnectorHandler**: Google Drive
- **FtpConnectorHandler**: FTP/FTPS
- **SftpConnectorHandler**: SFTP/SSH
- **LocalFolderConnectorHandler**: File system locale

#### IIngestionService
- Ingestion orchestration
- Error handling e retry
- Progress tracking
- Notification

#### IIngestionSchedulerHelper
- Schedule parsing (Cron)
- Next run calculation
- Job triggering

### 4.7 Utility Services

#### ICacheService
- Unified caching interface
- Memory cache + distributed cache
- Cache invalidation
- Statistics

#### IDistributedCacheService
- Redis integration
- Serialization/deserialization
- Connection resilience

#### ILogService
- Business logic logging
- Log querying
- Log retention

#### IAuditService
- Audit trail creation
- Compliance reporting
- Data access tracking

#### IAlertingService
- Alert generation
- Threshold monitoring
- Notification routing

### 4.8 Background Services

#### BatchEmbeddingProcessor
- Background embedding generation
- Queue-based processing
- Batch optimization
- Error recovery

#### IngestionSchedulerService
- Scheduled ingestion execution
- Cron-based triggering
- Job orchestration
- Health monitoring

---

## 5. API CONTROLLERS (DocN.Server/Controllers)

### 5.1 DocumentsController
```
POST   /api/documents/upload          - Upload documento
GET    /api/documents                 - Lista documenti
GET    /api/documents/{id}            - Dettaglio documento
DELETE /api/documents/{id}            - Elimina documento
POST   /api/documents/{id}/process    - Processa documento
GET    /api/documents/{id}/chunks     - Chunks documento
POST   /api/documents/batch-upload    - Upload multiplo
```

### 5.2 SearchController
```
POST   /api/search                    - Ricerca ibrida
POST   /api/search/semantic           - Ricerca semantica pura
POST   /api/search/hybrid             - Ricerca ibrida configurabile
POST   /api/search/multi-hop          - Ricerca multi-step
GET    /api/search/suggestions        - Suggerimenti query
```

### 5.3 SemanticChatController / ChatController
```
POST   /api/chat/ask                  - Chat RAG
POST   /api/chat/stream               - Chat streaming
GET    /api/chat/history              - Storico conversazioni
POST   /api/chat/feedback             - Feedback risposta
```

### 5.4 ConfigController
```
GET    /api/config/ai-providers       - Lista provider AI
POST   /api/config/ai-providers       - Crea configurazione
PUT    /api/config/ai-providers/{id}  - Aggiorna configurazione
DELETE /api/config/ai-providers/{id}  - Elimina configurazione
POST   /api/config/test-connection    - Test connessione provider
GET    /api/config/active             - Configurazione attiva
```

### 5.5 ConnectorsController
```
GET    /api/connectors                - Lista connettori
POST   /api/connectors                - Crea connettore
PUT    /api/connectors/{id}           - Aggiorna connettore
DELETE /api/connectors/{id}           - Elimina connettore
POST   /api/connectors/{id}/test      - Test connessione
POST   /api/connectors/{id}/sync      - Sincronizzazione manuale
```

### 5.6 IngestionController
```
GET    /api/ingestion/schedules       - Lista schedule
POST   /api/ingestion/schedules       - Crea schedule
PUT    /api/ingestion/schedules/{id}  - Aggiorna schedule
DELETE /api/ingestion/schedules/{id}  - Elimina schedule
POST   /api/ingestion/run/{id}        - Esegui manualmente
GET    /api/ingestion/logs            - Log ingestion
```

### 5.7 RAGQualityController
```
POST   /api/rag-quality/evaluate      - Valuta qualità RAG
GET    /api/rag-quality/metrics       - Metriche qualità
POST   /api/rag-quality/auto-remediate - Auto-rimedio
```

### 5.8 RAGEnhancementsController
```
POST   /api/rag-enhancements/demo/semantic-chunking     - Test chunking semantico
POST   /api/rag-enhancements/demo/extract-structure     - Estrai struttura documento
GET    /api/rag-enhancements/documents/{id}/chunk-metadata - Metadata chunks
POST   /api/rag-enhancements/retrieval/evaluate/{datasetId} - Valuta retrieval
POST   /api/rag-enhancements/retrieval/ab-test          - A/B test configurazioni
POST   /api/rag-enhancements/fine-tuning/prepare-training-data - Prepara dati training
POST   /api/rag-enhancements/fine-tuning/contrastive-pairs - Genera coppie contrastive
POST   /api/rag-enhancements/fine-tuning/evaluate-model - Valuta modello embedding
```

### 5.9 GoldenDatasetsController
```
GET    /api/golden-datasets           - Lista datasets
POST   /api/golden-datasets           - Crea dataset
GET    /api/golden-datasets/{id}      - Dettaglio dataset
PUT    /api/golden-datasets/{id}      - Aggiorna dataset
DELETE /api/golden-datasets/{id}      - Elimina dataset
POST   /api/golden-datasets/{id}/evaluate - Valuta sistema
```

### 5.10 AuditController
```
GET    /api/audit/logs                - Log audit
GET    /api/audit/logs/{id}           - Dettaglio log
GET    /api/audit/report              - Report compliance
POST   /api/audit/export              - Export audit trail
```

### 5.11 LogsController
```
GET    /api/logs                      - Lista log applicazione
GET    /api/logs/errors               - Solo errori
GET    /api/logs/search               - Ricerca log
```

### 5.12 AlertsController
```
GET    /api/alerts                    - Lista alert
GET    /api/alerts/active             - Alert attivi
POST   /api/alerts/acknowledge/{id}   - Conferma alert
GET    /api/alerts/metrics            - Metriche alert
```

---

## 6. FRONTEND BLAZOR (DocN.Client)

### 6.1 Architettura Frontend
- **Blazor Server**: Interactive Server Components
- **Rendering Mode**: Server-side rendering con SignalR
- **State Management**: Cascading parameters, dependency injection
- **Authentication**: ASP.NET Core Identity

### 6.2 Componenti Principali (Razor Components)

#### Layout Components
- **MainLayout.razor**: Layout principale applicazione
- **NavMenu.razor**: Menu navigazione laterale
- **LoginDisplay.razor**: Display utente e logout

#### Pages
- **Documents.razor**: 
  - Lista documenti con filtering e paginazione
  - Upload documenti (singolo e multiplo)
  - Drag & drop support
  - Progress bar upload
  - Preview documenti
  
- **Search.razor**:
  - Search bar avanzata
  - Filtri (data, tipo, autore)
  - Configurazione hybrid search (vector weight, text weight)
  - Display risultati con highlighting
  - Similarity score visualization
  
- **SemanticChat.razor**:
  - Interfaccia chat conversazionale
  - Streaming responses
  - Citation display
  - Feedback mechanism
  - Storico conversazioni
  
- **ConfigDiagnostics.razor**:
  - Configurazione AI providers
  - Test connessioni
  - Health status
  - Metrics visualization
  
- **GoldenDatasets.razor**:
  - CRUD golden datasets
  - Query management
  - Evaluation results
  - Metrics charts
  
- **Connectors.razor**:
  - Configurazione connettori
  - Schedule ingestion
  - Monitoring sync status
  
- **AuditLogs.razor**:
  - Visualizzazione audit trail
  - Filtering e ricerca
  - Export funzionalità

#### Shared Components
- **DocumentUpload.razor**: Componente upload riusabile
- **SearchResults.razor**: Display risultati ricerca
- **ChatMessage.razor**: Singolo messaggio chat
- **MetricsChart.razor**: Grafici metriche
- **LoadingSpinner.razor**: Indicatore caricamento

### 6.3 Services Frontend

#### HttpClient Configuration
```csharp
// Named HttpClient per backend API
builder.Services.AddHttpClient("BackendAPI", client =>
{
    client.BaseAddress = new Uri(configuration["BackendApiUrl"]);
    client.Timeout = TimeSpan.FromMinutes(5); // Timeout esteso per AI
});
```

#### State Management
- Scoped services per stato applicazione
- Cascading authentication state
- Real-time updates via SignalR

---

## 7. MIDDLEWARE E INTERCEPTORS

### 7.1 Custom Middleware

#### SecurityHeadersMiddleware
```csharp
- X-Frame-Options: DENY
- X-Content-Type-Options: nosniff
- Referrer-Policy: strict-origin-when-cross-origin
- Content-Security-Policy: Configurabile
- X-XSS-Protection: 1; mode=block
```

#### AlertMetricsMiddleware
- Tracking request metrics
- Response time monitoring
- Error rate tracking
- Alert threshold checking

### 7.2 Exception Handling
- Global exception handler
- Detailed error logging
- User-friendly error messages
- Error telemetry

---

## 8. CONFIGURAZIONE E DEPLOYMENT

### 8.1 File di Configurazione

#### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "...",
    "DocArc": "...",
    "Redis": "..."
  },
  "OpenAI": {
    "ApiKey": "...",
    "Model": "gpt-4",
    "EmbeddingModel": "text-embedding-3-small"
  },
  "AzureOpenAI": {
    "Endpoint": "...",
    "ApiKey": "...",
    "ChatDeployment": "gpt-4",
    "EmbeddingDeployment": "text-embedding-ada-002"
  },
  "Gemini": {
    "ApiKey": "...",
    "Model": "gemini-pro"
  },
  "FileStorage": {
    "UploadPath": "Uploads",
    "MaxFileSizeInMB": 100,
    "AllowedExtensions": [".pdf", ".docx", ".txt"]
  },
  "EnhancedRAG": {
    "UseEnhancedAgentRAG": false,
    "ContextualCompression": {
      "Enabled": true,
      "MaxContextLength": 4000
    }
  },
  "BatchProcessing": {
    "BatchSize": 10,
    "MaxRetries": 3,
    "RetryDelaySeconds": 5
  },
  "AlertManager": {
    "EnableAlerts": true,
    "ThresholdLatencyMs": 5000,
    "ThresholdErrorRate": 0.1
  }
}
```

#### appsettings.Development.json
- Override per development
- Connection string locali
- Logging verboso
- Swagger enabled

#### .env.template
- Template per environment variables
- Documentazione configurazione
- Security best practices

### 8.2 Database Migrations

#### Esecuzione Migrations
```bash
# Aggiungere migration
dotnet ef migrations add MigrationName --project DocN.Data --startup-project DocN.Server

# Applicare migrations
dotnet ef database update --project DocN.Data --startup-project DocN.Server

# Applicazione automatica al startup (Program.cs)
await dbContext.Database.MigrateAsync();
```

### 8.3 Deployment

#### Docker Support (Raccomandato)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DocN.Server/DocN.Server.csproj", "DocN.Server/"]
COPY ["DocN.Client/DocN.Client.csproj", "DocN.Client/"]
COPY ["DocN.Data/DocN.Data.csproj", "DocN.Data/"]
COPY ["DocN.Core/DocN.Core.csproj", "DocN.Core/"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DocN.Server.dll"]
```

#### Kubernetes
- Health checks configured (/health/live, /health/ready)
- Horizontal Pod Autoscaling support
- ConfigMaps per configuration
- Secrets per credenziali

#### Azure App Service
- Native .NET 10 support
- Managed Identity per Azure OpenAI
- Easy scaling
- Deployment slots

---

## 9. FEATURES AVANZATE

### 9.1 Multi-Tenancy
- **Isolation Mode**: Discriminator column (TenantId)
- **Alternative Modes**: Database per tenant, Schema per tenant
- **Tenant Resolution**: Subdomain, header, claim-based
- **Query Filters**: Automatic tenant filtering in EF Core

### 9.2 Real-time Features
- **SignalR**: Real-time notifications
- **Use Cases**:
  - Document processing status
  - Chat streaming responses
  - Ingestion progress
  - Alert notifications

### 9.3 Batch Processing
- **Queue System**: In-memory o Redis
- **Batch Embedding Generation**: Ottimizzazione costi API
- **Parallel Processing**: Configurable concurrency
- **Error Handling**: Retry con exponential backoff

### 9.4 Advanced Search Features

#### Query Expansion
- Synonym expansion
- Related terms
- Spell correction

#### Reranking
- Cross-encoder models
- Score recalculation
- Improved relevance

#### Multi-Hop Reasoning
- Query decomposition
- Iterative refinement
- Aggregazione risultati

#### Contextual Compression
- Context trimming
- Relevance filtering
- Token optimization

### 9.5 RAG Quality Monitoring

#### RAGAS Metrics
- **Faithfulness**: Fedeltà alla sorgente
- **Answer Relevance**: Rilevanza risposta
- **Context Relevance**: Rilevanza contesto

#### Automated Quality Checks
- Response validation
- Source grounding
- Hallucination detection

#### A/B Testing
- Configuration comparison
- Statistical significance
- Winner selection

---

## 10. SECURITY BEST PRACTICES

### 10.1 Data Protection
- **Encryption at Rest**: Database TDE
- **Encryption in Transit**: HTTPS/TLS
- **API Keys**: Encrypted storage con Data Protection API
- **Key Vault**: Azure Key Vault o AWS Secrets Manager

### 10.2 Authentication & Authorization
- **Multi-Factor Authentication**: Supporto 2FA
- **Role-Based Access Control**: User, Admin, Tenant Admin
- **Claims-Based Authorization**: Fine-grained permissions
- **Token Security**: JWT con expiration

### 10.3 Input Validation
- **Model Validation**: Data annotations
- **File Upload**: Type checking, size limits, virus scan
- **SQL Injection**: Parameterized queries (EF Core)
- **XSS Protection**: Auto-escaping in Razor

### 10.4 Compliance
- **GDPR**: Right to erasure, data portability
- **SOC2**: Audit logging, access controls
- **Privacy**: Data minimization, consent management

---

## 11. PERFORMANCE OPTIMIZATION

### 11.1 Database Optimization
- **Indexes**: Strategic indexing su query frequenti
- **Connection Pooling**: Configurazione pool size
- **Query Optimization**: LINQ optimization, compiled queries
- **Caching**: Query result caching

### 11.2 API Optimization
- **Response Compression**: Gzip/Brotli
- **Pagination**: Cursor-based pagination
- **Partial Loading**: Select only needed fields
- **CDN**: Static assets caching

### 11.3 AI Operations Optimization
- **Batch Embeddings**: Riduci chiamate API
- **Embedding Cache**: Cache embeddings generati
- **Semantic Cache**: Cache query simili
- **Connection Pooling**: HttpClient reuse

### 11.4 Frontend Optimization
- **Lazy Loading**: Component lazy loading
- **Virtual Scrolling**: Liste lunghe
- **Image Optimization**: Lazy loading, responsive images
- **State Management**: Minimize re-renders

---

## 12. TESTING STRATEGY

### 12.1 Unit Tests
- **xUnit**: Framework testing
- **Moq**: Mocking dependencies
- **FluentAssertions**: Readable assertions
- **Coverage**: Target 80%+

### 12.2 Integration Tests
- **WebApplicationFactory**: ASP.NET Core integration tests
- **Testcontainers**: Database containers
- **In-Memory Database**: Fast tests

### 12.3 E2E Tests
- **Playwright**: Browser automation
- **Selenium**: Alternative option
- **Test Scenarios**: Critical user flows

### 12.4 Load Testing
- **k6**: Performance testing
- **JMeter**: Alternative option
- **Metrics**: Response time, throughput, error rate

---

## 13. DOCUMENTATION

### 13.1 Code Documentation
- **XML Comments**: API documentation
- **README**: Setup instructions
- **CHANGELOG**: Version history

### 13.2 Technical Documentation
- **Architecture Diagrams**: C4 model
- **API Documentation**: Swagger/OpenAPI
- **Database Schema**: ER diagrams

### 13.3 User Documentation
- **User Guide**: Feature walkthrough
- **Admin Guide**: Configuration, maintenance
- **FAQ**: Common issues

---

## 14. CI/CD PIPELINE

### 14.1 Build Pipeline
```yaml
stages:
  - restore
  - build
  - test
  - publish
  - deploy

restore:
  dotnet restore

build:
  dotnet build --configuration Release

test:
  dotnet test --no-build --verbosity normal

publish:
  dotnet publish --configuration Release --output ./publish

deploy:
  # Deploy to target environment
```

### 14.2 Code Quality
- **SonarQube**: Static code analysis
- **CodeQL**: Security vulnerability scanning
- **Dependency Scanning**: Vulnerabilità dependencies

### 14.3 Deployment Strategy
- **Blue-Green Deployment**: Zero downtime
- **Canary Releases**: Gradual rollout
- **Rollback Strategy**: Automatic rollback su failure

---

## 15. MONITORING E OBSERVABILITY

### 15.1 Application Monitoring
- **Application Insights**: Azure monitoring
- **Prometheus + Grafana**: Metrics visualization
- **Jaeger**: Distributed tracing
- **ELK Stack**: Log aggregation

### 15.2 Alerts
- **High Error Rate**: > 5% errori
- **High Latency**: > 5s response time
- **AI Provider Failures**: Fallback triggered
- **Database Connection Issues**: Connection pool exhausted
- **Disk Space**: Storage > 90%

### 15.3 Dashboards
- **System Health**: CPU, memory, disk
- **Application Metrics**: Request rate, error rate, latency
- **Business Metrics**: Documents processed, queries executed
- **AI Metrics**: Token usage, cost, latency
- **RAG Quality**: RAGAS scores, retrieval metrics

---

## 16. UPGRADE E MAINTENANCE

### 16.1 Dependency Updates
- **Regular Updates**: Monthly dependency review
- **Security Patches**: Immediate application
- **Breaking Changes**: Careful testing

### 16.2 Database Maintenance
- **Backup Strategy**: Daily backups, weekly full backup
- **Retention Policy**: 30 days
- **Disaster Recovery**: Tested recovery procedures

### 16.3 Performance Tuning
- **Query Optimization**: Regular query analysis
- **Index Maintenance**: Rebuild fragmented indexes
- **Cache Tuning**: Hit rate monitoring

---

## 17. TROUBLESHOOTING COMMON ISSUES

### 17.1 AI Provider Issues
- **Problem**: API key invalid
- **Solution**: Verify key, check expiration, test connection

- **Problem**: Rate limit exceeded
- **Solution**: Implement exponential backoff, use fallback provider

### 17.2 Database Issues
- **Problem**: Connection timeout
- **Solution**: Check connection string, network connectivity, firewall rules

- **Problem**: Migration failure
- **Solution**: Rollback, fix migration, reapply

### 17.3 Performance Issues
- **Problem**: Slow queries
- **Solution**: Add indexes, optimize query, enable query caching

- **Problem**: High memory usage
- **Solution**: Tune cache size, check memory leaks, scale up

---

## 18. FUTURE ENHANCEMENTS (ROADMAP)

### 18.1 Planned Features
- **Multi-modal RAG**: Support immagini, audio, video
- **Advanced Analytics**: BI dashboard, report builder
- **Workflow Engine**: Approval workflows, business rules
- **Mobile App**: iOS, Android native apps
- **Voice Interface**: Voice commands, speech-to-text

### 18.2 Research Areas
- **Agentic RAG**: Autonomous agent reasoning
- **Federated Learning**: Privacy-preserving model training
- **Graph RAG**: Knowledge graph integration
- **Quantum Search**: Quantum-inspired algorithms

---

## 19. GETTING STARTED (QUICKSTART)

### 19.1 Prerequisites
```bash
- .NET 10.0 SDK
- SQL Server 2019+ o PostgreSQL 15+
- Node.js 20+ (per tooling frontend)
- Redis 7+ (optional, ma raccomandato)
- API Keys (almeno un AI provider)
```

### 19.2 Setup Steps
```bash
# 1. Clone repository
git clone https://github.com/your-org/doc-archiviazione.git
cd doc-archiviazione

# 2. Restore packages
dotnet restore

# 3. Update appsettings.json
# Configurare connection string e API keys

# 4. Apply migrations
dotnet ef database update --project DocN.Data --startup-project DocN.Server

# 5. Run Server
cd DocN.Server
dotnet run

# 6. Run Client (in another terminal)
cd DocN.Client
dotnet run

# 7. Access application
# Server API: https://localhost:5211
# Client UI: https://localhost:7114
# Swagger: https://localhost:5211/swagger
```

### 19.3 First Steps
1. **Login**: admin@example.com / Admin123!
2. **Configure AI Provider**: /config
3. **Upload Document**: /documents
4. **Try Search**: /search
5. **Try Chat**: /chat

---

## 20. CONCLUSIONI

Questo documento fornisce una specifica tecnica completa per generare un progetto C# Blazor con:

✅ **Architettura moderna**: Clean Architecture, multi-layer, separazione responsabilità  
✅ **Stack tecnologico aggiornato**: .NET 10, EF Core 10, Blazor Server  
✅ **AI/ML Integration**: Semantic Kernel, Multi-Provider, RAG avanzato  
✅ **Database**: SQL Server/PostgreSQL con pgvector  
✅ **Document Processing**: OCR, PDF parsing, chunking semantico  
✅ **Search**: Hybrid search (vector + keyword), reranking, multi-hop  
✅ **Quality Assurance**: Retrieval metrics, RAGAS, A/B testing  
✅ **Monitoring**: OpenTelemetry, Prometheus, Health Checks  
✅ **Security**: Identity, RBAC, Audit logging, GDPR compliance  
✅ **Scalability**: Redis cache, Hangfire jobs, rate limiting  
✅ **DevOps**: CI/CD ready, Docker support, Kubernetes ready  

Il progetto è production-ready con best practices, security hardening, comprehensive monitoring, e extensive documentation.

---

## APPENDICE A: PACKAGE VERSIONS COMPLETE

```xml
<!-- DocN.Server -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.1" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.1" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
<PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.0.1" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="10.1.0" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.10.0-beta.1" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.10.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.10.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.11.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.SqlClient" Version="1.10.0-beta.1" />
<PackageReference Include="OpenTelemetry.Api" Version="1.11.2" />
<PackageReference Include="App.Metrics.AspNetCore" Version="4.3.0" />
<PackageReference Include="App.Metrics.Formatters.Prometheus" Version="4.3.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.0" />
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.14" />
<PackageReference Include="Hangfire.SqlServer" Version="1.8.14" />
<PackageReference Include="Hangfire.Console" Version="1.4.3" />
<PackageReference Include="StackExchange.Redis" Version="2.8.16" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.0.0" />

<!-- DocN.Client -->
<PackageReference Include="OpenAI" Version="2.1.0" />
<PackageReference Include="Microsoft.AspNetCore.Components.Authorization" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.1" />

<!-- DocN.Core -->
<PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
<PackageReference Include="OllamaSharp" Version="5.4.12" />
<PackageReference Include="OpenAI" Version="2.1.0" />
<PackageReference Include="Microsoft.SemanticKernel" Version="1.29.0" />
<PackageReference Include="Microsoft.SemanticKernel.Agents.Core" Version="1.29.0-alpha" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.OpenAI" Version="1.29.0" />
<PackageReference Include="Mscc.GenerativeAI" Version="2.1.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />

<!-- DocN.Data -->
<PackageReference Include="ClosedXML" Version="0.104.2" />
<PackageReference Include="Cronos" Version="0.8.4" />
<PackageReference Include="DocumentFormat.OpenXml" Version="3.2.0" />
<PackageReference Include="Google.Apis.Drive.v3" Version="1.70.0.3643" />
<PackageReference Include="Hangfire.Core" Version="1.8.14" />
<PackageReference Include="itext7" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.1" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.1" />
<PackageReference Include="Microsoft.Graph" Version="5.87.0" />
<PackageReference Include="PnP.Framework" Version="1.15.0" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.12" />
<PackageReference Include="Tesseract" Version="5.2.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0-preview.1" />
<PackageReference Include="Pgvector" Version="0.3.0" />
```

---

## APPENDICE B: FILE STRUCTURE COMPLETA

```
DocArchiviazione/
├── Doc_archiviazione.sln
├── .gitignore
├── .gitattributes
├── .env.template
├── SPECIFICA_TECNICA_PROGETTO_BLAZOR.md (questo file)
├── IMPLEMENTATION_SUMMARY.md
├── HYBRID_SEARCH_IMPLEMENTATION.md
├── RAG_ENHANCEMENTS_GUIDE.md
├── CREDENZIALI_LOGIN.md
│
├── DocN.Server/
│   ├── DocN.Server.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── appsettings.example.json
│   ├── Controllers/
│   │   ├── DocumentsController.cs
│   │   ├── SearchController.cs
│   │   ├── SemanticChatController.cs
│   │   ├── ChatController.cs
│   │   ├── ConfigController.cs
│   │   ├── ConnectorsController.cs
│   │   ├── IngestionController.cs
│   │   ├── RAGQualityController.cs
│   │   ├── RAGEnhancementsController.cs
│   │   ├── GoldenDatasetsController.cs
│   │   ├── AuditController.cs
│   │   ├── LogsController.cs
│   │   └── AlertsController.cs
│   ├── Middleware/
│   │   ├── SecurityHeadersMiddleware.cs
│   │   └── AlertMetricsMiddleware.cs
│   ├── Services/
│   │   └── HealthChecks/
│   │       ├── AIProviderHealthCheck.cs
│   │       ├── OCRServiceHealthCheck.cs
│   │       ├── SemanticKernelHealthCheck.cs
│   │       └── FileStorageHealthCheck.cs
│   └── Properties/
│       └── launchSettings.json
│
├── DocN.Client/
│   ├── DocN.Client.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Components/
│   │   ├── App.razor
│   │   ├── Routes.razor
│   │   ├── _Imports.razor
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor
│   │   │   ├── NavMenu.razor
│   │   │   └── LoginDisplay.razor
│   │   └── Pages/
│   │       ├── Home.razor
│   │       ├── Documents.razor
│   │       ├── Search.razor
│   │       ├── SemanticChat.razor
│   │       ├── ConfigDiagnostics.razor
│   │       ├── GoldenDatasets.razor
│   │       ├── Connectors.razor
│   │       ├── AuditLogs.razor
│   │       ├── Login.razor
│   │       ├── Register.razor
│   │       └── ForgotPassword.razor
│   └── wwwroot/
│       ├── css/
│       ├── js/
│       └── images/
│
├── DocN.Data/
│   ├── DocN.Data.csproj
│   ├── ApplicationDbContext.cs
│   ├── DocArcContext.cs
│   ├── ApplicationDbContextFactory.cs
│   ├── DocArcContextFactory.cs
│   ├── DesignTimeDbContextFactory.cs
│   ├── Models/
│   │   ├── Document.cs
│   │   ├── DocumentChunk.cs
│   │   ├── DocumentTag.cs
│   │   ├── DocumentShare.cs
│   │   ├── DocumentStatistics.cs
│   │   ├── DocumentConnector.cs
│   │   ├── ApplicationUser.cs
│   │   ├── Tenant.cs
│   │   ├── UserGroup.cs
│   │   ├── AIConfiguration.cs
│   │   ├── GoldenDataset.cs
│   │   ├── GoldenDatasetEvaluationRecord.cs
│   │   ├── IngestionSchedule.cs
│   │   ├── IngestionLog.cs
│   │   ├── AuditLog.cs
│   │   ├── LogEntry.cs
│   │   ├── AppSettings.cs
│   │   ├── AgentTemplate.cs
│   │   ├── AgentUsageLog.cs
│   │   └── SimilarDocument.cs
│   ├── Migrations/
│   │   └── [EF Core migrations...]
│   ├── Services/
│   │   ├── DocumentService.cs
│   │   ├── ChunkingService.cs
│   │   ├── EmbeddingService.cs
│   │   ├── CategoryService.cs
│   │   ├── FileProcessingService.cs
│   │   ├── TesseractOCRService.cs
│   │   ├── LogService.cs
│   │   ├── MultiProviderAIService.cs
│   │   ├── SemanticRAGService.cs
│   │   ├── EnhancedAgentRAGService.cs
│   │   ├── HybridSearchService.cs
│   │   ├── BM25Service.cs
│   │   ├── SemanticCacheService.cs
│   │   ├── MultiHopSearchService.cs
│   │   ├── SemanticChunkingService.cs
│   │   ├── RetrievalMetricsService.cs
│   │   ├── EmbeddingFineTuningService.cs
│   │   ├── RAGQualityService.cs
│   │   ├── RAGASMetricsService.cs
│   │   ├── GoldenDatasetService.cs
│   │   ├── QueryIntentClassifier.cs
│   │   ├── DocumentStatisticsService.cs
│   │   ├── StatisticalAnswerGenerator.cs
│   │   ├── AuditService.cs
│   │   ├── AlertingService.cs
│   │   ├── CacheService.cs
│   │   ├── DistributedCacheService.cs
│   │   ├── ConnectorService.cs
│   │   ├── IngestionService.cs
│   │   ├── IngestionSchedulerHelper.cs
│   │   ├── IngestionSchedulerService.cs
│   │   ├── BatchEmbeddingProcessor.cs
│   │   ├── DocumentWorkflowService.cs
│   │   ├── SemanticKernelFactory.cs
│   │   ├── KernelProvider.cs
│   │   ├── ApplicationSeeder.cs
│   │   ├── DatabaseSeeder.cs
│   │   ├── HyDEService.cs
│   │   ├── Agents/
│   │   │   ├── RetrievalAgent.cs
│   │   │   ├── SynthesisAgent.cs
│   │   │   ├── ClassificationAgent.cs
│   │   │   └── AgentOrchestrator.cs
│   │   └── Connectors/
│   │       ├── BaseConnectorHandler.cs
│   │       ├── SharePointConnectorHandler.cs
│   │       ├── OneDriveConnectorHandler.cs
│   │       ├── GoogleDriveConnectorHandler.cs
│   │       ├── FtpConnectorHandler.cs
│   │       ├── SftpConnectorHandler.cs
│   │       └── LocalFolderConnectorHandler.cs
│   ├── Configuration/
│   │   └── BatchProcessingConfiguration.cs
│   ├── Constants/
│   │   └── [Constants...]
│   ├── Utilities/
│   │   └── [Utility classes...]
│   └── Jobs/
│       └── [Hangfire job classes...]
│
└── DocN.Core/
    ├── DocN.Core.csproj
    ├── Interfaces/
    │   ├── IDocumentService.cs
    │   ├── IChunkingService.cs
    │   ├── IEmbeddingService.cs
    │   ├── ISemanticRAGService.cs
    │   ├── IMultiProviderAIService.cs
    │   ├── ICacheService.cs
    │   ├── IDistributedCacheService.cs
    │   ├── IHybridSearchService.cs
    │   ├── IBM25Service.cs
    │   ├── ISemanticCacheService.cs
    │   ├── IMultiHopSearchService.cs
    │   ├── ISemanticChunkingService.cs
    │   ├── IRetrievalMetricsService.cs
    │   ├── IEmbeddingFineTuningService.cs
    │   ├── IRAGQualityService.cs
    │   ├── IRAGASMetricsService.cs
    │   ├── IGoldenDatasetService.cs
    │   ├── IQueryIntentClassifier.cs
    │   ├── IDocumentStatisticsService.cs
    │   ├── IStatisticalAnswerGenerator.cs
    │   ├── IAuditService.cs
    │   ├── IAlertingService.cs
    │   ├── IConnectorService.cs
    │   ├── IIngestionService.cs
    │   ├── IIngestionSchedulerHelper.cs
    │   ├── IDocumentWorkflowService.cs
    │   ├── ISemanticKernelFactory.cs
    │   ├── IKernelProvider.cs
    │   ├── IRetrievalAgent.cs
    │   ├── ISynthesisAgent.cs
    │   ├── IClassificationAgent.cs
    │   └── IAgentOrchestrator.cs
    ├── AI/
    │   └── Configuration/
    │       ├── AlertManagerConfiguration.cs
    │       └── EnhancedRAGConfiguration.cs
    ├── SemanticKernel/
    │   └── [SK plugins and configurations...]
    └── Extensions/
        └── [Extension methods...]
```

---

**FINE DOCUMENTO**

**Versione**: 1.0  
**Data Creazione**: 2026-01-22  
**Autore**: Sistema AI  
**Scopo**: Prompt per generazione progetto C# Blazor con RAG  
