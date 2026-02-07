# 🔐 Credenziali di Default - DocN

## Amministratore Sistema

Al primo avvio dell'applicazione, il sistema crea automaticamente un utente amministratore con le seguenti credenziali:

```
Email:    admin@docn.local
Password: Admin@123
```

## ⚠️ IMPORTANTE - Sicurezza

1. **Cambia SUBITO la password** dopo il primo login in ambiente di produzione
2. Queste credenziali sono create automaticamente dal file `DocN.Data/Services/ApplicationSeeder.cs`
3. L'utente admin viene assegnato automaticamente al ruolo **SuperAdmin** con tutti i permessi

## 🚀 Come Accedere

1. Avvia l'applicazione (Server + Client)
2. Apri il browser su: `http://localhost:5036`
3. Clicca su "Login" o vai a: `http://localhost:5036/login`
4. Inserisci le credenziali sopra indicate
5. **CAMBIA LA PASSWORD** nelle impostazioni del tuo profilo

## 🔍 Verifica Utente Admin

Per verificare che l'utente admin sia stato creato correttamente, controlla i log del Server al primo avvio:

```
Created default admin user: admin@docn.local with role: SuperAdmin
⚠️  IMPORTANT: Change the default admin password after first login!
```

## 🛡️ Ruoli e Permessi

L'utente admin ha il ruolo **SuperAdmin** che include:
- Gestione completa degli utenti
- Gestione dei ruoli e permessi
- Accesso a tutte le funzionalità del sistema
- Configurazione del sistema
- Visualizzazione dashboard amministrativa

## 📝 Note

- Il database viene popolato automaticamente al primo avvio
- Se l'utente admin esiste già, non viene ricreato
- Per reimpostare l'utente admin, elimina il database e riavvia l'applicazione

---

**Ultimo aggiornamento**: 7 Febbraio 2026  
**Versione**: 1.0
