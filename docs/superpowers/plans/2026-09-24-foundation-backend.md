# Foundation Backend Implementation Plan (Foundation plan 1 of 3)

> The project was renamed to Bayubai on 2026-09-30 (`docs/superpowers/specs/rename-to-bayubai.md`); names below are historical.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A running .NET 10 modular-monolith backend where a parent or consultant signs in (email magic link, Google, Yandex ID, VK ID, Telegram), manages a profile, a consultant invites a parent who becomes their client, and a user can delete their account, all enforced by integration and architecture tests.

**Architecture:** One ASP.NET Core Minimal API host (`CareNest.Api`) loads modules; this plan builds the first module, `CareNest.Identity`, on ASP.NET Core Identity with a password-less store. Each module owns a PostgreSQL schema and a DbContext; consultant-owned rows are scoped by an EF global query filter built in `CareNest.SharedKernel`. .NET Aspire runs PostgreSQL, Mailpit, a migration worker and the API locally.

**Tech Stack:** .NET 10 (SDK 10.0.204), Aspire 13.5.4, EF Core 10 + Npgsql + NodaTime, ASP.NET Core Identity, AspNet.Security.OAuth.Yandex / VkId 10.0.0, MailKit, xUnit v3, Shouldly, Testcontainers, NetArchTest.

**Spec:** `docs/superpowers/specs/foundation-design.md`

**Scope of this plan.** The spec is delivered in three plans, each producing working, tested software:

1. **Backend (this plan):** solution, SharedKernel, Identity module, API host, Aspire AppHost, migrations, backend CI, `CLAUDE.md`.
2. **Web apps:** `web/` pnpm workspace, `client` and `studio` apps, i18n, PWA, orval API client, Playwright e2e with a fake OAuth provider, contract checks (i18n parity, generated client drift).
3. **Delivery:** Azure deploy with `azd`, Key Vault, domain and cookie layout, hygiene CI (forbidden-reference guard, gitleaks, CodeQL, Dependabot), `build-test` and `how-to-test` skills, `REVIEW.md`.

Plans 2 and 3 are written after this one lands, against the real OpenAPI document.

Acceptance criteria covered here (spec section 8): 3, 4 and 5 at API level, 8 fully, 1 for the backend part. Criteria 2, 6, 7, 9, 10 belong to plans 2 and 3.

## Global Constraints

- .NET SDK pinned to `10.0.204` in `global.json` (`rollForward: latestFeature`); all projects target `net10.0`, `Nullable` and `TreatWarningsAsErrors` on.
- Package versions (exact): Aspire.* `13.5.4`, CommunityToolkit.Aspire.Hosting.MailPit `13.5.0`, Microsoft.AspNetCore.* / Microsoft.EntityFrameworkCore* `10.0.12`, Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime `10.0.3`, NodaTime `3.3.4`, NodaTime.Serialization.SystemTextJson `1.4.0`, NodaTime.Testing `3.3.4`, AspNet.Security.OAuth.Yandex `10.0.0`, AspNet.Security.OAuth.VkId `10.0.0`, MailKit `4.18.0`, xunit.v3 `3.2.2`, xunit.runner.visualstudio `3.1.5`, Microsoft.NET.Test.Sdk `17.14.1`, Shouldly `4.3.0`, Testcontainers.PostgreSql `4.15.0`, NetArchTest.Rules `1.3.2`, dotnet-ef tool `10.0.12`.
- Errors are ProblemDetails (RFC 9457) with a stable `code` extension (e.g. `identity.invite_expired`); the API never returns human-readable text.
- Time: NodaTime only. `DateTime` / `DateTimeOffset` are not used in SharedKernel or module code (EF migrations excepted, they are generated).
- Every consultant-owned entity implements `IConsultantOwned` and is filtered by `ApplyConsultantQueryFilters`.
- Session cookie: HttpOnly, Secure, SameSite=Lax; no tokens in JavaScript.
- Magic link: valid 15 minutes, single use. Invitation: default validity 14 days, single use.
- Accounts are never merged by matching email; a method is linked only by a signed-in user.
- No personal data about parents or children in logs or traces; log ids only.
- Migrations never run at API startup.
- Text in code, docs and commits uses the plain hyphen `-`, never em or en dashes.
- **Spec deviation (recorded in Task 1):** request validation uses DataAnnotations run by a shared endpoint filter, not the built-in .NET 10 Minimal API validation. Verified on 2026-09-24: `AddValidation()` silently skips request types declared in another assembly (every module), and the opt-in `[ValidatableType]` is marked experimental (ASP0029) and still did not validate in a probe.

## Review Focus

1. **Look-alike return and callback URLs** (`https://app.example.test.evil.com`, `https://app.example.test@evil.com`, other scheme or port, relative paths): must be rejected, never redirected to. Pinned in Task 4 (ReturnUrlPolicy tests) and Task 6 (callback URL test).
2. **The same email typed with different case or spaces** (`" Anna@Example.Test "` vs `anna@example.test`): must reach one account, not create two. Pinned in Task 6.
3. **A session that outlives its account** (second device still holding a cookie after deletion): must get 401, not act as a ghost user. Pinned in Task 10.
4. **A parent or a plain consultant calling consultant or admin endpoints**: must get 403. Pinned in Task 9.
5. **An invitation reused, accepted by its own consultant, or accepted twice by the same parent**: must return `identity.invite_used` / `identity.invite_own`, and never produce a duplicate client link. Pinned in Task 9.

---

## File map

```
global.json, Directory.Build.props, .editorconfig, .gitignore, CareNest.slnx, CLAUDE.md
.config/dotnet-tools.json
.github/workflows/backend.yml
src/CareNest.SharedKernel/
  Errors/ApiError.cs, ApiProblems.cs, CommonErrors.cs
  Validation/ValidationFilter.cs, ValidationExtensions.cs, IanaTimeZoneAttribute.cs
  Localization/Languages.cs
  Time/TimeZones.cs
  Web/FrontendOptions.cs
  Consultants/IConsultantOwned.cs, ICurrentConsultant.cs, IConsultantScopedDbContext.cs, ConsultantQueryFilters.cs
src/CareNest.ServiceDefaults/        (Aspire template)
src/CareNest.AppHost/                (Aspire template + AppHost.cs)
src/CareNest.MigrationService/       Program.cs, MigrationWorker.cs
src/CareNest.Api/                    Program.cs, NodaTimeSchemaTransformer.cs, appsettings*.json
src/Modules/CareNest.Identity/
  IdentityModule.cs, IdentityRoles.cs                      (public)
  IdentityErrors.cs, IdentityPolicies.cs, IdentityModuleOptions.cs
  Domain/User.cs, MagicLinkToken.cs, Invitation.cs, ClientLink.cs
  Persistence/IdentityModuleDbContext.cs, DesignTimeDbContextFactory.cs, NoCurrentConsultant.cs, RoleSeed.cs, Migrations/
  Security/SecureTokens.cs, ReturnUrlPolicy.cs, ErrorRedirect.cs, IdentityResultExtensions.cs, ClaimsPrincipalExtensions.cs, HttpCurrentConsultant.cs
  Telegram/TelegramLoginVerifier.cs
  Accounts/EmailLogin.cs, DisplayNames.cs, SignInModes.cs, AccountService.cs, AccountDeletionService.cs
  Email/EmailMessage.cs, IEmailSender.cs, EmailOptions.cs, SmtpEmailSender.cs, MagicLinkEmail.cs
  External/ExternalProviders.cs
  Endpoints/Contracts.cs, EmailSignInEndpoints.cs, ExternalSignInEndpoints.cs, ProfileEndpoints.cs, AdminEndpoints.cs, InvitationEndpoints.cs
tests/Directory.Build.props
tests/CareNest.SharedKernel.Tests/
tests/CareNest.Identity.Tests/
tests/CareNest.Api.IntegrationTests/ Infrastructure/{ApiFactory,FakeEmailSender,TestHttp,SignInExtensions,TelegramPayload}.cs + test classes
tests/CareNest.ArchitectureTests/
```

---

### Task 1: Repository scaffolding and SharedKernel

**Files:**
- Create: `global.json`, `Directory.Build.props`, `.editorconfig`, `.gitignore`, `CareNest.slnx`, `.config/dotnet-tools.json`, `CLAUDE.md`
- Create: `src/CareNest.SharedKernel/**` (all files listed in the file map)
- Create: `tests/Directory.Build.props`, `tests/CareNest.SharedKernel.Tests/**`
- Modify: `docs/superpowers/specs/foundation-design.md` (status line, section 3 layout, section 4 validation line)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `record ApiError(string Code, int Status)`; `ApiProblems.CodeKey = "code"`; `ProblemHttpResult ApiError.ToProblem()`; `CommonErrors.ValidationFailed = "validation_failed"`.
  - `RouteHandlerBuilder WithRequestValidation<TRequest>()`; `ValidationFilter<TRequest>.InvalidValue = "invalid"`; `[IanaTimeZone]`.
  - `Languages.Russian = "ru"`, `Languages.English = "en"`, `Languages.Default = "en"`, `Languages.All`, `bool IsSupported(string?)`, `string OrDefault(string?)`.
  - `TimeZones.Default = "UTC"`, `bool IsValid(string?)`, `string OrDefault(string?)`.
  - `FrontendOptions { Section = "Frontend"; string[] Origins; string ClientAppUrl }`.
  - `IConsultantOwned { Guid ConsultantId }`, `ICurrentConsultant { Guid? ConsultantId }`, `IConsultantScopedDbContext { Guid? CurrentConsultantId }`, `ModelBuilder.ApplyConsultantQueryFilters<TContext>(TContext context)`.

- [ ] **Step 1: Create root files**

`global.json`:
```json
{
  "sdk": {
    "version": "10.0.204",
    "rollForward": "latestFeature"
  }
}
```

`Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

`.editorconfig`:
```ini
root = true

[*]
charset = utf-8
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 4

[*.{json,yml,yaml,xml,csproj,props,slnx,md}]
indent_size = 2
```

Run:
```bash
dotnet new gitignore
dotnet new sln -n CareNest
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.12
dotnet new install Aspire.ProjectTemplates::13.5.4
```
Expected: `CareNest.slnx` (the .NET 10 default format), `.gitignore`, `.config/dotnet-tools.json` exist.

- [ ] **Step 2: Write `CLAUDE.md`**

```markdown
# CareNest

Platform that automates an independent consultant's work with parents; the first domain is infant and toddler sleep. Specs: `docs/superpowers/specs`. Plans: `docs/superpowers/plans`.

## Stack

- .NET 10, ASP.NET Core Minimal APIs, EF Core + PostgreSQL (Npgsql), NodaTime, ASP.NET Core Identity (password-less), .NET Aspire 13
- Tests: xUnit v3, Shouldly, Testcontainers (PostgreSQL), NetArchTest
- Frontend (from Foundation plan 2): Vite, React, TypeScript, pnpm workspace in `web/`

## Commands

- Build: `dotnet build CareNest.slnx`
- All tests (Docker must be running): `dotnet test CareNest.slnx`
- One project: `dotnet test tests/CareNest.Identity.Tests`
- Run locally (PostgreSQL, Mailpit, migrations, API, Aspire dashboard): `dotnet run --project src/CareNest.AppHost`
- New Identity migration: `dotnet ef migrations add <Name> --project src/Modules/CareNest.Identity --output-dir Persistence/Migrations --namespace CareNest.Identity.Persistence.Migrations`

## Layout and module boundaries

- `src/CareNest.Api` is the host only: startup, middleware, module registration. No business logic.
- `src/CareNest.SharedKernel` holds primitives every module may use; it depends on no module.
- `src/Modules/CareNest.<Module>` is one project per module. Only types in the module's root namespace are public (the `<Module>Module` entry point and contracts); everything else is `internal`. Modules never reference each other; a cross-module call goes through a public interface in the callee's root namespace. Architecture tests enforce this.
- Each module owns one PostgreSQL schema and one DbContext with its own migrations.
- Migrations never run at API startup: `CareNest.MigrationService` applies them locally, a deploy step applies them in production.

## Standing rules (checked in review)

1. Consultant-owned data: every row a consultant owns implements `IConsultantOwned`, and the module DbContext calls `ApplyConsultantQueryFilters`. Any `IgnoreQueryFilters()` needs a comment saying why the query is safe.
2. Time: NodaTime only (`Instant`, `LocalDateTime` plus an IANA zone id, injected `IClock`). No `DateTime` or `DateTimeOffset` in domain or persistence code.
3. No UI text outside the i18n dictionaries. The API returns stable error codes (`code` in ProblemDetails, e.g. `identity.invite_expired`), never human-readable text.
4. No personal data about parents or children in logs or traces. Log ids only.
5. Account deletion removes all personal data. Every new table holding personal data must be covered by account deletion and by its test.

## Conventions

- Errors: `ApiError(code, status)` plus `.ToProblem()`. Validation: DataAnnotations on the request type plus `.WithRequestValidation<T>()` on the endpoint (the built-in .NET 10 validation does not see types declared in module assemblies).
- Tests: HTTP behaviour in `CareNest.Api.IntegrationTests` (real PostgreSQL via Testcontainers, fake clock, fake email sender); pure logic in the module's unit test project.
- Tracking: GitHub Issues. Branches `feature/cn-<issue>-<slug>`; spec and plan files `cn-<issue>-<slug>.md` (no issue: `<slug>.md`).
- Writing: plain hyphen `-`, never em or en dashes. Code comments: one dry sentence, why not what.
- Secrets never enter the repo. Local: `dotnet user-secrets`. Production: Key Vault.
```

- [ ] **Step 3: Update the spec**

In `docs/superpowers/specs/foundation-design.md`:
- Replace `Status: draft for review` with `Status: approved`.
- In the section 3 layout block, add under `src/`: `│  ├─ CareNest.MigrationService/   applies module migrations (local run and deploy step)`; add under `tests/`: `│  ├─ CareNest.SharedKernel.Tests/`.
- Replace the line `- **Validation** uses the built-in .NET 10 Minimal API validation.` with `- **Validation** uses DataAnnotations on request types, run by a shared endpoint filter. The built-in .NET 10 Minimal API validation skips request types declared in module assemblies (verified 2026-09-24), so it is not used.`

- [ ] **Step 4: Create the SharedKernel project**

Run:
```bash
dotnet new classlib -n CareNest.SharedKernel -o src/CareNest.SharedKernel
rm src/CareNest.SharedKernel/Class1.cs
dotnet sln add src/CareNest.SharedKernel
```

Replace `src/CareNest.SharedKernel/CareNest.SharedKernel.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.12" />
    <PackageReference Include="NodaTime" Version="3.3.4" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Create the shared test props and the SharedKernel test project**

`tests/Directory.Build.props`:
```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <!-- xUnit1051 would force a CancellationToken argument on every awaited call in tests. -->
    <NoWarn>$(NoWarn);xUnit1051</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit.v3" Version="3.2.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageReference Include="Shouldly" Version="4.3.0" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="Shouldly" />
  </ItemGroup>
</Project>
```

`tests/CareNest.SharedKernel.Tests/CareNest.SharedKernel.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\src\CareNest.SharedKernel\CareNest.SharedKernel.csproj" />
  </ItemGroup>

</Project>
```

Run: `dotnet sln add tests/CareNest.SharedKernel.Tests`

- [ ] **Step 6: Write the failing tests**

`tests/CareNest.SharedKernel.Tests/LanguagesAndTimeZonesTests.cs`:
```csharp
using CareNest.SharedKernel.Localization;
using CareNest.SharedKernel.Time;
using CareNest.SharedKernel.Validation;

namespace CareNest.SharedKernel.Tests;

public class LanguagesAndTimeZonesTests
{
    [Theory]
    [InlineData("ru", true)]
    [InlineData("en", true)]
    [InlineData("RU", false)]
    [InlineData("de", false)]
    [InlineData(null, false)]
    public void Language_support(string? value, bool expected) => Languages.IsSupported(value).ShouldBe(expected);

    [Fact]
    public void Unsupported_language_falls_back_to_default() => Languages.OrDefault("de").ShouldBe(Languages.English);

    [Theory]
    [InlineData("Europe/Moscow", true)]
    [InlineData("UTC", true)]
    [InlineData("Mars/Olympus", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Time_zone_validity(string? value, bool expected) => TimeZones.IsValid(value).ShouldBe(expected);

    [Fact]
    public void Invalid_time_zone_falls_back_to_utc() => TimeZones.OrDefault("Mars/Olympus").ShouldBe("UTC");

    [Fact]
    public void Iana_attribute_accepts_null_and_known_zones_only()
    {
        var attribute = new IanaTimeZoneAttribute();
        attribute.IsValid(null).ShouldBeTrue();
        attribute.IsValid("Asia/Yekaterinburg").ShouldBeTrue();
        attribute.IsValid("Moscow").ShouldBeFalse();
        attribute.IsValid(42).ShouldBeFalse();
    }
}
```

`tests/CareNest.SharedKernel.Tests/ErrorsAndValidationTests.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using CareNest.SharedKernel.Errors;
using CareNest.SharedKernel.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CareNest.SharedKernel.Tests;

public class ErrorsAndValidationTests
{
    public sealed record SampleRequest
    {
        [Required, StringLength(3)]
        public string? DisplayName { get; init; }
    }

    [Fact]
    public void Api_error_becomes_problem_with_status_and_code()
    {
        var problem = new ApiError("identity.invite_used", 409).ToProblem();

        problem.StatusCode.ShouldBe(409);
        problem.ProblemDetails.Extensions[ApiProblems.CodeKey].ShouldBe("identity.invite_used");
    }

    [Fact]
    public async Task Invalid_request_short_circuits_with_coded_validation_problem()
    {
        var filter = new ValidationFilter<SampleRequest>();
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), new SampleRequest { DisplayName = "too long" });
        var nextCalled = false;

        var result = await filter.InvokeAsync(context, _ => { nextCalled = true; return ValueTask.FromResult<object?>(null); });

        nextCalled.ShouldBeFalse();
        var problem = result.ShouldBeOfType<ValidationProblem>();
        problem.ProblemDetails.Errors.Keys.ShouldBe(new[] { "displayName" });
        problem.ProblemDetails.Errors["displayName"].ShouldBe(new[] { ValidationFilter<SampleRequest>.InvalidValue });
        problem.ProblemDetails.Extensions[ApiProblems.CodeKey].ShouldBe(CommonErrors.ValidationFailed);
    }

    [Fact]
    public async Task Valid_request_reaches_the_handler()
    {
        var filter = new ValidationFilter<SampleRequest>();
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), new SampleRequest { DisplayName = "Ann" });

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("handled"));

        result.ShouldBe("handled");
    }
}
```

- [ ] **Step 7: Run the tests to verify they fail**

Run: `dotnet test tests/CareNest.SharedKernel.Tests`
Expected: build FAILS with `CS0246` for `Languages`, `TimeZones`, `IanaTimeZoneAttribute`, `ApiError`, `ValidationFilter<>`.

- [ ] **Step 8: Implement SharedKernel**

`src/CareNest.SharedKernel/Errors/ApiError.cs`:
```csharp
namespace CareNest.SharedKernel.Errors;

// Code is the contract clients translate; the API never sends human-readable text.
public sealed record ApiError(string Code, int Status);
```

`src/CareNest.SharedKernel/Errors/ApiProblems.cs`:
```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CareNest.SharedKernel.Errors;

public static class ApiProblems
{
    public const string CodeKey = "code";

    public static ProblemHttpResult ToProblem(this ApiError error) =>
        TypedResults.Problem(
            statusCode: error.Status,
            extensions: new Dictionary<string, object?> { [CodeKey] = error.Code });
}
```

`src/CareNest.SharedKernel/Errors/CommonErrors.cs`:
```csharp
namespace CareNest.SharedKernel.Errors;

public static class CommonErrors
{
    public const string ValidationFailed = "validation_failed";
}
```

`src/CareNest.SharedKernel/Validation/ValidationFilter.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CareNest.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;

namespace CareNest.SharedKernel.Validation;

public sealed class ValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    // Field-level values stay codes too, so clients translate them like any other error.
    public const string InvalidValue = "invalid";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            return await next(context);
        }

        var errors = results
            .SelectMany(result => result.MemberNames)
            .Select(member => JsonNamingPolicy.CamelCase.ConvertName(member))
            .Distinct()
            .ToDictionary(member => member, _ => new[] { InvalidValue });

        return TypedResults.ValidationProblem(
            errors,
            extensions: new Dictionary<string, object?> { [ApiProblems.CodeKey] = CommonErrors.ValidationFailed });
    }
}
```

`src/CareNest.SharedKernel/Validation/ValidationExtensions.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CareNest.SharedKernel.Validation;

public static class ValidationExtensions
{
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder) where TRequest : class =>
        builder.AddEndpointFilter<ValidationFilter<TRequest>>().ProducesValidationProblem();
}
```

`src/CareNest.SharedKernel/Validation/IanaTimeZoneAttribute.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using CareNest.SharedKernel.Time;

namespace CareNest.SharedKernel.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class IanaTimeZoneAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || (value is string id && TimeZones.IsValid(id));
}
```

`src/CareNest.SharedKernel/Localization/Languages.cs`:
```csharp
using System.Diagnostics.CodeAnalysis;

namespace CareNest.SharedKernel.Localization;

public static class Languages
{
    public const string Russian = "ru";
    public const string English = "en";
    public const string Default = English;

    public static IReadOnlyList<string> All { get; } = [Russian, English];

    public static bool IsSupported([NotNullWhen(true)] string? value) => value is not null && All.Contains(value);

    public static string OrDefault(string? value) => IsSupported(value) ? value : Default;
}
```

`src/CareNest.SharedKernel/Time/TimeZones.cs`:
```csharp
using System.Diagnostics.CodeAnalysis;
using NodaTime;

namespace CareNest.SharedKernel.Time;

public static class TimeZones
{
    public const string Default = "UTC";

    public static bool IsValid([NotNullWhen(true)] string? id) =>
        !string.IsNullOrEmpty(id) && DateTimeZoneProviders.Tzdb.GetZoneOrNull(id) is not null;

    public static string OrDefault(string? id) => IsValid(id) ? id : Default;
}
```

`src/CareNest.SharedKernel/Web/FrontendOptions.cs`:
```csharp
namespace CareNest.SharedKernel.Web;

public sealed class FrontendOptions
{
    public const string Section = "Frontend";

    // Browser origins of the web apps; used for CORS and for validating return and callback URLs.
    public string[] Origins { get; set; } = [];

    public string ClientAppUrl { get; set; } = "";
}
```

`src/CareNest.SharedKernel/Consultants/IConsultantOwned.cs`:
```csharp
namespace CareNest.SharedKernel.Consultants;

public interface IConsultantOwned
{
    Guid ConsultantId { get; }
}
```

`src/CareNest.SharedKernel/Consultants/ICurrentConsultant.cs`:
```csharp
namespace CareNest.SharedKernel.Consultants;

public interface ICurrentConsultant
{
    // Null when the caller is not a consultant; consultant-owned queries then return nothing.
    Guid? ConsultantId { get; }
}
```

`src/CareNest.SharedKernel/Consultants/IConsultantScopedDbContext.cs`:
```csharp
namespace CareNest.SharedKernel.Consultants;

public interface IConsultantScopedDbContext
{
    Guid? CurrentConsultantId { get; }
}
```

`src/CareNest.SharedKernel/Consultants/ConsultantQueryFilters.cs`:
```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace CareNest.SharedKernel.Consultants;

public static class ConsultantQueryFilters
{
    // Referencing the context instance makes EF read CurrentConsultantId per query instead of baking it into the cached model.
    public static ModelBuilder ApplyConsultantQueryFilters<TContext>(this ModelBuilder modelBuilder, TContext context)
        where TContext : DbContext, IConsultantScopedDbContext
    {
        var currentConsultantId = Expression.Property(
            Expression.Constant(context),
            nameof(IConsultantScopedDbContext.CurrentConsultantId));

        var ownedTypes = modelBuilder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(type => typeof(IConsultantOwned).IsAssignableFrom(type))
            .ToList();

        foreach (var type in ownedTypes)
        {
            var entity = Expression.Parameter(type, "entity");
            var ownerId = Expression.Convert(
                Expression.Property(entity, nameof(IConsultantOwned.ConsultantId)),
                typeof(Guid?));
            modelBuilder.Entity(type).HasQueryFilter(Expression.Lambda(Expression.Equal(ownerId, currentConsultantId), entity));
        }

        return modelBuilder;
    }
}
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/CareNest.SharedKernel.Tests`
Expected: PASS, 16 tests.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution, CLAUDE.md and SharedKernel"
```

---

### Task 2: API host, service defaults and Aspire AppHost

**Files:**
- Create: `src/CareNest.ServiceDefaults/**` (template), `src/CareNest.AppHost/**` (template + `AppHost.cs`)
- Create: `src/CareNest.Api/CareNest.Api.csproj`, `Program.cs`, `NodaTimeSchemaTransformer.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` (template)
- Create: `tests/CareNest.Api.IntegrationTests/CareNest.Api.IntegrationTests.csproj`, `Infrastructure/ApiFactory.cs`, `Infrastructure/TestHttp.cs`, `HostTests.cs`

**Interfaces:**
- Consumes: `FrontendOptions` (Task 1).
- Produces:
  - `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` registered as an xUnit assembly fixture; constants `ClientAppUrl = "https://app.example.test"`, `AdminEmail = "admin@example.test"`, `TelegramBotToken = "123456:TEST-TOKEN"`; properties `FakeClock Clock`, `string ConnectionString`; method `HttpClient CreateHttpsClient()`.
  - `TestHttp.Json` (`JsonSerializerOptions` with NodaTime), `Task<T> ReadAsAsync<T>(this HttpResponseMessage)`, `Task ShouldBeProblemAsync(this HttpResponseMessage, HttpStatusCode, string code)`.
  - API: `GET /openapi/v1.json`; CORS for `Frontend:Origins` with credentials.

- [ ] **Step 1: Generate ServiceDefaults, AppHost and the API project**

Run:
```bash
dotnet new aspire-servicedefaults -n CareNest.ServiceDefaults -o src/CareNest.ServiceDefaults
dotnet new aspire-apphost -n CareNest.AppHost -o src/CareNest.AppHost
dotnet new web -n CareNest.Api -o src/CareNest.Api
dotnet sln add src/CareNest.ServiceDefaults src/CareNest.AppHost src/CareNest.Api
dotnet add src/CareNest.AppHost package Aspire.Hosting.PostgreSQL --version 13.5.4
dotnet add src/CareNest.AppHost package CommunityToolkit.Aspire.Hosting.MailPit --version 13.5.0
dotnet add src/CareNest.AppHost reference src/CareNest.Api
```
Keep the generated ServiceDefaults `Extensions.cs` unchanged.

- [ ] **Step 2: Write the API project file and settings**

Replace `src/CareNest.Api/CareNest.Api.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <UserSecretsId>carenest-api</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
    <PackageReference Include="NodaTime.Serialization.SystemTextJson" Version="1.4.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CareNest.ServiceDefaults\CareNest.ServiceDefaults.csproj" />
    <ProjectReference Include="..\CareNest.SharedKernel\CareNest.SharedKernel.csproj" />
  </ItemGroup>

</Project>
```

Replace `src/CareNest.Api/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Frontend": {
    "Origins": [],
    "ClientAppUrl": ""
  }
}
```

Replace `src/CareNest.Api/appsettings.Development.json`:
```json
{
  "Frontend": {
    "Origins": [ "http://localhost:5173", "http://localhost:5174" ],
    "ClientAppUrl": "http://localhost:5173"
  }
}
```

- [ ] **Step 3: Create the integration test project and fixture**

`tests/CareNest.Api.IntegrationTests/CareNest.Api.IntegrationTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.15.0" />
    <PackageReference Include="NodaTime.Testing" Version="3.3.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CareNest.Api\CareNest.Api.csproj" />
  </ItemGroup>

</Project>
```

Run: `dotnet sln add tests/CareNest.Api.IntegrationTests`

`tests/CareNest.Api.IntegrationTests/Infrastructure/ApiFactory.cs`:
```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Testing;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CareNest.Api.IntegrationTests.Infrastructure.ApiFactory))]
// Tests share one database and one fake clock, and some tests move the clock.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CareNest.Api.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ClientAppUrl = "https://app.example.test";
    public const string AdminEmail = "admin@example.test";
    public const string TelegramBotToken = "123456:TEST-TOKEN";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeClock Clock { get; } = new(Instant.FromUtc(2026, 1, 5, 9, 0));

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:carenest", ConnectionString);
        builder.UseSetting("Frontend:Origins:0", ClientAppUrl);
        builder.UseSetting("Frontend:ClientAppUrl", ClientAppUrl);
        builder.UseSetting("Identity:AdminEmails:0", AdminEmail);
        builder.UseSetting("Identity:TelegramBotToken", TelegramBotToken);
        builder.UseSetting("Identity:TelegramBotName", "carenest_test_bot");
        builder.UseSetting("Identity:Providers:Google:ClientId", "test-google-client");
        builder.UseSetting("Identity:Providers:Google:ClientSecret", "test-google-secret");
        builder.ConfigureTestServices(services => services.AddSingleton<IClock>(Clock));
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
```

`tests/CareNest.Api.IntegrationTests/Infrastructure/TestHttp.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace CareNest.Api.IntegrationTests.Infrastructure;

internal static class TestHttp
{
    public static JsonSerializerOptions Json { get; } =
        new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("code").GetString().ShouldBe(code);
    }
}
```

- [ ] **Step 4: Write the failing host tests**

`tests/CareNest.Api.IntegrationTests/HostTests.cs`:
```csharp
using System.Net;
using CareNest.Api.IntegrationTests.Infrastructure;

namespace CareNest.Api.IntegrationTests;

public class HostTests(ApiFactory factory)
{
    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var response = await factory.CreateHttpsClient().GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"openapi\"");
    }

    [Fact]
    public async Task Cors_allows_frontend_origin_with_credentials()
    {
        var response = await SendPreflightAsync(ApiFactory.ClientAppUrl);

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(new[] { ApiFactory.ClientAppUrl });
        response.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(new[] { "true" });
    }

    [Fact]
    public async Task Cors_ignores_unknown_origin()
    {
        var response = await SendPreflightAsync("https://evil.example");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private Task<HttpResponseMessage> SendPreflightAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/openapi/v1.json");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return factory.CreateHttpsClient().SendAsync(request);
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test tests/CareNest.Api.IntegrationTests`
Expected: FAIL. `OpenApi_document_is_served` gets 404 and the CORS tests find no `Access-Control-Allow-Origin` header (the template `Program.cs` only maps `/`).

- [ ] **Step 6: Implement the host**

Replace `src/CareNest.Api/Program.cs`:
```csharp
using CareNest.Api;
using CareNest.SharedKernel.Web;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddSchemaTransformer(NodaTimeSchemaTransformer.TransformAsync));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddCors();

var app = builder.Build();

var frontend = app.Configuration.GetSection(FrontendOptions.Section).Get<FrontendOptions>() ?? new FrontendOptions();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(policy => policy.WithOrigins(frontend.Origins).AllowCredentials().AllowAnyHeader().AllowAnyMethod());

app.MapOpenApi();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
```

`src/CareNest.Api/NodaTimeSchemaTransformer.cs`:
```csharp
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NodaTime;

namespace CareNest.Api;

// NodaTime serializes Instant as an ISO-8601 string; without this the document shows it as an object.
internal static class NodaTimeSchemaTransformer
{
    public static Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(Instant) || type == typeof(Instant?))
        {
            schema.Type = type == typeof(Instant) ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
            schema.Format = "date-time";
            schema.Properties?.Clear();
        }

        return Task.CompletedTask;
    }
}
```

Replace `src/CareNest.AppHost/AppHost.cs`:
```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("carenest");
var email = builder.AddMailPit("email");

builder.AddProject<Projects.CareNest_Api>("api")
    .WithReference(database)
    .WithReference(email)
    .WaitFor(database);

builder.Build().Run();
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet build CareNest.slnx` then `dotnet test tests/CareNest.Api.IntegrationTests`
Expected: build succeeds with 0 warnings; 3 tests PASS (Docker must be running).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: add API host, service defaults and Aspire AppHost"
```

---

### Task 3: Identity persistence, migrations and consultant isolation

**Files:**
- Create: `src/Modules/CareNest.Identity/CareNest.Identity.csproj`, `IdentityModule.cs`, `IdentityRoles.cs`
- Create: `src/Modules/CareNest.Identity/Domain/{User,MagicLinkToken,Invitation,ClientLink}.cs`
- Create: `src/Modules/CareNest.Identity/Persistence/{IdentityModuleDbContext,DesignTimeDbContextFactory,NoCurrentConsultant,RoleSeed}.cs`, `Persistence/Migrations/*` (generated)
- Create: `src/CareNest.MigrationService/**`
- Modify: `src/CareNest.Api/CareNest.Api.csproj`, `src/CareNest.Api/Program.cs`, `src/CareNest.AppHost/AppHost.cs`, `tests/CareNest.Api.IntegrationTests/Infrastructure/ApiFactory.cs`
- Create: `tests/CareNest.Identity.Tests/**`, `tests/CareNest.Api.IntegrationTests/PersistenceTests.cs`

**Interfaces:**
- Consumes: `IConsultantOwned`, `ICurrentConsultant`, `IConsultantScopedDbContext`, `ApplyConsultantQueryFilters` (Task 1).
- Produces:
  - Public: `IdentityRoles.Parent = "parent"`, `Consultant = "consultant"`, `Admin = "admin"`; `IdentityModule.ConnectionStringName = "carenest"`; `IHostApplicationBuilder AddIdentityPersistence()`; `Task MigrateIdentityDatabaseAsync(this IServiceProvider, CancellationToken)`.
  - Internal: `User : IdentityUser<Guid>` with `DisplayName`, `Language`, `TimeZone`, `Instant CreatedAt`; `MagicLinkToken`, `Invitation : IConsultantOwned`, `ClientLink : IConsultantOwned` (fields below); `IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext>, ICurrentConsultant)` with `Schema = "identity"`, `DbSet`s `MagicLinkTokens`, `Invitations`, `ClientLinks`, `static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder)`; `NoCurrentConsultant`.
  - `ApiFactory` now migrates the database in `InitializeAsync`.

- [ ] **Step 1: Create the Identity project**

Run:
```bash
dotnet new classlib -n CareNest.Identity -o src/Modules/CareNest.Identity
rm src/Modules/CareNest.Identity/Class1.cs
dotnet sln add src/Modules/CareNest.Identity
```

Replace `src/Modules/CareNest.Identity/CareNest.Identity.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="13.5.4" />
    <PackageReference Include="AspNet.Security.OAuth.VkId" Version="10.0.0" />
    <PackageReference Include="AspNet.Security.OAuth.Yandex" Version="10.0.0" />
    <PackageReference Include="MailKit" Version="4.18.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="10.0.12" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.12" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime" Version="10.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\CareNest.SharedKernel\CareNest.SharedKernel.csproj" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="CareNest.Identity.Tests" />
    <InternalsVisibleTo Include="CareNest.Api.IntegrationTests" />
    <InternalsVisibleTo Include="CareNest.ArchitectureTests" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the Identity unit test project with the failing model test**

`tests/CareNest.Identity.Tests/CareNest.Identity.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\src\Modules\CareNest.Identity\CareNest.Identity.csproj" />
  </ItemGroup>

</Project>
```

Run: `dotnet sln add tests/CareNest.Identity.Tests`

`tests/CareNest.Identity.Tests/ConsultantQueryFilterModelTests.cs`:
```csharp
using CareNest.Identity.Persistence;
using CareNest.SharedKernel.Consultants;
using Microsoft.EntityFrameworkCore;

namespace CareNest.Identity.Tests;

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
```

- [ ] **Step 3: Run it to verify it fails**

Run: `dotnet test tests/CareNest.Identity.Tests`
Expected: build FAILS with `CS0246` for `IdentityModuleDbContext` and `NoCurrentConsultant`.

- [ ] **Step 4: Implement the domain and the DbContext**

`src/Modules/CareNest.Identity/IdentityRoles.cs`:
```csharp
namespace CareNest.Identity;

public static class IdentityRoles
{
    public const string Parent = "parent";
    public const string Consultant = "consultant";
    public const string Admin = "admin";
}
```

`src/Modules/CareNest.Identity/Domain/User.cs`:
```csharp
using Microsoft.AspNetCore.Identity;
using NodaTime;

namespace CareNest.Identity.Domain;

internal sealed class User : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    public required string Language { get; set; }

    public required string TimeZone { get; set; }

    public Instant CreatedAt { get; set; }
}
```

`src/Modules/CareNest.Identity/Domain/MagicLinkToken.cs`:
```csharp
using NodaTime;

namespace CareNest.Identity.Domain;

internal sealed class MagicLinkToken
{
    public Guid Id { get; set; }

    public required string TokenHash { get; set; }

    public required string Email { get; set; }

    public required string Language { get; set; }

    public required string TimeZone { get; set; }

    // Set when a signed-in user adds this email as a sign-in method; only that user may complete it.
    public Guid? LinkUserId { get; set; }

    public Instant CreatedAt { get; set; }

    public Instant ExpiresAt { get; set; }

    public Instant? UsedAt { get; set; }
}
```

`src/Modules/CareNest.Identity/Domain/Invitation.cs`:
```csharp
using CareNest.SharedKernel.Consultants;
using NodaTime;

namespace CareNest.Identity.Domain;

internal sealed class Invitation : IConsultantOwned
{
    public Guid Id { get; set; }

    public Guid ConsultantId { get; set; }

    public required string TokenHash { get; set; }

    public Instant CreatedAt { get; set; }

    public Instant ExpiresAt { get; set; }

    public Instant? AcceptedAt { get; set; }

    public Guid? AcceptedByUserId { get; set; }
}
```

`src/Modules/CareNest.Identity/Domain/ClientLink.cs`:
```csharp
using CareNest.SharedKernel.Consultants;
using NodaTime;

namespace CareNest.Identity.Domain;

internal sealed class ClientLink : IConsultantOwned
{
    public Guid ConsultantId { get; set; }

    public Guid ParentUserId { get; set; }

    public Instant LinkedAt { get; set; }
}
```

`src/Modules/CareNest.Identity/Persistence/NoCurrentConsultant.cs`:
```csharp
using CareNest.SharedKernel.Consultants;

namespace CareNest.Identity.Persistence;

internal sealed class NoCurrentConsultant : ICurrentConsultant
{
    public Guid? ConsultantId => null;
}
```

`src/Modules/CareNest.Identity/Persistence/RoleSeed.cs`:
```csharp
using Microsoft.AspNetCore.Identity;

namespace CareNest.Identity.Persistence;

internal static class RoleSeed
{
    public static IdentityRole<Guid>[] Roles { get; } =
    [
        Create("0199a5c0-0000-7000-8000-000000000001", IdentityRoles.Parent),
        Create("0199a5c0-0000-7000-8000-000000000002", IdentityRoles.Consultant),
        Create("0199a5c0-0000-7000-8000-000000000003", IdentityRoles.Admin),
    ];

    // Fixed ids and stamps keep the seed stable across migrations.
    private static IdentityRole<Guid> Create(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id,
    };
}
```

`src/Modules/CareNest.Identity/Persistence/IdentityModuleDbContext.cs`:
```csharp
using CareNest.Identity.Domain;
using CareNest.SharedKernel.Consultants;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace CareNest.Identity.Persistence;

internal sealed class IdentityModuleDbContext(
    DbContextOptions<IdentityModuleDbContext> options,
    ICurrentConsultant currentConsultant)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IConsultantScopedDbContext
{
    public const string Schema = "identity";

    public Guid? CurrentConsultantId => currentConsultant.ConsultantId;

    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<ClientLink> ClientLinks => Set<ClientLink>();

    public static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.UseNodaTime().MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<User>(user =>
        {
            user.Property(u => u.DisplayName).HasMaxLength(100);
            user.Property(u => u.Language).HasMaxLength(5);
            user.Property(u => u.TimeZone).HasMaxLength(64);
        });

        builder.Entity<IdentityRole<Guid>>().HasData(RoleSeed.Roles);

        builder.Entity<MagicLinkToken>(token =>
        {
            token.ToTable("magic_link_tokens");
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.Property(t => t.Email).HasMaxLength(256);
            token.Property(t => t.Language).HasMaxLength(5);
            token.Property(t => t.TimeZone).HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => new { t.Email, t.CreatedAt });
            token.HasOne<User>().WithMany().HasForeignKey(t => t.LinkUserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invitation>(invitation =>
        {
            invitation.ToTable("invitations");
            invitation.Property(i => i.TokenHash).HasMaxLength(64);
            invitation.HasIndex(i => i.TokenHash).IsUnique();
            invitation.HasOne<User>().WithMany().HasForeignKey(i => i.ConsultantId).OnDelete(DeleteBehavior.Cascade);
            invitation.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ClientLink>(link =>
        {
            link.ToTable("client_links");
            link.HasKey(l => new { l.ConsultantId, l.ParentUserId });
            link.HasOne<User>().WithMany().HasForeignKey(l => l.ConsultantId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne<User>().WithMany().HasForeignKey(l => l.ParentUserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.ApplyConsultantQueryFilters(this);
    }
}
```

`src/Modules/CareNest.Identity/Persistence/DesignTimeDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareNest.Identity.Persistence;

// Used only by dotnet-ef to generate migrations; it never opens a connection.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityModuleDbContext>
{
    public IdentityModuleDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityModuleDbContext>()
            .UseNpgsql("Host=localhost;Database=carenest_design", IdentityModuleDbContext.ConfigureNpgsql)
            .Options;
        return new IdentityModuleDbContext(options, new NoCurrentConsultant());
    }
}
```

`src/Modules/CareNest.Identity/IdentityModule.cs`:
```csharp
using CareNest.Identity.Persistence;
using CareNest.SharedKernel.Consultants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CareNest.Identity;

public static class IdentityModule
{
    public const string ConnectionStringName = "carenest";

    public static IHostApplicationBuilder AddIdentityPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddScoped<ICurrentConsultant, NoCurrentConsultant>();
        builder.Services.AddDbContext<IdentityModuleDbContext>((services, options) =>
            options.UseNpgsql(
                services.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName),
                IdentityModuleDbContext.ConfigureNpgsql));
        // Retries are off so explicit transactions work without an execution strategy wrapper.
        builder.EnrichNpgsqlDbContext<IdentityModuleDbContext>(settings => settings.DisableRetry = true);
        return builder;
    }

    public static async Task MigrateIdentityDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Generate the initial migration**

Run:
```bash
dotnet ef migrations add InitialIdentity --project src/Modules/CareNest.Identity --output-dir Persistence/Migrations --namespace CareNest.Identity.Persistence.Migrations
```
Expected: `Persistence/Migrations/<timestamp>_InitialIdentity.cs`, its `.Designer.cs` and `IdentityModuleDbContextModelSnapshot.cs`. Open the migration and confirm: every table is created in schema `identity`, three `InsertData` rows for roles, tables `magic_link_tokens`, `invitations`, `client_links`, and `Instant` columns typed `timestamp with time zone`.

- [ ] **Step 6: Run the model test to verify it passes**

Run: `dotnet test tests/CareNest.Identity.Tests`
Expected: PASS, 1 test.

- [ ] **Step 7: Write the failing database tests**

Modify `src/CareNest.Api/CareNest.Api.csproj`, add inside the `ProjectReference` item group:
```xml
    <ProjectReference Include="..\Modules\CareNest.Identity\CareNest.Identity.csproj" />
```

Modify `tests/CareNest.Api.IntegrationTests/Infrastructure/ApiFactory.cs`: add `using CareNest.Identity;` and replace `InitializeAsync` with:
```csharp
    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await Services.MigrateIdentityDatabaseAsync(CancellationToken.None);
    }
```

`tests/CareNest.Api.IntegrationTests/PersistenceTests.cs`:
```csharp
using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity;
using CareNest.Identity.Domain;
using CareNest.Identity.Persistence;
using CareNest.SharedKernel.Consultants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace CareNest.Api.IntegrationTests;

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
```

- [ ] **Step 8: Run them to verify they fail**

Run: `dotnet test tests/CareNest.Api.IntegrationTests`
Expected: every test FAILS during fixture setup with `No service for type 'CareNest.Identity.Persistence.IdentityModuleDbContext' has been registered`.

- [ ] **Step 9: Register persistence in the API and add the migration worker**

Modify `src/CareNest.Api/Program.cs`: add `using CareNest.Identity;` and, after `builder.Services.AddCors();`, add:
```csharp
builder.AddIdentityPersistence();
```

Run:
```bash
dotnet new worker -n CareNest.MigrationService -o src/CareNest.MigrationService
rm src/CareNest.MigrationService/Worker.cs
dotnet sln add src/CareNest.MigrationService
dotnet add src/CareNest.AppHost reference src/CareNest.MigrationService
```

Replace `src/CareNest.MigrationService/CareNest.MigrationService.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">

  <PropertyGroup>
    <UserSecretsId>carenest-migrations</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\CareNest.ServiceDefaults\CareNest.ServiceDefaults.csproj" />
    <ProjectReference Include="..\Modules\CareNest.Identity\CareNest.Identity.csproj" />
  </ItemGroup>

</Project>
```

Replace `src/CareNest.MigrationService/Program.cs`:
```csharp
using CareNest.Identity;
using CareNest.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddIdentityPersistence();
builder.Services.AddHostedService<MigrationWorker>();

builder.Build().Run();
```

`src/CareNest.MigrationService/MigrationWorker.cs`:
```csharp
using CareNest.Identity;

namespace CareNest.MigrationService;

internal sealed class MigrationWorker(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await services.MigrateIdentityDatabaseAsync(stoppingToken);
            logger.LogInformation("Database migrations applied");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Database migration failed");
            // A non-zero exit code keeps the API from starting in Aspire (WaitForCompletion).
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
```

Replace `src/CareNest.AppHost/AppHost.cs`:
```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("carenest");
var email = builder.AddMailPit("email");

var migrations = builder.AddProject<Projects.CareNest_MigrationService>("migrations")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.CareNest_Api>("api")
    .WithReference(database)
    .WithReference(email)
    .WaitFor(database)
    .WaitForCompletion(migrations);

builder.Build().Run();
```

- [ ] **Step 10: Run all tests to verify they pass**

Run: `dotnet build CareNest.slnx` then `dotnet test CareNest.slnx`
Expected: build with 0 warnings; all tests PASS (SharedKernel 16, Identity 1, Integration 5).

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat: add Identity persistence, consultant query filter and migration worker"
```

---

### Task 4: Security primitives

**Files:**
- Create: `src/Modules/CareNest.Identity/Security/SecureTokens.cs`, `Security/ReturnUrlPolicy.cs`
- Create: `src/Modules/CareNest.Identity/Telegram/TelegramLoginVerifier.cs`
- Create: `src/Modules/CareNest.Identity/Accounts/EmailLogin.cs`, `Accounts/DisplayNames.cs`
- Create: `tests/CareNest.Identity.Tests/{SecureTokensTests,ReturnUrlPolicyTests,TelegramLoginVerifierTests,EmailLoginTests}.cs`

**Interfaces:**
- Consumes: `FrontendOptions` (Task 1).
- Produces:
  - `record SecureToken(string Value, string Hash)`; `SecureTokens.Create()`, `SecureTokens.Hash(string value)` (lowercase hex SHA-256, 64 chars).
  - `ReturnUrlPolicy(IOptions<FrontendOptions>)` with `bool IsAllowed([NotNullWhen(true)] string? url)`.
  - `record TelegramLogin(string Id, string? DisplayName)`; `TelegramLoginVerifier.MaxAge` (10 minutes); `TelegramLogin? Verify(IReadOnlyDictionary<string, string> fields, string botToken, Instant now)`.
  - `EmailLogin.Provider = "email"`, `string Normalize(string email)`, `string DisplayNameFrom(string normalizedEmail)`.
  - `DisplayNames.MaxLength = 100`, `string Normalize(string? name)`.

- [ ] **Step 1: Write the failing tests**

`tests/CareNest.Identity.Tests/SecureTokensTests.cs`:
```csharp
using System.Security.Cryptography;
using System.Text;
using CareNest.Identity.Security;

namespace CareNest.Identity.Tests;

public class SecureTokensTests
{
    [Fact]
    public void Token_is_url_safe_and_hash_is_sha256_hex()
    {
        var token = SecureTokens.Create();

        token.Value.Length.ShouldBe(43);
        token.Value.ShouldAllBe(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
        token.Hash.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token.Value))));
        SecureTokens.Hash(token.Value).ShouldBe(token.Hash);
    }

    [Fact]
    public void Tokens_are_unique() => SecureTokens.Create().Value.ShouldNotBe(SecureTokens.Create().Value);
}
```

`tests/CareNest.Identity.Tests/ReturnUrlPolicyTests.cs`:
```csharp
using CareNest.Identity.Security;
using CareNest.SharedKernel.Web;
using Microsoft.Extensions.Options;

namespace CareNest.Identity.Tests;

public class ReturnUrlPolicyTests
{
    private readonly ReturnUrlPolicy _policy = new(Options.Create(new FrontendOptions
    {
        Origins = ["https://app.example.test", "http://localhost:5173/"],
    }));

    [Theory]
    [InlineData("https://app.example.test/invite/abc")]
    [InlineData("https://APP.example.test/")]
    [InlineData("https://app.example.test:443/auth")]
    [InlineData("http://localhost:5173/auth/email")]
    public void Allows_urls_on_frontend_origins(string url) => _policy.IsAllowed(url).ShouldBeTrue();

    [Theory]
    [InlineData("https://app.example.test.evil.com/")]
    [InlineData("https://app.example.test@evil.com/")]
    [InlineData("https://evil.com/?next=https://app.example.test")]
    [InlineData("http://app.example.test/")]
    [InlineData("https://app.example.test:8443/")]
    [InlineData("//evil.com/")]
    [InlineData("/relative/path")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_everything_else(string? url) => _policy.IsAllowed(url).ShouldBeFalse();
}
```

`tests/CareNest.Identity.Tests/TelegramLoginVerifierTests.cs`:
```csharp
using System.Security.Cryptography;
using System.Text;
using CareNest.Identity.Telegram;
using NodaTime;

namespace CareNest.Identity.Tests;

public class TelegramLoginVerifierTests
{
    private const string BotToken = "123456:TEST-TOKEN";
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 5, 9, 0);

    [Fact]
    public void Valid_payload_returns_login_with_full_name()
    {
        var login = TelegramLoginVerifier.Verify(Signed(Payload(Now)), BotToken, Now);

        login.ShouldNotBeNull();
        login.Id.ShouldBe("987654321");
        login.DisplayName.ShouldBe("Anna Petrova");
    }

    [Fact]
    public void Unknown_future_fields_are_part_of_the_signature()
    {
        var fields = Payload(Now);
        fields["allows_write_to_pm"] = "true";

        TelegramLoginVerifier.Verify(Signed(fields), BotToken, Now).ShouldNotBeNull();
    }

    [Fact]
    public void Username_is_used_when_there_is_no_name()
    {
        var fields = Payload(Now);
        fields.Remove("first_name");
        fields.Remove("last_name");

        TelegramLoginVerifier.Verify(Signed(fields), BotToken, Now)!.DisplayName.ShouldBe("anna_p");
    }

    [Fact]
    public void Tampered_field_is_rejected()
    {
        var fields = Signed(Payload(Now));
        fields["id"] = "1";

        TelegramLoginVerifier.Verify(fields, BotToken, Now).ShouldBeNull();
    }

    [Fact]
    public void Other_bot_token_is_rejected() =>
        TelegramLoginVerifier.Verify(Signed(Payload(Now)), "999:OTHER", Now).ShouldBeNull();

    [Fact]
    public void Missing_or_malformed_hash_is_rejected()
    {
        var withoutHash = Payload(Now);
        var malformed = Payload(Now);
        malformed["hash"] = "not-hex";

        TelegramLoginVerifier.Verify(withoutHash, BotToken, Now).ShouldBeNull();
        TelegramLoginVerifier.Verify(malformed, BotToken, Now).ShouldBeNull();
    }

    [Fact]
    public void Old_or_future_auth_date_is_rejected()
    {
        TelegramLoginVerifier.Verify(Signed(Payload(Now - Duration.FromMinutes(11))), BotToken, Now).ShouldBeNull();
        TelegramLoginVerifier.Verify(Signed(Payload(Now + Duration.FromMinutes(2))), BotToken, Now).ShouldBeNull();
    }

    private static Dictionary<string, string> Payload(Instant authDate) => new()
    {
        ["id"] = "987654321",
        ["first_name"] = "Anna",
        ["last_name"] = "Petrova",
        ["username"] = "anna_p",
        ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    // Independent implementation of https://core.telegram.org/widgets/login#checking-authorization
    private static Dictionary<string, string> Signed(Dictionary<string, string> fields)
    {
        var dataCheckString = string.Join('\n', fields.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(BotToken));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString)));
        return new Dictionary<string, string>(fields) { ["hash"] = hash };
    }
}
```

`tests/CareNest.Identity.Tests/EmailLoginTests.cs`:
```csharp
using CareNest.Identity.Accounts;

namespace CareNest.Identity.Tests;

public class EmailLoginTests
{
    [Fact]
    public void Normalize_trims_and_lowercases() => EmailLogin.Normalize("  Anna.P@Example.TEST ").ShouldBe("anna.p@example.test");

    [Fact]
    public void Display_name_is_the_local_part() => EmailLogin.DisplayNameFrom("anna.p@example.test").ShouldBe("anna.p");

    [Fact]
    public void Display_names_are_trimmed_and_capped()
    {
        DisplayNames.Normalize("  Anna  ").ShouldBe("Anna");
        DisplayNames.Normalize(null).ShouldBe("");
        DisplayNames.Normalize(new string('a', 150)).Length.ShouldBe(DisplayNames.MaxLength);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/CareNest.Identity.Tests`
Expected: build FAILS with `CS0246` for `SecureTokens`, `ReturnUrlPolicy`, `TelegramLoginVerifier`, `EmailLogin`, `DisplayNames`.

- [ ] **Step 3: Implement**

`src/Modules/CareNest.Identity/Security/SecureTokens.cs`:
```csharp
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CareNest.Identity.Security;

internal sealed record SecureToken(string Value, string Hash);

internal static class SecureTokens
{
    // Only the hash is stored, so a database leak does not expose usable links.
    public static SecureToken Create()
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return new SecureToken(value, Hash(value));
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
```

`src/Modules/CareNest.Identity/Security/ReturnUrlPolicy.cs`:
```csharp
using System.Diagnostics.CodeAnalysis;
using CareNest.SharedKernel.Web;
using Microsoft.Extensions.Options;

namespace CareNest.Identity.Security;

internal sealed class ReturnUrlPolicy(IOptions<FrontendOptions> frontend)
{
    private readonly string[] _origins = frontend.Value.Origins
        .Select(origin => new Uri(origin).GetLeftPart(UriPartial.Authority))
        .ToArray();

    public bool IsAllowed([NotNullWhen(true)] string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.UserInfo.Length > 0)
        {
            return false;
        }

        var origin = uri.GetLeftPart(UriPartial.Authority);
        return _origins.Any(allowed => string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase));
    }
}
```

`src/Modules/CareNest.Identity/Telegram/TelegramLoginVerifier.cs`:
```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NodaTime;

namespace CareNest.Identity.Telegram;

internal sealed record TelegramLogin(string Id, string? DisplayName);

internal static class TelegramLoginVerifier
{
    public static readonly Duration MaxAge = Duration.FromMinutes(10);
    private static readonly Duration ClockSkew = Duration.FromMinutes(1);

    // https://core.telegram.org/widgets/login#checking-authorization
    public static TelegramLogin? Verify(IReadOnlyDictionary<string, string> fields, string botToken, Instant now)
    {
        if (!fields.TryGetValue("hash", out var hashHex)
            || !fields.TryGetValue("id", out var id)
            || !fields.TryGetValue("auth_date", out var authDateText)
            || !long.TryParse(authDateText, NumberStyles.None, CultureInfo.InvariantCulture, out var authDateSeconds))
        {
            return null;
        }

        byte[] actual;
        try
        {
            actual = Convert.FromHexString(hashHex);
        }
        catch (FormatException)
        {
            return null;
        }

        var dataCheckString = string.Join('\n', fields
            .Where(field => field.Key != "hash")
            .OrderBy(field => field.Key, StringComparer.Ordinal)
            .Select(field => $"{field.Key}={field.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return null;
        }

        var age = now - Instant.FromUnixTimeSeconds(authDateSeconds);
        if (age > MaxAge || age < -ClockSkew)
        {
            return null;
        }

        var fullName = string.Join(' ', new[] { fields.GetValueOrDefault("first_name"), fields.GetValueOrDefault("last_name") }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        return new TelegramLogin(id, fullName.Length > 0 ? fullName : fields.GetValueOrDefault("username"));
    }
}
```

`src/Modules/CareNest.Identity/Accounts/EmailLogin.cs`:
```csharp
namespace CareNest.Identity.Accounts;

// Email is stored as an external login (provider "email") so it never becomes a merge key for other providers.
internal static class EmailLogin
{
    public const string Provider = "email";

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public static string DisplayNameFrom(string normalizedEmail)
    {
        var at = normalizedEmail.IndexOf('@');
        return at > 0 ? normalizedEmail[..at] : normalizedEmail;
    }
}
```

`src/Modules/CareNest.Identity/Accounts/DisplayNames.cs`:
```csharp
namespace CareNest.Identity.Accounts;

internal static class DisplayNames
{
    public const int MaxLength = 100;

    public static string Normalize(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/CareNest.Identity.Tests`
Expected: PASS, 27 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add secure tokens, return URL policy and Telegram login verification"
```

---

### Task 5: Account resolution and authentication wiring

**Files:**
- Create: `src/Modules/CareNest.Identity/{IdentityErrors,IdentityPolicies,IdentityModuleOptions}.cs`
- Create: `src/Modules/CareNest.Identity/Accounts/{SignInModes,AccountService}.cs`
- Create: `src/Modules/CareNest.Identity/Security/{IdentityResultExtensions,ClaimsPrincipalExtensions,HttpCurrentConsultant}.cs`
- Modify: `src/Modules/CareNest.Identity/IdentityModule.cs`, `src/CareNest.Api/Program.cs`
- Create: `tests/CareNest.Api.IntegrationTests/AccountServiceTests.cs`

**Interfaces:**
- Consumes: `User`, `IdentityModuleDbContext`, `AddIdentityPersistence` (Task 3); `EmailLogin`, `DisplayNames`, `ReturnUrlPolicy` (Task 4); `ApiError`, `Languages`, `TimeZones`, `FrontendOptions` (Task 1).
- Produces:
  - Public: `IHostApplicationBuilder AddIdentityModule()`, `IEndpointRouteBuilder MapIdentityEndpoints()` (group `/api/identity`).
  - `IdentityErrors` (all codes listed in Step 3), `IdentityPolicies.Consultant`, `IdentityPolicies.Admin`.
  - `IdentityModuleOptions { Section = "Identity"; string[] AdminEmails; string? CookieDomain; int InvitationLifetimeDays = 14; string? TelegramBotToken; string? TelegramBotName }`.
  - `enum SignInMode { SignIn, Link }`; `SignInModes.SignIn = "signin"`, `SignInModes.Link = "link"`, `bool TryParse(string?, out SignInMode)`, `string ToName(SignInMode)`.
  - `record ExternalIdentity(string Provider, string ProviderKey, string? DisplayName)`, `record NewUserDefaults(string Language, string TimeZone)`, `record SignInOutcome(User? User, ApiError? Error)` with `Success(User)` / `Failure(ApiError)`.
  - `AccountService.ResolveAsync(ExternalIdentity, SignInMode, Guid? currentUserId, NewUserDefaults, CancellationToken) : Task<SignInOutcome>`; `EnsureAdminRoleAsync(User, string normalizedEmail) : Task`; `EnsureConsultantAsync(string email, string displayName, CancellationToken) : Task<User>`.
  - `IdentityResult.ThrowIfFailed()`, `ClaimsPrincipal.GetUserId() : Guid?`, `HttpCurrentConsultant : ICurrentConsultant`.

- [ ] **Step 1: Write the failing tests**

`tests/CareNest.Api.IntegrationTests/AccountServiceTests.cs`:
```csharp
using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity;
using CareNest.Identity.Accounts;
using CareNest.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CareNest.Api.IntegrationTests;

public class AccountServiceTests(ApiFactory factory)
{
    private static readonly NewUserDefaults Russian = new("ru", "Europe/Moscow");

    [Fact]
    public async Task New_identity_creates_parent_with_login_and_defaults()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var users = Users(scope);
        var identity = NewIdentity("Anna");

        var outcome = await accounts.ResolveAsync(identity, SignInMode.SignIn, null, Russian, CancellationToken.None);

        var user = outcome.User.ShouldNotBeNull();
        user.DisplayName.ShouldBe("Anna");
        user.Language.ShouldBe("ru");
        user.TimeZone.ShouldBe("Europe/Moscow");
        (await users.GetRolesAsync(user)).ShouldBe(new[] { IdentityRoles.Parent });
        (await users.FindByLoginAsync(identity.Provider, identity.ProviderKey))!.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Known_identity_signs_in_the_same_user()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var identity = NewIdentity("Anna");

        var first = await accounts.ResolveAsync(identity, SignInMode.SignIn, null, Russian, CancellationToken.None);
        var second = await accounts.ResolveAsync(identity, SignInMode.SignIn, null, Russian, CancellationToken.None);

        second.User!.Id.ShouldBe(first.User!.Id);
    }

    [Fact]
    public async Task Invalid_defaults_fall_back_to_english_and_utc()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);

        var outcome = await accounts.ResolveAsync(NewIdentity(null), SignInMode.SignIn, null, new NewUserDefaults("de", "Moscow"), CancellationToken.None);

        outcome.User!.Language.ShouldBe("en");
        outcome.User.TimeZone.ShouldBe("UTC");
        outcome.User.DisplayName.ShouldBe("");
    }

    [Fact]
    public async Task Link_adds_the_method_to_the_signed_in_user()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var users = Users(scope);
        var owner = (await accounts.ResolveAsync(NewIdentity("Anna"), SignInMode.SignIn, null, Russian, CancellationToken.None)).User!;
        var second = NewIdentity("Anna");

        var outcome = await accounts.ResolveAsync(second, SignInMode.Link, owner.Id, Russian, CancellationToken.None);

        outcome.User!.Id.ShouldBe(owner.Id);
        (await users.GetLoginsAsync(owner)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Link_of_a_method_owned_by_someone_else_fails()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var taken = NewIdentity("Other");
        await accounts.ResolveAsync(taken, SignInMode.SignIn, null, Russian, CancellationToken.None);
        var me = (await accounts.ResolveAsync(NewIdentity("Me"), SignInMode.SignIn, null, Russian, CancellationToken.None)).User!;

        var outcome = await accounts.ResolveAsync(taken, SignInMode.Link, me.Id, Russian, CancellationToken.None);

        outcome.Error.ShouldBe(IdentityErrors.LoginAlreadyLinked);
    }

    [Fact]
    public async Task Link_without_a_signed_in_user_fails()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);

        var outcome = await accounts.ResolveAsync(NewIdentity("Anna"), SignInMode.Link, null, Russian, CancellationToken.None);

        outcome.Error.ShouldBe(IdentityErrors.NotSignedIn);
    }

    [Fact]
    public async Task Ensure_consultant_creates_a_consultant_only_user_and_is_idempotent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var users = Users(scope);
        var email = $"Consultant-{Guid.NewGuid():N}@Example.Test";

        var created = await accounts.EnsureConsultantAsync(email, "Anna", CancellationToken.None);
        var again = await accounts.EnsureConsultantAsync(email.ToLowerInvariant(), "Anna", CancellationToken.None);

        again.Id.ShouldBe(created.Id);
        created.DisplayName.ShouldBe("Anna");
        (await users.GetRolesAsync(created)).ShouldBe(new[] { IdentityRoles.Consultant });
    }

    [Fact]
    public async Task Ensure_consultant_promotes_an_existing_parent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var users = Users(scope);
        var email = $"parent-{Guid.NewGuid():N}@example.test";
        var parent = (await accounts.ResolveAsync(new ExternalIdentity(EmailLogin.Provider, email, "P"), SignInMode.SignIn, null, Russian, CancellationToken.None)).User!;

        var consultant = await accounts.EnsureConsultantAsync(email, "P", CancellationToken.None);

        consultant.Id.ShouldBe(parent.Id);
        (await users.GetRolesAsync(consultant)).Order().ShouldBe(new[] { IdentityRoles.Consultant, IdentityRoles.Parent });
    }

    private static ExternalIdentity NewIdentity(string? name) => new("Google", Guid.NewGuid().ToString("N"), name);

    private static AccountService Accounts(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<AccountService>();

    private static UserManager<User> Users(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<UserManager<User>>();
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/CareNest.Api.IntegrationTests --filter AccountServiceTests`
Expected: build FAILS with `CS0246` for `AccountService`, `SignInMode`, `ExternalIdentity`, `NewUserDefaults`, `IdentityErrors`.

- [ ] **Step 3: Implement errors, options and policies**

`src/Modules/CareNest.Identity/IdentityErrors.cs`:
```csharp
using CareNest.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;

namespace CareNest.Identity;

internal static class IdentityErrors
{
    public static readonly ApiError InvalidReturnUrl = new("identity.invalid_return_url", StatusCodes.Status400BadRequest);
    public static readonly ApiError InvalidSignInMode = new("identity.invalid_sign_in_mode", StatusCodes.Status400BadRequest);
    public static readonly ApiError ProviderUnavailable = new("identity.provider_unavailable", StatusCodes.Status404NotFound);
    public static readonly ApiError ExternalLoginFailed = new("identity.external_login_failed", StatusCodes.Status400BadRequest);
    public static readonly ApiError LoginAlreadyLinked = new("identity.login_already_linked", StatusCodes.Status409Conflict);
    public static readonly ApiError NotSignedIn = new("identity.not_signed_in", StatusCodes.Status401Unauthorized);
    public static readonly ApiError LinkSessionMismatch = new("identity.link_session_mismatch", StatusCodes.Status403Forbidden);
    public static readonly ApiError MagicLinkInvalid = new("identity.magic_link_invalid", StatusCodes.Status400BadRequest);
    public static readonly ApiError MagicLinkUsed = new("identity.magic_link_used", StatusCodes.Status409Conflict);
    public static readonly ApiError MagicLinkExpired = new("identity.magic_link_expired", StatusCodes.Status410Gone);
    public static readonly ApiError InviteNotFound = new("identity.invite_not_found", StatusCodes.Status404NotFound);
    public static readonly ApiError InviteUsed = new("identity.invite_used", StatusCodes.Status409Conflict);
    public static readonly ApiError InviteExpired = new("identity.invite_expired", StatusCodes.Status410Gone);
    public static readonly ApiError InviteOwn = new("identity.invite_own", StatusCodes.Status400BadRequest);
}
```

`src/Modules/CareNest.Identity/IdentityPolicies.cs`:
```csharp
namespace CareNest.Identity;

internal static class IdentityPolicies
{
    public const string Consultant = "consultant";
    public const string Admin = "admin";
}
```

`src/Modules/CareNest.Identity/IdentityModuleOptions.cs`:
```csharp
namespace CareNest.Identity;

internal sealed class IdentityModuleOptions
{
    public const string Section = "Identity";

    // Users signing in with one of these emails receive the admin role.
    public string[] AdminEmails { get; set; } = [];

    // Shared parent domain of app., studio. and api. in production; null locally.
    public string? CookieDomain { get; set; }

    public int InvitationLifetimeDays { get; set; } = 14;

    public string? TelegramBotToken { get; set; }

    public string? TelegramBotName { get; set; }
}
```

- [ ] **Step 4: Implement the security helpers**

`src/Modules/CareNest.Identity/Security/IdentityResultExtensions.cs`:
```csharp
using Microsoft.AspNetCore.Identity;

namespace CareNest.Identity.Security;

internal static class IdentityResultExtensions
{
    // Identity error codes carry no personal data, so they are safe in the exception message.
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Identity operation failed: " + string.Join(", ", result.Errors.Select(error => error.Code)));
        }
    }
}
```

`src/Modules/CareNest.Identity/Security/ClaimsPrincipalExtensions.cs`:
```csharp
using System.Security.Claims;

namespace CareNest.Identity.Security;

internal static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
```

`src/Modules/CareNest.Identity/Security/HttpCurrentConsultant.cs`:
```csharp
using CareNest.SharedKernel.Consultants;
using Microsoft.AspNetCore.Http;

namespace CareNest.Identity.Security;

internal sealed class HttpCurrentConsultant(IHttpContextAccessor accessor) : ICurrentConsultant
{
    public Guid? ConsultantId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            return principal is not null && principal.IsInRole(IdentityRoles.Consultant) ? principal.GetUserId() : null;
        }
    }
}
```

- [ ] **Step 5: Implement account resolution**

`src/Modules/CareNest.Identity/Accounts/SignInModes.cs`:
```csharp
using CareNest.Identity.Domain;
using CareNest.SharedKernel.Errors;

namespace CareNest.Identity.Accounts;

internal enum SignInMode
{
    SignIn,
    Link,
}

internal static class SignInModes
{
    public const string SignIn = "signin";
    public const string Link = "link";

    public static bool TryParse(string? value, out SignInMode mode)
    {
        switch (value)
        {
            case null or SignIn:
                mode = SignInMode.SignIn;
                return true;
            case Link:
                mode = SignInMode.Link;
                return true;
            default:
                mode = SignInMode.SignIn;
                return false;
        }
    }

    public static string ToName(SignInMode mode) => mode == SignInMode.Link ? Link : SignIn;
}

internal sealed record ExternalIdentity(string Provider, string ProviderKey, string? DisplayName);

internal sealed record NewUserDefaults(string Language, string TimeZone);

internal sealed record SignInOutcome(User? User, ApiError? Error)
{
    public static SignInOutcome Success(User user) => new(user, null);

    public static SignInOutcome Failure(ApiError error) => new(null, error);
}
```

`src/Modules/CareNest.Identity/Accounts/AccountService.cs`:
```csharp
using CareNest.Identity.Domain;
using CareNest.Identity.Persistence;
using CareNest.Identity.Security;
using CareNest.SharedKernel.Localization;
using CareNest.SharedKernel.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NodaTime;

namespace CareNest.Identity.Accounts;

internal sealed class AccountService(
    UserManager<User> users,
    IdentityModuleDbContext db,
    IClock clock,
    IOptions<IdentityModuleOptions> options)
{
    public async Task<SignInOutcome> ResolveAsync(
        ExternalIdentity identity,
        SignInMode mode,
        Guid? currentUserId,
        NewUserDefaults defaults,
        CancellationToken cancellationToken)
    {
        var existing = await users.FindByLoginAsync(identity.Provider, identity.ProviderKey);
        if (mode == SignInMode.Link)
        {
            return await LinkAsync(identity, existing, currentUserId);
        }

        return SignInOutcome.Success(existing ?? await CreateUserAsync(identity, defaults, IdentityRoles.Parent, cancellationToken));
    }

    public async Task EnsureAdminRoleAsync(User user, string normalizedEmail)
    {
        var isAdminEmail = options.Value.AdminEmails.Any(email => EmailLogin.Normalize(email) == normalizedEmail);
        if (isAdminEmail && !await users.IsInRoleAsync(user, IdentityRoles.Admin))
        {
            (await users.AddToRoleAsync(user, IdentityRoles.Admin)).ThrowIfFailed();
        }
    }

    public async Task<User> EnsureConsultantAsync(string email, string displayName, CancellationToken cancellationToken)
    {
        var normalized = EmailLogin.Normalize(email);
        var user = await users.FindByLoginAsync(EmailLogin.Provider, normalized)
            ?? await CreateUserAsync(
                new ExternalIdentity(EmailLogin.Provider, normalized, displayName),
                new NewUserDefaults(Languages.Default, TimeZones.Default),
                IdentityRoles.Consultant,
                cancellationToken);

        if (!await users.IsInRoleAsync(user, IdentityRoles.Consultant))
        {
            (await users.AddToRoleAsync(user, IdentityRoles.Consultant)).ThrowIfFailed();
            // Existing sessions lack the new role; a new stamp makes them re-authenticate.
            (await users.UpdateSecurityStampAsync(user)).ThrowIfFailed();
        }

        return user;
    }

    private async Task<SignInOutcome> LinkAsync(ExternalIdentity identity, User? existing, Guid? currentUserId)
    {
        var current = currentUserId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
        if (current is null)
        {
            return SignInOutcome.Failure(IdentityErrors.NotSignedIn);
        }

        if (existing is not null)
        {
            return existing.Id == current.Id ? SignInOutcome.Success(current) : SignInOutcome.Failure(IdentityErrors.LoginAlreadyLinked);
        }

        (await users.AddLoginAsync(current, new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider))).ThrowIfFailed();
        return SignInOutcome.Success(current);
    }

    private async Task<User> CreateUserAsync(ExternalIdentity identity, NewUserDefaults defaults, string role, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        var user = new User
        {
            Id = id,
            UserName = id.ToString("N"),
            DisplayName = DisplayNames.Normalize(identity.DisplayName),
            Language = Languages.OrDefault(defaults.Language),
            TimeZone = TimeZones.OrDefault(defaults.TimeZone),
            CreatedAt = clock.GetCurrentInstant(),
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        (await users.CreateAsync(user)).ThrowIfFailed();
        (await users.AddLoginAsync(user, new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider))).ThrowIfFailed();
        (await users.AddToRoleAsync(user, role)).ThrowIfFailed();
        await transaction.CommitAsync(cancellationToken);
        return user;
    }
}
```

- [ ] **Step 6: Wire authentication in the module and the host**

Replace `src/Modules/CareNest.Identity/IdentityModule.cs`:
```csharp
using CareNest.Identity.Accounts;
using CareNest.Identity.Domain;
using CareNest.Identity.Persistence;
using CareNest.Identity.Security;
using CareNest.SharedKernel.Consultants;
using CareNest.SharedKernel.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CareNest.Identity;

public static class IdentityModule
{
    public const string ConnectionStringName = "carenest";

    public static IHostApplicationBuilder AddIdentityModule(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<IdentityModuleOptions>().Bind(builder.Configuration.GetSection(IdentityModuleOptions.Section));
        services.AddOptions<FrontendOptions>().Bind(builder.Configuration.GetSection(FrontendOptions.Section));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentConsultant, HttpCurrentConsultant>();
        builder.AddIdentityPersistence();

        services.AddIdentityCore<User>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddSignInManager();
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(5));

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(ConfigureSessionCookie);
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<IdentityModuleOptions>>((cookie, identity) => cookie.Cookie.Domain = identity.Value.CookieDomain);

        services.AddAuthorizationBuilder()
            .AddPolicy(IdentityPolicies.Consultant, policy => policy.RequireRole(IdentityRoles.Consultant))
            .AddPolicy(IdentityPolicies.Admin, policy => policy.RequireRole(IdentityRoles.Admin));

        services.AddSingleton<ReturnUrlPolicy>();
        services.AddScoped<AccountService>();
        return builder;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        // Routes are added from Task 6 on.
        app.MapGroup("/api/identity").WithTags("Identity");
        return app;
    }

    public static IHostApplicationBuilder AddIdentityPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddScoped<ICurrentConsultant, NoCurrentConsultant>();
        builder.Services.AddDbContext<IdentityModuleDbContext>((services, options) =>
            options.UseNpgsql(
                services.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName),
                IdentityModuleDbContext.ConfigureNpgsql));
        // Retries are off so explicit transactions work without an execution strategy wrapper.
        builder.EnrichNpgsqlDbContext<IdentityModuleDbContext>(settings => settings.DisableRetry = true);
        return builder;
    }

    public static async Task MigrateIdentityDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static void ConfigureSessionCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = "cn_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // An API answers with status codes; redirects to a login page make no sense to a SPA.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }
}
```
Modify `src/CareNest.Api/Program.cs`:
- Replace `builder.AddIdentityPersistence();` with `builder.AddIdentityModule();`.
- After `app.UseCors(...)` add:
```csharp
app.UseAuthentication();
app.UseAuthorization();
```
- After `app.MapDefaultEndpoints();` add:
```csharp
app.MapIdentityEndpoints();
```

- [ ] **Step 7: Run all tests to verify they pass**

Run: `dotnet test CareNest.slnx`
Expected: all PASS (SharedKernel 16, Identity 27, Integration 13).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: add account resolution and cookie authentication"
```

---

### Task 6: Email magic-link sign-in, current user and sign-out

**Files:**
- Create: `src/Modules/CareNest.Identity/Email/{EmailMessage,IEmailSender,EmailOptions,SmtpEmailSender,MagicLinkEmail}.cs`
- Create: `src/Modules/CareNest.Identity/Endpoints/{Contracts,EmailSignInEndpoints,ProfileEndpoints}.cs`
- Modify: `src/Modules/CareNest.Identity/IdentityModule.cs`
- Create: `tests/CareNest.Identity.Tests/EmailTests.cs`
- Create: `tests/CareNest.Api.IntegrationTests/Infrastructure/{FakeEmailSender,SignInExtensions}.cs`, `tests/CareNest.Api.IntegrationTests/EmailSignInTests.cs`
- Modify: `tests/CareNest.Api.IntegrationTests/Infrastructure/ApiFactory.cs`

**Interfaces:**
- Consumes: `AccountService`, `SignInModes`, `IdentityErrors`, `GetUserId` (Task 5); `SecureTokens`, `ReturnUrlPolicy`, `EmailLogin` (Task 4).
- Produces:
  - `record EmailMessage(string To, string Subject, string TextBody)`; `IEmailSender.SendAsync(EmailMessage, CancellationToken)`; `EmailOptions` (section `Email`, connection string name `email`, `void ApplyConnectionString(string?)`); `MagicLinkEmail.Compose(string to, string language, string link)`.
  - Contracts: `EmailStartRequest { Email, CallbackUrl, Language, TimeZone, Mode = "signin" }`, `EmailCompleteRequest { Token }`, `record MeResponse(Guid Id, string DisplayName, string Language, string TimeZone, IReadOnlyList<string> Roles, IReadOnlyList<string> SignInMethods)`.
  - Endpoints: `POST /api/identity/email/start` -> 202; `POST /api/identity/email/complete` -> 204 + session cookie; `GET /api/identity/me` -> `MeResponse`; `POST /api/identity/signout` -> 204.
  - `ProfileEndpoints.ToResponseAsync(User, UserManager<User>) : Task<MeResponse>` (reused in Task 8).
  - Test helpers: `ApiFactory.Emails` (`FakeEmailSender`), `ApiFactory.GetAdminClientAsync()`, `SignInExtensions.NewEmail(string prefix)`, `HttpClient.SignInWithEmailAsync(ApiFactory, string email, string language = "en", string timeZone = "Europe/Moscow")`, `ApiFactory.SignedInClientAsync(string email)`, `HttpClient.GetMeAsync()`.

- [ ] **Step 1: Write the failing unit tests for email composition**

`tests/CareNest.Identity.Tests/EmailTests.cs`:
```csharp
using CareNest.Identity.Email;

namespace CareNest.Identity.Tests;

public class EmailTests
{
    private const string Link = "https://app.example.test/auth/email?token=abc";

    [Fact]
    public void Russian_email_contains_the_link_and_russian_text()
    {
        var message = MagicLinkEmail.Compose("anna@example.test", "ru", Link);

        message.To.ShouldBe("anna@example.test");
        message.Subject.ShouldBe("Вход в CareNest");
        message.TextBody.ShouldContain(Link);
    }

    [Fact]
    public void English_email_is_the_fallback()
    {
        var message = MagicLinkEmail.Compose("anna@example.test", "en", Link);

        message.Subject.ShouldBe("Sign in to CareNest");
        message.TextBody.ShouldContain(Link);
    }

    [Fact]
    public void Aspire_connection_string_sets_host_and_port()
    {
        var options = new EmailOptions();

        options.ApplyConnectionString("smtp://localhost:41025");

        options.Host.ShouldBe("localhost");
        options.Port.ShouldBe(41025);
    }

    [Fact]
    public void Missing_connection_string_keeps_configured_values()
    {
        var options = new EmailOptions { Host = "smtp.azurecomm.net", Port = 587 };

        options.ApplyConnectionString(null);

        options.Host.ShouldBe("smtp.azurecomm.net");
        options.Port.ShouldBe(587);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/CareNest.Identity.Tests --filter EmailTests`
Expected: build FAILS with `CS0246` for `MagicLinkEmail` and `EmailOptions`.

- [ ] **Step 3: Implement the email components**

`src/Modules/CareNest.Identity/Email/EmailMessage.cs`:
```csharp
namespace CareNest.Identity.Email;

internal sealed record EmailMessage(string To, string Subject, string TextBody);
```

`src/Modules/CareNest.Identity/Email/IEmailSender.cs`:
```csharp
namespace CareNest.Identity.Email;

internal interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
```

`src/Modules/CareNest.Identity/Email/EmailOptions.cs`:
```csharp
namespace CareNest.Identity.Email;

internal sealed class EmailOptions
{
    public const string Section = "Email";
    public const string ConnectionStringName = "email";

    public string From { get; set; } = "CareNest <no-reply@carenest.local>";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public bool UseStartTls { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    // Aspire's Mailpit resource provides "smtp://host:port"; production configures the Email section instead.
    public void ApplyConnectionString(string? connectionString)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            Host = uri.Host;
            Port = uri.Port;
        }
    }
}
```

`src/Modules/CareNest.Identity/Email/SmtpEmailSender.cs`:
```csharp
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CareNest.Identity.Email;

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(settings.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.TextBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        if (!string.IsNullOrEmpty(settings.UserName))
        {
            await client.AuthenticateAsync(settings.UserName, settings.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
```

`src/Modules/CareNest.Identity/Email/MagicLinkEmail.cs`:
```csharp
using CareNest.SharedKernel.Localization;

namespace CareNest.Identity.Email;

// Server-sent emails are localised on the server in the recipient's language (spec section 6).
internal static class MagicLinkEmail
{
    public static EmailMessage Compose(string to, string language, string link) => language == Languages.Russian
        ? new EmailMessage(
            to,
            "Вход в CareNest",
            $"Чтобы войти в CareNest, откройте ссылку (она действует 15 минут):\n\n{link}\n\nЕсли вы не запрашивали вход, просто проигнорируйте это письмо.")
        : new EmailMessage(
            to,
            "Sign in to CareNest",
            $"To sign in to CareNest, open this link (valid for 15 minutes):\n\n{link}\n\nIf you did not request this, ignore this email.");
}
```

- [ ] **Step 4: Run the unit tests to verify they pass**

Run: `dotnet test tests/CareNest.Identity.Tests --filter EmailTests`
Expected: PASS, 4 tests.

- [ ] **Step 5: Add the test infrastructure for signing in**

`tests/CareNest.Api.IntegrationTests/Infrastructure/FakeEmailSender.cs`:
```csharp
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CareNest.Identity.Email;

namespace CareNest.Api.IntegrationTests.Infrastructure;

internal sealed partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> SentTo(string address) => _sent.Where(message => message.To == address).ToList();

    public string LatestTokenFor(string address) => TokenPattern().Match(SentTo(address)[^1].TextBody).Groups[1].Value;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    [GeneratedRegex("token=([A-Za-z0-9_-]+)")]
    private static partial Regex TokenPattern();
}
```

Modify `tests/CareNest.Api.IntegrationTests/Infrastructure/ApiFactory.cs`:
- Add `using CareNest.Identity.Email;`.
- Add members:
```csharp
    private HttpClient? _adminClient;

    internal FakeEmailSender Emails { get; } = new();

    // One cached admin session: the per-email throttle would block repeated admin sign-ins on the fixed clock.
    public async Task<HttpClient> GetAdminClientAsync() => _adminClient ??= await this.SignedInClientAsync(AdminEmail);
```
- Replace the `ConfigureTestServices` line with:
```csharp
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton<IEmailSender>(Emails);
        });
```

`tests/CareNest.Api.IntegrationTests/Infrastructure/SignInExtensions.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using CareNest.Identity.Accounts;
using CareNest.Identity.Endpoints;

namespace CareNest.Api.IntegrationTests.Infrastructure;

internal static class SignInExtensions
{
    public const string EmailCallbackUrl = ApiFactory.ClientAppUrl + "/auth/email";

    public static string NewEmail(string prefix = "parent") => $"{prefix}-{Guid.NewGuid():N}@example.test";

    public static async Task SignInWithEmailAsync(this HttpClient client, ApiFactory factory, string email, string language = "en", string timeZone = "Europe/Moscow")
    {
        var start = await client.PostAsJsonAsync("/api/identity/email/start", new { email, callbackUrl = EmailCallbackUrl, language, timeZone });
        start.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var complete = await client.PostAsJsonAsync("/api/identity/email/complete", new { token = factory.Emails.LatestTokenFor(EmailLogin.Normalize(email)) });
        complete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    public static async Task<HttpClient> SignedInClientAsync(this ApiFactory factory, string email)
    {
        var client = factory.CreateHttpsClient();
        await client.SignInWithEmailAsync(factory, email);
        return client;
    }

    public static async Task<MeResponse> GetMeAsync(this HttpClient client)
    {
        var response = await client.GetAsync("/api/identity/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadAsAsync<MeResponse>();
    }
}
```

- [ ] **Step 6: Write the failing integration tests**

`tests/CareNest.Api.IntegrationTests/EmailSignInTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity;
using CareNest.Identity.Accounts;
using NodaTime;
using static CareNest.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace CareNest.Api.IntegrationTests;

public class EmailSignInTests(ApiFactory factory)
{
    [Fact]
    public async Task New_email_signs_in_as_parent_with_browser_defaults()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();

        await client.SignInWithEmailAsync(factory, email, language: "ru", timeZone: "Asia/Yekaterinburg");

        var me = await client.GetMeAsync();
        me.Roles.ShouldBe(new[] { IdentityRoles.Parent });
        me.SignInMethods.ShouldBe(new[] { EmailLogin.Provider });
        me.Language.ShouldBe("ru");
        me.TimeZone.ShouldBe("Asia/Yekaterinburg");
        me.DisplayName.ShouldBe(email[..email.IndexOf('@')]);
        factory.Emails.SentTo(email)[0].Subject.ShouldBe("Вход в CareNest");
    }

    [Fact]
    public async Task Email_case_and_spaces_reach_one_account()
    {
        var local = $"anna-{Guid.NewGuid():N}";
        var first = factory.CreateHttpsClient();
        var second = factory.CreateHttpsClient();

        await first.SignInWithEmailAsync(factory, $"  {local.ToUpperInvariant()}@Example.TEST ");
        await second.SignInWithEmailAsync(factory, $"{local}@example.test");

        (await second.GetMeAsync()).Id.ShouldBe((await first.GetMeAsync()).Id);
    }

    [Fact]
    public async Task Magic_link_works_once()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();
        await client.SignInWithEmailAsync(factory, email);

        var reuse = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/complete", new { token = factory.Emails.LatestTokenFor(email) });

        await reuse.ShouldBeProblemAsync(HttpStatusCode.Conflict, "identity.magic_link_used");
    }

    [Fact]
    public async Task Magic_link_expires_after_15_minutes()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();
        await StartAsync(client, email);
        factory.Clock.Advance(Duration.FromMinutes(15));

        var complete = await client.PostAsJsonAsync("/api/identity/email/complete", new { token = factory.Emails.LatestTokenFor(email) });

        await complete.ShouldBeProblemAsync(HttpStatusCode.Gone, "identity.magic_link_expired");
    }

    [Fact]
    public async Task Unknown_token_is_rejected()
    {
        var complete = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/complete", new { token = "unknown" });

        await complete.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.magic_link_invalid");
    }

    [Fact]
    public async Task Callback_url_outside_frontend_origins_is_rejected_and_nothing_is_sent()
    {
        var email = NewEmail();

        var start = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/start",
            new { email, callbackUrl = "https://app.example.test.evil.com/auth/email", language = "en", timeZone = "UTC" });

        await start.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.invalid_return_url");
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task Start_sends_at_most_three_links_per_email_in_ten_minutes()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();

        for (var i = 0; i < 4; i++)
        {
            (await StartAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        factory.Emails.SentTo(email).Count.ShouldBe(3);
        factory.Clock.Advance(Duration.FromMinutes(11));
        await StartAsync(client, email);
        factory.Emails.SentTo(email).Count.ShouldBe(4);
    }

    [Fact]
    public async Task Configured_admin_email_gets_the_admin_role()
    {
        var admin = await factory.GetAdminClientAsync();

        (await admin.GetMeAsync()).Roles.ShouldContain(IdentityRoles.Admin);
    }

    [Fact]
    public async Task Invalid_request_returns_coded_validation_problem()
    {
        var start = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/start",
            new { email = NewEmail(), callbackUrl = EmailCallbackUrl, language = "de", timeZone = "Moscow" });

        await start.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var body = await start.Content.ReadAsStringAsync();
        body.ShouldContain("\"language\"");
        body.ShouldContain("\"timeZone\"");
    }

    [Fact]
    public async Task Link_mode_requires_a_signed_in_user()
    {
        var start = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/start",
            new { email = NewEmail(), callbackUrl = EmailCallbackUrl, language = "en", timeZone = "UTC", mode = "link" });

        await start.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "identity.not_signed_in");
    }

    [Fact]
    public async Task Signed_in_user_links_a_second_email()
    {
        var client = await factory.SignedInClientAsync(NewEmail());
        var secondEmail = NewEmail();
        await StartAsync(client, secondEmail, mode: "link");

        var complete = await client.PostAsJsonAsync("/api/identity/email/complete", new { token = factory.Emails.LatestTokenFor(secondEmail) });

        complete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var viaSecond = await factory.SignedInClientAsync(secondEmail);
        (await viaSecond.GetMeAsync()).Id.ShouldBe((await client.GetMeAsync()).Id);
    }

    [Fact]
    public async Task Link_token_cannot_be_completed_from_another_session()
    {
        var client = await factory.SignedInClientAsync(NewEmail());
        var secondEmail = NewEmail();
        await StartAsync(client, secondEmail, mode: "link");

        var complete = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/complete", new { token = factory.Emails.LatestTokenFor(secondEmail) });

        await complete.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "identity.link_session_mismatch");
    }

    [Fact]
    public async Task Me_requires_a_session_and_sign_out_ends_it()
    {
        (await factory.CreateHttpsClient().GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var client = await factory.SignedInClientAsync(NewEmail());

        (await client.PostAsync("/api/identity/signout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, string email, string mode = "signin") =>
        client.PostAsJsonAsync("/api/identity/email/start", new { email, callbackUrl = EmailCallbackUrl, language = "en", timeZone = "UTC", mode });
}
```

- [ ] **Step 7: Run them to verify they fail**

Run: `dotnet test tests/CareNest.Api.IntegrationTests --filter EmailSignInTests`
Expected: build FAILS with `CS0234`/`CS0246` for `CareNest.Identity.Endpoints` and `MeResponse`.

- [ ] **Step 8: Implement contracts and endpoints**

`src/Modules/CareNest.Identity/Endpoints/Contracts.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using CareNest.Identity.Accounts;
using CareNest.SharedKernel.Localization;
using CareNest.SharedKernel.Validation;

namespace CareNest.Identity.Endpoints;

internal sealed record EmailStartRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [Required, StringLength(2048)]
    public required string CallbackUrl { get; init; }

    [Required, AllowedValues(Languages.Russian, Languages.English)]
    public required string Language { get; init; }

    [Required, IanaTimeZone]
    public required string TimeZone { get; init; }

    [Required, AllowedValues(SignInModes.SignIn, SignInModes.Link)]
    public string Mode { get; init; } = SignInModes.SignIn;
}

internal sealed record EmailCompleteRequest
{
    [Required, StringLength(128)]
    public required string Token { get; init; }
}

internal sealed record MeResponse(
    Guid Id,
    string DisplayName,
    string Language,
    string TimeZone,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> SignInMethods);
```

`src/Modules/CareNest.Identity/Endpoints/EmailSignInEndpoints.cs`:
```csharp
using CareNest.Identity.Accounts;
using CareNest.Identity.Domain;
using CareNest.Identity.Email;
using CareNest.Identity.Persistence;
using CareNest.Identity.Security;
using CareNest.SharedKernel.Errors;
using CareNest.SharedKernel.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace CareNest.Identity.Endpoints;

internal static class EmailSignInEndpoints
{
    public static readonly Duration LinkLifetime = Duration.FromMinutes(15);
    public static readonly Duration ThrottleWindow = Duration.FromMinutes(10);
    public const int MaxLinksPerWindow = 3;

    public static void MapEmailSignIn(this RouteGroupBuilder group)
    {
        group.MapPost("/email/start", StartAsync).WithRequestValidation<EmailStartRequest>();
        group.MapPost("/email/complete", CompleteAsync).WithRequestValidation<EmailCompleteRequest>();
    }

    private static async Task<Results<Accepted, ProblemHttpResult>> StartAsync(
        EmailStartRequest request,
        HttpContext http,
        ReturnUrlPolicy returnUrls,
        IdentityModuleDbContext db,
        UserManager<User> users,
        IEmailSender sender,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!returnUrls.IsAllowed(request.CallbackUrl))
        {
            return IdentityErrors.InvalidReturnUrl.ToProblem();
        }

        SignInModes.TryParse(request.Mode, out var mode);
        var linkUserId = mode == SignInMode.Link ? http.User.GetUserId() : null;
        if (mode == SignInMode.Link && linkUserId is null)
        {
            return IdentityErrors.NotSignedIn.ToProblem();
        }

        var email = EmailLogin.Normalize(request.Email);
        var now = clock.GetCurrentInstant();
        var windowStart = now - ThrottleWindow;
        // The response is identical when throttled so the endpoint does not reveal anything about the address.
        if (await db.MagicLinkTokens.CountAsync(t => t.Email == email && t.CreatedAt > windowStart, cancellationToken) >= MaxLinksPerWindow)
        {
            return TypedResults.Accepted((string?)null);
        }

        var token = SecureTokens.Create();
        db.MagicLinkTokens.Add(new MagicLinkToken
        {
            Id = Guid.CreateVersion7(),
            TokenHash = token.Hash,
            Email = email,
            Language = request.Language,
            TimeZone = request.TimeZone,
            LinkUserId = linkUserId,
            CreatedAt = now,
            ExpiresAt = now + LinkLifetime,
        });
        await db.SaveChangesAsync(cancellationToken);

        var existing = await users.FindByLoginAsync(EmailLogin.Provider, email);
        var link = QueryHelpers.AddQueryString(request.CallbackUrl, "token", token.Value);
        await sender.SendAsync(MagicLinkEmail.Compose(email, existing?.Language ?? request.Language, link), cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> CompleteAsync(
        EmailCompleteRequest request,
        HttpContext http,
        IdentityModuleDbContext db,
        AccountService accounts,
        SignInManager<User> signIn,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var hash = SecureTokens.Hash(request.Token);
        var token = await db.MagicLinkTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        var now = clock.GetCurrentInstant();
        if (token is null)
        {
            return IdentityErrors.MagicLinkInvalid.ToProblem();
        }

        if (token.UsedAt is not null)
        {
            return IdentityErrors.MagicLinkUsed.ToProblem();
        }

        if (token.ExpiresAt <= now)
        {
            return IdentityErrors.MagicLinkExpired.ToProblem();
        }

        if (token.LinkUserId is not null && token.LinkUserId != http.User.GetUserId())
        {
            return IdentityErrors.LinkSessionMismatch.ToProblem();
        }

        // The conditional update makes the token single-use even when two requests race.
        var claimed = await db.MagicLinkTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UsedAt, (Instant?)now), cancellationToken);
        if (claimed == 0)
        {
            return IdentityErrors.MagicLinkUsed.ToProblem();
        }

        var outcome = await accounts.ResolveAsync(
            new ExternalIdentity(EmailLogin.Provider, token.Email, EmailLogin.DisplayNameFrom(token.Email)),
            token.LinkUserId is null ? SignInMode.SignIn : SignInMode.Link,
            token.LinkUserId,
            new NewUserDefaults(token.Language, token.TimeZone),
            cancellationToken);
        if (outcome.Error is not null)
        {
            return outcome.Error.ToProblem();
        }

        await accounts.EnsureAdminRoleAsync(outcome.User!, token.Email);
        await signIn.SignInAsync(outcome.User!, isPersistent: true);
        return TypedResults.NoContent();
    }
}
```

`src/Modules/CareNest.Identity/Endpoints/ProfileEndpoints.cs`:
```csharp
using System.Security.Claims;
using CareNest.Identity.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace CareNest.Identity.Endpoints;

internal static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfile(this RouteGroupBuilder group)
    {
        var me = group.MapGroup("/me").RequireAuthorization();
        me.MapGet("", GetAsync);
        group.MapPost("/signout", SignOutAsync);
        return me;
    }

    public static async Task<MeResponse> ToResponseAsync(User user, UserManager<User> users)
    {
        var roles = await users.GetRolesAsync(user);
        var logins = await users.GetLoginsAsync(user);
        return new MeResponse(
            user.Id,
            user.DisplayName,
            user.Language,
            user.TimeZone,
            roles.Order(StringComparer.Ordinal).ToList(),
            logins.Select(login => login.LoginProvider).Distinct().Order(StringComparer.Ordinal).ToList());
    }

    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> GetAsync(ClaimsPrincipal principal, UserManager<User> users)
    {
        // A valid cookie can outlive its user (deleted account on another device).
        var user = await users.GetUserAsync(principal);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await ToResponseAsync(user, users));
    }

    private static async Task<NoContent> SignOutAsync(SignInManager<User> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }
}
```

Modify `src/Modules/CareNest.Identity/IdentityModule.cs`:
- Add `using CareNest.Identity.Email;` and `using CareNest.Identity.Endpoints;`.
- In `AddIdentityModule`, before `return builder;`, add:
```csharp
        services.AddOptions<EmailOptions>()
            .Bind(builder.Configuration.GetSection(EmailOptions.Section))
            .PostConfigure<IConfiguration>((email, configuration) =>
                email.ApplyConnectionString(configuration.GetConnectionString(EmailOptions.ConnectionStringName)));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
```
- Replace the body of `MapIdentityEndpoints` with:
```csharp
        var group = app.MapGroup("/api/identity").WithTags("Identity");
        group.MapEmailSignIn();
        group.MapProfile();
        return app;
```

- [ ] **Step 9: Run all tests to verify they pass**

Run: `dotnet test CareNest.slnx`
Expected: all PASS (SharedKernel 16, Identity 31, Integration 26).

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat: add email magic-link sign-in, current user and sign-out"
```

---

### Task 7: Google, Yandex ID, VK ID and Telegram sign-in

**Files:**
- Create: `src/Modules/CareNest.Identity/External/ExternalProviders.cs`, `src/Modules/CareNest.Identity/Security/ErrorRedirect.cs`, `src/Modules/CareNest.Identity/Endpoints/ExternalSignInEndpoints.cs`
- Modify: `src/Modules/CareNest.Identity/Endpoints/Contracts.cs`, `src/Modules/CareNest.Identity/IdentityModule.cs`
- Create: `tests/CareNest.Api.IntegrationTests/Infrastructure/TelegramPayload.cs`, `tests/CareNest.Api.IntegrationTests/ExternalSignInTests.cs`

**Interfaces:**
- Consumes: `AccountService`, `SignInModes`, `IdentityModuleOptions`, `IdentityErrors` (Task 5); `ReturnUrlPolicy`, `TelegramLoginVerifier` (Task 4); test helpers from Task 6.
- Produces:
  - `ExternalProviders.Google = "Google"`, `Yandex = "Yandex"`, `VkId = "VkId"`, `Telegram = "Telegram"`, `OAuth` (list of the three OAuth schemes), `void Register(AuthenticationBuilder, IConfiguration)` reading `Identity:Providers:<Name>:ClientId|ClientSecret`.
  - `ErrorRedirect.For(string returnUrl, ApiError error)` -> `returnUrl?error=<code>`.
  - Contracts: `TelegramCompleteRequest { Dictionary<string, JsonElement> Auth; Mode; Language; TimeZone }`, `record ProvidersResponse(IReadOnlyList<string> Providers, string? TelegramBotName)`.
  - Endpoints: `GET /api/identity/providers`; `GET /api/identity/external/{provider}/start?returnUrl&mode&language&timeZone` -> 302 to provider; `GET /api/identity/external/callback` -> 302 to `returnUrl` (or `returnUrl?error=<code>`); `POST /api/identity/telegram/complete` -> 204.
  - Provider callback paths (register these in each provider console): `/signin-google`, `/signin-yandex`, `/signin-vkid` on the API host.

- [ ] **Step 1: Add the Telegram payload test helper**

`tests/CareNest.Api.IntegrationTests/Infrastructure/TelegramPayload.cs`:
```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NodaTime;

namespace CareNest.Api.IntegrationTests.Infrastructure;

internal static class TelegramPayload
{
    // Mirrors what the Telegram Login Widget passes to its JavaScript callback.
    public static Dictionary<string, object> Create(string id, string firstName, Instant authDate, string botToken = ApiFactory.TelegramBotToken)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["first_name"] = firstName,
            ["id"] = id,
        };
        var dataCheckString = string.Join('\n', fields.Select(field => $"{field.Key}={field.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString)));

        return new Dictionary<string, object>
        {
            ["id"] = long.Parse(id, CultureInfo.InvariantCulture),
            ["first_name"] = firstName,
            ["auth_date"] = authDate.ToUnixTimeSeconds(),
            ["hash"] = hash,
        };
    }
}
```

- [ ] **Step 2: Write the failing integration tests**

`tests/CareNest.Api.IntegrationTests/ExternalSignInTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity.Endpoints;
using static CareNest.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace CareNest.Api.IntegrationTests;

public class ExternalSignInTests(ApiFactory factory)
{
    private const string ReturnUrl = ApiFactory.ClientAppUrl + "/";

    [Fact]
    public async Task Providers_lists_only_configured_methods()
    {
        var response = await factory.CreateHttpsClient().GetAsync("/api/identity/providers");

        var providers = await response.ReadAsAsync<ProvidersResponse>();
        providers.Providers.ShouldBe(new[] { "email", "Google", "Telegram" });
        providers.TelegramBotName.ShouldBe("carenest_test_bot");
    }

    [Fact]
    public async Task Start_redirects_to_the_provider_with_pkce()
    {
        var response = await factory.CreateHttpsClient().GetAsync(StartUrl("Google", ReturnUrl));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.ShouldStartWith("https://accounts.google.com/");
        location.ShouldContain("code_challenge=");
    }

    [Fact]
    public async Task Start_with_an_unconfigured_provider_is_rejected()
    {
        var response = await factory.CreateHttpsClient().GetAsync(StartUrl("Yandex", ReturnUrl));

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "identity.provider_unavailable");
    }

    [Fact]
    public async Task Start_with_a_foreign_return_url_is_rejected()
    {
        var response = await factory.CreateHttpsClient().GetAsync(StartUrl("Google", "https://evil.example/"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.invalid_return_url");
    }

    [Fact]
    public async Task Start_with_an_unknown_mode_is_rejected()
    {
        var response = await factory.CreateHttpsClient().GetAsync(StartUrl("Google", ReturnUrl, mode: "merge"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.invalid_sign_in_mode");
    }

    [Fact]
    public async Task Callback_without_an_external_session_fails()
    {
        var response = await factory.CreateHttpsClient().GetAsync("/api/identity/external/callback");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.external_login_failed");
    }

    [Fact]
    public async Task Telegram_signs_in_a_new_parent()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/identity/telegram/complete", new
        {
            auth = TelegramPayload.Create(NewTelegramId(), "Anna", factory.Clock.GetCurrentInstant()),
            language = "ru",
            timeZone = "Europe/Moscow",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var me = await client.GetMeAsync();
        me.SignInMethods.ShouldBe(new[] { "Telegram" });
        me.DisplayName.ShouldBe("Anna");
        me.Language.ShouldBe("ru");
    }

    [Fact]
    public async Task Telegram_is_linked_to_the_signed_in_user()
    {
        var client = await factory.SignedInClientAsync(NewEmail());

        var response = await client.PostAsJsonAsync("/api/identity/telegram/complete", new
        {
            auth = TelegramPayload.Create(NewTelegramId(), "Anna", factory.Clock.GetCurrentInstant()),
            mode = "link",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetMeAsync()).SignInMethods.ShouldBe(new[] { "Telegram", "email" });
    }

    [Fact]
    public async Task Telegram_account_of_another_user_cannot_be_linked()
    {
        var telegramId = NewTelegramId();
        await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/telegram/complete",
            new { auth = TelegramPayload.Create(telegramId, "Owner", factory.Clock.GetCurrentInstant()) });
        var other = await factory.SignedInClientAsync(NewEmail());

        var response = await other.PostAsJsonAsync("/api/identity/telegram/complete", new
        {
            auth = TelegramPayload.Create(telegramId, "Owner", factory.Clock.GetCurrentInstant()),
            mode = "link",
        });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "identity.login_already_linked");
    }

    [Fact]
    public async Task Telegram_payload_signed_with_another_token_is_rejected()
    {
        var response = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/telegram/complete",
            new { auth = TelegramPayload.Create(NewTelegramId(), "Anna", factory.Clock.GetCurrentInstant(), botToken: "999:OTHER") });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.external_login_failed");
    }

    private static string StartUrl(string provider, string returnUrl, string mode = "signin") =>
        $"/api/identity/external/{provider}/start?returnUrl={Uri.EscapeDataString(returnUrl)}&mode={mode}&language=en&timeZone=UTC";

    private static string NewTelegramId() => Random.Shared.NextInt64(1_000_000, long.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
```

- [ ] **Step 3: Run them to verify they fail**

Run: `dotnet test tests/CareNest.Api.IntegrationTests --filter ExternalSignInTests`
Expected: build FAILS with `CS0246` for `ProvidersResponse`.

- [ ] **Step 4: Implement provider registration and error redirects**

`src/Modules/CareNest.Identity/Security/ErrorRedirect.cs`:
```csharp
using CareNest.SharedKernel.Errors;
using Microsoft.AspNetCore.WebUtilities;

namespace CareNest.Identity.Security;

internal static class ErrorRedirect
{
    public static string For(string returnUrl, ApiError error) => QueryHelpers.AddQueryString(returnUrl, "error", error.Code);
}
```

`src/Modules/CareNest.Identity/External/ExternalProviders.cs`:
```csharp
using CareNest.Identity.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace CareNest.Identity.External;

internal static class ExternalProviders
{
    public const string Google = "Google";
    public const string Yandex = "Yandex";
    public const string VkId = "VkId";
    public const string Telegram = "Telegram";

    public const string ReturnUrlItem = "cn.returnUrl";
    public const string ModeItem = "cn.mode";
    public const string LanguageItem = "cn.language";
    public const string TimeZoneItem = "cn.timeZone";

    public static IReadOnlyList<string> OAuth { get; } = [Google, Yandex, VkId];

    private sealed record Credentials(string ClientId, string ClientSecret);

    // Only configured providers are registered: an OAuth scheme without a ClientId breaks OpenAPI generation (verified 2026-09-24).
    public static void Register(AuthenticationBuilder authentication, IConfiguration configuration)
    {
        if (Read(configuration, Google) is { } google)
        {
            authentication.AddGoogle(Google, options => Configure(options, google));
        }

        if (Read(configuration, Yandex) is { } yandex)
        {
            authentication.AddYandex(Yandex, options => Configure(options, yandex));
        }

        if (Read(configuration, VkId) is { } vk)
        {
            authentication.AddVkId(VkId, options => Configure(options, vk));
        }
    }

    private static Credentials? Read(IConfiguration configuration, string provider)
    {
        var section = configuration.GetSection($"{IdentityModuleOptions.Section}:Providers:{provider}");
        var clientId = section["ClientId"];
        var clientSecret = section["ClientSecret"];
        return string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) ? null : new Credentials(clientId, clientSecret);
    }

    private static void Configure(OAuthOptions options, Credentials credentials)
    {
        options.ClientId = credentials.ClientId;
        options.ClientSecret = credentials.ClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        // VK ID requires PKCE; the others accept it.
        options.UsePkce = true;
        options.Events.OnRemoteFailure = context =>
        {
            var returnUrl = context.Properties?.GetString(ReturnUrlItem);
            context.Response.Redirect(returnUrl is null ? "/" : ErrorRedirect.For(returnUrl, IdentityErrors.ExternalLoginFailed));
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }
}
```
If `AddYandex(string, Action<>)` or `AddVkId(string, Action<>)` does not resolve, add `using AspNet.Security.OAuth.Yandex;` / `using AspNet.Security.OAuth.VkId;` (the VK ID extension lives in that namespace; this was verified for `AddVkId`).

- [ ] **Step 5: Implement the endpoints**

Append to `src/Modules/CareNest.Identity/Endpoints/Contracts.cs` (add `using System.Text.Json;` and `using CareNest.SharedKernel.Time;` at the top):
```csharp
internal sealed record TelegramCompleteRequest
{
    [Required]
    public required Dictionary<string, JsonElement> Auth { get; init; }

    [Required, AllowedValues(SignInModes.SignIn, SignInModes.Link)]
    public string Mode { get; init; } = SignInModes.SignIn;

    [Required, AllowedValues(Languages.Russian, Languages.English)]
    public string Language { get; init; } = Languages.Default;

    [Required, IanaTimeZone]
    public string TimeZone { get; init; } = TimeZones.Default;
}

internal sealed record ProvidersResponse(IReadOnlyList<string> Providers, string? TelegramBotName);
```

`src/Modules/CareNest.Identity/Endpoints/ExternalSignInEndpoints.cs`:
```csharp
using System.Security.Claims;
using System.Text.Json;
using CareNest.Identity.Accounts;
using CareNest.Identity.Domain;
using CareNest.Identity.External;
using CareNest.Identity.Security;
using CareNest.Identity.Telegram;
using CareNest.SharedKernel.Errors;
using CareNest.SharedKernel.Localization;
using CareNest.SharedKernel.Time;
using CareNest.SharedKernel.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NodaTime;

namespace CareNest.Identity.Endpoints;

internal static class ExternalSignInEndpoints
{
    public const string CallbackPath = "/api/identity/external/callback";

    public static void MapExternalSignIn(this RouteGroupBuilder group)
    {
        group.MapGet("/providers", GetProvidersAsync);
        group.MapGet("/external/{provider}/start", StartAsync);
        group.MapGet("/external/callback", CallbackAsync);
        group.MapPost("/telegram/complete", TelegramAsync).WithRequestValidation<TelegramCompleteRequest>();
    }

    private static async Task<Ok<ProvidersResponse>> GetProvidersAsync(IAuthenticationSchemeProvider schemes, IOptions<IdentityModuleOptions> options)
    {
        var providers = new List<string> { EmailLogin.Provider };
        foreach (var provider in ExternalProviders.OAuth)
        {
            if (await schemes.GetSchemeAsync(provider) is not null)
            {
                providers.Add(provider);
            }
        }

        if (!string.IsNullOrEmpty(options.Value.TelegramBotToken))
        {
            providers.Add(ExternalProviders.Telegram);
        }

        return TypedResults.Ok(new ProvidersResponse(providers, options.Value.TelegramBotName));
    }

    private static async Task<Results<ChallengeHttpResult, ProblemHttpResult>> StartAsync(
        string provider,
        string returnUrl,
        string? mode,
        string? language,
        string? timeZone,
        IAuthenticationSchemeProvider schemes,
        ReturnUrlPolicy returnUrls,
        SignInManager<User> signIn)
    {
        if (!ExternalProviders.OAuth.Contains(provider) || await schemes.GetSchemeAsync(provider) is null)
        {
            return IdentityErrors.ProviderUnavailable.ToProblem();
        }

        if (!returnUrls.IsAllowed(returnUrl))
        {
            return IdentityErrors.InvalidReturnUrl.ToProblem();
        }

        if (!SignInModes.TryParse(mode, out var signInMode))
        {
            return IdentityErrors.InvalidSignInMode.ToProblem();
        }

        var properties = signIn.ConfigureExternalAuthenticationProperties(provider, CallbackPath);
        properties.SetString(ExternalProviders.ReturnUrlItem, returnUrl);
        properties.SetString(ExternalProviders.ModeItem, SignInModes.ToName(signInMode));
        properties.SetString(ExternalProviders.LanguageItem, Languages.OrDefault(language));
        properties.SetString(ExternalProviders.TimeZoneItem, TimeZones.OrDefault(timeZone));
        return TypedResults.Challenge(properties, [provider]);
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> CallbackAsync(
        HttpContext http,
        SignInManager<User> signIn,
        AccountService accounts,
        ReturnUrlPolicy returnUrls,
        CancellationToken cancellationToken)
    {
        var info = await signIn.GetExternalLoginInfoAsync();
        await http.SignOutAsync(IdentityConstants.ExternalScheme);
        var properties = info?.AuthenticationProperties;
        var returnUrl = properties?.GetString(ExternalProviders.ReturnUrlItem);
        if (info is null || properties is null || !returnUrls.IsAllowed(returnUrl))
        {
            return IdentityErrors.ExternalLoginFailed.ToProblem();
        }

        SignInModes.TryParse(properties.GetString(ExternalProviders.ModeItem), out var mode);
        var outcome = await accounts.ResolveAsync(
            new ExternalIdentity(info.LoginProvider, info.ProviderKey, info.Principal.FindFirstValue(ClaimTypes.Name)),
            mode,
            http.User.GetUserId(),
            new NewUserDefaults(
                properties.GetString(ExternalProviders.LanguageItem) ?? Languages.Default,
                properties.GetString(ExternalProviders.TimeZoneItem) ?? TimeZones.Default),
            cancellationToken);
        if (outcome.Error is not null)
        {
            return TypedResults.Redirect(ErrorRedirect.For(returnUrl, outcome.Error));
        }

        await signIn.SignInAsync(outcome.User!, isPersistent: true);
        return TypedResults.Redirect(returnUrl);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> TelegramAsync(
        TelegramCompleteRequest request,
        HttpContext http,
        IOptions<IdentityModuleOptions> options,
        AccountService accounts,
        SignInManager<User> signIn,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var botToken = options.Value.TelegramBotToken;
        if (string.IsNullOrEmpty(botToken))
        {
            return IdentityErrors.ProviderUnavailable.ToProblem();
        }

        // Numbers keep their raw JSON text, which is exactly what Telegram signed.
        var fields = request.Auth.ToDictionary(
            field => field.Key,
            field => field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString() ?? string.Empty : field.Value.GetRawText());
        var login = TelegramLoginVerifier.Verify(fields, botToken, clock.GetCurrentInstant());
        if (login is null)
        {
            return IdentityErrors.ExternalLoginFailed.ToProblem();
        }

        SignInModes.TryParse(request.Mode, out var mode);
        var outcome = await accounts.ResolveAsync(
            new ExternalIdentity(ExternalProviders.Telegram, login.Id, login.DisplayName),
            mode,
            http.User.GetUserId(),
            new NewUserDefaults(request.Language, request.TimeZone),
            cancellationToken);
        if (outcome.Error is not null)
        {
            return outcome.Error.ToProblem();
        }

        await signIn.SignInAsync(outcome.User!, isPersistent: true);
        return TypedResults.NoContent();
    }
}
```

Modify `src/Modules/CareNest.Identity/IdentityModule.cs`:
- Add `using CareNest.Identity.External;`.
- Replace `services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();` with:
```csharp
        var authentication = services.AddAuthentication(IdentityConstants.ApplicationScheme);
        authentication.AddIdentityCookies();
        ExternalProviders.Register(authentication, builder.Configuration);
```
- In `MapIdentityEndpoints`, after `group.MapProfile();` add `group.MapExternalSignIn();`.

- [ ] **Step 6: Run all tests to verify they pass**

Run: `dotnet test CareNest.slnx`
Expected: all PASS (SharedKernel 16, Identity 31, Integration 36). `HostTests.OpenApi_document_is_served` still passes with Google configured.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add Google, Yandex ID, VK ID and Telegram sign-in"
```

---

### Task 8: Profile update

**Files:**
- Modify: `src/Modules/CareNest.Identity/Endpoints/Contracts.cs`, `src/Modules/CareNest.Identity/Endpoints/ProfileEndpoints.cs`
- Create: `tests/CareNest.Api.IntegrationTests/ProfileTests.cs`

**Interfaces:**
- Consumes: `ProfileEndpoints.ToResponseAsync`, `MeResponse` (Task 6); `ThrowIfFailed` (Task 5).
- Produces: `UpdateProfileRequest { DisplayName, Language, TimeZone }`; `PUT /api/identity/me` -> 200 `MeResponse`.

- [ ] **Step 1: Write the failing tests**

`tests/CareNest.Api.IntegrationTests/ProfileTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity.Endpoints;
using static CareNest.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace CareNest.Api.IntegrationTests;

public class ProfileTests(ApiFactory factory)
{
    [Fact]
    public async Task Profile_update_is_saved()
    {
        var client = await factory.SignedInClientAsync(NewEmail());

        var response = await client.PutAsJsonAsync("/api/identity/me", new { displayName = "  Anna Petrova ", language = "ru", timeZone = "Asia/Novosibirsk" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsAsync<MeResponse>()).DisplayName.ShouldBe("Anna Petrova");
        var me = await client.GetMeAsync();
        me.DisplayName.ShouldBe("Anna Petrova");
        me.Language.ShouldBe("ru");
        me.TimeZone.ShouldBe("Asia/Novosibirsk");
    }

    [Theory]
    [InlineData("", "ru", "UTC", "displayName")]
    [InlineData("   ", "ru", "UTC", "displayName")]
    [InlineData("Anna", "de", "UTC", "language")]
    [InlineData("Anna", "ru", "Moscow", "timeZone")]
    public async Task Invalid_profile_is_rejected(string displayName, string language, string timeZone, string field)
    {
        var client = await factory.SignedInClientAsync(NewEmail());

        var response = await client.PutAsJsonAsync("/api/identity/me", new { displayName, language, timeZone });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await response.Content.ReadAsStringAsync()).ShouldContain($"\"{field}\"");
    }

    [Fact]
    public async Task Profile_update_requires_a_session()
    {
        var response = await factory.CreateHttpsClient().PutAsJsonAsync("/api/identity/me", new { displayName = "Anna", language = "ru", timeZone = "UTC" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/CareNest.Api.IntegrationTests --filter ProfileTests`
Expected: FAIL. `PUT /api/identity/me` returns 405 Method Not Allowed instead of 200 / 400 (the unauthenticated case already returns 401 and passes).

- [ ] **Step 3: Implement**

Append to `src/Modules/CareNest.Identity/Endpoints/Contracts.cs`:
```csharp
internal sealed record UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public required string DisplayName { get; init; }

    [Required, AllowedValues(Languages.Russian, Languages.English)]
    public required string Language { get; init; }

    [Required, IanaTimeZone]
    public required string TimeZone { get; init; }
}
```

Modify `src/Modules/CareNest.Identity/Endpoints/ProfileEndpoints.cs`:
- Add `using CareNest.Identity.Security;` and `using CareNest.SharedKernel.Validation;`.
- In `MapProfile`, after `me.MapGet("", GetAsync);` add:
```csharp
        me.MapPut("", UpdateAsync).WithRequestValidation<UpdateProfileRequest>();
```
- Add the handler:
```csharp
    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> UpdateAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        UserManager<User> users)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Language = request.Language;
        user.TimeZone = request.TimeZone;
        (await users.UpdateAsync(user)).ThrowIfFailed();
        return TypedResults.Ok(await ToResponseAsync(user, users));
    }
```

- [ ] **Step 4: Run all tests to verify they pass**

Run: `dotnet test CareNest.slnx`
Expected: all PASS (Integration 42).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add profile update"
```

---

### Task 9: Consultants, invitations and clients

**Files:**
- Create: `src/Modules/CareNest.Identity/Endpoints/AdminEndpoints.cs`, `src/Modules/CareNest.Identity/Endpoints/InvitationEndpoints.cs`
- Modify: `src/Modules/CareNest.Identity/Endpoints/Contracts.cs`, `src/Modules/CareNest.Identity/IdentityModule.cs`, `tests/CareNest.Api.IntegrationTests/Infrastructure/SignInExtensions.cs`
- Create: `tests/CareNest.Api.IntegrationTests/InvitationTests.cs`

**Interfaces:**
- Consumes: `AccountService.EnsureConsultantAsync`, `IdentityPolicies`, `ICurrentConsultant` via `HttpCurrentConsultant` (Task 5); `SecureTokens` (Task 4); `Invitation`, `ClientLink` (Task 3); `FrontendOptions.ClientAppUrl` (Task 1).
- Produces:
  - Contracts: `CreateConsultantRequest { Email, DisplayName }`, `record CreateConsultantResponse(Guid UserId)`, `record CreateInvitationResponse(Guid Id, string Url, Instant ExpiresAt)`, `record InvitationResponse(Guid Id, Instant CreatedAt, Instant ExpiresAt, string Status)` (`pending` / `accepted` / `expired`), `AcceptInvitationRequest { Token }`, `record ClientResponse(Guid UserId, string DisplayName, Instant LinkedAt)`.
  - Endpoints: `POST /api/identity/admin/consultants` (admin); `POST /api/identity/invitations`, `GET /api/identity/invitations`, `GET /api/identity/clients` (consultant); `POST /api/identity/invitations/accept` (any signed-in user). Invitation URL: `{Frontend:ClientAppUrl}/invite/{token}`.
  - Test helper: `ApiFactory.CreateConsultantClientAsync()`.

- [ ] **Step 1: Add the consultant test helper**

Append to `tests/CareNest.Api.IntegrationTests/Infrastructure/SignInExtensions.cs` (inside the class):
```csharp
    public static async Task<HttpClient> CreateConsultantClientAsync(this ApiFactory factory)
    {
        var email = NewEmail("consultant");
        var admin = await factory.GetAdminClientAsync();
        var created = await admin.PostAsJsonAsync("/api/identity/admin/consultants", new { email, displayName = "Consultant" });
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await factory.SignedInClientAsync(email);
    }
```

- [ ] **Step 2: Write the failing tests**

`tests/CareNest.Api.IntegrationTests/InvitationTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity;
using CareNest.Identity.Endpoints;
using NodaTime;
using static CareNest.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace CareNest.Api.IntegrationTests;

public class InvitationTests(ApiFactory factory)
{
    [Fact]
    public async Task Admin_creates_a_consultant_who_signs_in_with_the_consultant_role()
    {
        var consultant = await factory.CreateConsultantClientAsync();

        (await consultant.GetMeAsync()).Roles.ShouldBe(new[] { IdentityRoles.Consultant });
    }

    [Fact]
    public async Task Non_admin_cannot_create_consultants()
    {
        var parent = await factory.SignedInClientAsync(NewEmail());

        var response = await parent.PostAsJsonAsync("/api/identity/admin/consultants", new { email = NewEmail(), displayName = "X" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Parent_cannot_use_consultant_endpoints()
    {
        var parent = await factory.SignedInClientAsync(NewEmail());

        (await parent.PostAsync("/api/identity/invitations", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await parent.GetAsync("/api/identity/invitations")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await parent.GetAsync("/api/identity/clients")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task New_parent_accepting_an_invitation_becomes_a_client()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var invitation = await CreateInvitationAsync(consultant);
        invitation.Url.ShouldStartWith(ApiFactory.ClientAppUrl + "/invite/");
        invitation.ExpiresAt.ShouldBe(factory.Clock.GetCurrentInstant() + Duration.FromDays(14));
        var parent = await factory.SignedInClientAsync(NewEmail());

        (await AcceptAsync(parent, invitation)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var clients = await (await consultant.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>();
        clients.Select(c => c.UserId).ShouldBe(new[] { (await parent.GetMeAsync()).Id });
        var invitations = await (await consultant.GetAsync("/api/identity/invitations")).ReadAsAsync<List<InvitationResponse>>();
        invitations.Single().Status.ShouldBe("accepted");
    }

    [Fact]
    public async Task Used_invitation_is_rejected()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var invitation = await CreateInvitationAsync(consultant);
        await AcceptAsync(await factory.SignedInClientAsync(NewEmail()), invitation);

        var second = await AcceptAsync(await factory.SignedInClientAsync(NewEmail()), invitation);

        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "identity.invite_used");
    }

    [Fact]
    public async Task Expired_invitation_is_rejected_and_listed_as_expired()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var invitation = await CreateInvitationAsync(consultant);
        var parent = await factory.SignedInClientAsync(NewEmail());
        factory.Clock.Advance(Duration.FromDays(14));

        var response = await AcceptAsync(parent, invitation);

        await response.ShouldBeProblemAsync(HttpStatusCode.Gone, "identity.invite_expired");
        var invitations = await (await consultant.GetAsync("/api/identity/invitations")).ReadAsAsync<List<InvitationResponse>>();
        invitations.Single().Status.ShouldBe("expired");
    }

    [Fact]
    public async Task Unknown_invitation_is_not_found()
    {
        var parent = await factory.SignedInClientAsync(NewEmail());

        var response = await parent.PostAsJsonAsync("/api/identity/invitations/accept", new { token = "unknown" });

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "identity.invite_not_found");
    }

    [Fact]
    public async Task Consultant_cannot_accept_their_own_invitation()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var invitation = await CreateInvitationAsync(consultant);

        var response = await AcceptAsync(consultant, invitation);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.invite_own");
    }

    [Fact]
    public async Task Second_invitation_from_the_same_consultant_keeps_one_client_link()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var parent = await factory.SignedInClientAsync(NewEmail());
        await AcceptAsync(parent, await CreateInvitationAsync(consultant));

        (await AcceptAsync(parent, await CreateInvitationAsync(consultant))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var clients = await (await consultant.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>();
        clients.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Consultant_cannot_see_another_consultants_invitations_or_clients()
    {
        var consultantA = await factory.CreateConsultantClientAsync();
        var consultantB = await factory.CreateConsultantClientAsync();
        await AcceptAsync(await factory.SignedInClientAsync(NewEmail()), await CreateInvitationAsync(consultantA));

        (await (await consultantB.GetAsync("/api/identity/invitations")).ReadAsAsync<List<InvitationResponse>>()).ShouldBeEmpty();
        (await (await consultantB.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>()).ShouldBeEmpty();
        (await (await consultantA.GetAsync("/api/identity/invitations")).ReadAsAsync<List<InvitationResponse>>()).Count.ShouldBe(1);
        (await (await consultantA.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>()).Count.ShouldBe(1);
    }

    private static async Task<CreateInvitationResponse> CreateInvitationAsync(HttpClient consultant)
    {
        var response = await consultant.PostAsync("/api/identity/invitations", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadAsAsync<CreateInvitationResponse>();
    }

    private static Task<HttpResponseMessage> AcceptAsync(HttpClient parent, CreateInvitationResponse invitation) =>
        parent.PostAsJsonAsync("/api/identity/invitations/accept", new { token = invitation.Url.Split("/invite/")[1] });
}
```

- [ ] **Step 3: Run them to verify they fail**

Run: `dotnet test tests/CareNest.Api.IntegrationTests --filter InvitationTests`
Expected: build FAILS with `CS0246` for `ClientResponse`, `InvitationResponse`, `CreateInvitationResponse`.

- [ ] **Step 4: Implement contracts and endpoints**

Append to `src/Modules/CareNest.Identity/Endpoints/Contracts.cs` (add `using NodaTime;` at the top):
```csharp
internal sealed record CreateConsultantRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [Required, StringLength(100, MinimumLength = 1)]
    public required string DisplayName { get; init; }
}

internal sealed record CreateConsultantResponse(Guid UserId);

internal sealed record CreateInvitationResponse(Guid Id, string Url, Instant ExpiresAt);

internal sealed record InvitationResponse(Guid Id, Instant CreatedAt, Instant ExpiresAt, string Status);

internal sealed record AcceptInvitationRequest
{
    [Required, StringLength(128)]
    public required string Token { get; init; }
}

internal sealed record ClientResponse(Guid UserId, string DisplayName, Instant LinkedAt);
```

`src/Modules/CareNest.Identity/Endpoints/AdminEndpoints.cs`:
```csharp
using CareNest.Identity.Accounts;
using CareNest.SharedKernel.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CareNest.Identity.Endpoints;

internal static class AdminEndpoints
{
    public static void MapAdmin(this RouteGroupBuilder group)
    {
        group.MapPost("/admin/consultants", CreateConsultantAsync)
            .RequireAuthorization(IdentityPolicies.Admin)
            .WithRequestValidation<CreateConsultantRequest>();
    }

    private static async Task<Ok<CreateConsultantResponse>> CreateConsultantAsync(
        CreateConsultantRequest request,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        var user = await accounts.EnsureConsultantAsync(request.Email, request.DisplayName.Trim(), cancellationToken);
        return TypedResults.Ok(new CreateConsultantResponse(user.Id));
    }
}
```

`src/Modules/CareNest.Identity/Endpoints/InvitationEndpoints.cs`:
```csharp
using System.Security.Claims;
using CareNest.Identity.Domain;
using CareNest.Identity.Persistence;
using CareNest.Identity.Security;
using CareNest.SharedKernel.Consultants;
using CareNest.SharedKernel.Errors;
using CareNest.SharedKernel.Validation;
using CareNest.SharedKernel.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;

namespace CareNest.Identity.Endpoints;

internal static class InvitationEndpoints
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Expired = "expired";

    public static void MapInvitations(this RouteGroupBuilder group)
    {
        var consultant = group.MapGroup("").RequireAuthorization(IdentityPolicies.Consultant);
        consultant.MapPost("/invitations", CreateAsync);
        consultant.MapGet("/invitations", ListAsync);
        consultant.MapGet("/clients", ListClientsAsync);

        group.MapPost("/invitations/accept", AcceptAsync)
            .RequireAuthorization()
            .WithRequestValidation<AcceptInvitationRequest>();
    }

    private static async Task<Ok<CreateInvitationResponse>> CreateAsync(
        ICurrentConsultant consultant,
        IdentityModuleDbContext db,
        IClock clock,
        IOptions<IdentityModuleOptions> identity,
        IOptions<FrontendOptions> frontend,
        CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var token = SecureTokens.Create();
        var invitation = new Invitation
        {
            Id = Guid.CreateVersion7(),
            // The consultant policy guarantees a consultant id here.
            ConsultantId = consultant.ConsultantId!.Value,
            TokenHash = token.Hash,
            CreatedAt = now,
            ExpiresAt = now + Duration.FromDays(identity.Value.InvitationLifetimeDays),
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        var url = $"{frontend.Value.ClientAppUrl.TrimEnd('/')}/invite/{token.Value}";
        return TypedResults.Ok(new CreateInvitationResponse(invitation.Id, url, invitation.ExpiresAt));
    }

    private static async Task<Ok<List<InvitationResponse>>> ListAsync(IdentityModuleDbContext db, IClock clock, CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var invitations = await db.Invitations.OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        return TypedResults.Ok(invitations
            .Select(i => new InvitationResponse(i.Id, i.CreatedAt, i.ExpiresAt, StatusOf(i, now)))
            .ToList());
    }

    private static async Task<Ok<List<ClientResponse>>> ListClientsAsync(IdentityModuleDbContext db, CancellationToken cancellationToken)
    {
        var clients = await (
            from link in db.ClientLinks
            join user in db.Users on link.ParentUserId equals user.Id
            orderby link.LinkedAt descending
            select new ClientResponse(user.Id, user.DisplayName, link.LinkedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(clients);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> AcceptAsync(
        AcceptInvitationRequest request,
        ClaimsPrincipal principal,
        IdentityModuleDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId()!.Value;
        var hash = SecureTokens.Hash(request.Token);
        // The caller is a parent, not the owning consultant, so the lookup is by secret token and bypasses the consultant filter.
        var invitation = await db.Invitations.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
        var now = clock.GetCurrentInstant();
        if (invitation is null)
        {
            return IdentityErrors.InviteNotFound.ToProblem();
        }

        if (invitation.AcceptedAt is not null)
        {
            return IdentityErrors.InviteUsed.ToProblem();
        }

        if (invitation.ExpiresAt <= now)
        {
            return IdentityErrors.InviteExpired.ToProblem();
        }

        if (invitation.ConsultantId == userId)
        {
            return IdentityErrors.InviteOwn.ToProblem();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Same reason as above; the conditional update also makes acceptance single-use under races.
        var claimed = await db.Invitations.IgnoreQueryFilters()
            .Where(i => i.Id == invitation.Id && i.AcceptedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.AcceptedAt, (Instant?)now)
                .SetProperty(i => i.AcceptedByUserId, (Guid?)userId), cancellationToken);
        if (claimed == 0)
        {
            return IdentityErrors.InviteUsed.ToProblem();
        }

        var alreadyLinked = await db.ClientLinks.IgnoreQueryFilters()
            .AnyAsync(l => l.ConsultantId == invitation.ConsultantId && l.ParentUserId == userId, cancellationToken);
        if (!alreadyLinked)
        {
            db.ClientLinks.Add(new ClientLink { ConsultantId = invitation.ConsultantId, ParentUserId = userId, LinkedAt = now });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static string StatusOf(Invitation invitation, Instant now) =>
        invitation.AcceptedAt is not null ? Accepted : invitation.ExpiresAt <= now ? Expired : Pending;
}
```

Modify `src/Modules/CareNest.Identity/IdentityModule.cs`: in `MapIdentityEndpoints`, after `group.MapExternalSignIn();` add:
```csharp
        group.MapAdmin();
        group.MapInvitations();
```

- [ ] **Step 5: Run all tests to verify they pass**

Run: `dotnet test CareNest.slnx`
Expected: all PASS (Integration 52).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add consultant creation, invitations and client links"
```

---

### Task 10: Account deletion

**Files:**
- Create: `src/Modules/CareNest.Identity/Accounts/AccountDeletionService.cs`
- Modify: `src/Modules/CareNest.Identity/Endpoints/ProfileEndpoints.cs`, `src/Modules/CareNest.Identity/IdentityModule.cs`
- Create: `tests/CareNest.Api.IntegrationTests/AccountDeletionTests.cs`

**Interfaces:**
- Consumes: everything that stores personal data: `User` + Identity tables, `MagicLinkToken` (by email and `LinkUserId`), `Invitation` (`ConsultantId`, `AcceptedByUserId`), `ClientLink` (both user FKs).
- Produces: `AccountDeletionService.DeleteAsync(User, CancellationToken)`; `DELETE /api/identity/me` -> 204 and the session is signed out.

- [ ] **Step 1: Write the failing tests**

`tests/CareNest.Api.IntegrationTests/AccountDeletionTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity.Endpoints;
using CareNest.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CareNest.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace CareNest.Api.IntegrationTests;

public class AccountDeletionTests(ApiFactory factory)
{
    [Fact]
    public async Task Deleting_a_parent_removes_the_user_methods_links_and_tokens()
    {
        var email = NewEmail();
        var consultant = await factory.CreateConsultantClientAsync();
        var parent = await factory.SignedInClientAsync(email);
        await parent.PostAsJsonAsync("/api/identity/telegram/complete", new
        {
            auth = TelegramPayload.Create(Random.Shared.NextInt64(1_000_000, long.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture), "Anna", factory.Clock.GetCurrentInstant()),
            mode = "link",
        });
        var invitation = await (await consultant.PostAsync("/api/identity/invitations", null)).ReadAsAsync<CreateInvitationResponse>();
        await parent.PostAsJsonAsync("/api/identity/invitations/accept", new { token = invitation.Url.Split("/invite/")[1] });
        var parentId = (await parent.GetMeAsync()).Id;

        (await parent.DeleteAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await parent.GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        (await db.Users.AnyAsync(u => u.Id == parentId)).ShouldBeFalse();
        (await db.UserLogins.AnyAsync(l => l.UserId == parentId)).ShouldBeFalse();
        (await db.UserRoles.AnyAsync(r => r.UserId == parentId)).ShouldBeFalse();
        (await db.ClientLinks.IgnoreQueryFilters().AnyAsync(l => l.ParentUserId == parentId)).ShouldBeFalse();
        (await db.MagicLinkTokens.AnyAsync(t => t.Email == email)).ShouldBeFalse();
        (await db.Invitations.IgnoreQueryFilters().AnyAsync(i => i.AcceptedByUserId == parentId)).ShouldBeFalse();
        (await (await consultant.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_a_consultant_removes_their_invitations_and_client_links()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var consultantId = (await consultant.GetMeAsync()).Id;
        var invitation = await (await consultant.PostAsync("/api/identity/invitations", null)).ReadAsAsync<CreateInvitationResponse>();
        var parent = await factory.SignedInClientAsync(NewEmail());
        await parent.PostAsJsonAsync("/api/identity/invitations/accept", new { token = invitation.Url.Split("/invite/")[1] });

        (await consultant.DeleteAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        (await db.Invitations.IgnoreQueryFilters().AnyAsync(i => i.ConsultantId == consultantId)).ShouldBeFalse();
        (await db.ClientLinks.IgnoreQueryFilters().AnyAsync(l => l.ConsultantId == consultantId)).ShouldBeFalse();
        (await parent.GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Session_on_another_device_stops_working_after_deletion()
    {
        var email = NewEmail();
        var phone = await factory.SignedInClientAsync(email);
        var laptop = await factory.SignedInClientAsync(email);

        (await phone.DeleteAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await laptop.GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await laptop.PutAsJsonAsync("/api/identity/me", new { displayName = "Ghost", language = "en", timeZone = "UTC" }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test tests/CareNest.Api.IntegrationTests --filter AccountDeletionTests`
Expected: FAIL. `DELETE /api/identity/me` returns 405 Method Not Allowed.

- [ ] **Step 3: Implement**

`src/Modules/CareNest.Identity/Accounts/AccountDeletionService.cs`:
```csharp
using CareNest.Identity.Domain;
using CareNest.Identity.Persistence;
using CareNest.Identity.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CareNest.Identity.Accounts;

// Foreign keys cascade logins, roles, invitations, client links and link tokens; email-keyed tokens have no FK and are removed here.
internal sealed class AccountDeletionService(UserManager<User> users, IdentityModuleDbContext db)
{
    public async Task DeleteAsync(User user, CancellationToken cancellationToken)
    {
        var emails = (await users.GetLoginsAsync(user))
            .Where(login => login.LoginProvider == EmailLogin.Provider)
            .Select(login => login.ProviderKey)
            .ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.MagicLinkTokens.Where(token => emails.Contains(token.Email)).ExecuteDeleteAsync(cancellationToken);
        (await users.DeleteAsync(user)).ThrowIfFailed();
        await transaction.CommitAsync(cancellationToken);
    }
}
```

Modify `src/Modules/CareNest.Identity/Endpoints/ProfileEndpoints.cs`:
- Add `using CareNest.Identity.Accounts;`.
- In `MapProfile`, after the `me.MapPut(...)` line add:
```csharp
        me.MapDelete("", DeleteAsync);
```
- Add the handler:
```csharp
    private static async Task<Results<NoContent, UnauthorizedHttpResult>> DeleteAsync(
        ClaimsPrincipal principal,
        UserManager<User> users,
        AccountDeletionService deletion,
        SignInManager<User> signIn,
        CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        await deletion.DeleteAsync(user, cancellationToken);
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }
```

Modify `src/Modules/CareNest.Identity/IdentityModule.cs`: after `services.AddScoped<AccountService>();` add:
```csharp
        services.AddScoped<AccountDeletionService>();
```

- [ ] **Step 4: Run all tests to verify they pass**

Run: `dotnet test CareNest.slnx`
Expected: all PASS (Integration 55).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add account deletion"
```

---

### Task 11: Architecture tests

**Files:**
- Create: `tests/CareNest.ArchitectureTests/CareNest.ArchitectureTests.csproj`, `tests/CareNest.ArchitectureTests/ArchitectureTests.cs`

**Interfaces:**
- Consumes: `Program` (API assembly), `ApiError` (SharedKernel assembly), `IdentityModule`.
- Produces: tests that fail the build when SharedKernel depends on a module, a module depends on another module or on the host, a module exposes public types outside its root namespace, or SharedKernel/module code uses `DateTime`/`DateTimeOffset`.

- [ ] **Step 1: Create the project**

`tests/CareNest.ArchitectureTests/CareNest.ArchitectureTests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="NetArchTest.Rules" Version="1.3.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CareNest.Api\CareNest.Api.csproj" />
  </ItemGroup>

</Project>
```

Run: `dotnet sln add tests/CareNest.ArchitectureTests`

- [ ] **Step 2: Write the tests**

`tests/CareNest.ArchitectureTests/ArchitectureTests.cs`:
```csharp
using System.Reflection;
using System.Text.RegularExpressions;
using CareNest.SharedKernel.Errors;
using NetArchTest.Rules;

namespace CareNest.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly Host = typeof(Program).Assembly;
    private static readonly Assembly SharedKernel = typeof(ApiError).Assembly;

    // Every CareNest assembly the host references, other than the kernel and Aspire defaults, is a module.
    private static readonly Assembly[] Modules = Host.GetReferencedAssemblies()
        .Where(name => name.Name!.StartsWith("CareNest.", StringComparison.Ordinal)
            && name.Name is not "CareNest.SharedKernel" and not "CareNest.ServiceDefaults")
        .Select(Assembly.Load)
        .ToArray();

    [Fact]
    public void Identity_is_discovered_as_a_module() =>
        Modules.Select(module => module.GetName().Name).ShouldContain("CareNest.Identity");

    [Fact]
    public void SharedKernel_depends_on_no_module_or_host()
    {
        var forbidden = Modules.Select(module => module.GetName().Name!).Append(Host.GetName().Name!).ToArray();

        var result = Types.InAssembly(SharedKernel).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Modules_depend_on_neither_each_other_nor_the_host()
    {
        foreach (var module in Modules)
        {
            var forbidden = Modules.Where(other => other != module).Select(other => other.GetName().Name!).Append(Host.GetName().Name!).ToArray();

            var result = Types.InAssembly(module).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

            result.IsSuccessful.ShouldBeTrue(Describe(result));
        }
    }

    [Fact]
    public void Modules_expose_public_types_only_in_their_root_namespace()
    {
        foreach (var module in Modules)
        {
            var root = module.GetName().Name!;

            var result = Types.InAssembly(module)
                .That().ArePublic()
                .And().DoNotResideInNamespace($"{root}.Persistence.Migrations")
                .Should().ResideInNamespaceMatching($"^{Regex.Escape(root)}$")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Describe(result));
        }
    }

    [Fact]
    public void Kernel_and_module_code_uses_NodaTime_instead_of_DateTime()
    {
        foreach (var assembly in Modules.Prepend(SharedKernel))
        {
            var result = Types.InAssembly(assembly)
                .That().DoNotResideInNamespace($"{assembly.GetName().Name}.Persistence.Migrations")
                .ShouldNot().HaveDependencyOnAny("System.DateTime", "System.DateTimeOffset")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Describe(result));
        }
    }

    private static string Describe(TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
```

- [ ] **Step 3: Run the tests**

Run: `dotnet test tests/CareNest.ArchitectureTests`
Expected: PASS, 5 tests. If `Kernel_and_module_code_uses_NodaTime_instead_of_DateTime` lists a type, fix that type to use NodaTime; do not widen the exclusion.

- [ ] **Step 4: Prove the rules bite**

Temporarily add to `src/Modules/CareNest.Identity/Accounts/DisplayNames.cs` inside the class: `public static DateTime Probe() => DateTime.UtcNow;`
Run: `dotnet test tests/CareNest.ArchitectureTests`
Expected: FAIL naming `CareNest.Identity.Accounts.DisplayNames`. Remove the line and re-run: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test: add architecture tests for module boundaries and NodaTime"
```

---

### Task 12: Backend CI and local smoke run

**Files:**
- Create: `.github/workflows/backend.yml`

**Interfaces:**
- Consumes: the whole solution.
- Produces: a `backend` workflow running build and all tests on every pull request and on pushes to `main`.

- [ ] **Step 1: Write the workflow**

`.github/workflows/backend.yml`:
```yaml
name: backend

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Restore
        run: dotnet restore CareNest.slnx

      - name: Build
        run: dotnet build CareNest.slnx --no-restore --configuration Release

      - name: Test
        run: dotnet test CareNest.slnx --no-build --configuration Release
```

- [ ] **Step 2: Run the same commands locally**

Run:
```bash
dotnet restore CareNest.slnx
dotnet build CareNest.slnx --no-restore --configuration Release
dotnet test CareNest.slnx --no-build --configuration Release
```
Expected: build with 0 warnings; all tests PASS (SharedKernel 16, Identity 31, Integration 55, Architecture 5).

- [ ] **Step 3: Smoke-run the stack with Aspire**

Run: `dotnet run --project src/CareNest.AppHost`
Expected in the Aspire dashboard (URL printed in the console): `postgres`, `email` and `migrations` start; `migrations` finishes with exit code 0; `api` becomes Running and healthy.

Then, with the API URL from the dashboard (`<api>`):
```bash
curl -sk -X POST <api>/api/identity/email/start -H "Content-Type: application/json" -d "{\"email\":\"smoke@example.test\",\"callbackUrl\":\"http://localhost:5173/auth/email\",\"language\":\"ru\",\"timeZone\":\"Europe/Moscow\"}" -o /dev/null -w "%{http_code}\n"
```
Expected: `202`, and the Mailpit UI (the `email` resource's http endpoint in the dashboard) shows a message "Вход в CareNest" containing `http://localhost:5173/auth/email?token=`. Also open `<api>/openapi/v1.json` and confirm `expiresAt` in `CreateInvitationResponse` is `"type": "string", "format": "date-time"`. Stop the AppHost with Ctrl+C.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "ci: build and test the backend on every pull request"
```

---

## Notes for plans 2 and 3

- Plan 2 needs from this plan: the OpenAPI document at `/openapi/v1.json`, `GET /api/identity/providers`, email callback page at `{client}/auth/email?token=` that POSTs to `/api/identity/email/complete`, invite page at `{client}/invite/{token}`, OAuth start URLs, Telegram widget posting to `/api/identity/telegram/complete`, error codes listed in `IdentityErrors` plus `validation_failed`. The e2e fake OAuth provider is registered only in a test environment.
- Plan 3 needs: `Identity:CookieDomain`, `Identity:Providers:*`, `Identity:TelegramBotToken`, `Identity:AdminEmails`, `Email:*` (Azure Communication Services SMTP) in Key Vault; health endpoints are mapped only in Development today (`MapDefaultEndpoints`), so Container Apps probes need a production-safe `/alive`; migrations run as a deploy step (the `CareNest.MigrationService` image or an EF bundle); `Frontend:Origins` and `Frontend:ClientAppUrl` per environment. Stale magic-link tokens accumulate until the background jobs of sub-project 5 add cleanup.
- Carried from the plan 1 final review (2026-09-25), for plan 2: magic-link login CSRF - bind the token to a nonce cookie set at `/email/start` so a link only completes in the browser that requested it; some responses carry no `code` (400 from JSON binding when a `required` member is missing, 500, status-code-page 401/403) - add a shared fallback code or make the frontend handle status-only errors.
- Carried from the plan 1 final review, for plan 3: `ForwardedHeaders` behind Container Apps ingress (otherwise OAuth `redirect_uri` becomes http), Data Protection key persistence (otherwise sessions die on restart), rate limiting on `/email/start` beyond the per-email throttle, `ValidateOnStart` for `Frontend:Origins` and `Frontend:ClientAppUrl`, SMTP TLS / Azure Communication Services mail settings.
- Sessions are validated against the database on every request (`SecurityStampValidatorOptions.ValidationInterval = 0`), so a deleted account or a changed role takes effect immediately; this costs one user lookup and a cookie re-issue per authenticated request - revisit if traffic grows.
- Requested by the owner (2026-09-25), for plan 2: a real local demo - the client and studio apps run under the Aspire AppHost next to the API, plus Playwright scenarios (sign-in, consultant invites a parent, parent accepts) that can be run as a live walkthrough.
