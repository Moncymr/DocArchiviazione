# Come Eseguire DocArchiviazione

## Problema Risolto: Client Non Crasha Più! ✅

Il problema del crash del Client quando lanciato da Visual Studio è stato **completamente risolto** disabilitando Interactive Server render mode.

## Nuovo Problema: Connessione Client-Server

Quando si esegue solo il Client da riga di comando, si verifica l'errore:
```
Errore nel caricamento dei documenti: 
Impossibile stabilire la connessione. 
Rifiuto persistente del computer di destinazione. (localhost:5211)
```

### Causa

Il Client cerca di connettersi al Server su `https://localhost:5211`, ma il Server non è in esecuzione.

## Soluzione: Eseguire Entrambi i Progetti

### Opzione 1: Da Visual Studio (CONSIGLIATO)

1. **Imposta Multiple Startup Projects**:
   - Click destro sulla Solution
   - Proprietà → Startup Project
   - Seleziona "Multiple startup projects"
   - Imposta **DocN.Server** → Action: **Start**
   - Imposta **DocN.Client** → Action: **Start**
   - **IMPORTANTE**: DocN.Server deve essere **PRIMA** di DocN.Client nell'ordine
   - Click OK

2. **Avvia con F5** o il pulsante Start

3. **Verifica**:
   - Server dovrebbe aprirsi su `https://localhost:5211`
   - Client dovrebbe aprirsi su `http://localhost:5036`
   - Il Client può ora comunicare con il Server

### Opzione 2: Da Riga di Comando

**Terminale 1 (Server):**
```bash
cd DocN.Server
dotnet run --launch-profile https
```

**Terminale 2 (Client):**
```bash
cd DocN.Client
dotnet run
```

### Opzione 3: Docker (Future)

```bash
docker-compose up
```

## Porte Configurate

| Servizio | HTTP | HTTPS |
|----------|------|-------|
| Server   | 5210 | 5211  |
| Client   | 5036 | 7114  |

## Configurazione

### Server (DocN.Server)

Configurato in `launchSettings.json`:
- HTTPS: `https://localhost:5211`
- HTTP: `http://localhost:5210`

### Client (DocN.Client)

Configurato in `appsettings.json`:
```json
{
  "BackendApiUrl": "https://localhost:5211/"
}
```

**IMPORTANTE**: Il Client è configurato per connettersi al Server su HTTPS porta 5211. Assicurati che il Server sia in esecuzione su questa porta.

## Troubleshooting

### "Impossibile stabilire la connessione (localhost:5211)"

**Problema**: Il Server non è in esecuzione.

**Soluzione**:
1. Avvia il Server PRIMA del Client
2. Verifica che il Server sia in esecuzione: apri `https://localhost:5211/swagger` nel browser
3. Se vedi Swagger UI, il Server funziona correttamente

### "Certificate error" o "SSL handshake failed"

**Problema**: Certificato SSL self-signed non fidato.

**Soluzione** (solo Development):
```bash
dotnet dev-certs https --trust
```

Poi riavvia il Server.

### "Port already in use"

**Problema**: Porta già occupata da un'altra applicazione.

**Soluzione**:
1. Chiudi l'applicazione che usa la porta, oppure
2. Cambia le porte in `launchSettings.json`

## Architettura

```
┌─────────────────┐         HTTPS/REST API        ┌─────────────────┐
│                 │ ───────────────────────────▶   │                 │
│  DocN.Client    │         (port 5211)            │  DocN.Server    │
│  (Blazor)       │ ◀───────────────────────────   │  (ASP.NET API)  │
│  :5036          │                                │  :5211          │
└─────────────────┘                                └─────────────────┘
```

## Note Importanti

1. **Il Server deve essere avviato PRIMA del Client** per evitare errori di connessione
2. **Usa sempre il profilo HTTPS** per il Server (porta 5211)
3. **I file `appsettings.json`** sono in `.gitignore` e non vengono committati
4. **Per produzione**, usa variabili d'ambiente per configurare le URL

## File di Configurazione

### Non Committare
- `appsettings.json`
- `appsettings.Development.json`
- `appsettings.Production.json`

### Committare
- `appsettings.example.json` (template)
- `appsettings.Development.example.json` (template)
- `launchSettings.json` (configurazione sviluppo)

## Testing

Per verificare che tutto funzioni:

1. **Avvia Server**: `cd DocN.Server && dotnet run --launch-profile https`
2. **Verifica Swagger**: Apri `https://localhost:5211/swagger`
3. **Avvia Client**: `cd DocN.Client && dotnet run`
4. **Apri Browser**: `http://localhost:5036`
5. **Naviga a Documents**: `http://localhost:5036/documents`
6. **Verifica**: La pagina dovrebbe caricare senza errori

## Changelog

### 2026-02-06

- ✅ **RISOLTO**: Crash del Client con AggregateException
- ✅ **RISOLTO**: Disabilitato Interactive Server render mode
- ✅ **AGGIUNTO**: File di configurazione appsettings.json
- ✅ **DOCUMENTATO**: Procedura corretta di avvio
- ⏳ **IN CORSO**: Test connettività Client-Server

---

**Prossimi Passi**: Testare la connessione Client-Server con entrambi i progetti in esecuzione.
