using Bayubai.Api.IntegrationTests.Infrastructure;
using Bayubai.Identity;
using Bayubai.Identity.Domain;
using Bayubai.Identity.Persistence;
using Bayubai.SharedKernel.Consultants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Bayubai.Api.IntegrationTests;

public class PersistenceTests(ApiFactory factory)
{
    private sealed class FixedConsultant(Guid? consultantId) : ICurrentConsultant
    {
        public Guid? ConsultantId => consultantId;
    }

    [Fact]
    public async Task Migrations_seed_the_three_roles()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();

        var roles = await db.Roles.Select(role => role.Name!).OrderBy(name => name).ToListAsync();

        roles.ShouldBe(new[] { IdentityRoles.Admin, IdentityRoles.Consultant, IdentityRoles.Parent });
    }

    [Fact]
    public async Task Consultant_filter_scopes_rows_to_the_current_consultant()
    {
        var consultantA = Guid.CreateVersion7();
        var consultantB = Guid.CreateVersion7();
        await using (var unscoped = CreateContext(null))
        {
            unscoped.Users.AddRange(NewUser(consultantA), NewUser(consultantB));
            unscoped.Invitations.AddRange(NewInvitation(consultantA), NewInvitation(consultantB));
            await unscoped.SaveChangesAsync();
        }

        await using var asA = CreateContext(consultantA);
        await using var asB = CreateContext(consultantB);
        await using var asNobody = CreateContext(null);

        (await asA.Invitations.Select(i => i.ConsultantId).ToListAsync()).ShouldBe(new[] { consultantA });
        (await asB.Invitations.Select(i => i.ConsultantId).ToListAsync()).ShouldBe(new[] { consultantB });
        (await asNobody.Invitations.AnyAsync()).ShouldBeFalse();
    }

    private IdentityModuleDbContext CreateContext(Guid? consultantId) => new(
        new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql(factory.ConnectionString, IdentityModuleDbContext.ConfigureNpgsql)
            .Options,
        new FixedConsultant(consultantId));

    private User NewUser(Guid id) => new()
    {
        Id = id,
        UserName = id.ToString("N"),
        DisplayName = "Test",
        Language = "en",
        TimeZone = "UTC",
        CreatedAt = factory.Clock.GetCurrentInstant(),
    };

    private Invitation NewInvitation(Guid consultantId) => new()
    {
        Id = Guid.CreateVersion7(),
        ConsultantId = consultantId,
        TokenHash = Guid.NewGuid().ToString("N"),
        CreatedAt = factory.Clock.GetCurrentInstant(),
        ExpiresAt = factory.Clock.GetCurrentInstant() + Duration.FromDays(14),
    };
}
