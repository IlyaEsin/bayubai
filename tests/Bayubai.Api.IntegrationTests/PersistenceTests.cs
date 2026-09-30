using Bayubai.Api.IntegrationTests.Infrastructure;
using Bayubai.Identity;
using Bayubai.Identity.Domain;
using Bayubai.Identity.Persistence;
using Bayubai.SharedKernel.Consultants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Npgsql;

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

    [Theory]
    [InlineData("CREATE TABLE identity.probe (id int)")]
    [InlineData("CREATE TABLE public.probe (id int)")]
    [InlineData("DROP TABLE identity.\"AspNetUsers\"")]
    [InlineData("ALTER TABLE identity.\"AspNetUsers\" ADD COLUMN probe int")]
    public async Task App_role_cannot_change_the_schema(string ddl)
    {
        await using var connection = new NpgsqlConnection(factory.AppConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(ddl, connection);

        var error = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Preparing_the_database_again_keeps_the_app_role_working()
    {
        await factory.PrepareDatabaseAsync(CancellationToken.None);

        await using var connection = new NpgsqlConnection(factory.AppConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM identity.\"AspNetRoles\"", connection);
        ((long)(await command.ExecuteScalarAsync())!).ShouldBe(3);
    }

    private IdentityModuleDbContext CreateContext(Guid? consultantId) => new(
        new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql(factory.AdminConnectionString, IdentityModuleDbContext.ConfigureNpgsql)
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
