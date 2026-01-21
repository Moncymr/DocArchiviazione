using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocN.Data;

/// <summary>
/// Design-time factory for DocArcContext - used only for EF Core migrations.
/// The connection string here is only used during migration generation and should match your development environment.
/// At runtime, the actual connection string is injected from configuration.
/// </summary>
public class DocArcContextFactory : IDesignTimeDbContextFactory<DocArcContext>
{
    public DocArcContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocArcContext>();
        // This connection string is only used for design-time operations (migrations)
        optionsBuilder.UseSqlServer("Server=NTSPJ-060-02\\SQL2025;Database=DocNDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");

        return new DocArcContext(optionsBuilder.Options);
    }
}
