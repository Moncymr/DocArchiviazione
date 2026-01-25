# Database Scripts

This directory contains SQL scripts for database setup and management.

## Scripts

### CheckVersion.sql

**Database version check script** - Quick script to verify which version of the database is currently installed and whether it needs updating.

```bash
sqlcmd -S your_server -d DocN -i Database/CheckVersion.sql
```

This script will show:
- ✅ Current database version
- ✅ List of applied migrations
- ✅ Whether Dashboard tables exist
- ⚠️  Whether an update is needed

### ManualUpdate_To_V3.sql

**Manual update script with existence checks** - SQL script that manually checks for the existence of tables and columns before creating/adding them.

```bash
sqlcmd -S your_server -d DocN -i Database/ManualUpdate_To_V3.sql
```

**Features:**
- ✅ Checks if tables exist before creating them
- ✅ Checks if columns exist before adding them
- ✅ Safe to run multiple times (idempotent)
- ✅ Transaction-based with automatic rollback on error
- ✅ Detailed progress output in Italian/English
- ✅ Updates `__EFMigrationsHistory` table

**What it adds:**
- Dashboard tables: `DashboardWidgets`, `SavedSearches`, `UserActivities`
- Workflow fields on `Documents` table
- Enhanced fields on `DocumentChunks` table
- Golden Dataset tables (if not already present)

### CreateDatabase_Complete_V3.sql

**Complete database creation script** - This is an idempotent script that creates the entire DocN database schema including all tables, indexes, and constraints.

**This script can also be used to update an existing database** - it's idempotent and will only apply missing migrations.

### UPDATE_GUIDE.md

**Database update guide** - Comprehensive guide explaining how to update an existing database to the current version using different methods:
- Entity Framework Migrations (recommended)
- Idempotent SQL script
- Custom incremental scripts

See [UPDATE_GUIDE.md](UPDATE_GUIDE.md) for detailed instructions on updating from previous versions.

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

The database schema will be created. When you start the application for the first time, the `ApplicationSeeder` service will automatically create the default admin user:
- **Email:** `admin@docn.local`
- **Password:** `Admin@123`

See [CREDENZIALI_LOGIN.md](../CREDENZIALI_LOGIN.md) for more details.

**Note:** The SQL script only creates the database schema (tables, indexes, constraints). Initial data seeding (like the admin user) is performed automatically by the application when it starts for the first time.

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
