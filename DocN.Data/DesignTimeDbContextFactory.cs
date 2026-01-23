using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocN.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        // Try to load connection string from environment variable or configuration
        var connectionString = Environment.GetEnvironmentVariable("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            // Use a placeholder connection string for design-time operations
            // Developers should set the DefaultConnection environment variable or update appsettings.json
            connectionString = "Server=localhost;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=True";
            Console.WriteLine("Warning: Using default connection string for design-time operations. Set DefaultConnection environment variable for your specific database.");
        }
        
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
