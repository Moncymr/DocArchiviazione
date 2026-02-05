# 🚀 DocN - Document Archive System

> **Sistema di gestione documentale intelligente con ricerca semantica e AI**

## ⚡ Quick Start - Avvio Rapido

### Visual Studio (Raccomandato)
Apri `Doc_archiviazione.sln` in Visual Studio e premi **F5**

Il sistema è configurato per avviare automaticamente Server e Client nell'ordine corretto.

📖 **[Guida Visual Studio completa →](VISUAL_STUDIO_SETUP_IT.md)** | **[Visual Studio Setup Guide (EN) →](VISUAL_STUDIO_SETUP.md)**

### Windows
Doppio click su `start-docn.bat` oppure:
```batch
start-docn.bat
```

### PowerShell (Windows/Linux/macOS)
```powershell
.\start-docn.ps1
```

### Linux/macOS
```bash
./start-docn.sh
```

### Manualmente (Se gli script non funzionano)

**Terminale 1 - Avvia il Server (Backend API):**
```bash
cd DocN.Server
dotnet run
```

**Terminale 2 - Avvia il Client (Frontend UI):**
```bash
cd DocN.Client
dotnet run
```

**Browser - Accedi all'applicazione:**
```
http://localhost:5036
```

---

## 🔧 Requisiti di Sistema

- **.NET 10.0 SDK** o superiore
  - Download: https://dotnet.microsoft.com/download
  - Verifica installazione: `dotnet --version`

- **SQL Server** (per produzione) o SQL Server LocalDB (per sviluppo)
  - Il database verrà creato automaticamente al primo avvio

---

## 🌐 Porte e URL

| Applicazione | Tipo | URL | Descrizione |
|-------------|------|-----|-------------|
| **DocN.Server** | Backend API | https://localhost:5211 | API REST e endpoints |
| **DocN.Server** | Backend HTTP | http://localhost:5210 | Alternativa HTTP |
| **DocN.Client** | Frontend UI | http://localhost:5036 | Interfaccia utente Blazor |

---

## ⚠️ Risoluzione Problemi Comuni

### "Unable to connect to server" (Durante l'avvio da Visual Studio)

**Problema:** Il Client mostra questo errore quando si avvia da Visual Studio

**✅ Soluzione Automatica:** Il sistema ora gestisce automaticamente questo problema!
- Il Client attende automaticamente che il Server sia pronto
- Vedrai messaggi di "Checking Server availability..." nella console
- Attendi il messaggio "✅ Server is available and ready"

📖 **[Guida completa Visual Studio →](VISUAL_STUDIO_SETUP_IT.md)**

### "Unable to connect to server" (In generale)

**Problema:** Il Client non riesce a connettersi al Server

**Soluzione:**
1. Assicurati che il **Server sia in esecuzione** prima del Client
2. Verifica che il Server sia in ascolto su `https://localhost:5211`
3. Controlla i log del Server per errori di avvio

```bash
# Verifica lo stato del Server
curl https://localhost:5211/health/live

# Dovrebbe rispondere: Healthy
```

### Errore "Port already in use"

**Problema:** Le porte 5211, 5210 o 5036 sono già occupate

**Soluzione:**
```bash
# Windows - Trova il processo che usa la porta
netstat -ano | findstr :5211
taskkill /PID [PID_NUMBER] /F

# Linux/macOS - Trova il processo che usa la porta
lsof -i :5211
kill -9 [PID_NUMBER]
```

### Errore database connection

**Problema:** Il Server non riesce a connettersi al database

**Soluzione:**
1. Controlla la connection string in `DocN.Server/appsettings.Development.json`
2. Assicurati che SQL Server sia in esecuzione
3. Verifica i permessi di accesso al database

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=DocNDb;Trusted_Connection=True;..."
  }
}
```

### Certificato SSL non valido

**Problema:** Errori relativi a certificati SSL in sviluppo

**Soluzione:** Il Client è configurato per accettare certificati self-signed in ambiente Development. Se hai ancora problemi:

```bash
# Fidati del certificato di sviluppo .NET
dotnet dev-certs https --trust
```

---

## 📁 Struttura del Progetto

```
DocArchiviazione/
├── DocN.Server/          # Backend API (ASP.NET Core)
│   ├── Controllers/      # API REST endpoints
│   ├── Services/         # Business logic
│   └── Program.cs        # Server startup
│
├── DocN.Client/          # Frontend UI (Blazor Server)
│   ├── Components/       # UI components
│   ├── Pages/           # Application pages
│   ├── Services/        # Client services
│   └── Program.cs       # Client startup
│
├── DocN.Core/           # Shared core logic
├── DocN.Data/           # Data models and EF Core
│
├── start-docn.bat       # Windows startup script
├── start-docn.ps1       # PowerShell startup script
└── start-docn.sh        # Linux/macOS startup script
```

---

## 🎯 Funzionalità Principali

- ✅ **Upload e Organizzazione Documenti**
  - Supporto multi-formato (PDF, DOC, DOCX, TXT, immagini)
  - Categorizzazione automatica con AI
  
- ✅ **Ricerca Semantica**
  - Ricerca in linguaggio naturale
  - Embeddings vettoriali per similarity search
  
- ✅ **Chat AI con i Documenti**
  - Interazione conversazionale
  - RAG (Retrieval Augmented Generation)
  
- ✅ **Dashboard e Analytics**
  - Statistiche utilizzo
  - Visualizzazioni interattive
  
- ✅ **Gestione Utenti e Ruoli**
  - Autenticazione ASP.NET Identity
  - Controllo accessi basato su ruoli
  
- ✅ **Notifiche Real-time**
  - SignalR per aggiornamenti live
  - Centro notifiche integrato

---

## 🔐 Primi Passi

### 1. Primo Accesso

Al primo avvio, il sistema crea automaticamente:
- Database `DocNDb`
- Utenti di default per test

**Credenziali amministratore di default:**
```
Email: admin@docn.local
Password: Admin123!
```

⚠️ **IMPORTANTE:** Cambia queste credenziali in produzione!

### 2. Configurazione Provider AI

Il sistema supporta multipli provider AI. Configura almeno uno in `DocN.Server/appsettings.Development.json`:

```json
{
  "AIProvider": {
    "DefaultProvider": "Ollama",  // o "OpenAI", "AzureOpenAI", "Gemini"
    "OpenAI": {
      "ApiKey": "your-openai-key",
      "ChatModel": "gpt-4-turbo"
    },
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "ChatModel": "llama3"
    }
  }
}
```

---

## 🛠️ Sviluppo

### Build del Progetto

```bash
# Build dell'intera solution
dotnet build

# Build specifico
dotnet build DocN.Server
dotnet build DocN.Client
```

### Test

```bash
# Esegui tutti i test
dotnet test

# Test specifici
dotnet test DocN.Tests
```

### Database Migrations

```bash
cd DocN.Server

# Crea una nuova migration
dotnet ef migrations add MigrationName

# Applica le migrations
dotnet ef database update
```

---

## 📚 Documentazione Completa

- [SOMMARIO_ESECUTIVO.md](./SOMMARIO_ESECUTIVO.md) - Overview per management
- [STATO_REALE_E_PROSSIME_VERSIONI.md](./STATO_REALE_E_PROSSIME_VERSIONI.md) - Documento tecnico completo
- [QUICK_REFERENCE.md](./QUICK_REFERENCE.md) - Riferimento rapido
- [LEGGIMI.md](./LEGGIMI.md) - Guida generale

---

## 🤝 Supporto

Per problemi o domande:
1. Controlla la sezione **Risoluzione Problemi** sopra
2. Consulta i log delle applicazioni
3. Verifica che entrambe le applicazioni siano in esecuzione

### Log Files

```bash
# Server logs
DocN.Server/logs/

# Client logs (console output)
```

---

## 📄 Licenza

Copyright © 2026 - Tutti i diritti riservati

---

## ✨ Caratteristiche Tecniche

- **Backend:** ASP.NET Core 10.0, Entity Framework Core
- **Frontend:** Blazor Server, FluentUI Components
- **Database:** SQL Server
- **AI/ML:** Semantic Kernel, OpenAI, Ollama, Gemini
- **Real-time:** SignalR
- **Authentication:** ASP.NET Identity
- **Caching:** Redis (opzionale)

---

**Versione:** 1.0.0  
**Ultimo aggiornamento:** 2026-02-05
