namespace Bayubai.AppHost;

internal static class LocalStack
{
    public static void AddLocalStack(this IDistributedApplicationBuilder builder)
    {
        var postgres = builder.AddPostgres("postgres").WithDataVolume();
        var database = postgres.AddDatabase(DatabaseAccess.Database);
        var appRolePassword = builder.AddParameter(
            "postgres-app-password", new GenerateParameterDefault { MinLength = 22, Special = false }, secret: true, persist: true);
        // Fixed ports so Playwright can read the inbox at a known address.
        var email = builder.AddMailPit("email", httpPort: 8025, smtpPort: 1025);

        var migrations = builder.AddProject<Projects.Bayubai_MigrationService>("migrations")
            .WithReference(database)
            .WithAppRoleSetup(appRolePassword)
            .WaitFor(database);

        // Local demo and e2e only: a one-click test sign-in and a known admin; index 99 leaves user-secrets admins at 0 untouched.
        var api = builder.AddProject<Projects.Bayubai_Api>("api")
            .WithEnvironment(
                $"ConnectionStrings__{DatabaseAccess.Database}",
                ReferenceExpression.Create(
                    $"Host={postgres.Resource.Host};Port={postgres.Resource.Port};Database={DatabaseAccess.Database};Username={DatabaseAccess.AppRole};Password={appRolePassword}"))
            .WithReference(email)
            .WaitFor(database)
            .WaitForCompletion(migrations)
            .WithEnvironment("Identity__Providers__Fake__Enabled", "true")
            .WithEnvironment("Identity__AdminEmails__99", "admin@bayubai.local")
            // Behind the Vite proxy every request comes from 127.0.0.1, so back-to-back e2e runs would hit the per-address limit.
            .WithEnvironment("Identity__EmailStartsPerAddressWindow", "100000");

        // Ports match Frontend:Origins in the API's appsettings.Development.json.
        var client = builder.AddViteApp("client", "../../web/apps/client")
            .WithPnpm()
            .WithEndpoint("http", endpoint => endpoint.Port = 5173)
            .WithEnvironment("API_URL", api.GetEndpoint("http"))
            .WaitFor(api);

        // The client's installer already installed the whole pnpm workspace.
        builder.AddViteApp("studio", "../../web/apps/studio")
            .WithPnpm(install: false)
            .WithEndpoint("http", endpoint => endpoint.Port = 5174)
            .WithEnvironment("API_URL", api.GetEndpoint("http"))
            .WaitFor(client);
    }
}
