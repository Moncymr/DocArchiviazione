# 🔨 ISTRUZIONI PER REBUILD DOPO IL FIX

## ⚠️ IMPORTANTE: Devi fare REBUILD!

Dopo aver fatto pull di questo branch, **DEVI fare un clean rebuild** per usare il codice aggiornato.

---

## 📋 STEP-BY-STEP

### Opzione 1: Da Visual Studio (CONSIGLIATO)

1. **Pull il branch**:
   ```
   git checkout copilot/fix-client-crash-visual-studio
   git pull
   ```

2. **Clean Solution**:
   - Menu: `Build` → `Clean Solution`
   - Oppure click destro su Solution → `Clean Solution`

3. **Rebuild Solution**:
   - Menu: `Build` → `Rebuild Solution`
   - Oppure click destro su Solution → `Rebuild Solution`

4. **Verifica Build**:
   - Controlla l'Output window
   - Deve dire: `Build succeeded`
   - 0 Errors (warnings OK)

5. **Run**:
   - Configure "Multiple startup projects"
   - Start Server FIRST
   - Start Client SECOND
   - Press F5

---

### Opzione 2: Da Command Line

```bash
# 1. Pull changes
git checkout copilot/fix-client-crash-visual-studio
git pull

# 2. Clean everything
dotnet clean

# 3. Rebuild Client
cd DocN.Client
dotnet build --no-incremental

# 4. Rebuild Server (se necessario)
cd ../DocN.Server
dotnet build --no-incremental

# 5. Run Server (Terminal 1)
cd ../DocN.Server
dotnet run --launch-profile https

# 6. Run Client (Terminal 2)
cd ../DocN.Client
dotnet run
```

---

## ✅ COSA È STATO FIXATO

### Problema 1: ProtectedSessionStorage (CLIENT)
**PRIMA**:
```
Unable to resolve service for type 'ProtectedSessionStorage'
```

**DOPO**: ✅ Risolto
- Sostituito con `HttpContextAccessor` + `Session`
- Compatible con Static Rendering
- Non richiede più Interactive Server mode

### Problema 2: IOCRService (SERVER)
Questo problema è ancora da fixare nel Server.

---

## 🔍 VERIFICA CHE IL FIX FUNZIONI

### Dopo il Rebuild, verifica:

**1. Build Output**:
```
✅ Build succeeded
   0 Error(s)
   [warnings OK]
```

**2. Client Start**:
```
Upload directory created/verified
HTTP request pipeline configured successfully ✓
Configuring Razor Components...
Razor Components configured successfully ✓
Starting the application...
Now listening on: http://localhost:5036
✅ [NO CRASH]
```

**3. No Errors**:
- ❌ Se vedi ancora "ProtectedSessionStorage" → NON hai fatto rebuild!
- ❌ Se vedi ancora crash → Controlla di aver pulito i vecchi bin/obj

---

## 🛑 SE NON FUNZIONA

### 1. Clean Manuale (più aggressivo)

```bash
# Remove all bin and obj folders
cd DocN.Client
rm -rf bin obj

cd ../DocN.Server  
rm -rf bin obj

cd ../DocN.Core
rm -rf bin obj

cd ../DocN.Data
rm -rf bin obj

# Rebuild
cd ..
dotnet build
```

### 2. Verifica che hai il codice aggiornato

```bash
# Check CustomAuthenticationStateProvider.cs
grep "IHttpContextAccessor" DocN.Client/Services/CustomAuthenticationStateProvider.cs
```

**Expected**: Deve trovare "IHttpContextAccessor"  
**If NOT found**: Fai pull di nuovo!

### 3. Verifica Program.cs

```bash
# Check Session services
grep "AddSession" DocN.Client/Program.cs
```

**Expected**: Deve trovare "AddSession"  
**If NOT found**: Fai pull di nuovo!

---

## 📝 FILES MODIFICATI IN QUESTO FIX

1. ✅ `DocN.Client/Services/CustomAuthenticationStateProvider.cs`
   - Rimosso `ProtectedSessionStorage`
   - Aggiunto `IHttpContextAccessor`
   - Usa `HttpContext.Session`

2. ✅ `DocN.Client/Program.cs`
   - Aggiunto `AddHttpContextAccessor()`
   - Aggiunto `AddSession()`
   - Aggiunto `UseSession()`

---

## 🎯 RISULTATO ATTESO

Dopo rebuild e run:

✅ **Client Build**: Success (0 errors)  
✅ **Client Start**: No crash  
✅ **Authentication**: Session-based, funzionante  
✅ **Login/Logout**: Funzionano  

⚠️ **Server**: Potrebbe ancora avere problema con `IOCRService` (fix separato necessario)

---

## 🆘 HELP

Se dopo aver seguito TUTTI questi step vedi ancora l'errore:

1. **Verifica di essere sul branch giusto**:
   ```bash
   git branch
   # Deve mostrare: * copilot/fix-client-crash-visual-studio
   ```

2. **Verifica l'ultimo commit**:
   ```bash
   git log -1 --oneline
   # Deve mostrare: "Replace ProtectedSessionStorage..."
   ```

3. **Elimina TUTTO e riclona** (ultima risorsa):
   ```bash
   cd ..
   rm -rf DocArchiviazione
   git clone [repo-url]
   cd DocArchiviazione
   git checkout copilot/fix-client-crash-visual-studio
   dotnet build
   ```

---

**Data**: 7 Febbraio 2026  
**Branch**: copilot/fix-client-crash-visual-studio  
**Status**: ✅ PRONTO (dopo rebuild)
