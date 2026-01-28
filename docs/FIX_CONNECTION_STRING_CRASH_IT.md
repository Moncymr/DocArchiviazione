# Fix Crash: GetConnectionString() e ExecuteSqlRawAsync()

## 🔴 Problema Risolto

L'applicazione crashava con `System.AggregateException` quando tentava di accedere al database durante lo startup, specificamente in queste righe:

```csharp
var connectionString = _context.Database.GetConnectionString();  // ❌ CRASH QUI
await _context.Database.ExecuteSqlRawAsync("SELECT 1");          // ❌ O CRASH QUI
```

### Sintomi
- ✅ Build compila senza errori
- ❌ Applicazione crasha durante lo startup
- ❌ Server termina con exit code 0
- ❌ Client termina con exit code -1 (0xffffffff)
- ❌ In QuickWatch: `error CS1073: Unexpected token 'connectionString'`
- ❌ Crash avviene PRIMA che il try-catch possa gestire l'errore

### Debug Confuso
Quando provi a debuggare in QuickWatch/Watch Window:
```csharp
_context.Database.GetConnectionString()  // ✅ Mostra il valore corretto
var connectionString = _context.Database.GetConnectionString();  // ❌ error CS1073
```

Questo è un **errore del debugger** (non può valutare espressioni async con assegnazioni), MA il vero problema è che l'applicazione crasha comunque a runtime!

## 🎯 Causa Principale

Ogni accesso a `_context.Database` fa partire la **validazione del modello EF Core**:

1. Accedi a `_context.Database.GetConnectionString()`
2. EF Core dice: "Aspetta, prima devo validare il modello!"
3. EF Core controlla tutte le tabelle nel DbContext
4. Trova problemi:
   - Tabella `Notifications` non esiste
   - Tabella `NotificationPreferences` non esiste  
   - Schema non corrisponde al modello
5. EF Core: **CRASH!** 💥
6. Il try-catch non riesce a gestirlo perché il crash avviene durante l'inizializzazione del DbContext

### Metodi che Crashano
Tutti questi crashano perché accedono a `_context.Database`:
```csharp
❌ _context.Database.GetConnectionString()
❌ _context.Database.ExecuteSqlRawAsync("SELECT 1")
❌ _context.Database.CanConnectAsync()
❌ Qualsiasi accesso a _context.Database.xxx
```

## ✅ Soluzione Implementata

### Idea Chiave
**Non accedere MAI a `_context.Database` durante i controlli di connessione!**

Invece, ottieni la connection string da `IConfiguration` e usa `SqlConnection` direttamente.

### Codice Prima (Crashava)

```csharp
public class ApplicationSeeder
{
    private readonly ApplicationDbContext _context;
    
    public ApplicationSeeder(ApplicationDbContext context, ...)
    {
        _context = context;
    }
    
    private async Task<bool> CanConnectToDatabaseAsync()
    {
        try
        {
            // ❌ CRASH: EF Core valida il modello
            var connectionString = _context.Database.GetConnectionString();
            await _context.Database.ExecuteSqlRawAsync("SELECT 1");
            return true;
        }
        catch (Exception ex)
        {
            // ❌ Non arriva mai qui - crash prima del try-catch!
            return false;
        }
    }
}
```

### Codice Dopo (Funziona!)

```csharp
public class ApplicationSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;  // ✅ AGGIUNTO
    
    public ApplicationSeeder(
        ApplicationDbContext context, 
        IConfiguration configuration,  // ✅ INIETTATO
        ...)
    {
        _context = context;
        _configuration = configuration;
    }
    
    private async Task<bool> CanConnectToDatabaseAsync()
    {
        try
        {
            // ✅ Ottieni connection string da configurazione (NO EF Core)
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("Connection string non configurata");
                return false;
            }

            // ✅ Test connessione con SqlConnection diretto (NO EF Core)
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                _logger.LogInformation("✅ Connessione database riuscita");
                return true;
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // ✅ Ora le eccezioni vengono gestite correttamente!
            _logger.LogError(sqlEx, "❌ Errore SQL: {Message} (Numero: {Number})", 
                sqlEx.Message, sqlEx.Number);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Errore connessione database");
            return false;
        }
    }
}
```

### Modifiche Applicate

**File Modificati:**
1. ✅ `DocN.Data/Services/ApplicationSeeder.cs`
2. ✅ `DocN.Server/Services/DatabaseSeeder.cs`

**Cambiamenti:**
1. ✅ Aggiunto `IConfiguration` al costruttore
2. ✅ Rimosso accesso a `_context.Database`
3. ✅ Connection string da `_configuration.GetConnectionString()`
4. ✅ Test connessione con `SqlConnection` diretto
5. ✅ Messaggi di errore migliorati

## 🎉 Benefici

### Prima (Crashava)
❌ Crash misterioso durante startup  
❌ Nessun messaggio di errore utile  
❌ Try-catch non funzionava  
❌ Impossibile avviare l'applicazione  
❌ QuickWatch confuso  

### Dopo (Funziona!)
✅ Applicazione parte sempre  
✅ Test connessione semplice e affidabile  
✅ Messaggi di errore chiari  
✅ Try-catch gestisce correttamente gli errori  
✅ Seeding saltato se connessione fallisce  
✅ Nessuna validazione EF Core durante il test  

## 🧪 Come Testare

### 1. Aggiorna il Codice
```bash
git pull origin copilot/implement-notification-center
```

### 2. Pulisci e Rebuilda
```bash
dotnet clean
dotnet build
```

### 3. Avvia l'Applicazione
```bash
dotnet run --project DocN.Server
```
oppure premi **F5** in Visual Studio

### 4. Risultato Atteso

**✅ Connessione Riuscita:**
```
info: DocN.Data.Services.ApplicationSeeder[0]
      Testing database connection...
info: DocN.Data.Services.ApplicationSeeder[0]
      ✅ Database connection successful
info: DocN.Data.Services.ApplicationSeeder[0]
      Database seeding completed successfully
```

**❌ Se Connessione Fallisce (ma non crasha!):**
```
error: DocN.Data.Services.ApplicationSeeder[0]
      ❌ SQL Exception: Cannot open database "DocNDb" (Error: 4060, State: 1)
warn: DocN.Data.Services.ApplicationSeeder[0]
      Cannot connect to database. Skipping seeding.
```

L'applicazione **continua a partire** anche se il database non è disponibile!

## 🔍 Troubleshooting

### "Connection string non configurata"
**Problema:** `_configuration.GetConnectionString("DefaultConnection")` restituisce null

**Soluzione:**
1. Controlla `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=NTSPJ-060-02\\SQL2025;Database=DocNDb;Integrated Security=True;..."
  }
}
```

2. Verifica che il file `appsettings.json` esista nella directory di output
3. Verifica che `Copy to Output Directory` sia impostato su "Copy if newer"

### "Cannot open database"
**Problema:** SQL Server non trova il database

**Soluzione:**
1. Esegui lo script di migrazione: `docs/database/migrations/04_add_notifications.sql`
2. Verifica che il database `DocNDb` esista
3. Verifica i permessi dell'utente

### QuickWatch ancora mostra errori
**Normale!** Il debugger ha limitazioni con espressioni async. L'importante è che l'applicazione non crashi più a runtime.

## 📝 Spiegazione Tecnica

### Perché `_context.Database` Crasha?

Quando accedi a qualsiasi proprietà di `DbContext.Database`, EF Core:

1. **Lazy Initialization**: Inizializza il modello se non è già inizializzato
2. **Model Validation**: Confronta il modello C# con lo schema del database
3. **Convention Discovery**: Applica le convenzioni e le configurazioni Fluent API
4. **Relationship Mapping**: Valida tutte le foreign key e navigazioni
5. **Database Schema Check**: Verifica che le tabelle esistano e corrispondano

Se **qualsiasi** di questi passaggi fallisce (es. tabella mancante), EF Core lancia un'eccezione durante l'inizializzazione che non può essere catturata dal try-catch della tua funzione.

### Perché SqlConnection Funziona?

`SqlConnection` è una classe ADO.NET di basso livello:

1. **No Model Validation**: Non sa nulla del tuo modello EF Core
2. **Simple Connection Test**: Apre solo una connessione TCP al server
3. **Catchable Exceptions**: Le eccezioni vengono lanciate nel try-catch della tua funzione
4. **No Dependencies**: Non dipende da DbContext o configurazioni EF Core

## 📚 Risorse Aggiuntive

- [EF Core Model Validation](https://learn.microsoft.com/en-us/ef/core/modeling/)
- [DbContext Lifetime](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [SqlConnection Class](https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection)

## ✅ Commit

**Commit:** eedd60c  
**Messaggio:** Fix crash by getting connection string from IConfiguration instead of DbContext

**File Modificati:**
- `DocN.Data/Services/ApplicationSeeder.cs`
- `DocN.Server/Services/DatabaseSeeder.cs`

---

**Status:** ✅ **RISOLTO**  
**Data:** 2026-01-28  
**Testato:** ✅ Build succeeds, application starts without crashes
