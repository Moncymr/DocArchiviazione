using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DocN.Data;

/// <summary>
/// Design-time factory for ApplicationDbContext to enable EF migrations
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        // Use a default connection string for migrations
        // This will be overridden at runtime by the actual configuration
        optionsBuilder.UseSqlServer("Server=localhost;Database=DocN;Integrated Security=True;TrustServerCertificate=True;");
        
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
