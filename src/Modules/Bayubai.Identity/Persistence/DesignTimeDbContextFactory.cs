using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bayubai.Identity.Persistence;

// Used only by dotnet-ef to generate migrations; it never opens a connection.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql("Host=localhost;Database=bayubai_design", IdentityModuleDbContext.ConfigureNpgsql)
            .Options;
        return new IdentityModuleDbContext(options, new NoCurrentConsultant());
    }
}
