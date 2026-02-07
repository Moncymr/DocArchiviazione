# ========================================
# Script di Diagnosi - Problemi di Login
# ========================================
# Questo script verifica automaticamente la configurazione
# e identifica problemi comuni che impediscono il login

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   DocN - Diagnosi Problemi Login" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$issues = @()
$warnings = @()
$success = @()

# ========================================
# 1. Verifica SQL Server
# ========================================
Write-Host "1. Verifica SQL Server..." -ForegroundColor Yellow

$sqlServices = @("MSSQLSERVER", "MSSQL`$SQLEXPRESS", "MSSQL`$SQL2025")
$sqlRunning = $false

foreach ($serviceName in $sqlServices) {
    try {
        $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        if ($service) {
            if ($service.Status -eq "Running") {
                $success += "   ✓ SQL Server in esecuzione: $serviceName"
                $sqlRunning = $true
                break
            } else {
                $warnings += "   ⚠ SQL Server trovato ma non in esecuzione: $serviceName (Status: $($service.Status))"
            }
        }
    } catch {
        # Servizio non trovato, continua
    }
}

if (-not $sqlRunning) {
    $issues += "   ✗ NESSUN SQL Server in esecuzione!"
    $issues += "     Soluzione: Avvia SQL Server con:"
    $issues += "     Start-Service MSSQLSERVER"
    $issues += "     (oppure MSSQL`$SQLEXPRESS o MSSQL`$SQL2025)"
}

# ========================================
# 2. Verifica Server in esecuzione (porta 5211)
# ========================================
Write-Host "2. Verifica DocN Server (porta 5211)..." -ForegroundColor Yellow

$serverRunning = $false
try {
    $connections = netstat -ano | Select-String "5211" | Select-String "LISTENING"
    if ($connections) {
        $success += "   ✓ DocN Server in esecuzione su porta 5211"
        $serverRunning = $true
    } else {
        $issues += "   ✗ DocN Server NON in esecuzione su porta 5211"
        $issues += "     Soluzione: Avvia il Server con:"
        $issues += "     cd DocN.Server"
        $issues += "     dotnet run --launch-profile https"
    }
} catch {
    $issues += "   ✗ Errore verifica porta 5211: $($_.Exception.Message)"
}

# ========================================
# 3. Verifica Client in esecuzione (porta 5036)
# ========================================
Write-Host "3. Verifica DocN Client (porta 5036)..." -ForegroundColor Yellow

$clientRunning = $false
try {
    $connections = netstat -ano | Select-String "5036" | Select-String "LISTENING"
    if ($connections) {
        $success += "   ✓ DocN Client in esecuzione su porta 5036"
        $clientRunning = $true
    } else {
        $warnings += "   ⚠ DocN Client NON in esecuzione su porta 5036"
        $warnings += "     Suggerimento: Avvia il Client con:"
        $warnings += "     cd DocN.Client"
        $warnings += "     dotnet run"
    }
} catch {
    $warnings += "   ⚠ Errore verifica porta 5036: $($_.Exception.Message)"
}

# ========================================
# 4. Verifica file configurazione
# ========================================
Write-Host "4. Verifica file di configurazione..." -ForegroundColor Yellow

$serverAppSettings = "DocN.Server\appsettings.json"
if (Test-Path $serverAppSettings) {
    $success += "   ✓ File $serverAppSettings esiste"
    
    # Controlla connection string
    try {
        $config = Get-Content $serverAppSettings -Raw | ConvertFrom-Json
        if ($config.ConnectionStrings -and $config.ConnectionStrings.DefaultConnection) {
            $connStr = $config.ConnectionStrings.DefaultConnection
            $success += "   ✓ Connection string trovata"
            
            # Mostra info connection string (nascondendo password)
            if ($connStr -match "Server=([^;]+)") {
                $server = $Matches[1]
                $success += "     Server: $server"
            }
            if ($connStr -match "Database=([^;]+)") {
                $database = $Matches[1]
                $success += "     Database: $database"
            }
        } else {
            $issues += "   ✗ Connection string mancante in $serverAppSettings"
            $issues += "     Soluzione: Aggiungi ConnectionStrings.DefaultConnection"
        }
    } catch {
        $warnings += "   ⚠ Impossibile leggere $serverAppSettings : $($_.Exception.Message)"
    }
} else {
    $issues += "   ✗ File $serverAppSettings NON esiste"
    $issues += "     Soluzione: Crea il file con una connection string valida"
}

$clientAppSettings = "DocN.Client\appsettings.json"
if (Test-Path $clientAppSettings) {
    $success += "   ✓ File $clientAppSettings esiste"
    
    try {
        $config = Get-Content $clientAppSettings -Raw | ConvertFrom-Json
        if ($config.BackendApiUrl) {
            $success += "   ✓ BackendApiUrl configurato: $($config.BackendApiUrl)"
        } else {
            $warnings += "   ⚠ BackendApiUrl mancante in $clientAppSettings"
        }
    } catch {
        $warnings += "   ⚠ Impossibile leggere $clientAppSettings : $($_.Exception.Message)"
    }
} else {
    $warnings += "   ⚠ File $clientAppSettings NON esiste (verrà creato automaticamente)"
}

# ========================================
# 5. Verifica database
# ========================================
Write-Host "5. Verifica database DocN..." -ForegroundColor Yellow

$dbExists = $false
if ($sqlRunning) {
    try {
        # Prova a verificare se il database esiste
        # Nota: questo richiede che sqlcmd sia installato
        $result = & sqlcmd -S "(localdb)\mssqllocaldb" -Q "SELECT DB_ID('DocNDb')" -h -1 -W 2>&1
        if ($result -match "NULL" -or $result -match "error") {
            $warnings += "   ⚠ Database DocNDb potrebbe non esistere"
            $warnings += "     Suggerimento: Crea il database con:"
            $warnings += "     cd DocN.Server"
            $warnings += "     dotnet ef database update"
        } else {
            $success += "   ✓ Database DocNDb sembra esistere"
            $dbExists = $true
        }
    } catch {
        $warnings += "   ⚠ Impossibile verificare database (sqlcmd non disponibile)"
        $warnings += "     Suggerimento: Verifica manualmente con SQL Server Management Studio"
    }
} else {
    $issues += "   ✗ Impossibile verificare database: SQL Server non in esecuzione"
}

# ========================================
# 6. Test connessione API
# ========================================
Write-Host "6. Test connessione API Server..." -ForegroundColor Yellow

if ($serverRunning) {
    try {
        # Ignora errori SSL per test locale
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
        
        $response = Invoke-WebRequest -Uri "https://localhost:5211/api/auth/status" -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $success += "   ✓ API Server risponde correttamente"
            $content = $response.Content | ConvertFrom-Json
            $success += "     Autenticato: $($content.isAuthenticated)"
        }
    } catch {
        $issues += "   ✗ Errore connessione API: $($_.Exception.Message)"
        $issues += "     Il Server potrebbe non essere completamente avviato"
    }
} else {
    $warnings += "   ⚠ Impossibile testare API: Server non in esecuzione"
}

# ========================================
# 7. Verifica log del Server
# ========================================
Write-Host "7. Verifica log del Server..." -ForegroundColor Yellow

$logPath = "DocN.Server\logs"
if (Test-Path $logPath) {
    $latestLog = Get-ChildItem $logPath -Filter "docn-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    
    if ($latestLog) {
        $success += "   ✓ File di log trovato: $($latestLog.Name)"
        
        # Cerca messaggi importanti
        $logContent = Get-Content $latestLog.FullName -Tail 100
        
        if ($logContent -match "Created default admin user") {
            $success += "   ✓ Utente admin creato con successo!"
        } elseif ($logContent -match "connection.*error|failed.*connect|network.*error" -and $logContent -match "SQL") {
            $issues += "   ✗ Errore di connessione al database nei log"
            $issues += "     Controlla il log per dettagli: $($latestLog.FullName)"
        }
        
        # Mostra ultime righe del log
        $warnings += "   📄 Ultime righe del log:"
        $logContent | Select-Object -Last 5 | ForEach-Object {
            $warnings += "     $_"
        }
    } else {
        $warnings += "   ⚠ Nessun file di log trovato in $logPath"
    }
} else {
    $warnings += "   ⚠ Directory log non trovata: $logPath"
}

# ========================================
# RISULTATI
# ========================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   RISULTATI DIAGNOSI" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($success.Count -gt 0) {
    Write-Host "✅ SUCCESSI:" -ForegroundColor Green
    $success | ForEach-Object { Write-Host $_ -ForegroundColor Green }
    Write-Host ""
}

if ($warnings.Count -gt 0) {
    Write-Host "⚠️  AVVISI:" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
    Write-Host ""
}

if ($issues.Count -gt 0) {
    Write-Host "❌ PROBLEMI CRITICI:" -ForegroundColor Red
    $issues | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host ""
}

# ========================================
# RACCOMANDAZIONI
# ========================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   RACCOMANDAZIONI" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $sqlRunning) {
    Write-Host "🔴 AZIONE PRIORITARIA: Avvia SQL Server" -ForegroundColor Red
    Write-Host "   Esegui: Start-Service MSSQLSERVER" -ForegroundColor White
    Write-Host ""
}

if (-not $serverRunning) {
    Write-Host "🔴 AZIONE PRIORITARIA: Avvia DocN Server" -ForegroundColor Red
    Write-Host "   cd DocN.Server" -ForegroundColor White
    Write-Host "   dotnet run --launch-profile https" -ForegroundColor White
    Write-Host ""
}

if (-not $dbExists -and $sqlRunning) {
    Write-Host "🟡 AZIONE RACCOMANDATA: Crea il database" -ForegroundColor Yellow
    Write-Host "   cd DocN.Server" -ForegroundColor White
    Write-Host "   dotnet ef database update" -ForegroundColor White
    Write-Host ""
}

if ($sqlRunning -and $serverRunning -and $dbExists) {
    Write-Host "✅ Sistema sembra configurato correttamente!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🔐 Prova il login:" -ForegroundColor Cyan
    Write-Host "   URL:      http://localhost:5036/login" -ForegroundColor White
    Write-Host "   Email:    admin@docn.local" -ForegroundColor White
    Write-Host "   Password: Admin@123" -ForegroundColor White
    Write-Host ""
    Write-Host "⚠️  RICORDA: La password è Admin@123 (con @ non !)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📚 Per maggiori dettagli, consulta:" -ForegroundColor Cyan
Write-Host "   - GUIDA-LOGIN-TROUBLESHOOTING.md" -ForegroundColor White
Write-Host "   - CREDENZIALI-DEFAULT.md" -ForegroundColor White
Write-Host "   - SWAGGER-ERROR-FIX.md" -ForegroundColor White
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
