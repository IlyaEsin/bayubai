using Bayubai.Identity.Persistence;
using Bayubai.SharedKernel.Consultants;
using Microsoft.EntityFrameworkCore;

namespace Bayubai.Identity.Tests;

public class ConsultantQueryFilterModelTests
{
    [Fact]
    public void Every_consultant_owned_entity_has_a_query_filter()
    {
        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql("Host=localhost", IdentityModuleDbContext.ConfigureNpgsql)
            .Options;
        using var db = new IdentityModuleDbContext(options, new NoCurrentConsultant());

        var owned = db.Model.GetEntityTypes()
            .Where(entityType => typeof(IConsultantOwned).IsAssignableFrom(entityType.ClrType))
            .ToList();

        owned.Select(entityType => entityType.ClrType.Name).Order().ShouldBe(new[] { "ClientLink", "Invitation" });
        owned.ShouldAllBe(entityType => entityType.GetDeclaredQueryFilters().Count > 0);
    }
}
