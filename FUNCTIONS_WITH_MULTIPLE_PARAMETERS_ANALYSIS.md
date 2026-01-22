# Analisi Funzioni con Più di Due Parametri
## Analysis of Functions with More Than Two Parameters

**Data Analisi / Analysis Date:** 2026-01-22

---

## Risposta alla Domanda / Answer to the Question

**Domanda:** Ci sono funzioni che hanno più di due parametri in input?  
**Question:** Are there functions that have more than two parameters as input?

**Risposta:** **Sì**, nel codebase sono presenti **28 metodi pubblici verificati** con 3 o più parametri.  
**Answer:** **Yes**, there are **28 verified public methods** in the codebase with 3 or more parameters.

---

## Dettaglio Metodi per Categoria / Methods by Category

### 1. Document Service Methods (6 metodi verificati)

#### File: `DocN.Data/Services/DocumentService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `ShareDocumentAsync` | **4 parametri**: `int documentId, string shareWithUserId, DocumentPermission permission, string currentUserId` | Gestione condivisione documenti |
| `ShareDocumentWithGroupAsync` | **4 parametri**: `int documentId, int groupId, DocumentPermission permission, string currentUserId` | Condivisione con gruppi |
| `RemoveUserShareAsync` | **3 parametri**: `int documentId, string userId, string currentUserId` | Rimozione condivisione utente |
| `RemoveGroupShareAsync` | **3 parametri**: `int documentId, int groupId, string currentUserId` | Rimozione condivisione gruppo |
| `UpdateDocumentVisibilityAsync` | **3 parametri**: `int documentId, DocumentVisibility visibility, string userId` | Aggiornamento visibilità |
| `GetUserDocumentsAsync` | **3 parametri**: `string userId, int page = 1, int pageSize = 20` | Paginazione documenti utente |

---

### 2. Connector Handler Methods (12 metodi - solo con 3+ parametri)

Metodi con 3+ parametri ripetuti in 6 handler diversi (GoogleDrive, FTP, LocalFolder, SFTP, SharePoint, OneDrive):

#### Files:
- `DocN.Data/Services/Connectors/GoogleDriveConnectorHandler.cs`
- `DocN.Data/Services/Connectors/FtpConnectorHandler.cs`
- `DocN.Data/Services/Connectors/LocalFolderConnectorHandler.cs`
- `DocN.Data/Services/Connectors/SftpConnectorHandler.cs`
- `DocN.Data/Services/Connectors/SharePointConnectorHandler.cs`
- `DocN.Data/Services/Connectors/OneDriveConnectorHandler.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `ListFilesAsync` | **3 parametri**: `string configuration, string? encryptedCredentials, string? path = null` | Elenco file |
| `DownloadFileAsync` | **3 parametri**: `string configuration, string? encryptedCredentials, string filePath` | Download file |

**Totale:** 2 metodi × 6 handler = 12 implementazioni

**Nota:** `TestConnectionAsync` ha solo 2 parametri quindi non è incluso in questa analisi.

---

### 3. AI/RAG Service Methods (4 metodi verificati)

#### File: `DocN.Data/Services/EnhancedAgentRAGService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `GenerateResponseAsync` | **5 parametri**: `string query, string userId, int? conversationId = null, List<int>? specificDocumentIds = null, int topK = 5` | Generazione risposta RAG con molte opzioni |
| `GenerateStreamingResponseAsync` | **4 parametri**: `string query, string userId, int? conversationId = null, List<int>? specificDocumentIds = null` | Generazione risposta streaming |
| `SearchDocumentsAsync` | **4 parametri**: `string query, string userId, int topK = 10, double minSimilarity = 0.7` | Ricerca documenti semantica |
| `SearchDocumentsWithEmbeddingAsync` | **4 parametri**: `float[] queryEmbedding, string userId, int topK = 10, double minSimilarity = 0.7` | Ricerca con embedding pre-calcolato |

---

### 4. Utility & Helper Methods (7 metodi verificati)

#### File: `DocN.Data/Services/LogService.cs`

| Metodo | Parametri | Note |
|--------|-----------|------|
| `LogInfoAsync` | **5 parametri**: `string category, string message, string? details = null, string? userId = null, string? fileName = null` | Log informativo |
| `LogWarningAsync` | **5 parametri**: `string category, string message, string? details = null, string? userId = null, string? fileName = null` | Log warning |
| `LogErrorAsync` | **6 parametri**: `string category, string message, string? details = null, string? userId = null, string? fileName = null, string? stackTrace = null` | Log errore con stack trace |
| `LogDebugAsync` | **5 parametri**: `string category, string message, string? details = null, string? userId = null, string? fileName = null` | Log debug |
| `GetLogsAsync` | **4 parametri**: `string? category = null, string? userId = null, DateTime? fromDate = null, int maxRecords = 100` | Recupero log con filtri |
| `GetUploadLogsAsync` | **3 parametri**: `string? userId = null, DateTime? fromDate = null, int maxRecords = 100` | Recupero log upload |

**Nota:** AlertingService non ha metodi pubblici con 3+ parametri. CreateAlertAsync, AcknowledgeAlertAsync e ResolveAlertAsync non sono presenti o hanno firme diverse.

---

## Statistiche / Statistics

| Categoria | Numero Metodi | Range Parametri |
|-----------|---------------|-----------------|
| Document Service | 6 | 3-4 parametri |
| Connector Handlers | 12 (2 metodi×6 handler) | 3 parametri |
| AI/RAG Services | 4 | 4-5 parametri |
| Utility/Helper (LogService) | 6 | 3-6 parametri |
| **TOTALE** | **28** | **3-6 parametri** |

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

#### 3. **LogService.LogErrorAsync** (6 parametri)
**Problema:** Troppi parametri opzionali, difficile da estendere  
**Soluzione:** Utilizzare un oggetto `LogEntry` o pattern builder

```csharp
// Prima / Before
LogErrorAsync(string category, string message, string? details = null, 
             string? userId = null, string? fileName = null, string? stackTrace = null)

// Dopo / After
public class LogEntry
{
    public string Category { get; set; }
    public string Message { get; set; }
    public string? Details { get; set; }
    public string? UserId { get; set; }
    public string? FileName { get; set; }
    public string? StackTrace { get; set; }
}

LogErrorAsync(LogEntry entry)

// Oppure con pattern Builder
new LogEntryBuilder()
    .WithCategory("Upload")
    .WithMessage("File upload failed")
    .WithUserId(userId)
    .WithStackTrace(ex.StackTrace)
    .LogError();
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
- [ ] `LogService` methods (5-6 parametri, usati frequentemente in tutto il codebase)
- [ ] `EnhancedAgentRAGService.GenerateResponseAsync` (5 parametri, API principale RAG)
- [ ] `LogService.LogErrorAsync` (6 parametri, il metodo con più parametri)

### Media Priorità / Medium Priority
- [ ] Metodi EnhancedAgentRAGService con 4 parametri (SearchDocumentsAsync, etc.)
- [ ] ShareDocumentAsync e ShareDocumentWithGroupAsync (4 parametri)

### Bassa Priorità / Low Priority
- [ ] Metodi con 3 parametri ben documentati
- [ ] Connector handlers (interfaccia uniforme necessaria)

---

## Conclusioni / Conclusions

**Risposta finale:** Sì, ci sono 28 metodi verificati con più di 2 parametri nel codebase.  
**Final Answer:** Yes, there are 28 verified methods with more than 2 parameters in the codebase.

La maggior parte sono progettate ragionevolmente, ma alcuni metodi (specialmente quelli con 5 parametri) potrebbero beneficiare di refactoring utilizzando oggetti parametro.

Most are reasonably designed, but some methods (especially those with 5 parameters) could benefit from refactoring using parameter objects.

---

## Riferimenti / References

- **Clean Code** by Robert C. Martin - Raccomanda max 3 parametri
- **Code Complete** by Steve McConnell - Suggerisce oggetti per 4+ parametri
- **C# Coding Conventions** - Microsoft guidelines su parameter objects
