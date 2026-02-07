# 🔐 Guida Completa - Risoluzione Problemi di Login

## ❌ Problema: "Non riesco a fare il login"

Questa guida ti aiuta a diagnosticare e risolvere i problemi di accesso all'applicazione DocN.

---

## 🎯 Diagnosi Rapida

### Passo 1: Verifica che Server e Client siano in esecuzione

**Server deve essere su:**
- `https://localhost:5211` (HTTPS)
- `http://localhost:5210` (HTTP)

**Client deve essere su:**
- `http://localhost:5036` (HTTP)
- `https://localhost:7114` (HTTPS)

**Come verificare:**

```powershell
# Su Windows, apri PowerShell e esegui:
netstat -ano | findstr "5211"
netstat -ano | findstr "5036"
```

**Risultato atteso:**
```
TCP    0.0.0.0:5211           0.0.0.0:0              LISTENING       12345
TCP    0.0.0.0:5036           0.0.0.0:0              LISTENING       67890
```

Se non vedi questi risultati, Server o Client non sono in esecuzione.

---

### Passo 2: Verifica la connessione al database

**Il problema più comune è che SQL Server non è connesso.**

Quando il Server non riesce a connettersi al database:
- ❌ L'utente admin NON viene creato
- ❌ Il login FALLISCE sempre con "Invalid email or password"
- ❌ Swagger mostra errore 500

**Verifica SQL Server:**

```powershell
# Controlla se SQL Server è in esecuzione
Get-Service MSSQLSERVER
# Oppure per SQL Express:
Get-Service MSSQL$SQLEXPRESS
# Oppure per SQL 2025:
Get-Service MSSQL$SQL2025
```

**Risultato atteso:**
```
Status   Name               DisplayName
------   ----               -----------
Running  MSSQLSERVER        SQL Server (MSSQLSERVER)
```

Se lo stato è "Stopped", avvia il servizio:

```powershell
Start-Service MSSQLSERVER
# Oppure:
Start-Service MSSQL$SQLEXPRESS
```

---

### Passo 3: Controlla i log del Server

**Apri i log del Server** per vedere errori specifici:

**Posizione log:**
```
DocN.Server/logs/docn-[data].log
```

**Cerca questi messaggi:**

#### ✅ BUONO - Database connesso:
```
Created default admin user: admin@docn.local with role: SuperAdmin
⚠️  IMPORTANT: Change the default admin password after first login!
```

#### ❌ CATTIVO - Database NON connesso:
```
A network-related or instance-specific error occurred while establishing a connection to SQL Server
```
oppure
```
Login failed for user
```

---

### Passo 4: Verifica le credenziali corrette

**Le credenziali ESATTE sono:**

```
Email:    admin@docn.local
Password: Admin@123
```

⚠️ **ATTENZIONE:**
- La password ha `@` tra "Admin" e "123", NON `!`
- Rispetta maiuscole e minuscole esattamente come scritto
- Non ci sono spazi prima o dopo

---

## 🔧 Soluzioni ai Problemi Comuni

### Problema 1: "Invalid email or password"

**Causa più probabile:** Database non connesso, quindi l'utente admin non è stato creato.

**Soluzione:**

1. **Ferma Server e Client** (chiudi Visual Studio o premi Ctrl+C nei terminali)

2. **Configura la connection string:**
   
   Crea/modifica il file:
   ```
   DocN.Server/appsettings.json
   ```
   
   **Per SQL Server LocalDB (sviluppo):**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False"
     }
   }
   ```
   
   **Per SQL Server Express:**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False"
     }
   }
   ```
   
   **Per SQL Server Standard (con nome istanza):**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=NOME-PC\\SQL2025;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False"
     }
   }
   ```

3. **Applica le migrazioni del database:**
   
   ```bash
   cd DocN.Server
   dotnet ef database update
   ```
   
   Se il comando non funziona, installa l'EF tool:
   ```bash
   dotnet tool install --global dotnet-ef
   dotnet ef database update
   ```

4. **Riavvia il Server:**
   
   ```bash
   cd DocN.Server
   dotnet run --launch-profile https
   ```

5. **Controlla i log** per verificare che l'utente admin sia stato creato:
   ```
   Created default admin user: admin@docn.local with role: SuperAdmin
   ```

6. **Riavvia il Client:**
   
   ```bash
   cd DocN.Client
   dotnet run
   ```

7. **Prova di nuovo il login** su `http://localhost:5036/login`

---

### Problema 2: "Unable to connect to server"

**Causa:** Il Client non riesce a raggiungere il Server.

**Soluzione:**

1. **Verifica che il Server sia in esecuzione** su porta 5211:
   ```powershell
   netstat -ano | findstr "5211"
   ```

2. **Controlla l'URL del Server nel Client:**
   
   File: `DocN.Client/appsettings.json`
   ```json
   {
     "BackendApiUrl": "https://localhost:5211/"
   }
   ```

3. **Verifica la configurazione CORS nel Server:**
   
   Il Server deve accettare richieste dal Client. Controlla in `DocN.Server/Program.cs`:
   ```csharp
   policy.WithOrigins("http://localhost:5036", "https://localhost:7114")
   ```

4. **Testa l'endpoint direttamente:**
   
   Apri il browser e vai su:
   ```
   https://localhost:5211/api/auth/status
   ```
   
   Dovresti vedere:
   ```json
   {"isAuthenticated":false,"userName":null}
   ```

---

### Problema 3: "Account locked due to multiple failed attempts"

**Causa:** Troppi tentativi di login falliti.

**Soluzione:**

1. **Aspetta 15 minuti** - il blocco è temporaneo

2. **OPPURE resetta il blocco nel database:**
   
   ```sql
   USE DocNDb;
   UPDATE AspNetUsers 
   SET AccessFailedCount = 0, LockoutEnd = NULL 
   WHERE Email = 'admin@docn.local';
   ```

3. **OPPURE ricrea l'utente admin:**
   
   ```bash
   # Elimina il database e ricrealo
   cd DocN.Server
   dotnet ef database drop --force
   dotnet ef database update
   ```

---

### Problema 4: Login funziona, ma viene reindirizzato subito al login

**Causa:** Problema con i cookie di autenticazione.

**Soluzione:**

1. **Cancella i cookie del browser:**
   - Chrome: F12 → Application → Cookies → Elimina tutto
   - Firefox: F12 → Storage → Cookies → Elimina tutto
   - Edge: F12 → Application → Cookies → Elimina tutto

2. **Prova in modalità incognito/privata**

3. **Verifica la configurazione dei cookie nel Server:**
   
   File: `DocN.Server/Program.cs`
   ```csharp
   options.Cookie.Name = "DocN.Auth";
   options.Cookie.HttpOnly = true;
   options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
   ```

---

## 🧪 Test di Verifica

### Test 1: Server raggiungibile

```bash
# Prova API status
curl https://localhost:5211/api/auth/status
```

**Risultato atteso:**
```json
{"isAuthenticated":false,"userName":null}
```

### Test 2: Swagger funzionante

Apri: `https://localhost:5211/swagger/index.html`

**Risultato atteso:** Vedi la documentazione API completa, NON errore 500.

### Test 3: Database connesso

```bash
cd DocN.Server
dotnet ef database update
```

**Risultato atteso:**
```
Build succeeded.
Applying migration '...'
Done.
```

### Test 4: Login API endpoint

```bash
curl -X POST https://localhost:5211/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@docn.local","password":"Admin@123","rememberMe":false}'
```

**Risultato atteso (successo):**
```json
{
  "success": true,
  "userId": "...",
  "email": "admin@docn.local",
  "firstName": "Admin",
  "lastName": "User"
}
```

**Risultato se fallisce:**
```json
{
  "error": "Invalid email or password"
}
```

---

## 📋 Checklist Completa

Segui questa checklist per risolvere il problema:

- [ ] SQL Server è in esecuzione (`Get-Service MSSQLSERVER`)
- [ ] Connection string configurata in `DocN.Server/appsettings.json`
- [ ] Database creato (`dotnet ef database update`)
- [ ] Server in esecuzione su porta 5211 (`netstat -ano | findstr "5211"`)
- [ ] Client in esecuzione su porta 5036 (`netstat -ano | findstr "5036"`)
- [ ] Log del Server mostra "Created default admin user"
- [ ] Swagger funziona senza errori (`https://localhost:5211/swagger`)
- [ ] Endpoint `/api/auth/status` risponde
- [ ] Credenziali corrette: `admin@docn.local` / `Admin@123`
- [ ] Cookie del browser cancellati
- [ ] Test di login effettuato con successo

---

## 🆘 Se Ancora Non Funziona

### Opzione 1: Ripristino Completo

```bash
# 1. Ferma tutto
# Chiudi Visual Studio o premi Ctrl+C nei terminali

# 2. Elimina il database
cd DocN.Server
dotnet ef database drop --force

# 3. Ricrea il database
dotnet ef database update

# 4. Riavvia Server
dotnet run --launch-profile https

# 5. Attendi fino a vedere nei log:
# "Created default admin user: admin@docn.local"

# 6. Riavvia Client (in un nuovo terminale)
cd ../DocN.Client
dotnet run

# 7. Apri browser in incognito
# http://localhost:5036/login

# 8. Prova login con: admin@docn.local / Admin@123
```

### Opzione 2: Test con Visual Studio

1. **Apri Visual Studio 2022**
2. **Carica la solution:** `Doc_archiviazione.sln`
3. **Configura multiple startup projects:**
   - Right-click sulla solution → Properties
   - Multiple startup projects
   - Imposta `DocN.Server` e `DocN.Client` su "Start"
4. **Premi F5** per avviare in debug
5. **Controlla l'Output window** per vedere i log
6. **Cerca errori** nella finestra Output

### Opzione 3: Verifica Manuale nel Database

```sql
-- Connettiti al database con SQL Server Management Studio o Azure Data Studio
USE DocNDb;

-- Verifica che la tabella utenti esista
SELECT * FROM AspNetUsers;

-- Verifica l'utente admin
SELECT Id, UserName, Email, IsActive, LastLoginAt 
FROM AspNetUsers 
WHERE Email = 'admin@docn.local';

-- Se non esiste, il problema è la connessione al database
```

---

## 📚 Documenti Correlati

- **[CREDENZIALI-DEFAULT.md](./CREDENZIALI-DEFAULT.md)** - Credenziali admin e informazioni di accesso
- **[SWAGGER-ERROR-FIX.md](./SWAGGER-ERROR-FIX.md)** - Risoluzione errore Swagger 500 (stesso problema del database)
- **[RIEPILOGO-ISSUES.md](./RIEPILOGO-ISSUES.md)** - Panoramica di tutti i problemi conosciuti
- **[ISTRUZIONI-UTENTE.md](./ISTRUZIONI-UTENTE.md)** - Guida completa per l'utilizzo

---

## 🔐 Credenziali Quick Reference

```
URL Login:  http://localhost:5036/login
Email:      admin@docn.local
Password:   Admin@123
```

⚠️ **Ricorda:** `Admin@123` con `@` non `!`

---

**Ultimo aggiornamento:** 7 Febbraio 2026  
**Versione:** 1.0  
**Autore:** DocN Support Team
