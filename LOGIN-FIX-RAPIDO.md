# 🚀 Guida Rapida - Come Risolvere il Problema di Login

## 📝 Problema: "Non riesco a fare il login"

### 🎯 Soluzione in 3 Passi

#### Passo 1: Avvia SQL Server

```powershell
# Su Windows PowerShell (come Administrator):
Start-Service MSSQLSERVER

# Oppure per SQL Express:
Start-Service MSSQL$SQLEXPRESS

# Verifica che sia in esecuzione:
Get-Service MSSQLSERVER
```

#### Passo 2: Crea il Database

```bash
# Apri un terminale nella cartella del progetto
cd DocN.Server
dotnet ef database update
```

**Output atteso:**
```
Build succeeded.
Applying migration '...'
Done.
```

#### Passo 3: Avvia Server e Client

**Opzione A - Con Visual Studio:**
1. Apri `Doc_archiviazione.sln`
2. Right-click sulla solution → Properties
3. Multiple startup projects → Imposta `DocN.Server` e `DocN.Client` su "Start"
4. Premi F5

**Opzione B - Da Terminale:**

```bash
# Terminale 1 - Server
cd DocN.Server
dotnet run --launch-profile https

# Terminale 2 - Client (apri un nuovo terminale)
cd DocN.Client
dotnet run
```

### ✅ Verifica che Funzioni

1. **Controlla i log del Server** - dovresti vedere:
   ```
   Created default admin user: admin@docn.local with role: SuperAdmin
   ```

2. **Apri il browser su:**
   ```
   http://localhost:5036/login
   ```

3. **Inserisci le credenziali:**
   ```
   Email:    admin@docn.local
   Password: Admin@123
   ```
   
   ⚠️ **ATTENZIONE:** La password ha `@` tra Admin e 123, NON `!`

4. **Clicca "Sign In"**

---

## 🔧 Diagnosi Automatica

Se i passi sopra non funzionano, esegui lo script di diagnosi:

```powershell
.\diagnose-login.ps1
```

Lo script verifica automaticamente:
- ✅ SQL Server in esecuzione
- ✅ DocN Server attivo (porta 5211)
- ✅ DocN Client attivo (porta 5036)
- ✅ File di configurazione
- ✅ Database creato
- ✅ API funzionante

E ti dice esattamente cosa devi fare per risolvere.

---

## ❌ Problemi Comuni

### "Invalid email or password"

**Causa:** Database non connesso, quindi l'utente admin non esiste.

**Soluzione:**
1. Avvia SQL Server: `Start-Service MSSQLSERVER`
2. Crea il database: `cd DocN.Server && dotnet ef database update`
3. Riavvia il Server
4. Controlla i log per "Created default admin user"

### "Unable to connect to server"

**Causa:** Il Server non è in esecuzione.

**Soluzione:**
1. Verifica porta 5211: `netstat -ano | findstr "5211"`
2. Se non c'è nulla, avvia il Server: `cd DocN.Server && dotnet run --launch-profile https`

### "Account locked"

**Causa:** Troppi tentativi di login falliti.

**Soluzione rapida:**
```bash
cd DocN.Server
dotnet ef database drop --force
dotnet ef database update
```

---

## 📚 Documentazione Completa

Se hai ancora problemi, consulta:

| Documento | Scopo |
|-----------|-------|
| **[GUIDA-LOGIN-TROUBLESHOOTING.md](./GUIDA-LOGIN-TROUBLESHOOTING.md)** | Guida completa (10KB) con diagnosi dettagliata e soluzioni |
| **[diagnose-login.ps1](./diagnose-login.ps1)** | Script automatico di diagnosi |
| **[CREDENZIALI-DEFAULT.md](./CREDENZIALI-DEFAULT.md)** | Credenziali admin e info di sicurezza |
| **[SWAGGER-ERROR-FIX.md](./SWAGGER-ERROR-FIX.md)** | Fix database (stesso problema) |
| **[RIEPILOGO-ISSUES.md](./RIEPILOGO-ISSUES.md)** | Panoramica di tutte le issues |

---

## 🆘 Ripristino Totale (Last Resort)

Se niente funziona, fai un reset completo:

```bash
# 1. Ferma tutto (chiudi Visual Studio o Ctrl+C nei terminali)

# 2. Elimina e ricrea il database
cd DocN.Server
dotnet ef database drop --force
dotnet ef database update

# 3. Avvia il Server
dotnet run --launch-profile https

# 4. Attendi nei log: "Created default admin user: admin@docn.local"

# 5. In un nuovo terminale, avvia il Client
cd ../DocN.Client
dotnet run

# 6. Apri browser in incognito su: http://localhost:5036/login

# 7. Login: admin@docn.local / Admin@123
```

---

## 🔐 Credenziali di Default

**Sempre le stesse:**

```
URL:      http://localhost:5036/login
Email:    admin@docn.local
Password: Admin@123
```

⚠️ **Ricorda:**
- Password con `@` non `!`
- Rispetta maiuscole/minuscole
- Nessuno spazio

---

## ✨ Dopo il Primo Login

1. Vai su **Profilo** o **Impostazioni**
2. **Cambia la password** per sicurezza
3. Esplora le funzionalità dell'app
4. Carica i tuoi primi documenti!

---

**Ultimo aggiornamento:** 7 Febbraio 2026  
**Versione:** 1.0  
**Supporto:** Consulta GUIDA-LOGIN-TROUBLESHOOTING.md per dettagli
