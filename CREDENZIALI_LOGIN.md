# Credenziali di Accesso Predefinite / Default Login Credentials

## 🔐 Utente Amministratore / Admin User

Dopo aver creato il database e avviato l'applicazione per la prima volta, utilizza queste credenziali per accedere:

After creating the database and starting the application for the first time, use these credentials to login:

### Credenziali / Credentials:
```
Email:    admin@docn.local
Password: Admin@123
```

**⚠️ ATTENZIONE / WARNING:**
- La password è `Admin@123` (con la "A" maiuscola)
- NON è `Amministratore@123`
- NON è `admin@123` (minuscolo)
- Cambiare la password immediatamente dopo il primo accesso!

**⚠️ IMPORTANT:**
- The password is `Admin@123` (with capital "A")
- NOT `Amministratore@123`
- NOT `admin@123` (lowercase)
- Change the password immediately after first login!

## 🔍 Risoluzione Problemi / Troubleshooting

### Non riesco ad accedere / Cannot login

1. **Verifica le credenziali / Check credentials:**
   - Email: `admin@docn.local` (tutto minuscolo / all lowercase)
   - Password: `Admin@123` (esattamente come scritto / exactly as written)

2. **Verifica che il database sia stato creato / Check database was created:**
   ```sql
   -- Verifica che l'utente esista / Check if user exists
   SELECT * FROM AspNetUsers WHERE Email = 'admin@docn.local';
   
   -- Verifica che l'utente abbia il ruolo Admin / Check if user has Admin role
   SELECT u.Email, r.Name 
   FROM AspNetUsers u
   JOIN AspNetUserRoles ur ON u.Id = ur.UserId
   JOIN AspNetRoles r ON ur.RoleId = r.Id
   WHERE u.Email = 'admin@docn.local';
   ```

3. **Se l'utente non esiste / If user doesn't exist:**
   - Esegui lo script SQL: `Database/CreateDatabase_Complete_V2.sql`
   - Oppure avvia l'applicazione e l'`ApplicationSeeder` lo creerà automaticamente
   
4. **Reset password (se necessario) / Reset password (if needed):**
   - Usa lo strumento di reset password nell'interfaccia
   - Oppure elimina l'utente dal database e riavvia l'applicazione

## 📝 Note Tecniche / Technical Notes

L'utente amministratore può essere creato in due modi / The admin user can be created in two ways:

1. **SQL Script** (`CreateDatabase_Complete_V2.sql`):
   - Crea l'utente direttamente nel database con un hash password pre-calcolato
   - Creates the user directly in the database with a pre-calculated password hash

2. **ApplicationSeeder** (eseguito all'avvio dell'app / runs on app startup):
   - Crea l'utente tramite `UserManager` se non esiste già
   - Creates the user via `UserManager` if it doesn't exist

**In entrambi i casi, la password è: `Admin@123`**

**In both cases, the password is: `Admin@123`**
