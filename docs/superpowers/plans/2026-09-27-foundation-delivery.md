# Foundation Delivery Implementation Plan (Foundation plan 3 of 3)

> The project was renamed to Bayubai on 2026-09-30 (`docs/superpowers/specs/rename-to-bayubai.md`); Tasks 1-6 below are historical, Tasks 7-10 use the new names.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** CareNest runs in production on Azure behind its own domain, deployed from `main` by GitHub Actions, with every sign-in method working against real providers, hygiene checks and branch protection on the repository, and the Claude tooling the spec asks for.

**Architecture:** The Aspire AppHost gets a second, publish-mode model (`AzureDeployment.cs`) next to the local one (`LocalStack.cs`): Container Apps for the API and a migrations job, PostgreSQL Flexible Server with password auth, an existing Key Vault created by a one-time bootstrap, Application Insights, and two Static Web Apps declared in a small Bicep template. `aspire publish` output is committed to `infra/` and checked for drift in CI; `aspire deploy` runs from a GitHub Actions workflow that signs in to Azure with OIDC, then the workflow runs the migrations job and uploads the two SPA builds to Static Web Apps. Secrets live only in Key Vault and reach the app as environment variables through Container Apps Key Vault references, so application code stays free of Azure SDKs.

**Tech Stack:** Aspire 13.5.4 (`Aspire.Hosting.Azure.AppContainers`, `.PostgreSQL`, `.ApplicationInsights`, `.KeyVault` 13.5.4, `Aspire.Cli` 13.5.4 as a local dotnet tool), `Azure.Monitor.OpenTelemetry.AspNetCore` 1.6.0 (ServiceDefaults only), `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.12, ASP.NET Core rate limiting, Azure Container Apps, Azure Database for PostgreSQL Flexible Server (Burstable B1ms), Azure Static Web Apps (Free), Key Vault, Application Insights, Brevo SMTP relay, GitHub Actions (`azure/login@v3`, `Azure/static-web-apps-deploy@v1`, `gitleaks/gitleaks-action@v3`), CodeQL default setup, Dependabot, GitHub rulesets.

**Spec:** `docs/superpowers/specs/foundation-design.md` (section 7 CI, deploy and Claude tooling; acceptance criteria 2, 3, 7, 9, 10). Notes carried from plans 1 and 2: "Notes for plans 2 and 3" in `docs/superpowers/plans/2026-09-24-foundation-backend.md` and "Notes for plan 3" in `docs/superpowers/plans/2026-09-25-foundation-web.md`.

**Owner decisions for this plan (2026-09-27).**

- Deploy with `aspire deploy`, not `azd`. Aspire 13 documentation says azd "is still supported with Aspire for existing workflows, but it is no longer the recommended default deployment path" (https://aspire.dev/deployment/azure/azure-developer-cli/). `infra/` holds the committed output of `aspire publish`. The spec is updated in Task 4.
- Email goes through **Brevo** (SMTP relay), not Azure Communication Services Email. Microsoft is retiring ACS Email on 30 September 2028 and advises against onboarding new workloads (https://learn.microsoft.com/en-us/azure/communication-services/acs-retirement-and-breaking-changes-guide). The code already speaks SMTP, so only configuration and DNS change.
- The API keeps **one warm replica** (min 1, max 2), so there are no cold starts. The estimated total is about 31 USD per month, so the budget alert is **40 USD**.
- Region `westeurope`. The domain is not bought yet, and there is no Azure subscription yet. Both are owner actions in Task 7.
- Fix two inaccuracies in `docs/tech-overview.md`:
  - Section 15 says `main` is protected; it is not yet (GitHub API: "Branch not protected").
  - Section 16 says Key Vault secrets reach the app "without environment variables"; in fact they arrive as environment variables filled from Key Vault references.

**STOP points.** The owner asked on 2026-09-27 that execution stops whenever a step needs them: buying something, signing in, registering apps, typing secrets, DNS, repository settings, device checks. These steps are marked **STOP (owner)**. The executor explains exactly what to do, waits for the owner's confirmation, and never simulates or skips such a step.

**Verified before writing (2026-09-27)** in a scratch worktree of `main` at `48b9727`:

- **Backend:** 147 tests (SharedKernel 22, Identity 41, Architecture 6, Integration 78). The new architecture test fails when the API references an Azure type (checked by temporarily adding one).
- **Web:** lint, typecheck and 84 unit tests after regenerating the client.
- **E2E:** all 7 Playwright scenarios against the refactored AppHost, plus a throwaway `how-to-test` scenario.
- **Generation:** `aspire publish` is deterministic (two runs, identical output), and the drift check is clean after a commit.
- **Linting and syntax:** every workflow passes actionlint 1.7.12; `bash -n` passes on every script; Bicep CLI 0.47.16 compiles `infra/main.bicep`, every module, `web.bicep` and `budget.bicep`, with linter warnings only in the generated files.
- **Secrets scan:** gitleaks 8.30.1 finds no leaks in the 51 commits of history.
- **Checked against source:** an explicitly configured Data Protection key store wins over the Container Apps one that Aspire switches on (`KeyManagementOptionsPostSetup` in aspnetcore `release/10.0`). Aspire 13.5.4 sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, turns on `autoConfigureDataProtection`, emits Key Vault references for `GetSecret`, and emits the custom-domain binding that switches from `Disabled` to `SniEnabled` once a certificate name is given.
- **Not verifiable without an Azure subscription:** anything that talks to Azure (`aspire deploy` itself, the job and Static Web Apps commands, role propagation, managed certificates). Tasks 8 and 9 verify these live, step by step.

## Prerequisites (once per machine)

- Everything from plans 1 and 2: Docker running, .NET SDK from `global.json`, Node 22.18+, pnpm 10.34.5, trusted dev certificate.
- For Tasks 7 to 10 only: Azure CLI (`az version`, 2.70 or newer) and GitHub CLI signed in as the repository owner (`gh auth status`).

## Global Constraints

- Package versions are exact. New ones: `Aspire.Hosting.Azure.AppContainers`, `Aspire.Hosting.Azure.PostgreSQL`, `Aspire.Hosting.Azure.ApplicationInsights`, `Aspire.Hosting.Azure.KeyVault` 13.5.4; `Aspire.Cli` 13.5.4 (local tool); `Azure.Monitor.OpenTelemetry.AspNetCore` 1.6.0; `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.12.
- No Azure SDK in `src/Modules/*`, `CareNest.SharedKernel` or `CareNest.Api` (architecture test). Azure-specific code lives only in `CareNest.AppHost`, `CareNest.ServiceDefaults` (the Azure Monitor exporter, active only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set), `infra/`, `deploy/` and `.github/workflows/deploy.yml`.
- Secrets never enter the repository or GitHub. Application secrets live in Key Vault. GitHub reaches Azure through OIDC federated credentials. The only GitHub secret is `FORBIDDEN_REFERENCES` (the denylist, spec section 7).
- `Identity:Providers:Fake:Enabled` is allowed only in `Development` and `Testing`. The Azure model never sets it.
- Region `westeurope`; resource group `rg-carenest`; Key Vault `kv-carenest-<6 hex>` (from `deploy/bootstrap.sh`); Static Web Apps `cn-client` and `cn-studio`; Container Apps environment `cn`; container app `api`; job `migrations`.
- Budget alert 40 USD per month. API scale: min 1, max 2 replicas.
- `infra/` is generated. After any change to the Azure model, run `dotnet aspire publish --apphost src/CareNest.AppHost/CareNest.AppHost.csproj --output-path infra` and commit the result; CI fails otherwise.
- Every new error code has RU and EN text in `web/packages/i18n/src/locales/*/errors.json`.
- `docs/tech-overview.md` (Russian) gets a section in the same task that adopts a technology.
- Text uses the plain hyphen `-`, never em or en dashes. Code comments: one dry sentence, why not what.

## Review Focus

1. **A Key Vault secret referenced before it exists.** Example: a provider added to `Deploy:Providers` before its secrets are in the vault. Container Apps would fail the new revision late and quietly. The deploy must stop before touching Azure and name the missing secret. Pinned in Task 4 (`deploy/check-secrets.sh`, run as a workflow step before `aspire deploy`).
2. **Requests arriving through the TLS-terminating ingress as plain http.** OAuth `redirect_uri` must still be `https`, or Google, Yandex and VK reject the sign-in. Pinned in Task 1 (`Forwarded_https_scheme_reaches_the_oauth_redirect_uri`).
3. **A restart, a new revision or a second replica.** Signed-in parents must stay signed in, and an OAuth round trip that started on one replica must finish on another. Pinned in Task 1 (keys stored in PostgreSQL, `Data_protection_keys_are_stored_in_the_database`).
4. **One network address requesting many sign-in emails for different addresses.** This is a script, or someone probing which emails exist. The API must answer 429 with a translatable code after 20 requests in 10 minutes, without affecting other addresses. Pinned in Task 2 (`Email_start_is_limited_per_network_address`).
5. **A deployed environment with a wrong or missing setting.** Examples: the fake provider flag set in a Staging slot, or `Frontend:ClientAppUrl` left empty. The API must refuse to start rather than run insecurely or send broken links. Pinned in Task 1 (`Fake_provider_refuses_to_register_in_a_deployed_environment`, `Host_refuses_to_start_without_an_absolute_client_app_url`).

---

## File map

```
.config/dotnet-tools.json                        + aspire.cli 13.5.4
.gitattributes                                   + infra/** LF
.github/workflows/backend.yml                    + infra drift check
.github/workflows/deploy.yml                     new: OIDC, aspire deploy, migrations, Static Web Apps
.github/workflows/hygiene.yml                    new: gitleaks, forbidden references
.github/scripts/forbidden-references.sh          new
.github/dependabot.yml                           new
.claude/skills/build-test/SKILL.md               new
.claude/skills/how-to-test/SKILL.md              new
REVIEW.md                                        new
CLAUDE.md, docs/tech-overview.md, docs/superpowers/specs/foundation-design.md
deploy/README.md, bootstrap.sh, budget.bicep, check-secrets.sh, run-migrations.sh    new
infra/**                                         generated by aspire publish
src/CareNest.Api/Program.cs                      forwarded headers, rate limiter
src/CareNest.AppHost/AppHost.cs, LocalStack.cs, AzureDeployment.cs, Templates/web.bicep, appsettings.json, CareNest.AppHost.csproj
src/CareNest.ServiceDefaults/Extensions.cs, CareNest.ServiceDefaults.csproj
src/CareNest.SharedKernel/Errors/CommonErrors.cs, Web/FrontendOptions.cs
src/Modules/CareNest.Identity/
  CareNest.Identity.csproj, IdentityModule.cs, IdentityModuleOptions.cs
  Endpoints/EmailSignInEndpoints.cs, External/ExternalProviders.cs
  Persistence/IdentityModuleDbContext.cs, Migrations/<timestamp>_DataProtectionKeys.cs
tests/CareNest.SharedKernel.Tests/FrontendOptionsTests.cs          new
tests/CareNest.Identity.Tests/ExternalProvidersTests.cs
tests/CareNest.ArchitectureTests/ArchitectureTests.cs
tests/CareNest.Api.IntegrationTests/ProductionHostingTests.cs      new
tests/CareNest.Api.IntegrationTests/Infrastructure/ApiFactory.cs
tests/e2e/playwright.config.ts, package.json     + how-to-test project
web/apps/{client,studio}/public/staticwebapp.config.json           new
web/packages/api-client/openapi.json, src/generated/**             regenerated
web/packages/i18n/src/locales/{ru,en}/errors.json
```

Execution branch: `feature/foundation-delivery` (push with `git push -u origin feature/foundation-delivery` after the first commit). Tasks 1 to 6 land in one pull request; Tasks 7 to 10 are live operations after that PR is merged, each with at most a small follow-up PR.

---

### Task 1: API ready for production hosting

Forwarded headers behind the ingress, Data Protection keys in PostgreSQL, `/alive` outside Development, startup validation of the frontend settings, the fake provider limited to local environments, and the no-Azure-SDK rule with the Azure Monitor exporter in ServiceDefaults.

**Files:**
- Modify: `src/CareNest.Api/Program.cs`
- Modify: `src/CareNest.ServiceDefaults/Extensions.cs`, `src/CareNest.ServiceDefaults/CareNest.ServiceDefaults.csproj`
- Modify: `src/CareNest.SharedKernel/Web/FrontendOptions.cs`
- Modify: `src/Modules/CareNest.Identity/IdentityModule.cs`, `CareNest.Identity.csproj`, `External/ExternalProviders.cs`, `Persistence/IdentityModuleDbContext.cs`
- Create: `src/Modules/CareNest.Identity/Persistence/Migrations/<timestamp>_DataProtectionKeys.cs` (+ `.Designer.cs`, snapshot update) via `dotnet ef`
- Create: `tests/CareNest.SharedKernel.Tests/FrontendOptionsTests.cs`, `tests/CareNest.Api.IntegrationTests/ProductionHostingTests.cs`
- Modify: `tests/CareNest.Identity.Tests/ExternalProvidersTests.cs`, `tests/CareNest.ArchitectureTests/ArchitectureTests.cs`
- Modify: `docs/tech-overview.md`

**Interfaces:**
- Consumes: `ApiFactory` (`CreateHttpsClient()`, `Services`, `ClientAppUrl`), `IdentityModuleDbContext`.
- Produces: `FrontendOptions.IsValid()`; `IdentityModuleDbContext.DataProtectionKeys`; `ExternalProviders.TestingEnvironment = "Testing"`; `/alive` mapped in every environment; `ProductionHostingTests` class (Task 2 adds a test to it).

- [ ] **Step 1: Write the failing unit tests**

`tests/CareNest.SharedKernel.Tests/FrontendOptionsTests.cs`:

```csharp
using CareNest.SharedKernel.Web;

namespace CareNest.SharedKernel.Tests;

public class FrontendOptionsTests
{
    [Fact]
    public void Absolute_origins_and_client_url_are_valid() =>
        new FrontendOptions { Origins = ["https://app.example.test", "https://studio.example.test"], ClientAppUrl = "https://app.example.test" }
            .IsValid().ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("/app")]
    [InlineData("app.example.test")]
    [InlineData("ftp://app.example.test")]
    public void Missing_or_relative_client_url_is_invalid(string clientAppUrl) =>
        new FrontendOptions { Origins = ["https://app.example.test"], ClientAppUrl = clientAppUrl }.IsValid().ShouldBeFalse();

    [Fact]
    public void No_origins_is_invalid() =>
        new FrontendOptions { ClientAppUrl = "https://app.example.test" }.IsValid().ShouldBeFalse();
}
```

In `tests/CareNest.Identity.Tests/ExternalProvidersTests.cs`, replace the two existing facts `Fake_provider_refuses_to_register_in_production` and `Fake_provider_registers_outside_production_only_when_enabled` with:

```csharp
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Fake_provider_refuses_to_register_in_a_deployed_environment(string environmentName) =>
        Should.Throw<InvalidOperationException>(() => Register(fakeEnabled: true, environmentName));

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task Fake_provider_registers_locally_only_when_enabled(string environmentName)
    {
        (await SchemeAsync(Register(fakeEnabled: true, environmentName))).ShouldNotBeNull();
        (await SchemeAsync(Register(fakeEnabled: false, environmentName))).ShouldBeNull();
    }
```

In `tests/CareNest.ArchitectureTests/ArchitectureTests.cs`, add before `private static string Describe(`:

```csharp
    [Fact]
    public void Host_kernel_and_modules_use_no_Azure_SDK()
    {
        // Portability: Azure reaches the app only as configuration, so a move to another host is a redeploy.
        foreach (var assembly in Modules.Append(SharedKernel).Append(Host))
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny("Azure", "Microsoft.Azure").GetResult();

            result.IsSuccessful.ShouldBeTrue(Describe(result));
        }
    }
```

- [ ] **Step 2: Write the failing integration tests**

`tests/CareNest.Api.IntegrationTests/ProductionHostingTests.cs`:

```csharp
using System.Net;
using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CareNest.Api.IntegrationTests;

public class ProductionHostingTests(ApiFactory factory)
{
    [Fact]
    public async Task Liveness_is_served_outside_development_but_the_health_report_is_not()
    {
        var client = factory.CreateHttpsClient();

        (await client.GetAsync("/alive")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Forwarded_https_scheme_reaches_the_oauth_redirect_uri()
    {
        // The ingress talks plain http to the container and reports the original scheme in X-Forwarded-Proto.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/identity/external/Google/start?returnUrl={Uri.EscapeDataString(ApiFactory.ClientAppUrl + "/")}&mode=signin&language=en&timeZone=UTC");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.Query.ShouldContain("redirect_uri=https%3A%2F%2Flocalhost%2Fapi%2Fidentity%2Fsignin-google");
    }

    [Fact]
    public async Task Data_protection_keys_are_stored_in_the_database()
    {
        factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("payload");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        (await db.DataProtectionKeys.CountAsync()).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Host_refuses_to_start_without_an_absolute_client_app_url()
    {
        using var broken = factory.WithWebHostBuilder(builder => builder.UseSetting("Frontend:ClientAppUrl", "/app"));

        Should.Throw<OptionsValidationException>(() => broken.CreateClient());
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/CareNest.SharedKernel.Tests` then `dotnet test tests/CareNest.Identity.Tests`, `dotnet test tests/CareNest.Api.IntegrationTests`.
Expected: build errors. `FrontendOptions` has no `IsValid`, and `IdentityModuleDbContext` has no `DataProtectionKeys`. After Step 4 compiles, the Staging and Testing cases, `/alive`, the forwarded scheme and the startup check fail until their step is done. `Host_kernel_and_modules_use_no_Azure_SDK` already passes: it guards a rule, and Step 10 proves it can fail.

- [ ] **Step 4: Frontend options validation**

Replace `src/CareNest.SharedKernel/Web/FrontendOptions.cs` with:

```csharp
namespace CareNest.SharedKernel.Web;

public sealed class FrontendOptions
{
    public const string Section = "Frontend";

    // Browser origins of the web apps; used for CORS and for validating return and callback URLs.
    public string[] Origins { get; set; } = [];

    public string ClientAppUrl { get; set; } = "";

    // An empty or relative value would silently break CORS and every emailed link, so the host refuses to start instead.
    public bool IsValid() =>
        Origins.Length > 0 && Origins.All(IsHttpUrl) && IsHttpUrl(ClientAppUrl);

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
```

In `src/Modules/CareNest.Identity/IdentityModule.cs` replace

```csharp
        services.AddOptions<FrontendOptions>().Bind(builder.Configuration.GetSection(FrontendOptions.Section));
```

with

```csharp
        services.AddOptions<FrontendOptions>()
            .Bind(builder.Configuration.GetSection(FrontendOptions.Section))
            .Validate(frontend => frontend.IsValid(), "Frontend:Origins and Frontend:ClientAppUrl must be absolute http(s) URLs.")
            .ValidateOnStart();
```

- [ ] **Step 5: Data Protection keys in PostgreSQL**

Add to `src/Modules/CareNest.Identity/CareNest.Identity.csproj`, after the Google package:

```xml
    <PackageReference Include="Microsoft.AspNetCore.DataProtection.EntityFrameworkCore" Version="10.0.12" />
```

In `src/Modules/CareNest.Identity/Persistence/IdentityModuleDbContext.cs`:
- add `using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;` above `using Microsoft.AspNetCore.Identity;`;
- add `, IDataProtectionKeyContext` after `IConsultantScopedDbContext` in the class declaration;
- after the `ClientLinks` property add

```csharp

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
```

- and in `OnModelCreating`, right before `builder.ApplyConsultantQueryFilters(this);`, add

```csharp
        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

```

In `src/Modules/CareNest.Identity/IdentityModule.cs` add `using Microsoft.AspNetCore.DataProtection;` (after `using Microsoft.AspNetCore.Builder;`) and, right after `builder.AddIdentityPersistence();`:

```csharp
        // Keys live in the database so sessions and OAuth state survive restarts and are shared by every replica; an explicit store also wins over the Container Apps one.
        services.AddDataProtection()
            .SetApplicationName("CareNest")
            .PersistKeysToDbContext<IdentityModuleDbContext>();
```

Create the migration:

```bash
dotnet tool restore
dotnet ef migrations add DataProtectionKeys --project src/Modules/CareNest.Identity --output-dir Persistence/Migrations --namespace CareNest.Identity.Persistence.Migrations
```

Expected: `<timestamp>_DataProtectionKeys.cs` creates table `identity.data_protection_keys` (`Id` integer identity, `FriendlyName` text, `Xml` text). The keys are not personal data, so account deletion is unaffected (standing rule 5).

- [ ] **Step 6: Forwarded headers**

In `src/CareNest.Api/Program.cs` add `using Microsoft.AspNetCore.HttpOverrides;` after the existing usings, then after `builder.Services.AddCors();`:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // The Container Apps ingress terminates TLS and is the only way in, so its scheme and client address are trusted; Host is not forwarded.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
```

and make `app.UseForwardedHeaders();` the first middleware (before `app.UseExceptionHandler();`). Aspire also sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` on Azure. A second pass of the middleware finds no headers left, so the explicit setup costs nothing and keeps any other host behind a TLS proxy working.

- [ ] **Step 7: Liveness in every environment**

In `src/CareNest.ServiceDefaults/Extensions.cs`, replace the body of `MapDefaultEndpoints` (everything before `return app;`) with:

```csharp
        // Liveness reveals nothing but "Healthy", so it is mapped everywhere for the Container Apps probes.
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        // The full report can name dependencies, so it stays in Development (https://aka.ms/aspire/healthchecks).
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);
        }
```

- [ ] **Step 8: Azure Monitor exporter, only when configured**

Add to `src/CareNest.ServiceDefaults/CareNest.ServiceDefaults.csproj`:

```xml
    <PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.6.0" />
```

In `Extensions.cs` add `using Azure.Monitor.OpenTelemetry.AspNetCore;` as the first using. Replace the commented-out Azure Monitor block in `AddOpenTelemetryExporters` with:

```csharp
        // Only the Azure deployment sets this variable, so any other host runs without the Azure exporter.
        if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            builder.Services.AddOpenTelemetry().UseAzureMonitor();
        }
```

- [ ] **Step 9: Fake provider only in Development and Testing**

In `src/Modules/CareNest.Identity/External/ExternalProviders.cs` add after `public const string Telegram = "Telegram";`:

```csharp
    public const string TestingEnvironment = "Testing";
```

and replace

```csharp
            if (environment.IsProduction())
            {
                throw new InvalidOperationException("The fake sign-in provider must never be enabled in Production.");
            }
```

with

```csharp
            // An allow-list, so a staging or any other deployed environment cannot get the test sign-in by a stray setting.
            if (!environment.IsDevelopment() && !environment.IsEnvironment(TestingEnvironment))
            {
                throw new InvalidOperationException("The fake sign-in provider is allowed only in Development and Testing.");
            }
```

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet build CareNest.slnx` then `dotnet test CareNest.slnx`.
Expected: 0 warnings. SharedKernel 22, Identity 41, Architecture 6. Integration: all green, including the 4 new `ProductionHostingTests`. The OpenAPI snapshot test still passes, because this task changes no contract.

Prove the architecture test bites: append `internal static class AzureProbe { public static System.Type T = typeof(Azure.Monitor.OpenTelemetry.AspNetCore.AzureMonitorOptions); }` to `Program.cs` and run `dotnet test tests/CareNest.ArchitectureTests`. Expected: FAIL with `Violations: AzureProbe`. Remove the line, run again, expect PASS.

- [ ] **Step 11: Document it**

In `docs/tech-overview.md`, rename `## 18. Что почитать и посмотреть` to `## 19. Что почитать и посмотреть`, and insert before it:

```markdown
## 18. API в продакшене

Несколько настроек, без которых API работает локально, но ломается за балансировщиком или при перезапуске. Они не зависят от Azure: на любом хостинге за TLS-прокси работают так же.

- **Forwarded headers.** В Azure Container Apps HTTPS заканчивается на входном прокси (ingress), а в контейнер запрос приходит по обычному http. Без `UseForwardedHeaders` API считал бы, что запрос пришёл по http, и, например, отдавал бы Google адрес возврата `http://...`, который провайдер отвергает. Прокси сообщает исходную схему и адрес клиента в заголовках `X-Forwarded-Proto` и `X-Forwarded-For`; `src/CareNest.Api/Program.cs` им доверяет, потому что снаружи к контейнеру можно попасть только через ingress. Заголовок `Host` от прокси не принимается, чтобы его нельзя было подменить.
- **Ключи Data Protection в базе.** ASP.NET Core шифрует cookie сессии и состояние OAuth ключами Data Protection. По умолчанию ключи живут в файловой системе контейнера и пропадают при каждом перезапуске - всех бы разлогинивало. У нас ключи лежат в таблице `identity.data_protection_keys` (пакет `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) и общие для всех реплик. У Container Apps есть своё хранилище ключей, и Aspire его включает, но явно настроенное хранилище приложения имеет приоритет (проверено по исходникам ASP.NET Core 10), так что источник один - база.
- **`/alive`.** Проверка "процесс жив": Container Apps вызывает её каждые несколько секунд и при сбоях перезапускает контейнер. Отвечает только `Healthy`, поэтому открыта везде; подробный `/health` - только в Development.
- **Проверка конфигурации при старте.** Если `Frontend:Origins` или `Frontend:ClientAppUrl` пустые или не абсолютные адреса, API не стартует (`ValidateOnStart`), а не ломает молча CORS и ссылки в письмах.
- **Тестовый вход только локально.** Тестовый провайдер входа разрешён только в окружениях `Development` и `Testing`; в любом другом (Production, Staging) API с ним не стартует.
- **Application Insights.** Экспортер Azure Monitor подключён в `CareNest.ServiceDefaults` и включается, только если задана переменная `APPLICATIONINSIGHTS_CONNECTION_STRING` (её задаёт деплой в Azure). Архитектурный тест следит, чтобы модули, SharedKernel и API не использовали Azure SDK: переезд на другой хостинг - это смена конфигурации, а не кода.

Официальная документация: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer, https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers
YouTube (EN): `ASP.NET Core data protection keys explained`
```

- [ ] **Step 12: Commit**

```bash
git checkout -b feature/foundation-delivery
git add src tests docs/tech-overview.md
git commit -m "feat: production hosting of the API (forwarded headers, key ring in PostgreSQL, liveness, config checks)"
git push -u origin feature/foundation-delivery
```

---

### Task 2: Per-address limit on sign-in emails

**Files:**
- Modify: `src/CareNest.SharedKernel/Errors/CommonErrors.cs`, `src/CareNest.Api/Program.cs`
- Modify: `src/Modules/CareNest.Identity/IdentityModule.cs`, `IdentityModuleOptions.cs`, `Endpoints/EmailSignInEndpoints.cs`
- Modify: `tests/CareNest.Api.IntegrationTests/ProductionHostingTests.cs`, `Infrastructure/ApiFactory.cs`
- Modify: `web/packages/api-client/openapi.json`, `web/packages/api-client/src/generated/**` (generated), `web/packages/i18n/src/locales/{en,ru}/errors.json`
- Modify: `docs/tech-overview.md`

**Interfaces:**
- Consumes: `ProductionHostingTests` (Task 1), `SignInExtensions.NewEmail()`, `SignInExtensions.EmailCallbackUrl`, `TestHttp.ShouldBeProblemAsync`.
- Produces: error code `rate_limited` (HTTP 429) in `CommonErrors.Codes` and in the OpenAPI `ErrorCode` enum; setting `Identity:EmailStartsPerAddressWindow` (default 20 per 10-minute window); rate limiter policy `EmailSignInEndpoints.StartRateLimit = "identity.email-start"`.

- [ ] **Step 1: Write the failing test**

In `ProductionHostingTests.cs` add the usings `using System.Net.Http.Json;` and `using static CareNest.Api.IntegrationTests.Infrastructure.SignInExtensions;`, then the test:

```csharp
    [Fact]
    public async Task Email_start_is_limited_per_network_address()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Identity:EmailStartsPerAddressWindow", "2"));
        var client = limited.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Task<HttpResponseMessage> StartAsync() =>
            client.PostAsJsonAsync("/api/identity/email/start", new { email = NewEmail(), callbackUrl = EmailCallbackUrl, language = "en", timeZone = "UTC" });

        (await StartAsync()).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await StartAsync()).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await (await StartAsync()).ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "rate_limited");
    }
```

In `ApiFactory.ConfigureWebHost`, after the `Identity:Providers:Fake:Enabled` setting:

```csharp
        // Test clients share one address, so the per-address limit is lifted; a dedicated test covers it.
        builder.UseSetting("Identity:EmailStartsPerAddressWindow", "100000");
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/CareNest.Api.IntegrationTests --filter Email_start_is_limited_per_network_address`
Expected: FAIL; the third call returns 202.

- [ ] **Step 3: Error code**

Replace `src/CareNest.SharedKernel/Errors/CommonErrors.cs` with:

```csharp
namespace CareNest.SharedKernel.Errors;

public static class CommonErrors
{
    public const string ValidationFailed = "validation_failed";

    public const string RateLimited = "rate_limited";

    public static ApiError TooManyRequests { get; } = new(RateLimited, 429);

    public static IReadOnlyList<string> Codes { get; } = [ValidationFailed, RateLimited];
}
```

- [ ] **Step 4: The limiter**

`src/Modules/CareNest.Identity/IdentityModuleOptions.cs`, after `InvitationLifetimeDays`:

```csharp

    // Sign-in emails one network address may request per throttle window, on top of the per-email limit.
    public int EmailStartsPerAddressWindow { get; set; } = 20;
```

`Endpoints/EmailSignInEndpoints.cs`: add `public const string StartRateLimit = "identity.email-start";` after `NonceCookie`, and replace the `/email/start` mapping with

```csharp
        group.MapPost("/email/start", StartAsync)
            .WithName("StartEmailSignIn")
            .WithRequestValidation<EmailStartRequest>()
            .RequireRateLimiting(StartRateLimit);
```

`IdentityModule.cs`:
- add `using System.Threading.RateLimiting;` as the first using and `using Microsoft.AspNetCore.RateLimiting;` after `using Microsoft.AspNetCore.Identity;`;
- after `services.AddSingleton<IEmailSender, SmtpEmailSender>();` add

```csharp

        services.AddRateLimiter(options => options.AddPolicy(EmailSignInEndpoints.StartRateLimit, http =>
            RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = http.RequestServices.GetRequiredService<IOptions<IdentityModuleOptions>>().Value.EmailStartsPerAddressWindow,
                    Window = EmailSignInEndpoints.ThrottleWindow.ToTimeSpan(),
                })));
```

`src/CareNest.Api/Program.cs`:
- add `using CareNest.SharedKernel.Errors;`;
- after the `ForwardedHeadersOptions` block add

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) => new ValueTask(CommonErrors.TooManyRequests.ToProblem().ExecuteAsync(context.HttpContext));
});
```

- and add `app.UseRateLimiter();` right after `app.UseAuthorization();`.

The partition key is the address `UseForwardedHeaders` restored. On Container Apps that is the rightmost `X-Forwarded-For` entry, which the ingress appends itself, so a client cannot spoof it.

- [ ] **Step 4a: Run the test to verify it passes**

Run: `dotnet test tests/CareNest.Api.IntegrationTests --filter Email_start_is_limited_per_network_address`
Expected: PASS.

- [ ] **Step 5: Contract, client and translations**

```bash
CARENEST_UPDATE_OPENAPI=1 dotnet test tests/CareNest.Api.IntegrationTests
git diff web/packages/api-client/openapi.json
```

Expected: one added line, `"rate_limited",`, in the `ErrorCode` enum.

Add after the `validation_failed` line:
- in `web/packages/i18n/src/locales/en/errors.json`: `"rate_limited": "Too many attempts. Please wait a few minutes and try again.",`
- in `web/packages/i18n/src/locales/ru/errors.json`: `"rate_limited": "Слишком много попыток. Подождите несколько минут и попробуйте снова.",`

```bash
cd web
pnpm generate:api
pnpm lint && pnpm typecheck && pnpm test
```

Expected: `packages/api-client/src/generated/model/errorCode.ts` gains `rate_limited`. All web tests pass (84), including the test that every error code is translated.

- [ ] **Step 6: Full backend run**

Run: `dotnet test CareNest.slnx`
Expected: 147 passed, 0 failed (SharedKernel 22, Identity 41, Architecture 6, Integration 78).

- [ ] **Step 7: Document it**

In `docs/tech-overview.md`, section 18, add after the `/alive` bullet:

```markdown
- **Ограничение частоты (rate limiting).** Встроенный в ASP.NET Core `RateLimiter`: с одного IP-адреса можно запросить не больше 20 писем для входа за 10 минут (`Identity:EmailStartsPerAddressWindow`). Сверх этого API отвечает 429 с кодом `rate_limited`, который интерфейс переводит. Это дополнение к лимиту в 3 письма на один email: тот защищает конкретный ящик, этот - почтовый сервис от перебора адресов.
```

- [ ] **Step 8: Commit**

```bash
git add src tests web docs/tech-overview.md
git commit -m "feat: per-address limit on sign-in emails with the rate_limited code"
```

---

### Task 3: Azure model in the AppHost, committed infra and its drift check

**Files:**
- Modify: `src/CareNest.AppHost/AppHost.cs`, `CareNest.AppHost.csproj`, `appsettings.json`
- Create: `src/CareNest.AppHost/LocalStack.cs`, `AzureDeployment.cs`, `Templates/web.bicep`
- Modify: `.config/dotnet-tools.json` (via `dotnet tool install`), `.gitattributes`, `.github/workflows/backend.yml`
- Create: `infra/**` (generated)
- Modify: `docs/tech-overview.md`

**Interfaces:**
- Consumes: `CareNest.Api`, `CareNest.MigrationService` projects; `/alive` (Task 1).
- Produces (read by Tasks 4 and 8):
  - Configuration keys `Deploy:Domain`, `Deploy:KeyVault`, `Deploy:PostgresPassword`, `Deploy:ApiHost`, `Deploy:ApiCertificate`, and in `appsettings.json` `Deploy:CustomDomain` (bool) and `Deploy:Providers` (array of `Google`, `Yandex`, `VkId`, `Telegram`).
  - Key Vault secret names `admin-email`, `email-username`, `email-password`, `postgres-password`, `<provider>-client-id`, `<provider>-client-secret`, `telegram-bot-token`, `telegram-bot-name`.
  - Azure resources: container app `api`, job `migrations`, static sites `cn-client` and `cn-studio`.

- [ ] **Step 1: Packages and the Aspire CLI as a pinned local tool**

```bash
cd src/CareNest.AppHost
for p in Aspire.Hosting.Azure.AppContainers Aspire.Hosting.Azure.PostgreSQL Aspire.Hosting.Azure.ApplicationInsights Aspire.Hosting.Azure.KeyVault; do dotnet add package $p --version 13.5.4; done
cd ../..
dotnet tool install Aspire.Cli --version 13.5.4
dotnet aspire --version
```

Expected: `13.5.4+9c1b401dd67746739044f68959cbf4d3d7af93a6`. `.config/dotnet-tools.json` now lists `aspire.cli`.

- [ ] **Step 2: Split the AppHost into a local and an Azure model**

`src/CareNest.AppHost/AppHost.cs`:

```csharp
using CareNest.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureDeployment();
}
else
{
    builder.AddLocalStack();
}

builder.Build().Run();
```

`src/CareNest.AppHost/LocalStack.cs` is the current run-mode model, unchanged in behaviour:

```csharp
namespace CareNest.AppHost;

internal static class LocalStack
{
    public static void AddLocalStack(this IDistributedApplicationBuilder builder)
    {
        var postgres = builder.AddPostgres("postgres").WithDataVolume();
        var database = postgres.AddDatabase("carenest");
        // Fixed ports so Playwright can read the inbox at a known address.
        var email = builder.AddMailPit("email", httpPort: 8025, smtpPort: 1025);

        var migrations = builder.AddProject<Projects.CareNest_MigrationService>("migrations")
            .WithReference(database)
            .WaitFor(database);

        // Local demo and e2e only: a one-click test sign-in and a known admin; index 99 leaves user-secrets admins at 0 untouched.
        var api = builder.AddProject<Projects.CareNest_Api>("api")
            .WithReference(database)
            .WithReference(email)
            .WaitFor(database)
            .WaitForCompletion(migrations)
            .WithEnvironment("Identity__Providers__Fake__Enabled", "true")
            .WithEnvironment("Identity__AdminEmails__99", "admin@carenest.local");

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
```

`src/CareNest.AppHost/AzureDeployment.cs`:

```csharp
using Aspire.Hosting.Azure;

namespace CareNest.AppHost;

// Production on Azure; the web apps are static files that the deploy workflow uploads to Static Web Apps, so they are not in this model.
internal static class AzureDeployment
{
    public static void AddAzureDeployment(this IDistributedApplicationBuilder builder)
    {
        // Read from configuration (Deploy__* variables in the deploy workflow) so names stay free of characters that environment variables dislike.
        var domain = builder.AddParameterFromConfiguration("domain", "Deploy:Domain");
        var vaultName = builder.AddParameterFromConfiguration("key-vault", "Deploy:KeyVault");
        // Fixed, because a generated default would change on every publish and Azure cannot rename the server admin.
        var postgresUser = builder.AddParameter("postgres-user", "carenest", publishValueAsDefault: true);
        var postgresPassword = builder.AddParameterFromConfiguration("postgres-password", "Deploy:PostgresPassword", secret: true);

        // The hosted Aspire dashboard would be another public surface and cost; Application Insights covers production.
        builder.AddAzureContainerAppEnvironment("cn").WithDashboard(false);
        var vault = builder.AddAzureKeyVault("secrets").PublishAsExisting(vaultName, null);
        var insights = builder.AddAzureApplicationInsights("insights");
        var database = builder.AddAzurePostgresFlexibleServer("postgres")
            .WithPasswordAuthentication(vault, postgresUser, postgresPassword)
            .AddDatabase("carenest");

        builder.AddProject<Projects.CareNest_MigrationService>("migrations")
            .WithReference(database)
            .WithReference(insights)
            .PublishAsAzureContainerAppJob();

        var api = builder.AddProject<Projects.CareNest_Api>("api")
            .WithExternalHttpEndpoints()
            .WithReference(database)
            .WithReference(insights)
            .WithEnvironment("Frontend__Origins__0", ReferenceExpression.Create($"https://app.{domain}"))
            .WithEnvironment("Frontend__Origins__1", ReferenceExpression.Create($"https://studio.{domain}"))
            .WithEnvironment("Frontend__ClientAppUrl", ReferenceExpression.Create($"https://app.{domain}"))
            .WithEnvironment("Identity__AdminEmails__0", vault.GetSecret("admin-email"))
            .WithEnvironment("Email__From", ReferenceExpression.Create($"CareNest <no-reply@{domain}>"))
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
```

`Identity:CookieDomain` is deliberately left unset. The session cookie stays host-only on `api.<domain>`, and the browser still sends it with `fetch` from `app.` and `studio.`, because they are the same site. Sharing it with the static hosts would only widen where it travels.

`src/CareNest.AppHost/Templates/web.bicep`:

```bicep
// Static Web Apps for the two SPAs; Aspire 13 has no Static Web Apps integration, so the deploy workflow uploads the files.
param location string = resourceGroup().location

resource client 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'cn-client'
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

resource studio 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'cn-studio'
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

output clientHostname string = client.properties.defaultHostname
output studioHostname string = studio.properties.defaultHostname
```

`src/CareNest.AppHost/appsettings.json` gains a `Deploy` section, so the model and therefore `infra/` are fully determined by committed files:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Aspire.Hosting.Dcp": "Warning"
    }
  },
  "Deploy": {
    "CustomDomain": false,
    "Providers": []
  }
}
```

- [ ] **Step 3: The local stack still works**

Run: `dotnet build CareNest.slnx`, then from `tests/e2e/`: `pnpm test`. Make sure no AppHost is already running, so Playwright starts the refactored one.
Expected: 0 warnings; `7 passed`.

- [ ] **Step 4: Generate and inspect infra**

Append to `.gitattributes`:

```
# Aspire writes infra/ with the OS line ending; LF in the repo keeps the CI drift check stable.
infra/** text=auto eol=lf
```

```bash
dotnet aspire publish --apphost src/CareNest.AppHost/CareNest.AppHost.csproj --output-path infra --non-interactive --nologo
```

Expected: `Pipeline succeeded`; 13 files. Check:
- `main.bicep` has `targetScope = 'subscription'`, a resource group, and params `key_vault`, `postgres_user` (default `'carenest'`) and secure `postgres_password`.
- `api/api.bicep` has `minReplicas: 1`, `maxReplicas: 2`, a `Liveness` probe on `/alive`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, and Key Vault references (`keyVaultUrl`) for `connectionstrings--carenest`, `admin-email`, `email-username` and `email-password`.
- `postgres/postgres.bicep` has `Standard_B1ms`, `Burstable`, 32 GB, `backupRetentionDays: 7`, `passwordAuth: 'Enabled'`.
- `cn/cn.bicep` has no `aspireDashboard`.
- `migrations/migrations.bicep` has a job with `triggerType: 'Manual'`.

Run the same command a second time and check that `git status --porcelain infra` shows the same files, unchanged.

- [ ] **Step 5: Drift check in CI**

Append to the `build-and-test` job in `.github/workflows/backend.yml`:

```yaml

      # infra/ is the reviewable output of the AppHost's Azure model; it must be regenerated and committed with every model change.
      - name: Committed infra matches the Azure model
        run: |
          dotnet tool restore
          dotnet aspire publish --apphost src/CareNest.AppHost/CareNest.AppHost.csproj --output-path infra --non-interactive --nologo
          git diff --exit-code -- infra
          test -z "$(git status --porcelain -- infra)"
```

- [ ] **Step 6: Document it**

In `docs/tech-overview.md`:

1. In section 2, below the `tests/` map block, add:

```markdown
Деплой: `infra/` - Bicep, сгенерированный из модели Aspire (`aspire publish`), коммитится и проверяется в CI; `deploy/` - скрипты разовой подготовки Azure и шагов деплоя (раздел 16).
```

2. At the end of section 11, add:

```markdown
### Публикация в Azure

У AppHost две модели. При `dotnet run` работает локальная (`LocalStack.cs`): контейнеры, Mailpit, Vite. При `aspire publish` и `aspire deploy` - азурная (`AzureDeployment.cs`): Container Apps, PostgreSQL Flexible Server, Key Vault, Application Insights и два Static Web Apps (раздел 16). `aspire publish` превращает модель в Bicep (декларативный язык описания ресурсов Azure) в папке `infra/`; этот вывод коммитится, чтобы изменение инфраструктуры было видно в pull request, а CI проверяет, что `infra/` совпадает с моделью. Настройки, от которых зависит модель, лежат в `src/CareNest.AppHost/appsettings.json` (раздел `Deploy`), а не в переменных окружения, поэтому `infra/` определяется только закоммиченными файлами.

Aspire CLI закреплён как локальный инструмент (`.config/dotnet-tools.json`): `dotnet tool restore`, затем `dotnet aspire publish --apphost src/CareNest.AppHost/CareNest.AppHost.csproj --output-path infra`.

Официальная документация: https://aspire.dev/deployment/azure/
```

3. In section 6, replace `при деплое в Azure миграции точно так же будут отдельным шагом (раздел 16)` with `при деплое в Azure это задание (job) Container Apps `migrations`, которое workflow деплоя запускает и дожидается (раздел 16)`.

- [ ] **Step 7: Commit**

```bash
git add .config .gitattributes .github/workflows/backend.yml src/CareNest.AppHost infra docs/tech-overview.md
git commit -m "feat: Azure deployment model in the AppHost with committed infra and a drift check"
```

---

### Task 4: Deploy workflow, bootstrap and runbook

**Files:**
- Create: `.github/workflows/deploy.yml`
- Create: `deploy/bootstrap.sh`, `deploy/budget.bicep`, `deploy/check-secrets.sh`, `deploy/run-migrations.sh`, `deploy/README.md`
- Create: `web/apps/client/public/staticwebapp.config.json`, `web/apps/studio/public/staticwebapp.config.json`
- Modify: `docs/superpowers/specs/foundation-design.md`, `docs/tech-overview.md`

**Interfaces:**
- Consumes: the configuration keys and secret names from Task 3; `infra/*/*.bicep`.
- Produces:
  - GitHub environment `production` with variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `DEPLOY_DOMAIN`, `DEPLOY_KEY_VAULT`, and later `DEPLOY_API_CERTIFICATE`.
  - Repository variable `DEPLOY_ENABLED`.
  - Scripts `deploy/check-secrets.sh <vault>` and `deploy/run-migrations.sh <resource-group>`.

- [ ] **Step 1: SPA fallback for Static Web Apps**

`web/apps/client/public/staticwebapp.config.json` and `web/apps/studio/public/staticwebapp.config.json` (identical):

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/assets/*", "/*.{js,json,webmanifest,png,svg,ico}"]
  }
}
```

Run from `web/`: `VITE_API_BASE_URL=https://api.example.test pnpm build`, then `ls apps/client/dist apps/studio/dist` and `grep -l "https://api.example.test" apps/client/dist/assets/*.js`.
Expected: `staticwebapp.config.json` is in both `dist` folders, and the bundle contains the API base URL.

- [ ] **Step 2: Scripts**

`deploy/check-secrets.sh`:

```bash
#!/usr/bin/env bash
# A Key Vault reference to a missing secret fails the new revision late and quietly, so the deploy checks every one first.
set -euo pipefail

vault="$1"
# The connection string secrets are written by the deploy itself.
names=$(grep -h -A1 "Microsoft.KeyVault/vaults/secrets@[^']*' existing" infra/*/*.bicep \
  | sed -n "s/^  name: '\(.*\)'$/\1/p" | grep -v '^connectionstrings--' | sort -u)

missing=0
for name in $names; do
  if ! az keyvault secret show --vault-name "$vault" --name "$name" --output none 2>/dev/null; then
    echo "Missing Key Vault secret: $name"
    missing=1
  fi
done

if [ "$missing" -eq 0 ]; then
  echo "All referenced secrets exist: $(echo $names | tr ' ' ',')"
fi
exit "$missing"
```

`deploy/run-migrations.sh`:

```bash
#!/usr/bin/env bash
# Container Apps jobs start asynchronously, so the deploy waits for the execution to finish and fails with it.
set -euo pipefail

resource_group="$1"
execution=$(az containerapp job start --name migrations --resource-group "$resource_group" --query name --output tsv)
echo "Started migrations execution $execution"

for _ in $(seq 1 60); do
  status=$(az containerapp job execution show --name migrations --resource-group "$resource_group" \
    --job-execution-name "$execution" --query properties.status --output tsv)
  case "$status" in
    Succeeded) echo "Migrations applied"; exit 0 ;;
    Failed | Stopped | Degraded) echo "Migrations execution ended as $status; see its logs in Log Analytics"; exit 1 ;;
  esac
  sleep 10
done

echo "Migrations execution did not finish within 10 minutes"
exit 1
```

`deploy/budget.bicep`:

```bicep
// Deployed once by bootstrap.sh: a budget's start date cannot move, so it stays out of the per-deploy templates.
param startDate string
param amount int = 40
param email string

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: 'cn-monthly'
  properties: {
    amount: amount
    category: 'Cost'
    timeGrain: 'Monthly'
    timePeriod: { startDate: startDate }
    notifications: {
      actual80: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 80, thresholdType: 'Actual', contactEmails: [ email ] }
      actual100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Actual', contactEmails: [ email ] }
      forecast100: { enabled: true, operator: 'GreaterThan', threshold: 100, thresholdType: 'Forecasted', contactEmails: [ email ] }
    }
  }
}
```

`deploy/bootstrap.sh`:

```bash
#!/usr/bin/env bash
# One-time Azure and GitHub setup before the first deploy, run by the owner signed in to `az login` and `gh auth login`.
# Safe to re-run: every step checks what already exists; secrets are typed at the prompt and never echoed.
set -euo pipefail

subscription="${1:?usage: bootstrap.sh <subscription-id> <domain> <budget-email>}"
domain="${2:?usage: bootstrap.sh <subscription-id> <domain> <budget-email>}"
budget_email="${3:?usage: bootstrap.sh <subscription-id> <domain> <budget-email>}"

repo="IlyaEsin/carenest"
location="westeurope"
resource_group="rg-carenest"
# Key Vault names are global; the suffix keeps the name stable for this subscription.
vault="kv-carenest-$(printf '%s' "$subscription" | sha256sum | cut -c1-6)"
deploy_app="carenest-deploy"

az account set --subscription "$subscription"
tenant=$(az account show --query tenantId --output tsv)

echo "== Resource providers"
for namespace in Microsoft.App Microsoft.ContainerRegistry Microsoft.DBforPostgreSQL Microsoft.KeyVault \
  Microsoft.ManagedIdentity Microsoft.OperationalInsights Microsoft.Insights Microsoft.Web Microsoft.Consumption; do
  az provider register --namespace "$namespace" --wait
done

echo "== Resource group and Key Vault"
az group create --name "$resource_group" --location "$location" --output none
if ! az keyvault show --name "$vault" --output none 2>/dev/null; then
  az keyvault create --name "$vault" --resource-group "$resource_group" --location "$location" \
    --enable-rbac-authorization true --output none
fi
vault_id=$(az keyvault show --name "$vault" --query id --output tsv)
group_id=$(az group show --name "$resource_group" --query id --output tsv)

me=$(az ad signed-in-user show --query id --output tsv)
az role assignment create --assignee-object-id "$me" --assignee-principal-type User \
  --role "Key Vault Secrets Officer" --scope "$vault_id" --output none

# A new role assignment can take a few minutes to apply, so the first secret write is retried.
set_secret() {
  local name="$1" value="$2"
  for _ in $(seq 1 30); do
    if az keyvault secret set --vault-name "$vault" --name "$name" --value "$value" --output none 2>/dev/null; then
      return 0
    fi
    sleep 10
  done
  echo "Could not write secret $name"
  return 1
}

ask_secret() {
  local name="$1" prompt="$2" value
  if az keyvault secret show --vault-name "$vault" --name "$name" --output none 2>/dev/null; then
    echo "Secret $name already set"
    return 0
  fi
  read -r -s -p "$prompt: " value
  echo
  set_secret "$name" "$value"
}

echo "== Secrets"
if ! az keyvault secret show --vault-name "$vault" --name postgres-password --output none 2>/dev/null; then
  set_secret postgres-password "$(openssl rand -base64 36 | tr -d '/+=')"
fi
ask_secret admin-email "Admin email (the address you sign in with)"
ask_secret email-username "Brevo SMTP login"
ask_secret email-password "Brevo SMTP key"

echo "== Budget"
az deployment group create --resource-group "$resource_group" --template-file deploy/budget.bicep \
  --parameters startDate="$(date -u +%Y-%m-01)T00:00:00Z" email="$budget_email" --output none

echo "== Deploy identity for GitHub Actions (OIDC, no stored credentials)"
app_id=$(az ad app list --display-name "$deploy_app" --query "[0].appId" --output tsv)
if [ -z "$app_id" ]; then
  app_id=$(az ad app create --display-name "$deploy_app" --query appId --output tsv)
fi
if ! az ad sp show --id "$app_id" --output none 2>/dev/null; then
  az ad sp create --id "$app_id" --output none
fi
sp_id=$(az ad sp show --id "$app_id" --query id --output tsv)
if [ -z "$(az ad app federated-credential list --id "$app_id" --query "[?name=='github-production'].name" --output tsv)" ]; then
  az ad app federated-credential create --id "$app_id" --parameters "{
    \"name\": \"github-production\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$repo:environment:production\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }" --output none
fi
# The Aspire template is subscription-scoped (it declares the resource group) and assigns roles to the app identities.
az role assignment create --assignee-object-id "$sp_id" --assignee-principal-type ServicePrincipal \
  --role Contributor --scope "/subscriptions/$subscription" --output none
az role assignment create --assignee-object-id "$sp_id" --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" --scope "$group_id" --output none
az role assignment create --assignee-object-id "$sp_id" --assignee-principal-type ServicePrincipal \
  --role "Key Vault Secrets User" --scope "$vault_id" --output none

echo "== GitHub environment and variables"
gh api --method PUT "repos/$repo/environments/production" \
  -F "deployment_branch_policy[protected_branches]=false" \
  -F "deployment_branch_policy[custom_branch_policies]=true" > /dev/null
if [ -z "$(gh api "repos/$repo/environments/production/deployment-branch-policies" --jq '.branch_policies[] | select(.name=="main") | .name')" ]; then
  gh api --method POST "repos/$repo/environments/production/deployment-branch-policies" -f name=main -f type=branch > /dev/null
fi
gh variable set AZURE_CLIENT_ID --env production --repo "$repo" --body "$app_id"
gh variable set AZURE_TENANT_ID --env production --repo "$repo" --body "$tenant"
gh variable set AZURE_SUBSCRIPTION_ID --env production --repo "$repo" --body "$subscription"
gh variable set AZURE_RESOURCE_GROUP --env production --repo "$repo" --body "$resource_group"
gh variable set DEPLOY_DOMAIN --env production --repo "$repo" --body "$domain"
gh variable set DEPLOY_KEY_VAULT --env production --repo "$repo" --body "$vault"

echo "Done. Key Vault: $vault, resource group: $resource_group"
```

Run: `bash -n deploy/bootstrap.sh deploy/check-secrets.sh deploy/run-migrations.sh` (each file separately).
Expected: no output. Then check the secret-name parsing against the committed infra:

```bash
grep -h -A1 "Microsoft.KeyVault/vaults/secrets@[^']*' existing" infra/*/*.bicep | sed -n "s/^  name: '\(.*\)'$/\1/p" | grep -v '^connectionstrings--' | sort -u
```

Expected: `admin-email`, `email-password`, `email-username`.

- [ ] **Step 3: The deploy workflow**

`.github/workflows/deploy.yml`:

```yaml
name: deploy

on:
  push:
    branches: [main]
  workflow_dispatch:

permissions:
  id-token: write
  contents: read

# Two deploys must never interleave; a queued one waits instead of cancelling a half-finished one.
concurrency:
  group: deploy-production
  cancel-in-progress: false

jobs:
  production:
    # Off until the Azure bootstrap is done (deploy/README.md), so merges before that do not fail.
    if: vars.DEPLOY_ENABLED == 'true'
    runs-on: ubuntu-latest
    environment: production
    timeout-minutes: 45
    env:
      Azure__SubscriptionId: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      Azure__TenantId: ${{ vars.AZURE_TENANT_ID }}
      Azure__ResourceGroup: ${{ vars.AZURE_RESOURCE_GROUP }}
      Azure__Location: westeurope
      Azure__CredentialSource: AzureCli
      Deploy__Domain: ${{ vars.DEPLOY_DOMAIN }}
      Deploy__ApiHost: api.${{ vars.DEPLOY_DOMAIN }}
      Deploy__ApiCertificate: ${{ vars.DEPLOY_API_CERTIFICATE }}
      Deploy__KeyVault: ${{ vars.DEPLOY_KEY_VAULT }}
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - uses: azure/login@v3
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      # The password lives only in Key Vault; the deploy reads it with its federated identity instead of a GitHub secret.
      - name: Read the database password
        run: |
          password=$(az keyvault secret show --vault-name "$Deploy__KeyVault" --name postgres-password --query value --output tsv)
          echo "::add-mask::$password"
          echo "Deploy__PostgresPassword=$password" >> "$GITHUB_ENV"

      - name: Every Key Vault secret the templates reference exists
        run: bash deploy/check-secrets.sh "$Deploy__KeyVault"

      - name: Deploy the API, the migrations job and Azure resources
        run: |
          az bicep install
          dotnet tool restore
          dotnet aspire deploy --apphost src/CareNest.AppHost/CareNest.AppHost.csproj --non-interactive --nologo

      - name: Apply database migrations
        run: bash deploy/run-migrations.sh "$Azure__ResourceGroup"

      - uses: pnpm/action-setup@v4
        with:
          package_json_file: web/package.json

      - uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: pnpm
          cache-dependency-path: web/pnpm-lock.yaml

      - name: Build the web apps
        working-directory: web
        env:
          VITE_API_BASE_URL: https://api.${{ vars.DEPLOY_DOMAIN }}
        run: |
          pnpm install --frozen-lockfile
          pnpm build

      # Deployment tokens are fetched per run, so no Static Web Apps token is stored in GitHub.
      - name: Read the Static Web Apps deployment tokens
        id: swa
        run: |
          for app in client studio; do
            token=$(az staticwebapp secrets list --name "cn-$app" --resource-group "$Azure__ResourceGroup" --query properties.apiKey --output tsv)
            echo "::add-mask::$token"
            echo "$app=$token" >> "$GITHUB_OUTPUT"
          done

      - uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ steps.swa.outputs.client }}
          action: upload
          app_location: web/apps/client/dist
          output_location: ''
          skip_app_build: true
          skip_api_build: true

      - uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ steps.swa.outputs.studio }}
          action: upload
          app_location: web/apps/studio/dist
          output_location: ''
          skip_app_build: true
          skip_api_build: true
```

Migrations run after the new API revision is live, because `aspire deploy` builds, pushes and rolls out in one step. For about a minute the new code runs against the old schema, so every migration must be backward compatible (expand, then contract in a later release). `REVIEW.md` (Task 6) enforces this.

Run: `actionlint .github/workflows/*.yml`. If actionlint is not installed, download it from https://github.com/rhysd/actionlint/releases (v1.7.12).
Expected: no output.

- [ ] **Step 4: Runbook**

`deploy/README.md`:

````markdown
# Deploy

Production runs on Azure (West Europe). A merge to `main` deploys through `.github/workflows/deploy.yml`:

1. GitHub signs in to Azure with OIDC (no stored credentials).
2. It checks that every Key Vault secret the templates reference exists.
3. `dotnet aspire deploy` builds the images and applies `infra/`.
4. It runs the `migrations` job and waits for it.
5. It uploads `client` and `studio` to Static Web Apps.

## Resources

| What | Name |
|---|---|
| Resource group | `rg-carenest` |
| Key Vault | `kv-carenest-<6 hex>` (printed by `bootstrap.sh`, GitHub variable `DEPLOY_KEY_VAULT`) |
| API | container app `api`, custom domain `api.<domain>` |
| Migrations | Container Apps job `migrations` |
| Web | Static Web Apps `cn-client` (`app.<domain>`), `cn-studio` (`studio.<domain>`) |
| Budget | `cn-monthly`, 40 USD, mails at 80% and 100% |

## Key Vault secrets

| Secret | Used as |
|---|---|
| `postgres-password` | server admin password (generated by `bootstrap.sh`) |
| `admin-email` | `Identity:AdminEmails:0` |
| `email-username`, `email-password` | Brevo SMTP login and SMTP key |
| `google-client-id`, `google-client-secret` | Google OAuth client |
| `yandex-client-id`, `yandex-client-secret` | Yandex ID app |
| `vkid-client-id`, `vkid-client-secret` | VK ID app |
| `telegram-bot-token`, `telegram-bot-name` | Telegram Login Widget bot |

Set or rotate one: `az keyvault secret set --vault-name <vault> --name <secret> --value "<value>"`, then restart the API revision so it re-reads references: `az containerapp revision restart --name api --resource-group rg-carenest --revision $(az containerapp show --name api --resource-group rg-carenest --query properties.latestRevisionName --output tsv)`.

## First-time setup

1. Buy the domain, create the Azure subscription, sign in with `az login` and `gh auth login`.
2. Create the Brevo account.
3. Run `bash deploy/bootstrap.sh <subscription-id> <domain> <budget-email>`.
4. Set the repository variable `DEPLOY_ENABLED=true` and run the deploy workflow.
5. Set up DNS, the API custom domain and the Static Web Apps domains ("API custom domain" below).

## Adding a sign-in provider

1. Register the app with the provider. The redirect URI is `https://api.<domain>/api/identity/signin-<google|yandex|vkid>`.
2. Put `<provider>-client-id` and `<provider>-client-secret` into Key Vault. For Telegram, put `telegram-bot-token` and `telegram-bot-name`.
3. Add the provider (`Google`, `Yandex`, `VkId`, `Telegram`) to `Deploy:Providers` in `src/CareNest.AppHost/appsettings.json`.
4. Regenerate `infra/` (`dotnet aspire publish --apphost src/CareNest.AppHost/CareNest.AppHost.csproj --output-path infra`) and merge a PR.

The order matters: a referenced secret that does not exist stops the deploy at the check step.

## API custom domain

The binding needs DNS records that can only point at the app after the first deploy, so it is switched on in three phases:

1. `az containerapp show --name api --resource-group rg-carenest --query "{fqdn: properties.configuration.ingress.fqdn, verification: properties.customDomainVerificationId}"`.
2. At the registrar:
   - `CNAME api` pointing at that fqdn;
   - `TXT asuid.api` with the verification id.
3. Add and bind the hostname with a free managed certificate:
   - `az containerapp hostname add --hostname api.<domain> --name api --resource-group rg-carenest`
   - `az containerapp hostname bind --hostname api.<domain> --name api --resource-group rg-carenest --environment $(az containerapp env list --resource-group rg-carenest --query "[0].name" --output tsv) --validation-method CNAME`
4. Find the certificate name:
   - `az containerapp env certificate list --name <environment> --resource-group rg-carenest --managed-certificates-only --query "[?properties.subjectName=='api.<domain>'].name" --output tsv`
   - store it: `gh variable set DEPLOY_API_CERTIFICATE --env production --body <name>`.
5. Set `Deploy:CustomDomain` to `true`, regenerate `infra/` and merge. From then on every deploy keeps the binding.
   - Merge nothing else between steps 3 and 5: a deploy without the flag drops the hostname.

Static Web Apps domains:
- `CNAME app` pointing at `cn-client`'s default hostname, `CNAME studio` pointing at `cn-studio`'s (`az staticwebapp show --name cn-client --resource-group rg-carenest --query defaultHostname --output tsv`);
- then `az staticwebapp hostname set --name cn-client --resource-group rg-carenest --hostname app.<domain>`, and the same for `studio`.

## Rollback

Revert the commit on `main` through a PR; the deploy workflow rolls the previous code forward. Migrations are backward compatible (REVIEW.md), so a code rollback never needs a schema rollback.
````

- [ ] **Step 5: Spec update**

In `docs/superpowers/specs/foundation-design.md`:
- Section 5, Email row: replace `sent via Azure Communication Services Email (Mailpit locally)` with `sent over SMTP via Brevo (Mailpit locally); Azure Communication Services Email was dropped because Microsoft retires it on 30 September 2028`.
- Section 7, how-to-test: replace `` `tests/how-to-test/cn-<n>/` `` with `` `tests/e2e/how-to-test/cn-<n>/` (inside the e2e project, so the scenarios reuse its Playwright install and helpers) ``.
- Section 7, Pipeline:
  - replace `A merge to `main` deploys to production via `azd`.` with `A merge to `main` deploys to production via `aspire deploy` (Aspire 13 no longer recommends azd); `infra/` is the committed output of `aspire publish`.`
  - replace `Static Web Apps provides free per-PR preview environments for the frontends.` with `Static Web Apps preview environments are not used: they live on `*.azurestaticapps.net`, a different site from `api.<domain>`, so sign-in cannot work there.`
  - replace `(EF migration bundle)` with `(the MigrationService image as a Container Apps job the deploy workflow starts and waits for; migrations must be backward compatible because they run after the new API revision)`.
- Section 7, Operations: replace `an Azure budget alert at 30 USD per month.` with `an Azure budget alert at 40 USD per month (the API keeps one warm replica; about 31 USD per month in total).`
- Section 7, Portability: replace `(Azure Communication Services SMTP relay in production)` with `(Brevo SMTP relay in production)`.
- Section 8, criterion 10: replace ``with `azd` `` with ``with `aspire deploy` ``.
- Section 9: replace the three items with:

```markdown
- Resolved in plan 1: `AspNet.Security.OAuth.VkId` 10.0.0 works with the current VK ID API.
- Resolved in plan 3: the domain is bought during delivery (Task 7). Free Static Web Apps with custom domains works with the cookie layout because the session cookie is host-only on `api.<domain>` and `app.`/`studio.` are the same site.
```

- [ ] **Step 6: Document it**

In `docs/tech-overview.md`:

1. Section 8, Email bullet: replace `отправляется через Azure Communication Services Email в проде и через Mailpit локально (раздел 12)` with `отправляется по SMTP через Brevo в проде и через Mailpit локально (раздел 12)`.
2. Section 12, replace the last paragraph (`В продакшене вместо Mailpit используется **Azure Communication Services Email**...`) with:

```markdown
В продакшене вместо Mailpit письма отправляет **Brevo** - сервис рассылок (французская компания, данные в ЕС) через свой SMTP-relay `smtp-relay.brevo.com:587` с STARTTLS; бесплатный тариф - 300 писем в день. Код тот же самый `SmtpEmailSender`, меняются только настройки `Email:*`, поэтому другой провайдер - это смена конфигурации. Azure Communication Services Email, который был в спецификации, не взяли: Microsoft выводит его из эксплуатации 30 сентября 2028 года. Чтобы письма не попадали в спам, домен отправителя подтверждается в Brevo DNS-записями (DKIM, DMARC).

Официальная документация: https://developers.brevo.com/docs/smtp-integration
```

3. Replace the whole section 16 (from `## 16. Azure (план 3, ещё не настроен и не оплачен)` up to `## 17. Фронтенд`) with:

```markdown
## 16. Azure и деплой

Продакшен работает в Azure, регион West Europe. Всё описано кодом: модель ресурсов - `src/CareNest.AppHost/AzureDeployment.cs`, сгенерированный из неё Bicep - `infra/` (раздел 11), деплой - `.github/workflows/deploy.yml`, разовая подготовка и эксплуатация - `deploy/bootstrap.sh` и `deploy/README.md`.

Ресурсы:
- **Azure Container Apps** - управляемый запуск контейнеров без администрирования виртуальных машин. Здесь живут API (одна всегда тёплая реплика, максимум две) и задание (job) `migrations`, которое применяет миграции базы.
- **Azure Container Registry** - хранилище Docker-образов, которые собирает деплой.
- **Azure Database for PostgreSQL Flexible Server** (Burstable B1ms, 32 ГБ, бэкапы 7 дней) - управляемый PostgreSQL; вход по паролю, чтобы приложению не нужен был Azure SDK.
- **Azure Static Web Apps** (бесплатный тариф) - `cn-client` и `cn-studio`, статические сборки двух приложений, с бесплатными сертификатами для `app.` и `studio.`.
- **Key Vault** - хранилище секретов: пароль базы, email администратора, логин и ключ SMTP, ключи OAuth-провайдеров, токен Telegram-бота.
- **Application Insights + Log Analytics** - логи, метрики и трассировки, которые локально видны в Aspire-дашборде (раздел 11).
- **Бюджет** 40 USD в месяц с письмами при 80% и 100% (создаётся один раз скриптом `deploy/bootstrap.sh`). Оценка расходов - около 31 USD в месяц: PostgreSQL ~19, Container Registry ~5, тёплая реплика API ~6, остальное почти бесплатно.

**Как секреты попадают в приложение.** Container Apps хранит не сами значения, а ссылки на секреты Key Vault (Key Vault references) и читает их управляемым удостоверением (managed identity) приложения. Приложение получает их как обычные переменные окружения (`Email__Password`, `ConnectionStrings__carenest` и т.д.) и ничего не знает про Key Vault. В коде нет ни клиента Key Vault, ни другого Azure SDK (архитектурный тест, раздел 18), поэтому переезд на другой хостинг - это новая инфраструктура, а не переписывание кода. Секреты кладёт в Key Vault владелец (`bootstrap.sh` спрашивает их без вывода на экран); в репозитории и в GitHub их нет.

**Как GitHub попадает в Azure.** Через OIDC (federated credentials): GitHub Actions получает короткоживущий токен, которому Azure доверяет для окружения `production` этого репозитория. Паролей и ключей Azure в GitHub нет.

**`aspire deploy`.** Команда Aspire CLI, которая собирает образы, пушит их в Container Registry и применяет Bicep. Раньше для этого использовали azd (Azure Developer CLI); начиная с Aspire 13 рекомендуемый путь - `aspire deploy`, azd поддерживается только для существующих проектов. После выкладки workflow запускает задание `migrations` и ждёт его, затем выкладывает оба фронтенда. Миграции идут после новой версии API, поэтому они обязаны быть обратно совместимыми (`REVIEW.md`).

**Домен.** Собственный домен с поддоменами `app.`, `studio.` и `api.`. Cookie сессии ставит `api.<домен>`, и браузер отправляет её на запросы с `app.<домен>`, потому что это один сайт (same-site). Стандартные адреса Azure (`*.azurestaticapps.net`, `*.azurecontainerapps.io`) - это другие сайты, на них вход не работает; поэтому превью-окружения Static Web Apps для pull request не используются.

Регион - EU; вопрос 152-ФЗ остаётся открытым, владелец решил деплоить в Azure, сохраняя возможность переезда (раздел 5).
```

4. Section 19 (reading list): replace the `**Azure (план 3)**` block with:

```markdown
**Azure и деплой**
- Aspire: деплой в Azure: https://aspire.dev/deployment/azure/
- Container Apps: https://learn.microsoft.com/en-us/azure/container-apps/overview
- Azure Database for PostgreSQL Flexible Server: https://learn.microsoft.com/en-us/azure/postgresql/overview
- Static Web Apps: https://learn.microsoft.com/en-us/azure/static-web-apps/overview
- Key Vault references в Container Apps: https://learn.microsoft.com/en-us/azure/container-apps/manage-secrets
- Application Insights + OpenTelemetry: https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview
- GitHub Actions + Azure OIDC: https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-openid-connect
- Brevo SMTP: https://developers.brevo.com/docs/smtp-integration
- YouTube (RU): `Azure Container Apps обзор`
- YouTube (EN): `.NET Aspire deploy to Azure Container Apps`, `GitHub Actions OIDC Azure login`
```

5. Section 5, the `Managed-вариант в Azure` bullet: replace `в проде (план 3) используется` with `в проде используется`.

- [ ] **Step 7: Commit**

```bash
git add .github/workflows/deploy.yml deploy web/apps/client/public web/apps/studio/public docs
git commit -m "feat: deploy workflow with OIDC, migrations job and Static Web Apps; bootstrap and runbook"
```

---

### Task 5: Repository hygiene checks

**Files:**
- Create: `.github/workflows/hygiene.yml`, `.github/scripts/forbidden-references.sh`, `.github/dependabot.yml`
- Modify: `docs/tech-overview.md`

**Interfaces:**
- Consumes: GitHub secret `FORBIDDEN_REFERENCES` (newline-separated extended regexes, case-insensitive), created by the owner in Step 5.
- Produces: check names `secrets-scan` and `forbidden-references` (required in Task 6's ruleset together with `build-and-test`, `checks` and `smoke`).

- [ ] **Step 1: The denylist guard**

`.github/scripts/forbidden-references.sh`:

```bash
#!/usr/bin/env bash
# The denylist is a repository secret, so the names never appear in this public repository or in its logs.
set -euo pipefail

if [ -z "${FORBIDDEN_REFERENCES:-}" ]; then
  echo "The FORBIDDEN_REFERENCES secret is not set"
  exit 1
fi

patterns=$(mktemp)
trap 'rm -f "$patterns"' EXIT
printf '%s\n' "$FORBIDDEN_REFERENCES" | sed '/^[[:space:]]*$/d' > "$patterns"

found=0
# Only file names and counts are printed: the matching lines would publish the names.
if git grep -I -i -c -E -f "$patterns" -- .; then
  found=1
fi

history=$(git log --format='%an%n%ae%n%cn%n%ce%n%B' | grep -i -c -E -f "$patterns" || true)
if [ "${history:-0}" -gt 0 ]; then
  echo "commit metadata or messages: $history matching lines"
  found=1
fi

if [ "$found" -eq 1 ]; then
  echo "Forbidden references found"
  exit 1
fi

echo "No forbidden references"
```

- [ ] **Step 2: Test the guard locally**

```bash
FORBIDDEN_REFERENCES=$'zzqqxx\n\nqqzzyy' bash .github/scripts/forbidden-references.sh; echo "exit=$?"
FORBIDDEN_REFERENCES='shouldly' bash .github/scripts/forbidden-references.sh; echo "exit=$?"
FORBIDDEN_REFERENCES='' bash .github/scripts/forbidden-references.sh; echo "exit=$?"
```

Expected:
- `No forbidden references`, `exit=0`;
- file names with counts (e.g. `tests/Directory.Build.props:2`), then `Forbidden references found`, `exit=1`;
- `The FORBIDDEN_REFERENCES secret is not set`, `exit=1`.

- [ ] **Step 3: Workflow and Dependabot**

`.github/workflows/hygiene.yml`:

```yaml
name: hygiene

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  secrets-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      # Free for repositories of personal accounts; organisations would need GITLEAKS_LICENSE.
      - uses: gitleaks/gitleaks-action@v3
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          GITLEAKS_ENABLE_COMMENTS: false

  forbidden-references:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: No forbidden third-party names in files or history
        env:
          FORBIDDEN_REFERENCES: ${{ secrets.FORBIDDEN_REFERENCES }}
        run: bash .github/scripts/forbidden-references.sh
```

`.github/dependabot.yml`:

```yaml
version: 2
updates:
  # Dependabot does not read .slnx, so every project directory is listed.
  - package-ecosystem: nuget
    directories: ["/src/*", "/src/Modules/*", "/tests/*"]
    schedule:
      interval: weekly
    groups:
      nuget:
        patterns: ["*"]

  - package-ecosystem: npm
    directories: ["/web", "/tests/e2e"]
    schedule:
      interval: weekly
    groups:
      npm:
        patterns: ["*"]

  - package-ecosystem: github-actions
    directory: "/"
    schedule:
      interval: weekly
```

Run: `actionlint .github/workflows/*.yml`, and gitleaks over the history. Install it from https://github.com/gitleaks/gitleaks/releases, v8.30.1. Command: `gitleaks git . --no-banner`.
Expected: actionlint prints nothing; gitleaks says `no leaks found`.

- [ ] **Step 4: Document it**

In `docs/tech-overview.md`, section 15:

1. Replace the paragraph ``` `main` защищён: изменения попадают туда только через pull request с зелёным CI.``` with:

```markdown
В backend-workflow есть ещё шаг "Committed infra matches the Azure model": он заново генерирует `infra/` из модели Aspire и падает, если результат отличается от закоммиченного (раздел 11).
```

2. Replace `Ещё два workflow:` with `Ещё четыре workflow:`.
3. Append to the list:

```markdown
- `.github/workflows/hygiene.yml` - две проверки гигиены. **gitleaks** ищет в файлах и во всей истории git то, что похоже на секреты (ключи, токены, пароли). Проверка запрещённых имён ищет в файлах, в сообщениях коммитов и в именах авторов имена, которых не должно быть в публичном репозитории; сам список лежит в секрете репозитория `FORBIDDEN_REFERENCES`, а в лог попадают только имена файлов и число совпадений, чтобы список не утёк через лог.
- `.github/workflows/deploy.yml` - деплой в Azure после каждого merge в `main` (раздел 16); включается переменной репозитория `DEPLOY_ENABLED`.

Кроме workflow:
- **CodeQL** - статический анализ кода на уязвимости от GitHub (C#, TypeScript, сами workflow). Включён как "default setup" в настройках репозитория, отдельного файла нет; находки видны во вкладке Security.
- **Dependabot** (`.github/dependabot.yml`) - раз в неделю открывает pull request'ы с обновлениями NuGet-, npm-пакетов и GitHub Actions, сгруппированные по экосистеме.
- **Push protection** - GitHub отклоняет push, в котором распознал секрет, ещё до того, как он попадёт в репозиторий.

Официальная документация: https://github.com/gitleaks/gitleaks, https://docs.github.com/en/code-security/code-scanning/enabling-code-scanning/configuring-default-setup-for-code-scanning, https://docs.github.com/en/code-security/dependabot, https://docs.github.com/en/code-security/secret-scanning/push-protection-for-repositories-and-organizations
YouTube (EN): `gitleaks GitHub Actions`, `GitHub CodeQL default setup`, `Dependabot tutorial`
```

The branch-protection paragraph comes back in Task 6, once the ruleset really exists. This fixes the section 15 inaccuracy.

- [ ] **Step 5: STOP (owner) - denylist secret**

Ask the owner to create the secret from their own list, one name or regex per line, never pasted into the chat:

```bash
gh secret set FORBIDDEN_REFERENCES --repo IlyaEsin/carenest < path/to/denylist.txt
gh secret set FORBIDDEN_REFERENCES --repo IlyaEsin/carenest --app dependabot < path/to/denylist.txt
```

The second copy is needed because workflows triggered by Dependabot pull requests only see Dependabot secrets. Wait for the owner's confirmation.

- [ ] **Step 6: Commit**

```bash
git add .github docs/tech-overview.md
git commit -m "ci: gitleaks, forbidden-reference guard and Dependabot"
```

---

### Task 6: Claude tooling, review checklist, pull request and branch protection

**Files:**
- Create: `.claude/skills/build-test/SKILL.md`, `.claude/skills/how-to-test/SKILL.md`, `REVIEW.md`
- Modify: `tests/e2e/playwright.config.ts`, `tests/e2e/package.json`
- Modify: `CLAUDE.md`, `docs/tech-overview.md`

**Interfaces:**
- Consumes: e2e helpers `tests/e2e/support/{actors,mail,urls}.ts`; the check names from Tasks 3 and 5 (`build-and-test`, `checks`, `smoke`, `secrets-scan`, `forbidden-references`).
- Produces: Playwright project `how-to-test` (test directory `tests/e2e/how-to-test/`), script `pnpm how-to-test <filter>`.

- [ ] **Step 1: how-to-test project**

In `tests/e2e/playwright.config.ts`, add a third project after `walkthrough`:

```ts
    // Per-issue "how to test" scenarios (how-to-test skill), shown in a visible browser.
    { name: 'how-to-test', testDir: './how-to-test', use: { headless: false, launchOptions: { slowMo: 500 } } },
```

In `tests/e2e/package.json` scripts, after `walkthrough`:

```json
    "how-to-test": "playwright test --project=how-to-test",
```

Verify with a throwaway scenario. Create `tests/e2e/how-to-test/cn-0/sign-in.spec.ts`:

```ts
import { expect, test } from '@playwright/test';
import { signedInParent } from '../../support/actors';

test('cn-0: a parent signs in and sees the empty home', async ({ browser }) => {
  const { page } = await signedInParent(browser);

  await expect(page.getByText('No active consultations')).toBeVisible();
});
```

Run from `tests/e2e/`: `pnpm typecheck`, `pnpm how-to-test cn-0`, then `CI=1 npx playwright test --project=smoke --list`.
Expected: typecheck clean; `1 passed` in a visible browser; the smoke list still shows `Total: 7 tests in 3 files`.
Then delete `tests/e2e/how-to-test/cn-0/`. The folder only appears with the first real scenario; the project runs fine without it.

- [ ] **Step 2: Skills**

`.claude/skills/build-test/SKILL.md`:

```markdown
---
name: build-test
description: Build and test CareNest scoped to what changed - backend build and tests, web lint/typecheck/tests, OpenAPI and infra drift. Use before saying a change works, before a commit, and before opening a PR.
---

# Build and test

1. List what changed: `git status --short` and `git diff --name-only origin/main...HEAD`.
2. Run every row whose paths match, from the repository root unless a folder is given. Docker must be running for integration tests.

| Changed paths | Run |
|---|---|
| `src/**`, `tests/CareNest.*/**`, `*.props`, `global.json` | `dotnet build CareNest.slnx` (0 warnings), `dotnet test CareNest.slnx` |
| an endpoint, request/response type or error code | `dotnet test tests/CareNest.Api.IntegrationTests`; if `OpenApiContractTests` fails on purpose: `CARENEST_UPDATE_OPENAPI=1 dotnet test tests/CareNest.Api.IntegrationTests`, then in `web/` `pnpm generate:api` |
| `src/CareNest.AppHost/**` | `dotnet tool restore`, `dotnet aspire publish --apphost src/CareNest.AppHost/CareNest.AppHost.csproj --output-path infra`, commit `infra/` if it changed |
| `web/**` | in `web/`: `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build` (the build regenerates `routeTree.gen.ts`; commit it if it changed) |
| a user flow (sign-in, profile, invitations) or `src/CareNest.AppHost/LocalStack.cs` | in `tests/e2e/`: `pnpm test` |
| `.github/workflows/**` | `actionlint .github/workflows/*.yml` if installed |

3. Report the exact counts from the output (for example "Integration 78 passed"). Never say green without having the output in front of you; if a check could not run, say which and why.
```

`.claude/skills/how-to-test/SKILL.md`:

````markdown
---
name: how-to-test
description: Write and run a Playwright "how to test" scenario for a GitHub issue in tests/e2e/how-to-test/cn-<n>/, so the owner can watch the change work in a real browser against the local Aspire stack. Use when an issue's change is ready to be shown.
---

# How to test

1. Find the issue number from the branch (`feature/cn-<n>-...`) or ask.
2. Write `tests/e2e/how-to-test/cn-<n>/<what-it-shows>.spec.ts`:
   - Reuse the helpers in `tests/e2e/support/`: `signedInParent`, `signedInConsultant`, `createInvitation` from `actors.ts`, `signInWithEmail` and `latestMagicLink` from `mail.ts`, and the URLs and `uniqueEmail` from `urls.ts`.
   - Name every test `cn-<n>: <what the person sees>`. Assert on what a person sees (role, label, text), never on CSS classes.
   - Use `en-US` UI text, as in `tests/e2e/specs/`.
3. Run from `tests/e2e/`: `pnpm typecheck`, then `pnpm how-to-test cn-<n>`.
   - A visible, slowed-down browser opens.
   - Playwright starts the AppHost if it is not running (Docker must be running).
   - Traces of failures land in `test-results/` (gitignored).
4. Commit the scenario with the change; it documents how to verify the issue later.
5. Tell the owner the command to watch it themselves:

   ```bash
   cd tests/e2e && pnpm how-to-test cn-<n>
   ```
````

- [ ] **Step 3: Review checklist**

`REVIEW.md`:

```markdown
# Review checklist

Checked on every pull request, by a person or by Claude. A "no" needs a fix or a written reason in the PR.

## Standing rules (CLAUDE.md)

- [ ] Consultant-owned rows implement `IConsultantOwned`; the module DbContext calls `ApplyConsultantQueryFilters`; every `IgnoreQueryFilters()` has a comment saying why it is safe.
- [ ] Time uses NodaTime only (`Instant`, `LocalDateTime` + IANA zone, injected `IClock`); no `DateTime`/`DateTimeOffset` in domain or persistence code.
- [ ] No UI text outside `web/packages/i18n`; the API returns codes, and every new code has RU and EN text.
- [ ] No personal data about parents or children in logs or traces (ids only). This includes exception messages that embed an email or a name.
- [ ] Every new table holding personal data is covered by account deletion and by its test.

## Module boundaries

- [ ] Only the module's root namespace is public; modules do not reference each other; cross-module calls go through a public interface in the callee's root namespace.
- [ ] No business logic in `CareNest.Api`.
- [ ] No Azure SDK outside `CareNest.AppHost` and `CareNest.ServiceDefaults`.

## Migrations (they run after the new API revision is live)

- [ ] The new code works against the old schema and the old code against the new one (expand, then contract in a later release): add nullable or defaulted columns; never rename or drop a column or table in the same release that stops using it.
- [ ] No long locks on big tables (no table rewrite, e.g. adding a non-null column without a default).
- [ ] Data migrations are idempotent.

## Contract and deploy

- [ ] An API change updated `openapi.json` and the generated client in the same PR; every endpoint has `.WithName(...)`.
- [ ] A change to the AppHost's Azure model regenerated `infra/`; the `infra/` diff is what was intended.
- [ ] A new secret is in Key Vault before the PR that references it (`deploy/README.md`); no secret in code, config or workflow files.
- [ ] Cookies stay `HttpOnly`, `Secure`, `SameSite=Lax`; CORS origins come from `Frontend:Origins` only.

## Docs

- [ ] A newly adopted technology has a section in `docs/tech-overview.md` (Russian).
- [ ] Text uses the plain hyphen `-`; comments are one dry sentence, why not what.
```

- [ ] **Step 4: CLAUDE.md**

In `CLAUDE.md`, section Commands, add after the E2E line:

```markdown
- How-to-test scenario for an issue (from `tests/e2e/`): `pnpm how-to-test cn-<n>` (skill `how-to-test`)
- After changing the AppHost's Azure model: `dotnet tool restore`, `dotnet aspire publish --apphost src/CareNest.AppHost/CareNest.AppHost.csproj --output-path infra`; commit `infra/`
- Deploy: merge to `main` (`.github/workflows/deploy.yml`); setup, secrets, providers and domains: `deploy/README.md`
```

In section Layout and module boundaries, add after the `CareNest.MigrationService` line:

```markdown
- `src/CareNest.AppHost`: `LocalStack.cs` is the local run, `AzureDeployment.cs` the production model; `infra/` is its generated Bicep (never edit by hand); `deploy/` holds the bootstrap and deploy scripts.
```

In section Conventions, add:

```markdown
- Review: `REVIEW.md` is the checklist for every PR; the `build-test` skill picks the checks for what changed.
- Migrations run after the new API revision is live, so they must be backward compatible (see `REVIEW.md`).
```

- [ ] **Step 5: Document it**

In `docs/tech-overview.md`, section 14, add after the Playwright bullet:

```markdown
- **how-to-test** - сценарии Playwright к конкретной задаче: `tests/e2e/how-to-test/cn-<номер>/`, запуск `pnpm how-to-test cn-<номер>` из `tests/e2e/` в видимом браузере. Их пишет Claude по навыку `.claude/skills/how-to-test`, чтобы изменение можно было увидеть своими глазами; навык `.claude/skills/build-test` выбирает, какие проверки запускать для изменённых файлов, а `REVIEW.md` - чек-лист ревью каждого pull request.
```

- [ ] **Step 6: Full verification, commit and pull request**

Run: `dotnet test CareNest.slnx`; in `web/`: `pnpm lint && pnpm typecheck && pnpm test`; in `tests/e2e/`: `pnpm test`.
Expected:
- backend 147 passed;
- web 84 passed;
- e2e `7 passed`.

```bash
git add .claude REVIEW.md CLAUDE.md tests/e2e docs/tech-overview.md
git commit -m "chore: build-test and how-to-test skills, review checklist"
git push
gh pr create --title "Foundation delivery: Azure deploy, hygiene CI, Claude tooling" --body "<summary of Tasks 1-6, the owner decisions from the plan header, test counts>"
```

Expected on the PR:
- green: `build-and-test` (including the infra drift step), `checks`, `smoke`, `secrets-scan`, `forbidden-references`;
- `deploy` does not run (pull requests don't trigger it).

- [ ] **Step 7: STOP (owner) - security settings and branch protection**

These change the public repository's settings. Show the owner the commands below and run them only after they confirm.

```bash
gh api --method PATCH repos/IlyaEsin/carenest --input - <<'EOF'
{"security_and_analysis":{"secret_scanning":{"status":"enabled"},"secret_scanning_push_protection":{"status":"enabled"}}}
EOF

gh api --method PATCH repos/IlyaEsin/carenest/code-scanning/default-setup --input - <<'EOF'
{"state":"configured","query_suite":"default","languages":["csharp","javascript-typescript","actions"]}
EOF

gh api --method POST repos/IlyaEsin/carenest/rulesets --input - <<'EOF'
{
  "name": "main",
  "target": "branch",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    { "type": "pull_request", "parameters": {
      "required_approving_review_count": 0, "dismiss_stale_reviews_on_push": true,
      "require_code_owner_review": false, "require_last_push_approval": false,
      "required_review_thread_resolution": false } },
    { "type": "required_status_checks", "parameters": {
      "strict_required_status_checks_policy": true,
      "required_status_checks": [
        { "context": "build-and-test" }, { "context": "checks" }, { "context": "smoke" },
        { "context": "secrets-scan" }, { "context": "forbidden-references" } ] } }
  ]
}
EOF
```

Verify:
- `gh api repos/IlyaEsin/carenest/rulesets --jq '.[].name'` prints `main`;
- `gh api repos/IlyaEsin/carenest --jq .security_and_analysis` shows both secret-scanning settings `enabled`;
- `gh api repos/IlyaEsin/carenest/code-scanning/default-setup --jq .state` prints `configured`;
- the PR page now lists the five required checks.

Then add back to `docs/tech-overview.md`, section 15, after the list:

```markdown
`main` защищён ruleset'ом `main`: изменения попадают туда только через pull request, обязательные проверки - `build-and-test`, `checks`, `smoke`, `secrets-scan`, `forbidden-references` (ветка PR должна быть актуальной относительно `main`); прямой push, force push и удаление ветки запрещены.
```

Then run: `git commit -am "docs: main is protected by a ruleset"` and `git push`.

- [ ] **Step 8: STOP (owner) - merge**

Ask the owner to review and merge the PR. After the merge, `deploy` shows as skipped, because `DEPLOY_ENABLED` is not set yet.

---

### Task 7: STOP (owner) - domain, Azure subscription, Brevo and bootstrap

Every step here is an owner action. The executor explains each one, waits for confirmation, and then checks the result with read-only commands.

**Interfaces:**
- Produces:
  - the domain `<domain>` (`bayubai.com`);
  - a subscription id;
  - Key Vault `kv-bayubai-<6 hex>` with `postgres-password`, `admin-email`, `email-username`, `email-password`;
  - budget `bb-monthly`;
  - the deploy identity and GitHub environment `production` with its variables.

- [ ] **Step 1: STOP (owner) - buy the domain**
  - Buy `bayubai.com` at any registrar that allows editing DNS records (CNAME and TXT for subdomains, TXT on the root).
  - Keep in mind the planned public landing page (owner idea, 2026-09-27): if it will carry a brand, the root of this domain may later host it, with `app.`, `studio.` and `api.` for the system.
  - DNS must be plain (no proxy such as Cloudflare's orange cloud) for `api.`, or Azure cannot issue the managed certificate.
  - The owner confirms the domain name.

- [ ] **Step 2: STOP (owner) - create the Azure subscription**
  - A personal pay-as-you-go subscription on a personal Microsoft account (not employer credits).
  - Install the Azure CLI and run `az login`.
  - The owner confirms, then the executor runs `az account list --query "[].{name:name, id:id, state:state}" --output table` and records the subscription id.

- [ ] **Step 3: STOP (owner) - Brevo**
  - Create a Brevo account (free plan).
  - Under Senders, Domains & Dedicated IPs, add and authenticate the domain: add the DNS records Brevo shows (Brevo code TXT, DKIM, DMARC) at the registrar.
  - Add sender `no-reply@<domain>` (shown to recipients as `Баюбай <no-reply@bayubai.com>`, already set as `Email:From` in `src/Bayubai.AppHost/AzureDeployment.cs`).
  - Under SMTP & API, generate an SMTP key and note the SMTP login Brevo shows.
  - Check the free-plan conditions for transactional emails on the Brevo pricing page (300 emails per day at the time of writing).
  - The owner confirms the domain shows as authenticated.

- [ ] **Step 4: STOP (owner) - run the bootstrap**

The owner runs it (it prompts for the admin email, the Brevo SMTP login and the SMTP key without echoing them):

```bash
! bash deploy/bootstrap.sh <subscription-id> <domain> <budget-email>
```

Expected: `Done. Key Vault: kv-bayubai-xxxxxx, resource group: rg-bayubai`. Then the executor verifies read-only:

```bash
az keyvault secret list --vault-name kv-bayubai-xxxxxx --query "[].name" --output tsv
az consumption budget list --resource-group rg-bayubai --query "[].{name:name, amount:amount}" --output table
gh variable list --env production --repo IlyaEsin/bayubai
```

Expected:
- secrets `admin-email`, `email-password`, `email-username`, `postgres-password`;
- budget `bb-monthly` 40;
- six variables (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`, `DEPLOY_DOMAIN`, `DEPLOY_KEY_VAULT`).

If a step of the script failed, fix the cause and re-run it; the script is idempotent.

---

### Task 8: First deploy, domains and email sign-in in production

**Interfaces:**
- Consumes: everything from Task 7.
- Produces: `https://app.<domain>`, `https://studio.<domain>` and `https://api.<domain>` live; variable `DEPLOY_API_CERTIFICATE`; `Deploy:CustomDomain = true` merged.

- [ ] **Step 1: STOP (owner) - enable and run the first deploy**

After confirmation:

```bash
gh variable set DEPLOY_ENABLED --repo IlyaEsin/bayubai --body true
gh workflow run deploy.yml --repo IlyaEsin/bayubai --ref main
gh run watch --repo IlyaEsin/bayubai $(gh run list --workflow deploy.yml --repo IlyaEsin/bayubai --limit 1 --json databaseId --jq '.[0].databaseId')
```

Expected: every step green; the migrations step prints `Migrations applied`.

This is the first real `aspire deploy`, so any failure here is new information. Use superpowers:systematic-debugging. Useful places to look:
- the step log;
- `az containerapp logs show --name api --resource-group rg-bayubai --tail 100`;
- `az containerapp job execution list --name migrations --resource-group rg-bayubai --output table`;
- the failed deployment in the resource group's Deployments blade.

Fix it on a branch through a PR, then re-run.

- [ ] **Step 2: Check the API on its Azure address**

```bash
fqdn=$(az containerapp show --name api --resource-group rg-bayubai --query properties.configuration.ingress.fqdn --output tsv)
curl -s -o /dev/null -w "%{http_code}\n" "https://$fqdn/alive"
curl -s "https://$fqdn/api/identity/providers"
curl -s -o /dev/null -w "%{http_code}\n" "https://$fqdn/scalar"
```

Expected:
- `200`;
- `{"providers":["Email"],"telegramBotName":null}`;
- `404` (Scalar is Development only).

- [ ] **Step 3: STOP (owner) - DNS for api, app and studio**

The executor prints the values:
- `az containerapp show --name api --resource-group rg-bayubai --query "{fqdn: properties.configuration.ingress.fqdn, verification: properties.customDomainVerificationId}"`
- `az staticwebapp show --name bb-client --resource-group rg-bayubai --query defaultHostname --output tsv`, and the same for `bb-studio`.

The owner creates at the registrar:
- `CNAME api` to the API fqdn;
- `TXT asuid.api` with the verification id;
- `CNAME app` to the `bb-client` hostname;
- `CNAME studio` to the `bb-studio` hostname.

After the owner confirms, check with `nslookup -type=CNAME api.<domain>` (and `app.`, `studio.`) and `nslookup -type=TXT asuid.api.<domain>`. DNS may take up to an hour.

- [ ] **Step 4: Bind the domains (after the owner confirms these write commands)**

Follow "API custom domain" in `deploy/README.md`, steps 3 and 4 (`hostname add`, `hostname bind`, find the certificate name, `gh variable set DEPLOY_API_CERTIFICATE`), then the two `az staticwebapp hostname set` commands.
Expected:
- `az containerapp hostname list --name api --resource-group rg-bayubai --output table` shows `api.<domain>` with binding `SniEnabled`;
- `az staticwebapp hostname list --name bb-client --resource-group rg-bayubai --output table` shows `app.<domain>` as `Ready` (certificate issuance can take several minutes).

- [ ] **Step 5: Keep the binding in the model**

On a branch `feature/api-custom-domain`, set `"CustomDomain": true` in `src/Bayubai.AppHost/appsettings.json`, then run `dotnet aspire publish --apphost src/Bayubai.AppHost/Bayubai.AppHost.csproj --output-path infra`.
Expected: `infra/api/api.bicep` gains `customDomains` with `bindingType: (api_certificate != '') ? 'SniEnabled' : 'Disabled'`.

Commit (`feat: keep the api custom domain binding on every deploy`), open a PR and ask the owner to merge it; merge nothing else in between.
After the deploy: `az containerapp hostname list ...` still shows `SniEnabled`, and `curl -s -o /dev/null -w "%{http_code}\n" https://api.<domain>/alive` returns `200`.

- [ ] **Step 6: STOP (owner) - email sign-in end to end**
  - The owner opens `https://studio.<domain>`, signs in by email with the admin address and receives the Brevo email.
  - Check that the email is not in spam and that the link completes sign-in in the same browser.
  - Then, in the admin screen, create the pilot consultant (her email is entered by the owner, never written into the repo).
  - Also test on a phone: `https://app.<domain>` signs in by email.

The executor checks the logs for errors: `az containerapp logs show --name api --resource-group rg-bayubai --tail 200`. Expected: no exceptions, and no emails or names in log lines (standing rule 4).

---

### Task 9: Sign-in providers in production (acceptance 2 and 3)

**Interfaces:**
- Consumes: `deploy/README.md` "Adding a sign-in provider"; Key Vault secret names from Task 3.
- Produces: `Deploy:Providers = ["Google", "Yandex", "VkId", "Telegram"]` merged; all five methods working.

- [ ] **Step 1: STOP (owner) - register the apps**

The redirect URIs are exactly those below. Put each value into Key Vault with `az keyvault secret set --vault-name <vault> --name <name> --value "<value>"` (the owner runs these; values never go into the chat).
- **Google** (https://console.cloud.google.com):
  - OAuth consent screen: External, scopes `openid`, `email`, `profile`, publishing status "In production".
  - OAuth client: type Web, authorized redirect URI `https://api.<domain>/api/identity/signin-google`.
  - Secrets `google-client-id`, `google-client-secret`.
- **Yandex ID** (https://oauth.yandex.ru):
  - Web app, redirect URI `https://api.<domain>/api/identity/signin-yandex`, access to email and name.
  - Secrets `yandex-client-id`, `yandex-client-secret`.
- **VK ID** (https://id.vk.com/about/business/go):
  - Web app, base domain `<domain>`, trusted redirect URL `https://api.<domain>/api/identity/signin-vkid`.
  - Secrets `vkid-client-id` (app ID), `vkid-client-secret` (protected key).
- **Telegram** (@BotFather):
  - `/newbot`, then `/setdomain` with `app.<domain>`.
  - Secrets `telegram-bot-token`, `telegram-bot-name` (the bot's username without `@`).

The owner confirms each provider as done. The executor checks with `az keyvault secret list --vault-name <vault> --query "[].name" --output tsv`.

- [ ] **Step 2: Turn them on**

On a branch `feature/production-providers`:
- set `"Providers": ["Google", "Yandex", "VkId", "Telegram"]` in `src/Bayubai.AppHost/appsettings.json`;
- regenerate `infra/`: `infra/api/api.bicep` gains eight Key Vault references and the matching `Identity__...` variables;
- commit (`feat: enable Google, Yandex ID, VK ID and Telegram sign-in in production`), open a PR, and the owner merges.

After the deploy, `curl -s https://api.<domain>/api/identity/providers` returns all five providers and the bot name.

- [ ] **Step 3: STOP (owner) - check every method (acceptance 2 and 3)**

On `https://app.<domain>`, with a fresh account per method:
- sign in with Google, Yandex ID, VK ID, Telegram and email;
- with one account, add a second method in the profile, sign out, and sign in with the other method: it must be the same account.

The Telegram widget is bound to one domain (`app.<domain>`). On `https://studio.<domain>`, check whether Telegram sign-in works. This was not verified before writing: Telegram documents one domain per bot.
- If the studio shows "Bot domain invalid", record it as a known limitation in the Notes (consultants use another method), or with the owner's agreement try `/setdomain <domain>` and re-check both apps.

Record the results (method, app, works yes/no) in the PR description of the final docs commit (Task 10).

---

### Task 10: Acceptance on devices, costs and closing notes

- [ ] **Step 1: STOP (owner) - PWA on real devices (acceptance 7)**

On an Android phone (Chrome) and an iPhone (Safari), open `https://app.<domain>`:
- install it (Android: "Install app"; iOS: Share, then "Add to Home Screen");
- open it from the home screen: it runs full screen with the Bayubai icon;
- switch the system to dark mode: the app follows;
- sign in inside the installed app with an OAuth provider.

The iOS email-link limitation from plan 2's notes (the installed app has its own cookie jar) is expected, not a failure. The owner reports the results.

- [ ] **Step 2: Costs and alerts (acceptance 10)**

Run `az consumption budget show --budget-name bb-monthly --resource-group rg-bayubai --query "{amount:amount, notifications:notifications}"`, and in the portal look at Cost Management, Cost analysis for `rg-bayubai` after a few days.
Expected: the budget exists with its three notifications, and the daily run rate is in line with about 31 USD per month. Tell the owner if it is not.

- [ ] **Step 3: Acceptance summary**

Walk through spec section 8 and write, for each criterion, how it was shown:
- 1: e2e, Task 6;
- 2 and 3: Task 9;
- 4, 5 and 8: plan 1 and 2 tests plus Task 8;
- 6: e2e;
- 7: Step 1;
- 9: the ruleset and the green required checks;
- 10: the deploy workflow run plus the budget.

- [ ] **Step 4: Close out the docs**

On a branch `docs/foundation-delivered`:
- Update `docs/tech-overview.md` where the live deploy taught something new; for example, a step in Task 8 needed a fix, or Telegram on the studio behaves differently. Keep each note to what was observed.
- Commit with the acceptance summary and the Task 9 results in the PR description; the owner merges.
- Update the project memory: Foundation delivered, next is sub-project 2.

---

## Notes for later sub-projects

- **Public landing page (owner idea, 2026-09-27).** Replace the consultant's Tilda site-card with a public page inside CareNest that is its facade: the call to action leads to sign-up, and the mother is linked to that consultant, instead of a Telegram chat.
  - Agreed direction: a page generated from platform data (the consultant's profile and the service catalog of sub-project 2), not a copy of one consultant's site, so every consultant gets one.
  - Texts, photos and prices are data in the consultant's account (loaded from `carenest-private`), never in this public repository.
  - Serve prerendered static HTML for search engines, for example as a third free Static Web App.
  - Its own small sub-project right after sub-project 2, starting with brainstorming.
- **Stale magic-link tokens** still accumulate until the background jobs of sub-project 5 add cleanup (plan 1 note).
- **iOS installed PWA and email links:** see plan 2's notes; a code-based email sign-in or the Telegram Mini App (sub-project 5) avoids it.
- **`cn_email_nonce` cookie and sibling subdomains** (plan 2 note): every subdomain of the domain must stay under our control. The Static Web Apps and Container Apps bindings are ours, and DNS is only at the owner's registrar.
- **Brevo processes parents' email addresses.** When the privacy policy is written (before public launch), list Brevo, Azure (EU) and the OAuth providers as processors.
- **152-FZ** stays open, as decided on 2026-09-25. The portability rule (no Azure SDK in the app, architecture test) keeps a move to a Russian host a redeploy.
- **Migrations after the API revision:** if a future change cannot be made backward compatible, split it into two releases rather than adding a pre-deploy migration step.
