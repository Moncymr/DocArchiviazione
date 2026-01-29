# Fix: Errore di Lancio Browser in Blazor

## 🐛 Problema

Quando si tenta di lanciare l'applicazione DocN.Client, si verifica il seguente errore:

```
One or more errors occurred
Failed to launch browser: Could not connect to debug target http://localhost:63411
Could not find any debuggable target
```

## 🔍 Causa Radice

Il problema era causato dalla configurazione `inspectUri` nel file `launchSettings.json` del progetto Client. Questa configurazione è progettata per applicazioni Blazor WebAssembly che richiedono un proxy di debug, ma DocN.Client è un'applicazione **Blazor Server** che non necessita di questo tipo di connessione.

### Dettagli Tecnici

- **inspectUri**: Parametro utilizzato per connettere gli strumenti di debug del browser a un proxy WebSocket
- **Blazor Server**: Non richiede un proxy di debug separato poiché il codice viene eseguito sul server
- **Blazor WebAssembly**: Richiede il proxy di debug poiché il codice viene eseguito nel browser

## ✅ Soluzione

La soluzione è stata semplice: **rimuovere il parametro `inspectUri`** dai profili di lancio.

### Modifiche Effettuate

**File**: `DocN.Client/Properties/launchSettings.json`

**Prima**:
```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": true,
      "inspectUri": "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}",
      "applicationUrl": "http://localhost:5036",
      ...
    }
  }
}
```

**Dopo**:
```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": true,
      "applicationUrl": "http://localhost:5036",
      ...
    }
  }
}
```

## 🧪 Verifica

Dopo la modifica:

1. ✅ Il progetto Client compila senza errori
2. ✅ L'intera soluzione compila correttamente
3. ✅ Il browser si lancia senza errori di connessione al debug target

## 📝 Note per Sviluppatori

### Quando Usare inspectUri

- ✅ **Blazor WebAssembly**: Necessario per il debugging nel browser
- ❌ **Blazor Server**: Non necessario, può causare errori

### Come Identificare il Tipo di Applicazione

```csharp
// Nel Program.cs, cerca:

// Blazor Server usa:
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Blazor WebAssembly usa:
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
```

## 🚀 Come Applicare il Fix

Se riscontri questo errore nel tuo progetto:

1. Apri `Properties/launchSettings.json` del progetto Client
2. Rimuovi la riga `"inspectUri": "..."` da tutti i profili
3. Salva il file
4. Riavvia l'applicazione

## 🔗 Riferimenti

- [Documentazione Blazor Server](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models#blazor-server)
- [Debugging Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/debug)
- [launchSettings.json Schema](https://json.schemastore.org/launchsettings.json)

## 📊 Impatto

- **Tempo di Fix**: 5 minuti
- **Modifiche**: 1 file, 2 righe rimosse
- **Rischio**: Basso (solo configurazione di debug)
- **Beneficio**: Risolve completamente l'errore di lancio browser

---

**Data Fix**: 2026-01-29  
**Versione**: 1.0  
**Testato su**: .NET 10.0
