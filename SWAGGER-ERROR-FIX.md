# 🔧 Fix Swagger 500 Error

## Problema

Quando si accede a `https://localhost:5211/swagger/index.html`, appare l'errore:

```
Failed to load API definition.
Fetch error
response status is 500 /swagger/v1/swagger.json
```

## Causa

L'errore 500 si verifica quando Swagger cerca di generare la documentazione OpenAPI ma incontra un problema nella configurazione dei controller. Le cause comuni includono:

1. **Route ambigue** - Due o più endpoint con lo stesso percorso HTTP
2. **Tipi di ritorno non supportati** - Tipi che Swagger non riesce a serializzare
3. **Parametri con configurazione non valida** - Parametri senza attributi corretti ([FromBody], [FromRoute], ecc.)
4. **Errori database durante l'avvio** - Se i controller dipendono dal database e questo non è accessibile

## Diagnosi

### Passo 1: Controlla i log del Server

Quando il server è in esecuzione, controlla i log nella console. L'errore dovrebbe mostrare quale controller o action sta causando il problema.

```bash
# Nel terminale dove il Server è in esecuzione, cerca errori come:
# "Swagger generation error at controller X"
# oppure
# "Ambiguous actions detected"
```

### Passo 2: Testa la generazione di Swagger manualmente

```bash
cd DocN.Server
dotnet build
# Se il build fallisce, questo potrebbe indicare problemi di configurazione
```

### Passo 3: Verifica la connessione al database

Il server attualmente mostra errori di connessione al database:
```
A network-related or instance-specific error occurred while establishing a connection to SQL Server.
```

Questo potrebbe essere la causa principale del problema Swagger se i controller dipendono dal database per inizializzarsi.

## Soluzione

### Soluzione 1: Fix Connessione Database (CONSIGLIATO)

La causa più probabile è la mancanza di connessione al database SQL Server. Il server non riesce a connettersi a SQL Server, il che potrebbe impedire a Swagger di generare correttamente la documentazione se i controller hanno dipendenze dal database.

**Passaggi:**

1. **Verifica che SQL Server sia in esecuzione**
   ```bash
   # Windows
   services.msc
   # Cerca "SQL Server (MSSQLSERVER)" e assicurati che sia avviato
   
   # oppure da PowerShell
   Get-Service MSSQLSERVER
   ```

2. **Aggiorna la connection string in `appsettings.json`**
   
   Il file è stato creato automaticamente in:
   ```
   DocN.Server/bin/Debug/net10.0/appsettings.json
   ```
   
   Aprilo e verifica la connection string:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True"
     }
   }
   ```
   
   **Opzioni comuni per la connection string:**
   
   - **SQL Server LocalDB (per sviluppo):**
     ```
     Server=(localdb)\\mssqllocaldb;Database=DocNDb;Trusted_Connection=True;
     ```
   
   - **SQL Server Express:**
     ```
     Server=localhost\\SQLEXPRESS;Database=DocNDb;Trusted_Connection=True;
     ```
   
   - **SQL Server con utente/password:**
     ```
     Server=localhost;Database=DocNDb;User Id=your_user;Password=your_password;
     ```

3. **Crea il database se non esiste**
   ```bash
   cd DocN.Server
   dotnet ef database update
   ```

### Soluzione 2: Disabilita temporaneamente la validazione database

Se non hai SQL Server disponibile e vuoi solo testare Swagger, puoi modificare `Program.cs` per permettere al server di avviarsi senza database:

**File:** `DocN.Server/Program.cs`

Trova la sezione che esegue le migrations (intorno alla linea 720-730) e commenta il blocco:

```csharp
// Commenta queste righe temporaneamente:
/*
try
{
    Log.Information("Checking for pending database migrations...");
    var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
    // ... resto del codice
}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred while applying database migrations");
    throw; // <-- RIMUOVI O COMMENTA QUESTO throw
}
*/
```

**⚠️ ATTENZIONE:** Questa è solo una soluzione temporanea per testare Swagger. L'applicazione non funzionerà correttamente senza il database.

### Soluzione 3: Verifica controllers specifici

Se il database funziona ma Swagger continua a dare errore, potrebbe esserci un problema in un controller specifico. Cerca nei controller per:

1. **Route duplicate:**
   ```csharp
   // SBAGLIATO - due metodi con la stessa route
   [HttpGet("documents")]
   public async Task<IActionResult> GetDocuments() { }
   
   [HttpGet("documents")]
   public async Task<IActionResult> GetAllDocuments() { }
   ```

2. **Parametri ambigui:**
   ```csharp
   // SBAGLIATO - manca [FromBody] o [FromRoute]
   [HttpPost("upload")]
   public async Task<IActionResult> Upload(DocumentModel doc) { }
   
   // CORRETTO
   [HttpPost("upload")]
   public async Task<IActionResult> Upload([FromBody] DocumentModel doc) { }
   ```

3. **Tipi di ritorno problematici:**
   ```csharp
   // Assicurati che tutti i modelli siano serializzabili
   public class MyModel
   {
       public string Property { get; set; } // OK
       public Stream FileStream { get; set; } // Potenzialmente problematico per Swagger
   }
   ```

## Test Dopo la Fix

Una volta risolto il problema:

1. **Avvia il Server:**
   ```bash
   cd DocN.Server
   dotnet run --launch-profile https
   ```

2. **Attendi che il server sia pronto:**
   ```
   Now listening on: https://localhost:5211
   Application started.
   ```

3. **Apri Swagger nel browser:**
   ```
   https://localhost:5211/swagger/index.html
   ```

4. **Verifica che carichi senza errori:**
   - Dovresti vedere la lista di tutti gli endpoint API
   - Puoi espandere ogni endpoint e vedere i dettagli
   - Puoi testare le API direttamente da Swagger UI

## Riepilogo

**Causa più probabile:** Connessione database mancante

**Soluzione principale:**
1. Assicurati che SQL Server sia in esecuzione
2. Configura correttamente la connection string
3. Esegui le migrations del database
4. Riavvia il Server

**Test finale:** Apri `https://localhost:5211/swagger/index.html` e verifica che carichi senza errori.

---

**Ultimo aggiornamento:** 7 Febbraio 2026  
**Status:** In attesa di test con database configurato
