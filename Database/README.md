# Database Scripts

This directory contains SQL scripts for database setup and management.

## Scripts

### CreateDatabase_Complete_V3.sql

**Complete database creation script** - This is an idempotent script that creates the entire DocN database schema including all tables, indexes, and constraints.

**Features:**
- ✅ Idempotent - Can be run multiple times safely
- ✅ Includes all migrations up to `20260124115302_AddDashboardAndRBACFeatures`
- ✅ Creates all necessary tables for:
  - User management (ASP.NET Identity)
  - Document management
  - RAG (Retrieval-Augmented Generation)
  - AI configurations
  - Dashboard widgets
  - Saved searches
  - User activities
  - Audit logs
  - Golden datasets
  - And more...

**Usage:**

1. **Using SQL Server Management Studio (SSMS):**
   ```
   - Open SSMS
   - Connect to your SQL Server instance
   - Create a new database named "DocN" (or your preferred name)
   - Open CreateDatabase_Complete_V3.sql
   - Execute the script
   ```

2. **Using sqlcmd:**
   ```bash
   sqlcmd -S your_server -d DocN -i CreateDatabase_Complete_V3.sql
   ```

3. **Using Entity Framework Migrations (Recommended):**
   ```bash
   # From the solution root directory
   dotnet ef database update --project DocN.Data --startup-project DocN.Server --context ApplicationDbContext
   ```

**After running the script:**

The database will be created with the default admin user:
- **Email:** `admin@docn.local`
- **Password:** `Admin@123`

See [CREDENZIALI_LOGIN.md](../CREDENZIALI_LOGIN.md) for more details.

## Updating the Script

To regenerate this script after adding new migrations:

```bash
# From the solution root directory
dotnet ef migrations script --project DocN.Data --context ApplicationDbContext --output Database/CreateDatabase_Complete_V3.sql --idempotent
```

Or to create a new version:

```bash
dotnet ef migrations script --project DocN.Data --context ApplicationDbContext --output Database/CreateDatabase_Complete_V4.sql --idempotent
```

## Version History

- **V3** (2026-01-25): Includes all migrations up to AddDashboardAndRBACFeatures
  - Added DashboardWidgets table
  - Added SavedSearches table
  - Added UserActivities table
  - Added enhanced workflow state fields to Documents
  - Added golden dataset tables for RAG quality testing
