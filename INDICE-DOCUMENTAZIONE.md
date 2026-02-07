# 📚 Indice Documentazione - DocN

## 🆘 HAI UN PROBLEMA? INIZIA QUI:

### 🔐 Non Riesci a Fare il Login?
👉 **[LOGIN-FIX-RAPIDO.md](./LOGIN-FIX-RAPIDO.md)** - Soluzione in 3 passi (< 5 minuti)

Alternative:
- **[GUIDA-LOGIN-TROUBLESHOOTING.md](./GUIDA-LOGIN-TROUBLESHOOTING.md)** - Guida completa e dettagliata
- **[diagnose-login.ps1](./diagnose-login.ps1)** - Script automatico di diagnosi

### ❓ Non Sai la Password di Admin?
👉 **[CREDENZIALI-DEFAULT.md](./CREDENZIALI-DEFAULT.md)** - Credenziali e istruzioni

**Quick answer:**
```
Email:    admin@docn.local
Password: Admin@123  (con @ non !)
```

### ⚠️ Swagger Mostra Errore 500?
👉 **[SWAGGER-ERROR-FIX.md](./SWAGGER-ERROR-FIX.md)** - Fix database connection

### 📋 Panoramica di Tutte le Issues
👉 **[RIEPILOGO-ISSUES.md](./RIEPILOGO-ISSUES.md)** - Riepilogo completo

---

## 📖 Documentazione Generale

### Per Iniziare

| Documento | Descrizione |
|-----------|-------------|
| **[LEGGIMI-PRIMA.md](./LEGGIMI-PRIMA.md)** | Prima lettura - introduzione al progetto |
| **[ISTRUZIONI-UTENTE.md](./ISTRUZIONI-UTENTE.md)** | Manuale utente completo |
| **[HOWTO-RUN.md](./HOWTO-RUN.md)** | Come avviare l'applicazione |
| **[README.md](./README.md)** | README principale (in inglese) |

### Setup e Configurazione

| Documento | Descrizione |
|-----------|-------------|
| **[VISUAL_STUDIO_SETUP_IT.md](./VISUAL_STUDIO_SETUP_IT.md)** | Setup Visual Studio (italiano) |
| **[VISUAL_STUDIO_SETUP.md](./VISUAL_STUDIO_SETUP.md)** | Setup Visual Studio (inglese) |
| **[PORT-CONFIGURATION.md](./PORT-CONFIGURATION.md)** | Configurazione porte |
| **[REBUILD-INSTRUCTIONS.md](./REBUILD-INSTRUCTIONS.md)** | Istruzioni per rebuild |

### Guide Tecniche

| Documento | Descrizione |
|-----------|-------------|
| **[IMPLEMENTATION_SUMMARY.md](./IMPLEMENTATION_SUMMARY.md)** | Riepilogo implementazione |
| **[ANALISI_RAG_E_PROMPT_IMPLEMENTAZIONE.md](./ANALISI_RAG_E_PROMPT_IMPLEMENTAZIONE.md)** | Analisi RAG e prompt |
| **[GUIDA_DOCUMENTAZIONE.md](./GUIDA_DOCUMENTAZIONE.md)** | Guida alla documentazione |

### Status e Completamento

| Documento | Descrizione |
|-----------|-------------|
| **[STATUS-FINALE.md](./STATUS-FINALE.md)** | Status finale del progetto |
| **[COMPLETAMENTO-LAVORO.md](./COMPLETAMENTO-LAVORO.md)** | Completamento lavoro |
| **[FIXES_SUMMARY.md](./FIXES_SUMMARY.md)** | Riepilogo fix applicati |
| **[STATO_REALE_E_PROSSIME_VERSIONI.md](./STATO_REALE_E_PROSSIME_VERSIONI.md)** | Stato e prossime versioni |

### Quick Reference

| Documento | Descrizione |
|-----------|-------------|
| **[QUICK_REFERENCE.md](./QUICK_REFERENCE.md)** | Riferimento rapido |
| **[SOLUZIONE-RAPIDA.md](./SOLUZIONE-RAPIDA.md)** | Soluzioni rapide |
| **[FIX-FINALE-RIEPILOGO.md](./FIX-FINALE-RIEPILOGO.md)** | Riepilogo fix finale |
| **[SOMMARIO_ESECUTIVO.md](./SOMMARIO_ESECUTIVO.md)** | Sommario esecutivo |

---

## 🚀 Quick Start

### 1. Avvia l'applicazione

**Windows:**
```powershell
.\start-docn.ps1
```

**Linux/Mac:**
```bash
./start-docn.sh
```

**Windows (bat):**
```cmd
start-docn.bat
```

### 2. Accedi all'applicazione

```
URL:      http://localhost:5036/login
Email:    admin@docn.local
Password: Admin@123
```

### 3. Se hai problemi

Esegui lo script di diagnosi:
```powershell
.\diagnose-login.ps1
```

---

## 🆘 Supporto

### Hai un problema non coperto?

1. **Controlla prima:**
   - [RIEPILOGO-ISSUES.md](./RIEPILOGO-ISSUES.md) - Lista di tutte le issues conosciute
   - [GUIDA-LOGIN-TROUBLESHOOTING.md](./GUIDA-LOGIN-TROUBLESHOOTING.md) - Troubleshooting login
   - [SWAGGER-ERROR-FIX.md](./SWAGGER-ERROR-FIX.md) - Fix errori database/Swagger

2. **Esegui diagnosi automatica:**
   ```powershell
   .\diagnose-login.ps1
   ```

3. **Consulta i log:**
   - Server: `DocN.Server/logs/docn-[data].log`
   - Output console durante l'esecuzione

4. **Controlla la configurazione:**
   - `DocN.Server/appsettings.json` - Connection string e configurazione server
   - `DocN.Client/appsettings.json` - URL del backend

---

## 📊 Struttura Progetto

```
DocArchiviazione/
├── DocN.Client/          # Applicazione Blazor Client
├── DocN.Server/          # API REST Backend
├── DocN.Core/            # Business Logic e Servizi
├── DocN.Data/            # Data Access Layer e Modelli
├── docs/                 # Documentazione aggiuntiva
└── *.md                  # Documentazione principale (25 file)
```

---

## 🔗 Link Utili

- **Repository GitHub:** [Moncymr/DocArchiviazione](https://github.com/Moncymr/DocArchiviazione)
- **Server API:** https://localhost:5211
- **Client Web:** http://localhost:5036
- **Swagger UI:** https://localhost:5211/swagger

---

## 📝 Note

- Tutti i documenti con suffisso `_IT` o in italiano sono tradotti per utenti italiani
- I documenti in inglese sono principalmente per sviluppatori
- Le guide "RAPIDO" o "QUICK" sono versioni condensate per soluzione veloce
- Le guide "TROUBLESHOOTING" sono complete e dettagliate

---

**Ultimo aggiornamento:** 7 Febbraio 2026  
**Branch:** copilot/fix-client-crash-visual-studio  
**Documentazione totale:** 25+ file markdown
