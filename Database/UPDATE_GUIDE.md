# Guida Aggiornamento Database / Database Update Guide

Questa guida spiega come aggiornare il database DocN alla versione corrente.
This guide explains how to update the DocN database to the current version.

## Versione Corrente / Current Version

**V3 (2026-01-25)** - Include le seguenti funzionalità:
- Dashboard personalizzabili con widget
- Sistema RBAC (Role-Based Access Control) granulare  
- Ricerca salvata (Saved Searches)
- Attività utente (User Activities)
- Campi workflow avanzati per documenti
- Dataset golden per test di qualità RAG

**V3 (2026-01-25)** - Includes the following features:
- Customizable dashboards with widgets
- Granular RBAC (Role-Based Access Control) system
- Saved searches
- User activities
- Advanced workflow fields for documents
- Golden datasets for RAG quality testing

## Metodi di Aggiornamento / Update Methods

### Metodo 1: Entity Framework Migrations (RACCOMANDATO / RECOMMENDED)

Questo è il metodo più sicuro e raccomandato.
This is the safest and recommended method.

```bash
# Dal root della soluzione / From solution root
cd /path/to/DocArchiviazione

# Aggiorna il database alla versione più recente
# Update database to latest version
dotnet ef database update --project DocN.Data --startup-project DocN.Server --context ApplicationDbContext
```

**Vantaggi / Advantages:**
- ✅ Automatico e sicuro / Automatic and safe
- ✅ Verifica quali migrazioni sono già applicate / Checks which migrations are already applied
- ✅ Applica solo le migrazioni mancanti / Applies only missing migrations
- ✅ Supporta rollback se necessario / Supports rollback if needed

### Metodo 2: Script SQL Manuale con Controlli (NUOVO / NEW)

Script SQL che controlla manualmente l'esistenza di tabelle e campi prima di crearli/aggiungerli.
SQL script that manually checks for existence of tables and fields before creating/adding them.

```bash
# Usando sqlcmd / Using sqlcmd
sqlcmd -S your_server -d DocN -i Database/ManualUpdate_To_V3.sql
```

**Come funziona / How it works:**
- Controlla se ogni tabella esiste prima di crearla
- Controlla se ogni campo esiste prima di aggiungerlo
- Usa transazioni con rollback automatico in caso di errore
- Output dettagliato in italiano e inglese

- Checks if each table exists before creating it
- Checks if each field exists before adding it
- Uses transactions with automatic rollback on error
- Detailed output in Italian and English

**Vantaggi / Advantages:**
- ✅ Non richiede .NET installato / Doesn't require .NET installed
- ✅ Controlli espliciti su ogni elemento / Explicit checks on each element
- ✅ Sicuro anche su database parzialmente aggiornati / Safe even on partially updated databases
- ✅ Output chiaro e dettagliato / Clear and detailed output

**Quando usarlo / When to use:**
- Database parzialmente aggiornato / Partially updated database
- Necessità di vedere cosa viene aggiunto / Need to see what's being added
- Ambiente senza .NET / Environment without .NET

### Metodo 3: Script SQL Idempotente (CreateDatabase_Complete_V3.sql)

Lo script completo V3 è **idempotente** - può essere eseguito su un database esistente.
The complete V3 script is **idempotent** - it can be run on an existing database.

```bash
# Usando sqlcmd / Using sqlcmd
sqlcmd -S your_server -d DocN -i Database/CreateDatabase_Complete_V3.sql
```

**Come funziona / How it works:**
- Lo script verifica nella tabella `__EFMigrationsHistory` quali migrazioni sono già state applicate
- Esegue solo le migrazioni mancanti
- È sicuro eseguirlo più volte

- The script checks the `__EFMigrationsHistory` table to see which migrations are already applied
- Executes only missing migrations
- Safe to run multiple times

**Vantaggi / Advantages:**
- ✅ Non richiede .NET installato / Doesn't require .NET installed
- ✅ Può essere eseguito da qualsiasi client SQL / Can be run from any SQL client
- ✅ Sicuro anche su database esistenti / Safe even on existing databases

**Svantaggi / Disadvantages:**
- ⚠️ Esegue l'intero script ogni volta (ma controlla le migrazioni già applicate)
- ⚠️ Runs the entire script each time (but checks for already applied migrations)

### Metodo 4: Script Incrementale Personalizzato

Se hai bisogno di uno script che aggiorna solo da una versione specifica:
If you need a script that updates only from a specific version:

```bash
# Esempio: aggiornare dalla migrazione 20260108043707 alla versione corrente
# Example: update from migration 20260108043707 to current version
dotnet ef migrations script 20260108043707 --project DocN.Data --context ApplicationDbContext --output Database/Update_From_20260108.sql --idempotent
```

## Scenari Comuni / Common Scenarios

### Scenario 1: Database Vuoto (Nuova Installazione)
### Scenario 1: Empty Database (New Installation)

```bash
# Metodo A: EF Migrations
dotnet ef database update --project DocN.Data --startup-project DocN.Server --context ApplicationDbContext

# Metodo B: Script SQL
sqlcmd -S your_server -d DocN -i Database/CreateDatabase_Complete_V3.sql
```

### Scenario 2: Database Esistente da Versione Precedente
### Scenario 2: Existing Database from Previous Version

```bash
# Metodo A: EF Migrations (RACCOMANDATO / RECOMMENDED)
dotnet ef database update --project DocN.Data --startup-project DocN.Server --context ApplicationDbContext

# Metodo B: Script SQL manuale con controlli (NUOVO / NEW)
sqlcmd -S your_server -d DocN -i Database/ManualUpdate_To_V3.sql

# Metodo C: Script SQL idempotente
sqlcmd -S your_server -d DocN -i Database/CreateDatabase_Complete_V3.sql
```

### Scenario 3: Verifica Stato Migrazioni Corrente
### Scenario 3: Check Current Migration Status

```bash
# Visualizza tutte le migrazioni e quali sono applicate
# Show all migrations and which are applied
dotnet ef migrations list --project DocN.Data --startup-project DocN.Server --context ApplicationDbContext

# Controlla la tabella __EFMigrationsHistory nel database
# Check the __EFMigrationsHistory table in the database
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
```

### Scenario 4: Genera Script SQL per Revisione
### Scenario 4: Generate SQL Script for Review

Se vuoi vedere cosa verrà eseguito prima di applicarlo:
If you want to see what will be executed before applying it:

```bash
# Genera script dalla versione corrente del DB alla più recente
# Generate script from current DB version to latest
dotnet ef migrations script --project DocN.Data --context ApplicationDbContext --idempotent
```

## Verifica Post-Aggiornamento / Post-Update Verification

Dopo aver aggiornato il database, verifica che tutto funzioni:
After updating the database, verify everything works:

```sql
-- 1. Verifica che le nuove tabelle esistano
-- 1. Verify new tables exist
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME IN ('DashboardWidgets', 'SavedSearches', 'UserActivities')
ORDER BY TABLE_NAME;

-- 2. Verifica l'ultima migrazione applicata
-- 2. Check last applied migration
SELECT TOP 1 * FROM __EFMigrationsHistory 
ORDER BY MigrationId DESC;

-- 3. Verifica che l'utente admin esista
-- 3. Verify admin user exists
SELECT Id, UserName, Email FROM AspNetUsers 
WHERE Email = 'admin@docn.local';
```

Risultato atteso / Expected result:
- 3 nuove tabelle (DashboardWidgets, SavedSearches, UserActivities) / 3 new tables
- Ultima migrazione: `20260124115302_AddDashboardAndRBACFeatures` / Last migration
- Utente admin presente / Admin user present

## Troubleshooting

### Errore: "Migration already applied"
**Soluzione:** Normale con script idempotente, verrà saltata automaticamente.
**Solution:** Normal with idempotent script, it will be skipped automatically.

### Errore: "Cannot find migration"
**Causa:** Probabile discrepanza tra codice e database.
**Cause:** Likely mismatch between code and database.

**Soluzione:**
```bash
# 1. Controlla quali migrazioni esistono nel codice
# 1. Check which migrations exist in code
ls -1 DocN.Data/Migrations/*.cs | grep -v Designer | grep -v Snapshot

# 2. Controlla quali sono nel database
# 2. Check which are in database
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;

# 3. Se ci sono discrepanze, usa lo script SQL completo
# 3. If there are discrepancies, use the complete SQL script
```

### Errore: "Database connection failed"
**Verifica:**
- Connection string in appsettings.json
- SQL Server è in esecuzione / SQL Server is running
- Credenziali corrette / Correct credentials
- Firewall permette la connessione / Firewall allows connection

### L'aggiornamento sembra bloccato / Update seems stuck
```bash
# Controlla se ci sono transazioni attive
# Check for active transactions
SELECT * FROM sys.dm_tran_active_transactions;

# Controlla se ci sono lock
# Check for locks
SELECT * FROM sys.dm_tran_locks;
```

## Backup (IMPORTANTE! / IMPORTANT!)

⚠️ **Crea sempre un backup prima di aggiornare!**
⚠️ **Always create a backup before updating!**

```sql
-- Backup del database
-- Database backup
BACKUP DATABASE [DocN] 
TO DISK = 'C:\Backups\DocN_Before_V3_Update.bak'
WITH FORMAT, COMPRESSION, STATS = 10;
```

## Cronologia Versioni / Version History

- **V3** (2026-01-25): Dashboard, RBAC, Saved Searches, User Activities
- **V2** (2025-12-27): AI Multi-provider, Similar Documents, Golden Datasets  
- **V1** (2025-01-01): Initial release with Vector support

## Supporto / Support

Se incontri problemi durante l'aggiornamento:
If you encounter issues during the update:

1. Controlla i log dell'applicazione / Check application logs
2. Verifica la tabella `__EFMigrationsHistory` / Check `__EFMigrationsHistory` table
3. Consulta questo documento / Consult this document
4. Apri un issue su GitHub / Open an issue on GitHub
