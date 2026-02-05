# Configurazione Avvio Multi-Progetto in Visual Studio

## Il Problema
Quando si avvia DocN da Visual Studio premendo F5, sia il Client che il Server partono simultaneamente. Se il Server non è pronto quando il Client si avvia, possono verificarsi errori come:
- "Unable to connect to server. Please check your connection"
- L'applicazione Client va in crash
- Le pagine di Login/Registrazione non funzionano

## La Soluzione

Abbiamo implementato **tre livelli di protezione** per garantire un avvio fluido da Visual Studio:

### 1. Configurazione di Avvio Visual Studio (`.slnLaunch.vs.json`)
Questo file dice a Visual Studio 2022+ di:
- Avviare il Server **per primo**
- Attendere 5 secondi
- Poi avviare il Client

**Posizione:** `.slnLaunch.vs.json` nella directory radice della soluzione

### 2. Controllo di Salute all'Avvio del Client
Il Client ora attende automaticamente che il Server sia pronto durante l'avvio:
- Controlla l'endpoint di salute del Server (`/health`)
- Riprova fino a 30 volte con backoff esponenziale
- Tempo di attesa massimo: ~2,5 minuti
- Fornisce feedback chiaro sulla console sullo stato del Server

**Implementazione:** `DocN.Client/Services/ServerHealthCheckService.cs`

### 3. Gestione Graziosa degli Errori
Tutte le pagine Client che comunicano con il Server ora gestiscono gracefully i fallimenti di connessione:
- La pagina Login mostra: "Unable to connect to server"
- Altre pagine mostrano messaggi di errore user-friendly
- Nessun crash o eccezioni non gestite

## Come Configurare Visual Studio

### Per Visual Studio 2022+

Il file `.slnLaunch.vs.json` è già configurato. Semplicemente:

1. Apri `Doc_archiviazione.sln` in Visual Studio
2. Premi **F5** per avviare il debug

Il Server partirà per primo, seguito dal Client dopo un ritardo di 5 secondi.

### Per Visual Studio 2019 o Precedenti

Queste versioni non supportano `.slnLaunch.vs.json`. Configura manualmente:

1. Fai clic destro sulla **Soluzione** in Esplora Soluzioni
2. Seleziona **Imposta progetti di avvio...**
3. Scegli **Progetti di avvio multipli**
4. Imposta l'azione per entrambi i progetti:
   - **DocN.Server** → **Avvio** (sposta in alto)
   - **DocN.Client** → **Avvio**
5. Fai clic su **OK**

**Nota:** VS 2019 non supporta ritardi di avvio, quindi potrebbe apparire un breve messaggio "Server non disponibile" durante l'avvio del Client. Il Client riproverà automaticamente e si connetterà una volta che il Server è pronto.

### Alternativa: Avvio Manuale (Più Affidabile)

Per l'avvio più affidabile, avvia le applicazioni separatamente:

#### Windows (PowerShell)
```powershell
.\start-docn.ps1
```

#### Windows (Prompt dei Comandi)
```cmd
start-docn.bat
```

#### Linux/macOS
```bash
./start-docn.sh
```

Questi script:
1. Avviano il Server
2. Attendono 10 secondi per l'inizializzazione
3. Avviano il Client
4. Mostrano i log da entrambe le applicazioni

## Risoluzione Problemi

### Avviso "Server not available" Durante l'Avvio

**Questo è normale!** Il Client attende automaticamente il Server. Vedrai:

```
════════════════════════════════════════════════════════════════
Checking Server availability...
════════════════════════════════════════════════════════════════
Server not available yet. Retry 1/30 in 1000ms...
Server not available yet. Retry 2/30 in 1650ms...
✅ Server is available and ready
════════════════════════════════════════════════════════════════
```

**Attendi il messaggio ✅** prima di aprire il browser.

### Il Server Non Diventa Mai Disponibile

Se vedi:
```
⚠️  WARNING: Server is not available
```

**Controlla:**
1. **Connessione Database**: Assicurati che SQL Server sia in esecuzione e la stringa di connessione sia corretta
2. **Console Server**: Cerca errori nella finestra di output del Server
3. **File di Configurazione**: Verifica che `appsettings.json` esista in entrambi i progetti
4. **Conflitti di Porta**: Assicurati che le porte 5210/5211 (Server) e 5036/7114 (Client) siano disponibili

### Il Client Va in Crash Immediatamente

Se il Client va in crash anche con queste correzioni:

1. **Controlla il log degli errori**: `DocN.Client/bin/Debug/net10.0/client-crash.log`
2. **Problemi Database**: Assicurati che il Server abbia seminato con successo il database
3. **Problemi Dipendenze**: Esegui `dotnet restore` nella directory della soluzione
4. **Problemi Build**: Esegui `dotnet build` e controlla gli errori di compilazione

### "Unable to connect to server" sulla Pagina di Login

Questo significa che il Server non è raggiungibile. Verifica:

1. **Il Server è in esecuzione**: Controlla Task Manager / Monitoraggio Attività
2. **URL Corretto**: Il predefinito è `https://localhost:5211/`
3. **Controlla `appsettings.json`**:
   ```json
   {
     "BackendApiUrl": "https://localhost:5211/"
   }
   ```
4. **Firewall**: Assicurati che Windows Firewall non stia bloccando le connessioni localhost
5. **Certificato HTTPS**: Accetta il certificato di sviluppo quando richiesto

## Dettagli Tecnici

### Endpoint di Salute del Server

Il Server espone endpoint di controllo salute:
- `/health` - Salute complessiva del sistema (usato dal controllo di avvio del Client)
- `/health/live` - Probe di vitalità (il server è in esecuzione)
- `/health/ready` - Probe di prontezza (il server è pronto ad accettare richieste)

### Configurazione Controllo Salute Client

Impostazioni predefinite (possono essere modificate in `Program.cs`):
- **Max Tentativi**: 30
- **Ritardo Iniziale**: 1000ms (1 secondo)
- **Ritardo Massimo**: 5000ms (5 secondi)
- **Timeout Totale**: 3 minuti
- **Strategia Backoff**: Esponenziale con jitter

### Perché è Importante

Quando entrambi i progetti partono simultaneamente da Visual Studio:
1. **Senza queste correzioni**: Il Client prova a connettersi immediatamente → Server non pronto → Errori di connessione → Confusione utente
2. **Con queste correzioni**: Il Client attende il Server → Il Server diventa pronto → Il Client si connette con successo → Utenti felici! 🎉

## Risorse Aggiuntive

- **README Principale**: `README.md` - Setup generale e utilizzo
- **Documentazione Italiana**: `LEGGIMI.md` - Documentazione completa in italiano
- **Architettura**: `docs/ARCHITECTURE_FIX_SUMMARY_IT.md` - Dettagli architettura tecnica
- **Script di Avvio**: `start-docn.ps1`, `start-docn.bat`, `start-docn.sh` - Metodi di avvio alternativi

## Riepilogo

✅ **Non devi fare niente di speciale!**

Il sistema è configurato per gestire automaticamente l'avvio concorrente. Basta premere F5 in Visual Studio e attendere che entrambe le applicazioni partano. Il Client attenderà automaticamente che il Server sia pronto.

Se incontri problemi, usa uno degli script di avvio (`start-docn.ps1` / `.bat` / `.sh`) per l'esperienza più affidabile.
