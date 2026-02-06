# SOLUZIONE RAPIDA - Server Si Chiude Immediatamente

## 🚨 PROBLEMA DAL TUO LOG

```
The program '[44448] DocN.Server.exe' has exited with code 0 (0x0).
The program '[27792] DocN.Client.exe' has exited with code 4294967295 (0xffffffff).
```

Il **Server si chiude** subito dopo essersi avviato, poi il **Client crasha** perché non trova più il Server.

---

## ✅ SOLUZIONE IMMEDIATA

### Verifica la Configurazione Visual Studio

1. **Click destro sulla Solution** (in alto nel Solution Explorer)

2. **Seleziona "Proprietà"** (o "Properties")

3. **Nel menu a sinistra** → "Startup Project"

4. **Verifica che sia selezionato** "Multiple startup projects"

5. **CONTROLLA CHE SIA COSÌ**:

   ```
   Project               | Action
   ---------------------|---------------------------
   ✅ DocN.Server       | Start (NOT "Start without debugging")
   ✅ DocN.Client       | Start (NOT "Start without debugging")
   ```

   **SE VEDI "Start without debugging" → CAMBIA A "Start"!**

6. **Click "Apply"** poi **"OK"**

7. **Riavvia con F5**

---

## 🔍 Come Capire se Hai Questo Problema

Guarda il dropdown in alto in Visual Studio dove c'è il pulsante Start:

❌ **SBAGLIATO**: 
```
[DocN.Server] Start without debugging  ← Questo è SBAGLIATO!
```

✅ **CORRETTO**:
```
Multiple startup projects
```

---

## 💡 Perché Succede?

**"Start without debugging"**:
- Avvia il progetto
- Ma NON lo mantiene aperto
- Si chiude subito dopo aver completato l'inizializzazione
- Il Client non trova più il Server e crasha

**"Start" (with debugging)**:
- Avvia il progetto
- Lo MANTIENE aperto
- Continua a girare finché non lo fermi manualmente
- Il Client può connettersi e tutto funziona ✅

---

## 📋 Checklist Veloce

Prima di premere F5, verifica:

- [ ] Multiple startup projects è selezionato?
- [ ] DocN.Server ha Action = "Start"?
- [ ] DocN.Client ha Action = "Start"?
- [ ] DocN.Server è PRIMA di DocN.Client nell'ordine?
- [ ] Nessuno dei due è su "Start without debugging"?

Se hai risposto **SÌ** a tutti, premi F5 e dovrebbe funzionare!

---

## 🎯 Risultato Atteso

Dopo aver premuto F5, dovresti vedere:

1. **2 finestre** del browser si aprono
2. **Output window** in Visual Studio mostra entrambi i progetti in esecuzione
3. **Nessun processo si chiude** da solo
4. **Entrambi** rimangono aperti finché non premi "Stop"

---

## 📞 Se Ancora Non Funziona

Se dopo questa modifica il problema persiste:

1. **Chiudi Visual Studio completamente**
2. **Riapri la Solution**
3. **Verifica di nuovo** la configurazione Multiple Startup Projects
4. **Prova a lanciare SOLO il Server** prima
   - Se anche il Server da solo si chiude immediatamente, c'è un altro problema
5. **Controlla l'Output window** per messaggi di errore

---

**Ultimo aggiornamento**: 6 Febbraio 2026 - Fix configurazione Visual Studio
