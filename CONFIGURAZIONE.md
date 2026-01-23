# Guida alla Configurazione / Configuration Guide

## 🔧 Configurazione della Stringa di Connessione / Connection String Configuration

### Italiano

Le stringhe di connessione al database **NON** sono più definite direttamente nel codice sorgente. Questo permette a ogni sviluppatore di utilizzare il proprio database senza modificare i file tracciati da Git.

#### Come Configurare

1. **Crea i file di configurazione locali** (questi file sono già in `.gitignore` e non verranno tracciati):
   - `DocN.Client/appsettings.json`
   - `DocN.Client/appsettings.Development.json`
   - `DocN.Server/appsettings.json`
   - `DocN.Server/appsettings.Development.json`

2. **Copia i file di esempio**:
   ```bash
   # Client
   cp DocN.Client/appsettings.example.json DocN.Client/appsettings.json
   cp DocN.Client/appsettings.Development.example.json DocN.Client/appsettings.Development.json
   
   # Server
   cp DocN.Server/appsettings.example.json DocN.Server/appsettings.json
   cp DocN.Server/appsettings.Development.example.json DocN.Server/appsettings.Development.json
   ```

3. **Modifica la stringa di connessione** nei file creati:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=TUO_SERVER;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True"
     }
   }
   ```

#### Esempi di Stringhe di Connessione

**SQL Server con Windows Authentication:**
```
Server=localhost;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True
```

**SQL Server con Named Instance:**
```
Server=NOMEPC\\SQLEXPRESS;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True
```

**SQL Server con SQL Authentication:**
```
Server=localhost;Database=DocNDb;User Id=tuoutente;Password=tuapassword;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True
```

**SQL Server remoto:**
```
Server=192.168.1.100,1433;Database=DocNDb;User Id=tuoutente;Password=tuapassword;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True
```

#### Variabili d'Ambiente (Opzionale)

In alternativa ai file di configurazione, puoi utilizzare variabili d'ambiente:

**Windows (PowerShell):**
```powershell
$env:ConnectionStrings__DefaultConnection="Server=localhost;Database=DocNDb;Trusted_Connection=True;..."
```

**Windows (CMD):**
```cmd
set ConnectionStrings__DefaultConnection=Server=localhost;Database=DocNDb;Trusted_Connection=True;...
```

**Linux/Mac:**
```bash
export ConnectionStrings__DefaultConnection="Server=localhost;Database=DocNDb;Trusted_Connection=True;..."
```

#### Design-Time Tools (Migrations)

Per gli strumenti di design-time Entity Framework (migrations), puoi impostare la variabile d'ambiente `DefaultConnection`:

```bash
# Windows PowerShell
$env:DefaultConnection="Server=localhost;Database=DocNDb;Trusted_Connection=True;..."

# Linux/Mac
export DefaultConnection="Server=localhost;Database=DocNDb;Trusted_Connection=True;..."

# Poi esegui le migrations
dotnet ef migrations add NomeMigration -p DocN.Data -s DocN.Server
```

---

### English

Database connection strings are **NO LONGER** defined directly in the source code. This allows each developer to use their own database without modifying Git-tracked files.

#### How to Configure

1. **Create local configuration files** (these files are already in `.gitignore` and won't be tracked):
   - `DocN.Client/appsettings.json`
   - `DocN.Client/appsettings.Development.json`
   - `DocN.Server/appsettings.json`
   - `DocN.Server/appsettings.Development.json`

2. **Copy the example files**:
   ```bash
   # Client
   cp DocN.Client/appsettings.example.json DocN.Client/appsettings.json
   cp DocN.Client/appsettings.Development.example.json DocN.Client/appsettings.Development.json
   
   # Server
   cp DocN.Server/appsettings.example.json DocN.Server/appsettings.json
   cp DocN.Server/appsettings.Development.example.json DocN.Server/appsettings.Development.json
   ```

3. **Edit the connection string** in the created files:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=YOUR_SERVER;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True"
     }
   }
   ```

#### Connection String Examples

**SQL Server with Windows Authentication:**
```
Server=localhost;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True
```

**SQL Server with Named Instance:**
```
Server=PCNAME\\SQLEXPRESS;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True
```

**SQL Server with SQL Authentication:**
```
Server=localhost;Database=DocNDb;User Id=yourusername;Password=yourpassword;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True
```

**Remote SQL Server:**
```
Server=192.168.1.100,1433;Database=DocNDb;User Id=yourusername;Password=yourpassword;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True
```

#### Environment Variables (Optional)

As an alternative to configuration files, you can use environment variables:

**Windows (PowerShell):**
```powershell
$env:ConnectionStrings__DefaultConnection="Server=localhost;Database=DocNDb;Trusted_Connection=True;..."
```

**Windows (CMD):**
```cmd
set ConnectionStrings__DefaultConnection=Server=localhost;Database=DocNDb;Trusted_Connection=True;...
```

**Linux/Mac:**
```bash
export ConnectionStrings__DefaultConnection="Server=localhost;Database=DocNDb;Trusted_Connection=True;..."
```

#### Design-Time Tools (Migrations)

For Entity Framework design-time tools (migrations), you can set the `DefaultConnection` environment variable:

```bash
# Windows PowerShell
$env:DefaultConnection="Server=localhost;Database=DocNDb;Trusted_Connection=True;..."

# Linux/Mac
export DefaultConnection="Server=localhost;Database=DocNDb;Trusted_Connection=True;..."

# Then run migrations
dotnet ef migrations add MigrationName -p DocN.Data -s DocN.Server
```

---

## ⚠️ Sicurezza / Security

### ❌ NON FARE / DO NOT:
- ❌ Non committare mai i file `appsettings.json` o `appsettings.Development.json` con le tue stringhe di connessione
- ❌ Non includere password o credenziali nei file tracciati da Git
- ❌ Non condividere le stringhe di connessione in chat o email non sicure

### ✅ FARE / DO:
- ✅ Usa i file di esempio (`.example.json`) come template
- ✅ Mantieni le stringhe di connessione nei file locali ignorati da Git
- ✅ Usa variabili d'ambiente per ambienti di produzione
- ✅ Usa Azure Key Vault o simili per gestire segreti in produzione

---

## 🔍 Risoluzione Problemi / Troubleshooting

### Errore: "Database connection string 'DefaultConnection' is not configured"

**Causa:** Il file `appsettings.json` non esiste o non contiene la stringa di connessione.

**Soluzione:**
1. Crea il file `appsettings.json` dalla copia del file di esempio
2. Aggiorna la stringa di connessione con i tuoi parametri del database

### L'applicazione non si connette al database

**Verifica:**
1. Il server SQL è in esecuzione?
2. Il nome del server è corretto?
3. Le credenziali sono corrette (se usi SQL Authentication)?
4. Il firewall permette la connessione?
5. Il database `DocNDb` esiste?

**Test della connessione:**
```bash
# SQL Server con Windows Auth
sqlcmd -S localhost -E -Q "SELECT @@VERSION"

# SQL Server con SQL Auth
sqlcmd -S localhost -U tuoutente -P tuapassword -Q "SELECT @@VERSION"
```

### Le migrations non funzionano

Assicurati che la variabile d'ambiente `DefaultConnection` sia impostata:
```bash
# Verifica
echo $env:DefaultConnection  # PowerShell
echo $DefaultConnection      # Linux/Mac
```

---

## 📁 Struttura File di Configurazione / Configuration Files Structure

```
DocArchiviazione/
├── .gitignore                    # Contiene regole per ignorare file di configurazione
├── CONFIGURAZIONE.md             # Questa guida
├── DocN.Client/
│   ├── appsettings.json          # ⛔ GIT-IGNORED - Tua configurazione locale
│   ├── appsettings.Development.json  # ⛔ GIT-IGNORED - Tua configurazione locale
│   ├── appsettings.example.json      # ✅ TRACKED - Template
│   └── appsettings.Development.example.json  # ✅ TRACKED - Template
└── DocN.Server/
    ├── appsettings.json          # ⛔ GIT-IGNORED - Tua configurazione locale
    ├── appsettings.Development.json  # ⛔ GIT-IGNORED - Tua configurazione locale
    ├── appsettings.example.json      # ✅ TRACKED - Template
    └── appsettings.Development.example.json  # ✅ TRACKED - Template
```
