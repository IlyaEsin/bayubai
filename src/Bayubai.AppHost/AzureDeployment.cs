using Aspire.Hosting.Azure;

namespace Bayubai.AppHost;

// Production on Azure; the web apps are static files that the deploy workflow uploads to Static Web Apps, so they are not in this model.
internal static class AzureDeployment
{
    public static void AddAzureDeployment(this IDistributedApplicationBuilder builder)
    {
        // Read from configuration (Deploy__* variables in the deploy workflow) so names stay free of characters that environment variables dislike.
        var domain = builder.AddParameterFromConfiguration("domain", "Deploy:Domain");
        var vaultName = builder.AddParameterFromConfiguration("key-vault", "Deploy:KeyVault");
        // Fixed, because a generated default would change on every publish and Azure cannot rename the server admin.
        var postgresUser = builder.AddParameter("postgres-user", "bayubai", publishValueAsDefault: true);
        var postgresPassword = builder.AddParameterFromConfiguration("postgres-password", "Deploy:PostgresPassword", secret: true);

        // The hosted Aspire dashboard would be another public surface and cost; Application Insights covers production.
        builder.AddAzureContainerAppEnvironment("bb").WithDashboard(false);
        var vault = builder.AddAzureKeyVault("secrets").PublishAsExisting(vaultName, null);
        var insights = builder.AddAzureApplicationInsights("insights");
        var database = builder.AddAzurePostgresFlexibleServer("postgres")
            .WithPasswordAuthentication(vault, postgresUser, postgresPassword)
            .AddDatabase("bayubai");

        builder.AddProject<Projects.Bayubai_MigrationService>("migrations")
            .WithReference(database)
            .WithReference(insights)
            .PublishAsAzureContainerAppJob();

        var api = builder.AddProject<Projects.Bayubai_Api>("api")
            .WithExternalHttpEndpoints()
            .WithReference(database)
            .WithReference(insights)
            .WithEnvironment("Frontend__Origins__0", ReferenceExpression.Create($"https://app.{domain}"))
            .WithEnvironment("Frontend__Origins__1", ReferenceExpression.Create($"https://studio.{domain}"))
            .WithEnvironment("Frontend__ClientAppUrl", ReferenceExpression.Create($"https://app.{domain}"))
            .WithEnvironment("Identity__AdminEmails__0", vault.GetSecret("admin-email"))
            .WithEnvironment("Email__From", ReferenceExpression.Create($"Баюбай <no-reply@{domain}>"))
            .WithEnvironment("Email__Host", "smtp-relay.brevo.com")
            .WithEnvironment("Email__Port", "587")
            .WithEnvironment("Email__UseStartTls", "true")
            .WithEnvironment("Email__UserName", vault.GetSecret("email-username"))
            .WithEnvironment("Email__Password", vault.GetSecret("email-password"))
            .PublishAsAzureContainerApp((infrastructure, app) =>
            {
                // One warm replica avoids cold starts for parents; the budget alert is sized for it.
                app.Template.Scale.MinReplicas = 1;
                app.Template.Scale.MaxReplicas = 2;
            });

        // Probes are experimental in Aspire 13.5; without one Container Apps only checks that the port accepts TCP.
#pragma warning disable ASPIREPROBES001
        api.WithHttpProbe(ProbeType.Liveness, "/alive");
#pragma warning restore ASPIREPROBES001

        // Off until the api host's DNS records and managed certificate exist (deploy/README.md); on, every deploy keeps the binding.
        if (bool.TryParse(builder.Configuration["Deploy:CustomDomain"], out var customDomain) && customDomain)
        {
            var apiHost = builder.AddParameterFromConfiguration("api-host", "Deploy:ApiHost");
            var apiCertificate = builder.AddParameterFromConfiguration("api-certificate", "Deploy:ApiCertificate");
            api.PublishAsAzureContainerApp((infrastructure, app) => app.ConfigureCustomDomain(apiHost, apiCertificate));
        }

        // A provider is listed in appsettings.json only once its secrets are in the vault, because a missing secret fails the revision.
        foreach (var provider in builder.Configuration.GetSection("Deploy:Providers").GetChildren().Select(item => item.Value!))
        {
            AddProvider(api, vault, provider);
        }

        builder.AddBicepTemplate("web", "Templates/web.bicep");
    }

    private static void AddProvider(IResourceBuilder<ProjectResource> api, IResourceBuilder<AzureKeyVaultResource> vault, string provider)
    {
        if (provider == "Telegram")
        {
            api.WithEnvironment("Identity__TelegramBotToken", vault.GetSecret("telegram-bot-token"))
                .WithEnvironment("Identity__TelegramBotName", vault.GetSecret("telegram-bot-name"));
            return;
        }

        var key = provider.ToLowerInvariant();
        api.WithEnvironment($"Identity__Providers__{provider}__ClientId", vault.GetSecret($"{key}-client-id"))
            .WithEnvironment($"Identity__Providers__{provider}__ClientSecret", vault.GetSecret($"{key}-client-secret"));
    }
}
