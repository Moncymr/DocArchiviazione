# ISTRUZIONI PER L'UTENTE - DocArchiviazione

## 🎉 PROBLEMA RISOLTO!

Il crash del Client è stato **completamente risolto**! L'applicazione ora si avvia correttamente.

---

## ❓ PERCHÉ VEDI "Impossibile stabilire la connessione (localhost:5211)"?

**Risposta semplice**: Stai eseguendo **solo il Client**, ma il **Server non è in esecuzione**.

L'applicazione DocArchiviazione è composta da **DUE progetti**:
- 🖥️ **DocN.Server** (Backend API) - deve girare su `https://localhost:5211`
- 🌐 **DocN.Client** (Frontend Web) - gira su `http://localhost:5036`

Il Client **ha bisogno** del Server per funzionare, come un browser ha bisogno di un server web.

---

## 🚀 SOLUZIONE: Eseguire ENTRAMBI i Progetti

### Metodo 1: Visual Studio (PIÙ FACILE) ⭐

Questo è il metodo **consigliato** perché Visual Studio avvia automaticamente entrambi i progetti.

#### Step-by-Step:

1. **Apri la Solution** in Visual Studio

2. **Click destro sulla Solution** (in alto nel Solution Explorer)
   
3. **Seleziona "Proprietà"** (o "Properties")

4. **Nel menu a sinistra**, seleziona **"Startup Project"**

5. **Seleziona "Multiple startup projects"**

6. **Configura i progetti**:
   ```
   ✅ DocN.Server    → Action: Start (WITH debugging, not "Start without debugging")
   ✅ DocN.Client    → Action: Start (WITH debugging, not "Start without debugging")
   ```
   
   **MOLTO IMPORTANTE**:
   - **Entrambi** devono avere Action = **"Start"** (NON "Start without debugging")
   - **DocN.Server** deve essere **PRIMA** di DocN.Client nell'ordine
   - Usa le frecce ↑↓ per cambiare l'ordine se necessario
   
   **Perché è importante?**
   - Se usi "Start without debugging", il progetto si avvia e si chiude immediatamente
   - Entrambi devono rimanere aperti con Visual Studio che li mantiene in esecuzione

7. **Click "OK"**

8. **Premi F5** o click sul pulsante ▶️ Start

#### Risultato:
- Si aprono **2 finestre del browser**:
  - Una per il Server (Swagger) su `https://localhost:5211/swagger`
  - Una per il Client su `http://localhost:5036`
- Entrambi sono in esecuzione contemporaneamente
- **Il Client può ora comunicare con il Server!** ✅

---

### Metodo 2: Riga di Comando (Avanzato)

Se preferisci la riga di comando, devi aprire **DUE terminali**.

#### Terminal 1 - Server (Avvia PRIMA):

```bash
cd C:\Doc_archiviazione\DocN.Server
dotnet run --launch-profile https
```

**Aspetta** che vedi questo messaggio:
```
Now listening on: https://localhost:5211
Application started. Press Ctrl+C to shut down.
```

#### Terminal 2 - Client (Avvia DOPO):

```bash
cd C:\Doc_archiviazione\DocN.Client
dotnet run
```

**Aspetta** che vedi:
```
Now listening on: http://localhost:5036
Application started.
```

#### Verifica:

1. **Apri browser** → `https://localhost:5211/swagger`
   - Se vedi Swagger UI → **Server funziona!** ✅

2. **Apri browser** → `http://localhost:5036`
   - Se vedi la home page → **Client funziona!** ✅

3. **Naviga a** → `http://localhost:5036/documents`
   - Se vedi la lista documenti → **Tutto funziona!** 🎉

---

## 🔍 COME CAPIRE SE FUNZIONA

### ✅ FUNZIONA se vedi:

**Nel browser** (`http://localhost:5036/documents`):
- Lista dei documenti
- Pulsanti per aggiungere/modificare/eliminare
- Nessun errore rosso

### ❌ NON FUNZIONA se vedi:

**Nel browser**:
```
Errore nel caricamento dei documenti:
Impossibile stabilire la connessione.
Rifiuto persistente del computer di destinazione. (localhost:5211)
```

**Significato**: Il Server non è in esecuzione! Torna allo step "Eseguire ENTRAMBI i Progetti" sopra.

---

## 🛠️ TROUBLESHOOTING

### Problema: "Port already in use" (Porta già in uso)

**Soluzione**:
1. Chiudi **tutti** i processi `dotnet.exe` in Task Manager
2. Oppure cambia le porte in `launchSettings.json`

### Problema: "Certificate error" o "SSL error"

**Soluzione** (solo la prima volta):
```bash
dotnet dev-certs https --trust
```

Poi clicca "Sì" quando chiede di fidarsi del certificato.

### Problema: Visual Studio non avvia entrambi i progetti

**Soluzione**:
1. Verifica che "Multiple startup projects" sia selezionato
2. Verifica che entrambi i progetti abbiano Action = "Start"
3. Verifica l'ordine: Server PRIMA, Client DOPO

### Problema: Il Server si chiude immediatamente (exit code 0)

**Sintomo**: Nel log vedi:
```
The program '[XXXXX] DocN.Server.exe' has exited with code 0 (0x0).
```
...e poi il Client crasha.

**Causa**: Hai configurato il Server su "Start without debugging" invece di "Start".

**Soluzione**:
1. Visual Studio → Click destro Solution → Properties
2. Startup Project → Multiple startup projects
3. **DocN.Server** → Cambia da "Start without debugging" a **"Start"**
4. **DocN.Client** → Assicurati sia "Start" (non "Start without debugging")
5. Click OK e riprova F5

**Spiegazione**: "Start without debugging" avvia il progetto ma non lo mantiene aperto. Il Server si avvia, completa la configurazione e si chiude immediatamente. Il Client poi crasha perché non trova più il Server.

---

## 📋 CHECKLIST RAPIDA

Prima di testare l'applicazione:

- [ ] Ho configurato "Multiple startup projects" in Visual Studio?
- [ ] DocN.Server è impostato su "Start"?
- [ ] DocN.Client è impostato su "Start"?
- [ ] DocN.Server è PRIMA di DocN.Client nell'ordine?
- [ ] Ho premuto F5 o Start?
- [ ] Vedo 2 finestre del browser aprirsi?
- [ ] Il Server è su `https://localhost:5211`?
- [ ] Il Client è su `http://localhost:5036`?

Se hai risposto **SÌ** a tutte, tutto dovrebbe funzionare! ✅

---

## 📞 SUPPORTO

Se hai ancora problemi:

1. **Verifica i log** nella finestra Output di Visual Studio
2. **Controlla** che entrambi i progetti siano in esecuzione
3. **Testa manualmente** il Server: `https://localhost:5211/swagger`
4. **Leggi** il file `HOWTO-RUN.md` per dettagli tecnici

---

## 🎯 RIEPILOGO

**Prima del fix**:
- ❌ Client crashava
- ❌ AggregateException
- ❌ Non funzionava da Visual Studio

**Dopo il fix**:
- ✅ Client si avvia correttamente
- ✅ Nessun crash
- ✅ Funziona da Visual Studio
- ✅ Basta eseguire ENTRAMBI i progetti!

---

**Ultimo aggiornamento**: 6 Febbraio 2026

**Versione**: 1.0 - Fix completo crash + configurazione
