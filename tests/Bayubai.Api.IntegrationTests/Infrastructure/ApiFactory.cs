using Bayubai.Identity;
using Bayubai.Identity.Email;
using Bayubai.SharedKernel.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NodaTime;
using NodaTime.Testing;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Bayubai.Api.IntegrationTests.Infrastructure.ApiFactory))]
// Tests share one database and one fake clock, and some tests move the clock.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Bayubai.Api.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ClientAppUrl = "https://app.example.test";
    public const string AdminEmail = "admin@example.test";
    public const string TelegramBotToken = "123456:TEST-TOKEN";
    public const string AppRole = "bayubai_app";
    private const string AppRolePassword = "app-role-test-password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private HttpClient? _adminClient;

    public FakeClock Clock { get; } = new(Instant.FromUtc(2026, 1, 5, 9, 0));

    internal FakeEmailSender Emails { get; } = new();

    // The container's superuser, as the migrations job uses the server admin; tests arrange data with it.
    public string AdminConnectionString => _postgres.GetConnectionString();

    // The API runs as the least-privileged role, as in production, so a missing grant fails here.
    public string AppConnectionString => new NpgsqlConnectionStringBuilder(AdminConnectionString)
    {
        Username = AppRole,
        Password = AppRolePassword,
    }.ConnectionString;

    // One cached admin session: the per-email throttle would block repeated admin sign-ins on the fixed clock.
    public async Task<HttpClient> GetAdminClientAsync() => _adminClient ??= await this.SignedInClientAsync(AdminEmail);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await PrepareDatabaseAsync(CancellationToken.None);
    }

    // Mirrors the migrations job: migrate as the admin, then create the application role and grant it data access.
    public async Task PrepareDatabaseAsync(CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{IdentityModule.ConnectionStringName}"] = AdminConnectionString,
        });
        builder.AddIdentityPersistence();
        using var host = builder.Build();

        await host.Services.MigrateIdentityDatabaseAsync(cancellationToken);
        await using (var connection = new NpgsqlConnection(AdminConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await PostgresAccess.EnsureLoginRoleAsync(connection, AppRole, AppRolePassword, cancellationToken);
        }

        await host.Services.GrantIdentityDatabaseAccessAsync(AppRole, cancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:bayubai", AppConnectionString);
        builder.UseSetting("Frontend:Origins:0", ClientAppUrl);
        builder.UseSetting("Frontend:ClientAppUrl", ClientAppUrl);
        builder.UseSetting("Identity:AdminEmails:0", AdminEmail);
        builder.UseSetting("Identity:TelegramBotToken", TelegramBotToken);
        builder.UseSetting("Identity:TelegramBotName", "bayubai_test_bot");
        builder.UseSetting("Identity:Providers:Google:ClientId", "test-google-client");
        builder.UseSetting("Identity:Providers:Google:ClientSecret", "test-google-secret");
        builder.UseSetting("Identity:Providers:Fake:Enabled", "true");
        // Test clients share one address, so the per-address limit is lifted; a dedicated test covers it.
        builder.UseSetting("Identity:EmailStartsPerAddressWindow", "100000");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    // Secure cookies are only sent over https, so every test client uses an https base address.
    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
