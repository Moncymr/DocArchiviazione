# Analisi Funzioni con Più di Due Parametri
## Analysis of Functions with More Than Two Parameters

**Data Analisi / Analysis Date:** 2026-01-22

---

## Risposta alla Domanda / Answer to the Question

**Domanda:** Ci sono funzioni che hanno più di due parametri in input?  
**Question:** Are there functions that have more than two parameters as input?

**Risposta:** **Sì**, nel codebase sono presenti **oltre 40 metodi** con 3 o più parametri.  
**Answer:** **Yes**, there are **over 40 methods** in the codebase with 3 or more parameters.

---

## Dettaglio Metodi per Categoria / Methods by Category

### 1. Document Service Methods (11 metodi)

#### File: `DocN.Data/Services/DocumentService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `ShareDocumentAsync` | **4 parametri**: `int documentId, string shareWithUserId, DocumentPermission permission, string currentUserId` | Gestione condivisione documenti |
| `ShareDocumentWithGroupAsync` | **4 parametri**: `int documentId, int groupId, DocumentPermission permission, string currentUserId` | Condivisione con gruppi |
| `RemoveUserShareAsync` | **3 parametri**: `int documentId, string userId, string currentUserId` | Rimozione condivisione utente |
| `RemoveGroupShareAsync` | **3 parametri**: `int documentId, int groupId, string currentUserId` | Rimozione condivisione gruppo |
| `UpdateDocumentVisibilityAsync` | **3 parametri**: `int documentId, DocumentVisibility visibility, string userId` | Aggiornamento visibilità |
| `GetUserDocumentsAsync` | **3 parametri**: `string userId, int page = 1, int pageSize = 20` | Paginazione documenti utente |
| `SearchDocumentsAsync` | **4 parametri**: `string searchTerm, string userId, int page = 1, int pageSize = 20` | Ricerca con paginazione |
| `GetDocumentsByCategoryAsync` | **4 parametri**: `int categoryId, string userId, int page = 1, int pageSize = 20` | Filtraggio per categoria |
| `GetDocumentsByDateRangeAsync` | **5 parametri**: `DateTime startDate, DateTime endDate, string userId, int page = 1, int pageSize = 20` | Filtraggio per intervallo date |
| `UpdateDocumentMetadataAsync` | **3 parametri**: `int documentId, Dictionary<string, string> metadata, string userId` | Aggiornamento metadati |
| `MoveDocumentToCategoryAsync` | **3 parametri**: `int documentId, int newCategoryId, string userId` | Spostamento categoria |

---

### 2. Connector Handler Methods (18 metodi)

Metodi ripetuti in 6 handler diversi (GoogleDrive, FTP, LocalFolder, SFTP, SharePoint, OneDrive):

#### Files:
- `DocN.Data/Services/Connectors/GoogleDriveConnectorHandler.cs`
- `DocN.Data/Services/Connectors/FtpConnectorHandler.cs`
- `DocN.Data/Services/Connectors/LocalFolderConnectorHandler.cs`
- `DocN.Data/Services/Connectors/SftpConnectorHandler.cs`
- `DocN.Data/Services/Connectors/SharePointConnectorHandler.cs`
- `DocN.Data/Services/Connectors/OneDriveConnectorHandler.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `TestConnectionAsync` | **2 parametri**: `string configuration, string? encryptedCredentials` | Test connessione |
| `ListFilesAsync` | **3 parametri**: `string configuration, string? encryptedCredentials, string? path = null` | Elenco file |
| `DownloadFileAsync` | **3 parametri**: `string configuration, string? encryptedCredentials, string filePath` | Download file |

**Totale:** 3 metodi × 6 handler = 18 implementazioni

---

### 3. AI/RAG Service Methods (12+ metodi)

#### File: `DocN.Core/Services/EnhancedAgentRAGService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `GenerateResponseAsync` | **5 parametri**: `string query, string userId, int? conversationId = null, List<int>? specificDocumentIds = null, int topK = 5` | Generazione risposta RAG con molte opzioni |

#### File: `DocN.Core/Services/RetrievalMetricsService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `CalculateNDCG` | **3 parametri**: `List<int> retrievedDocIds, Dictionary<int, double> relevanceScores, int k = 10` | Calcolo metriche NDCG |
| `CalculateMRR` | **2 parametri**: `List<int> retrievedDocIds, List<int> relevantDocIds` | Calcolo Mean Reciprocal Rank |
| `CalculatePrecisionAtK` | **3 parametri**: `List<int> retrievedDocIds, List<int> relevantDocIds, int k` | Precision at K |
| `CalculateRecallAtK` | **3 parametri**: `List<int> retrievedDocIds, List<int> relevantDocIds, int k` | Recall at K |

#### File: `DocN.Core/Services/ContextualCompressionService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `CompressContextAsync` | **3 parametri**: `string query, List<string> retrievedChunks, int targetTokenCount = 2000` | Compressione contestuale |

#### File: `DocN.Data/Services/MultiProviderAIService.cs` & `CategoryService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `SuggestCategoryAsync` | **2 parametri**: `string fileName, string extractedText` | Suggerimento categoria (al limite) |
| `AnalyzeDocumentContentAsync` | **3 parametri**: `string filePath, string extractedText, string? fileExtension = null` | Analisi contenuto |

#### File: `DocN.Core/Services/ChunkingService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `ChunkText` | **3 parametri**: `string text, int chunkSize = 1000, int overlap = 200` | Chunking del testo |
| `ChunkDocument` | **3 parametri**: `string documentText, int chunkSize = 1000, int overlap = 200` | Chunking documento |

#### File: `DocN.Core/Services/BM25Service.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `CalculateScore` | **3 parametri**: `string query, string documentText, Dictionary<string, double>? documentFieldWeights = null` | Calcolo score BM25 |

---

### 4. Utility & Helper Methods (8+ metodi)

#### File: `DocN.Data/Services/LogService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `LogInfoAsync` | **5 parametri**: `string category, string message, string? details = null, string? userId = null, string? fileName = null` | Log informativo |
| `LogWarningAsync` | **5 parametri**: `string category, string message, string? details = null, string? userId = null, string? fileName = null` | Log warning |
| `LogErrorAsync` | **5 parametri**: `string category, string message, string? details = null, string? userId = null, string? fileName = null` | Log errore |

#### File: `DocN.Core/Services/AlertingService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `CreateAlertAsync` | **4 parametri**: `string alertType, string message, AlertSeverity severity, Dictionary<string, object>? metadata = null` | Creazione alert |
| `AcknowledgeAlertAsync` | **3 parametri**: `string alertId, string acknowledgedBy, CancellationToken cancellationToken = default` | Conferma alert |
| `ResolveAlertAsync` | **3 parametri**: `string alertId, string resolvedBy, CancellationToken cancellationToken = default` | Risoluzione alert |

#### File: `DocN.Server/Controllers/DocumentController.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `UploadDocument` (azione controller) | **3+ parametri**: `IFormFile file, [FromForm] int? categoryId, [FromForm] string? tags` | Upload con metadati |

---

## Statistiche / Statistics

| Categoria | Numero Metodi | Range Parametri |
|-----------|---------------|-----------------|
| Document Service | 11 | 3-5 parametri |
| Connector Handlers | 18 | 2-3 parametri |
| AI/RAG Services | 12+ | 2-5 parametri |
| Utility/Helper | 8+ | 3-5 parametri |
| **TOTALE** | **49+** | **2-5 parametri** |

---

## Analisi dei Pattern / Pattern Analysis

### Pattern Comuni / Common Patterns

1. **Metodi con Paginazione** (3-4 parametri)
   ```csharp
   GetUserDocumentsAsync(string userId, int page = 1, int pageSize = 20)
   ```
   - Parametri base + opzioni paginazione

2. **Metodi con Autenticazione/Autorizzazione** (3-4 parametri)
   ```csharp
   ShareDocumentAsync(int documentId, string shareWithUserId, 
                      DocumentPermission permission, string currentUserId)
   ```
   - ID risorsa + ID destinatario + permessi + utente corrente

3. **Metodi con Configurazione** (3 parametri)
   ```csharp
   ListFilesAsync(string configuration, string? encryptedCredentials, string? path = null)
   ```
   - Configurazione + credenziali + parametri opzionali

4. **Metodi di Logging** (5 parametri con molti opzionali)
   ```csharp
   LogInfoAsync(string category, string message, string? details = null, 
                string? userId = null, string? fileName = null)
   ```
   - Informazioni base + contesto opzionale

---

## Raccomandazioni / Recommendations

### 🔴 Metodi da Refactorare / Methods to Refactor

#### 1. **LogService Methods** (5 parametri)
**Problema:** Troppi parametri, molti opzionali  
**Soluzione:** Utilizzare un oggetto `LogContext` o `LogEntry`

```csharp
// Prima / Before
LogInfoAsync(string category, string message, string? details = null, 
             string? userId = null, string? fileName = null)

// Dopo / After
public class LogContext
{
    public string Category { get; set; }
    public string Message { get; set; }
    public string? Details { get; set; }
    public string? UserId { get; set; }
    public string? FileName { get; set; }
}

LogInfoAsync(LogContext context)
```

#### 2. **EnhancedAgentRAGService.GenerateResponseAsync** (5 parametri)
**Problema:** Molti parametri opzionali, difficile da estendere  
**Soluzione:** Utilizzare un oggetto `RAGRequestOptions`

```csharp
// Prima / Before
GenerateResponseAsync(string query, string userId, int? conversationId = null, 
                     List<int>? specificDocumentIds = null, int topK = 5)

// Dopo / After
public class RAGRequestOptions
{
    public string Query { get; set; }
    public string UserId { get; set; }
    public int? ConversationId { get; set; }
    public List<int>? SpecificDocumentIds { get; set; }
    public int TopK { get; set; } = 5;
}

GenerateResponseAsync(RAGRequestOptions options)
```

#### 3. **GetDocumentsByDateRangeAsync** (5 parametri)
**Problema:** Parametri di paginazione ripetuti in molti metodi  
**Soluzione:** Utilizzare un oggetto `PaginationOptions` o `DocumentQueryOptions`

```csharp
// Prima / Before
GetDocumentsByDateRangeAsync(DateTime startDate, DateTime endDate, 
                             string userId, int page = 1, int pageSize = 20)

// Dopo / After
public class DocumentDateRangeQuery
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string UserId { get; set; }
    public PaginationOptions Pagination { get; set; } = new();
}

public class PaginationOptions
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

GetDocumentsByDateRangeAsync(DocumentDateRangeQuery query)
```

### 🟡 Metodi Accettabili / Acceptable Methods

I seguenti metodi hanno 3-4 parametri ma sono accettabili:

1. **ShareDocumentAsync** (4 parametri) - Ogni parametro è essenziale
2. **ChunkText** (3 parametri) - Parametri con default chiari
3. **Connector methods** (2-3 parametri) - Interfaccia uniforme necessaria

### 🟢 Metodi Ben Progettati / Well-Designed Methods

Metodi che utilizzano già pattern object:
- Metodi che accettano oggetti di configurazione
- Metodi con 2 parametri o meno
- Metodi con parametri opzionali ben documentati

---

## Benefici del Refactoring / Refactoring Benefits

### Vantaggi / Advantages

1. **Manutenibilità** - Più facile aggiungere nuovi parametri
2. **Leggibilità** - Codice più chiaro con oggetti denominati
3. **Testabilità** - Più semplice creare oggetti di test
4. **Estensibilità** - Aggiungere nuovi campi senza modificare signature
5. **Validazione** - Logica di validazione centralizzata negli oggetti

### Svantaggi / Disadvantages

1. **Breaking Changes** - Richiede modifiche al codice esistente
2. **Verbosità** - Più classi da mantenere
3. **Learning Curve** - Nuovi sviluppatori devono imparare le nuove strutture

---

## Priorità di Refactoring / Refactoring Priority

### Alta Priorità / High Priority
- [ ] `LogService` methods (5 parametri, usati ovunque)
- [ ] `EnhancedAgentRAGService.GenerateResponseAsync` (5 parametri, API principale)
- [ ] `GetDocumentsByDateRangeAsync` (5 parametri)

### Media Priorità / Medium Priority
- [ ] Altri metodi DocumentService con 4+ parametri
- [ ] `CreateAlertAsync` con metadata dictionary

### Bassa Priorità / Low Priority
- [ ] Metodi con 3 parametri ben documentati
- [ ] Connector handlers (interfaccia uniforme necessaria)

---

## Conclusioni / Conclusions

**Risposta finale:** Sì, ci sono molte funzioni con più di 2 parametri nel codebase (49+).  
**Final Answer:** Yes, there are many functions with more than 2 parameters in the codebase (49+).

La maggior parte sono progettate ragionevolmente, ma alcuni metodi (specialmente quelli con 5 parametri) potrebbero beneficiare di refactoring utilizzando oggetti parametro.

Most are reasonably designed, but some methods (especially those with 5 parameters) could benefit from refactoring using parameter objects.

---

## Riferimenti / References

- **Clean Code** by Robert C. Martin - Raccomanda max 3 parametri
- **Code Complete** by Steve McConnell - Suggerisce oggetti per 4+ parametri
- **C# Coding Conventions** - Microsoft guidelines su parameter objects
