# Visual Studio Multi-Project Startup Configuration

## The Problem
When launching DocN from Visual Studio using F5, both the Client and Server start simultaneously. If the Server is not ready when the Client starts, you may see errors like:
- "Unable to connect to server. Please check your connection"
- Client application crashes
- Login/Registration pages don't work

## The Solution

We've implemented **three layers of protection** to ensure smooth startup from Visual Studio:

### 1. Visual Studio Launch Configuration (`.slnLaunch.vs.json`)
This file tells Visual Studio 2022+ to:
- Start the Server **first**
- Wait 5 seconds
- Then start the Client

**Location:** `.slnLaunch.vs.json` in the solution root directory

### 2. Client Startup Health Check
The Client now automatically waits for the Server to be ready during startup:
- Checks Server health endpoint (`/health`)
- Retries up to 30 times with exponential backoff
- Maximum wait time: ~2.5 minutes
- Provides clear console feedback about Server status

**Implementation:** `DocN.Client/Services/ServerHealthCheckService.cs`

### 3. Graceful Error Handling
All Client pages that communicate with the Server now handle connection failures gracefully:
- Login page shows: "Unable to connect to server"
- Other pages display user-friendly error messages
- No crashes or unhandled exceptions

## How to Configure Visual Studio

### For Visual Studio 2022+

The `.slnLaunch.vs.json` file is already configured. Simply:

1. Open `Doc_archiviazione.sln` in Visual Studio
2. Press **F5** to start debugging

The Server will start first, followed by the Client after a 5-second delay.

### For Visual Studio 2019 or Earlier

These versions don't support `.slnLaunch.vs.json`. Configure manually:

1. Right-click the **Solution** in Solution Explorer
2. Select **Set Startup Projects...**
3. Choose **Multiple startup projects**
4. Set the action for both projects:
   - **DocN.Server** → **Start** (move to top)
   - **DocN.Client** → **Start**
5. Click **OK**

**Note:** VS 2019 doesn't support startup delays, so there may be a brief "Server not available" message during Client startup. The Client will automatically retry and connect once the Server is ready.

### Alternative: Manual Startup (Most Reliable)

For the most reliable startup, launch the applications separately:

#### Windows (PowerShell)
```powershell
.\start-docn.ps1
```

#### Windows (Command Prompt)
```cmd
start-docn.bat
```

#### Linux/macOS
```bash
./start-docn.sh
```

These scripts:
1. Start the Server
2. Wait 10 seconds for initialization
3. Start the Client
4. Display logs from both applications

## Troubleshooting

### "Server not available" Warning During Startup

**This is normal!** The Client waits for the Server automatically. You'll see:

```
════════════════════════════════════════════════════════════════
Checking Server availability...
════════════════════════════════════════════════════════════════
Server not available yet. Retry 1/30 in 1000ms...
Server not available yet. Retry 2/30 in 1650ms...
✅ Server is available and ready
════════════════════════════════════════════════════════════════
```

**Wait for the ✅ message** before opening your browser.

### Server Never Becomes Available

If you see:
```
⚠️  WARNING: Server is not available
```

**Check:**
1. **Database Connection**: Ensure SQL Server is running and connection string is correct
2. **Server Console**: Look for errors in the Server output window
3. **Configuration Files**: Verify `appsettings.json` exists in both projects
4. **Port Conflicts**: Ensure ports 5210/5211 (Server) and 5036/7114 (Client) are available

### Client Crashes Immediately

If the Client crashes even with these fixes:

1. **Check the error log**: `DocN.Client/bin/Debug/net10.0/client-crash.log`
2. **Database Issues**: Ensure the Server has successfully seeded the database
3. **Dependency Issues**: Run `dotnet restore` in the solution directory
4. **Build Issues**: Run `dotnet build` and check for compilation errors

### "Unable to connect to server" on Login Page

This means the Server is not reachable. Verify:

1. **Server is running**: Check Task Manager / Activity Monitor
2. **Correct URL**: Default is `https://localhost:5211/`
3. **Check `appsettings.json`**:
   ```json
   {
     "BackendApiUrl": "https://localhost:5211/"
   }
   ```
4. **Firewall**: Ensure Windows Firewall isn't blocking localhost connections
5. **HTTPS Certificate**: Accept the development certificate when prompted

## Technical Details

### Server Health Endpoint

The Server exposes health check endpoints:
- `/health` - Overall system health (used by Client startup check)
- `/health/live` - Liveness probe (server is running)
- `/health/ready` - Readiness probe (server is ready to accept requests)

### Client Health Check Configuration

Default settings (can be adjusted in `Program.cs`):
- **Max Retries**: 30
- **Initial Delay**: 1000ms (1 second)
- **Max Delay**: 5000ms (5 seconds)
- **Total Timeout**: 3 minutes
- **Backoff Strategy**: Exponential with jitter

### Why This Matters

When both projects start simultaneously from Visual Studio:
1. **Without these fixes**: Client tries to connect immediately → Server not ready → Connection errors → User confusion
2. **With these fixes**: Client waits for Server → Server becomes ready → Client connects successfully → Happy users! 🎉

## Additional Resources

- **Main README**: `README.md` - General setup and usage
- **Italian Documentation**: `LEGGIMI.md` - Documentazione in italiano
- **Architecture**: `docs/ARCHITECTURE_FIX_SUMMARY_IT.md` - Technical architecture details
- **Startup Scripts**: `start-docn.ps1`, `start-docn.bat`, `start-docn.sh` - Alternative startup methods

## Summary

✅ **You don't need to do anything special!**

The system is configured to handle concurrent startup automatically. Just press F5 in Visual Studio and wait for both applications to start. The Client will automatically wait for the Server to be ready.

If you encounter any issues, use one of the startup scripts (`start-docn.ps1` / `.bat` / `.sh`) for the most reliable experience.
