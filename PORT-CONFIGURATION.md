# 🔌 Port Configuration - DocN Client & Server

## Overview

DocN è un'applicazione **client-server** che richiede **entrambi i componenti** in esecuzione contemporaneamente per funzionare correttamente.

---

## 📊 Porte Configurate

| Componente | Porta HTTP | Porta HTTPS | Uso |
|------------|------------|-------------|-----|
| **Server** | 5210 | **5211** | Backend API (REST endpoints) |
| **Client** | 5036 | **7114** | Frontend UI (Blazor application) |

### Flusso di Comunicazione

```
User Browser
    ↓
https://localhost:7114 (Client UI)
    ↓
Client chiama API
    ↓
https://localhost:5211 (Server API)
    ↓
Server elabora e risponde
    ↓
Client visualizza risultato
```

---

## ⚠️ PROBLEMA COMUNE

### Errore: "Impossibile stabilire la connessione (localhost:5211)"

**Sintomo**:
```
Errore nel caricamento dei documenti: 
Impossibile stabilire la connessione. 
Rifiuto persistente del computer di destinazione. (localhost:5211)
```

**Causa**: 
Il **Server non è in esecuzione** sulla porta 5211, ma il Client sta cercando di connettersi.

**Soluzione**: 
Devi avviare **ENTRAMBI** Server e Client!

---

## 🚀 Come Avviare Correttamente

### ✅ Metodo 1: Visual Studio (CONSIGLIATO)

1. **Configura Multiple Startup Projects**:
   ```
   Right-click su Solution → Properties
   → Common Properties → Startup Project
   → Multiple startup projects
   ```

2. **Imposta Ordine**:
   ```
   ☑ DocN.Server  →  Action: Start   [AVVIA PRIMA]
   ☑ DocN.Client  →  Action: Start   [AVVIA DOPO]
   ```
   
3. **Press F5** o click "Start"

4. **Verifica**:
   - Console Server: "Now listening on: https://localhost:5211" ✅
   - Console Client: "Now listening on: https://localhost:7114" ✅
   - 2 finestre browser si aprono

---

### ✅ Metodo 2: Command Line (2 Terminals)

#### Terminal 1 - Server (AVVIA PRIMA!)

```bash
cd DocN.Server
dotnet run --launch-profile https
```

**Output Atteso**:
```
[10:13:33 INF] : Starting DocN Server...
[10:13:34 INF] : ASP.NET Core Identity configured successfully
[10:13:34 INF] : Redis not configured, using in-memory cache
[10:13:35 INF] : Now listening on: https://localhost:5211   ✅
[10:13:35 INF] : Now listening on: http://localhost:5210
[10:13:35 INF] : Application started. Press Ctrl+C to shut down.
```

**⚠️ IMPORTANTE**: Aspetta che appaia "Application started" prima di avviare il Client!

---

#### Terminal 2 - Client (AVVIA DOPO!)

```bash
cd DocN.Client
dotnet run --launch-profile https
```

**Output Atteso**:
```
info: DocN.Client[0]
      Upload directory created/verified: C:\...\Uploads
Configuring HTTP request pipeline...
  - Adding HttpsRedirection middleware...
  - Adding StaticFiles middleware...
  - Adding Session middleware...
  - Adding Antiforgery middleware...
  - Adding Authentication middleware...
  - Adding Authorization middleware...
HTTP request pipeline configured successfully ✓
Configuring Razor Components...
Razor Components configured successfully ✓ (Static rendering mode)
Starting the application...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7114   ✅
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5036
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

---

## 🧪 Test di Verifica

### Step 1: Verifica Server in Esecuzione

**Windows**:
```cmd
netstat -ano | findstr :5211
```

**Linux/Mac**:
```bash
lsof -i :5211
```

**Expected Output**: 
```
TCP    0.0.0.0:5211    0.0.0.0:0    LISTENING    12345
```

**Se nulla**: Server NON è in esecuzione! Avvialo!

---

### Step 2: Verifica Client in Esecuzione

**Windows**:
```cmd
netstat -ano | findstr :7114
```

**Expected Output**: 
```
TCP    0.0.0.0:7114    0.0.0.0:0    LISTENING    67890
```

---

### Step 3: Test Browser

1. **Apri Browser**: 
   ```
   https://localhost:7114
   ```

2. **Verifica Home Page si carica** ✅

3. **Naviga a Documents**:
   ```
   https://localhost:7114/documents
   ```

4. **Verifica**:
   - ✅ Nessun errore "Connection Refused"
   - ✅ Pagina documenti si carica
   - ✅ Client può comunicare con Server

---

## 🔧 Configurazione Files

### Server Launch Settings

**File**: `DocN.Server/Properties/launchSettings.json`

```json
{
  "profiles": {
    "https": {
      "commandName": "Project",
      "applicationUrl": "https://localhost:5211;http://localhost:5210",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

---

### Client Launch Settings

**File**: `DocN.Client/Properties/launchSettings.json`

```json
{
  "profiles": {
    "https": {
      "commandName": "Project",
      "applicationUrl": "https://localhost:7114;http://localhost:5036",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

---

### Client Backend Configuration

**File**: `DocN.Client/Program.cs` (riga ~207)

```csharp
var backendUrl = builder.Configuration["BackendApiUrl"] ?? "https://localhost:5211/";
logger.LogInformation($"Configuring HTTP client with backend URL: {backendUrl}");

builder.Services.AddHttpClient("BackendAPI", client =>
{
    client.BaseAddress = new Uri(backendUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

**Questo dice al Client di connettersi al Server su `https://localhost:5211`**

---

## 🐛 Troubleshooting

### Problema 1: "Port already in use"

**Errore**:
```
Unable to bind to https://localhost:5211 on the IPv4 loopback interface: 
'Address already in use'.
```

**Causa**: Un'altra applicazione sta usando la porta 5211 o 7114

**Soluzione Windows**:
```cmd
# Trova processo che usa porta 5211
netstat -ano | findstr :5211

# Output: ... LISTENING    12345
# PID è l'ultimo numero (12345)

# Kill il processo
taskkill /PID 12345 /F
```

**Soluzione Linux/Mac**:
```bash
# Trova processo
lsof -i :5211

# Kill processo
kill -9 <PID>
```

**Alternativa**: Cambia porta in `launchSettings.json`

---

### Problema 2: "Connection Refused"

**Errore nel Browser**:
```
Errore: Impossibile stabilire la connessione. 
Rifiuto persistente del computer di destinazione. (localhost:5211)
```

**Causa**: Server non in esecuzione

**Verifica**:
```bash
# Controlla se Server è in esecuzione
netstat -ano | findstr :5211    # Windows
lsof -i :5211                   # Linux/Mac
```

**Soluzione**: 
1. Avvia il Server PRIMA
2. Aspetta che dica "Application started"
3. POI avvia il Client

---

### Problema 3: Server si avvia ma Client non si connette

**Verifica Firewall**:
```
Windows Defender Firewall potrebbe bloccare localhost.
Aggiungi eccezione per DocN.Server.exe e DocN.Client.exe
```

**Verifica URL**:
```csharp
// In Program.cs del Client, verifica che BackendApiUrl sia corretto
var backendUrl = builder.Configuration["BackendApiUrl"] ?? "https://localhost:5211/";
```

**Test Manuale API**:
```bash
# Prova a chiamare API direttamente
curl https://localhost:5211/api/health
# Expected: 200 OK se Server funziona
```

---

### Problema 4: Certificate HTTPS non valido

**Errore**: 
```
The SSL connection could not be established
```

**Soluzione**:
```bash
# Installa certificato di sviluppo
dotnet dev-certs https --trust
```

---

## 📋 Checklist di Verifica

### Prima di Iniziare
- [ ] Visual Studio configurato per "Multiple startup projects"
- [ ] Ordine: Server PRIMA, Client DOPO
- [ ] Build di entrambi progetti riuscito (0 errors)
- [ ] Nessun'altra app usa porte 5211 o 7114

### Durante l'Avvio
- [ ] Server si avvia per primo
- [ ] Server log: "Now listening on: https://localhost:5211"
- [ ] Client si avvia dopo Server
- [ ] Client log: "Now listening on: https://localhost:7114"

### Test Funzionalità
- [ ] Browser aperto su https://localhost:7114
- [ ] Home page si carica correttamente
- [ ] Navigazione a /documents funziona
- [ ] Nessun errore "Connection Refused"
- [ ] API calls funzionano (controlla DevTools Network)

---

## 🎯 Summary

**Per far funzionare DocN correttamente**:

1. ✅ Avvia **Server** su porta **5211** (PRIMA)
2. ✅ Avvia **Client** su porta **7114** (DOPO)
3. ✅ Client si connette automaticamente al Server su 5211
4. ✅ Apri browser su https://localhost:7114
5. ✅ Enjoy! 🎉

**Ricorda**: **ENTRAMBI devono essere in esecuzione contemporaneamente!**

---

**Last Updated**: 7 Febbraio 2026
**Status**: ✅ Configuration Verified
