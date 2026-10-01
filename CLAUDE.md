# Bayubai

Bayubai (Russian: Баюбай) is a platform that automates an independent consultant's work with parents; the first domain is infant and toddler sleep. Specs: `docs/superpowers/specs`. Plans: `docs/superpowers/plans`.

## Stack

- .NET 10, ASP.NET Core Minimal APIs, EF Core + PostgreSQL (Npgsql), NodaTime, ASP.NET Core Identity (password-less), .NET Aspire 13
- Tests: xUnit v3, Shouldly, Testcontainers (PostgreSQL), NetArchTest
- Frontend: pnpm 10 workspace in `web/` - Vite, React 19, TypeScript 6 (not 7: typescript-eslint), TanStack Router and Query, orval, i18next, Tailwind 4, Vitest + MSW
- E2E: Playwright in `tests/e2e/` against the Aspire AppHost

## Commands

- Build: `dotnet build Bayubai.slnx`
- All tests (Docker must be running): `dotnet test Bayubai.slnx`
- One project: `dotnet test tests/Bayubai.Identity.Tests`
- Run locally (PostgreSQL, Mailpit, migrations, API, both web apps, Aspire dashboard): `dotnet run --project src/Bayubai.AppHost` - client http://localhost:5173, studio http://localhost:5174, Mailpit http://localhost:8025, demo admin `admin@bayubai.local`, "test sign-in" provider on
- Web (from `web/`): `pnpm install`, `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build`
- After an intended API contract change: `BAYUBAI_UPDATE_OPENAPI=1 dotnet test tests/Bayubai.Api.IntegrationTests`, then `pnpm generate:api` in `web/`; commit both
- E2E (from `tests/e2e/`): `pnpm test` (starts the AppHost unless it is running), `pnpm walkthrough` for a visible demo
- How-to-test scenario for an issue (from `tests/e2e/`): `pnpm how-to-test bb-<n>` (skill `how-to-test`)
- After changing the AppHost's Azure model: `dotnet tool restore`, `dotnet aspire publish --apphost src/Bayubai.AppHost/Bayubai.AppHost.csproj --output-path infra`; commit `infra/`
- Deploy: merge to `main` (`.github/workflows/deploy.yml`); setup, secrets, providers and domains: `deploy/README.md`
- New Identity migration: `dotnet ef migrations add <Name> --project src/Modules/Bayubai.Identity --output-dir Persistence/Migrations --namespace Bayubai.Identity.Persistence.Migrations`
- Try the API in a browser (Development): `<api>/scalar`; scripted scenario: `src/Bayubai.Api/Bayubai.Api.http`

## Layout and module boundaries

- `src/Bayubai.Api` is the host only: startup, middleware, module registration. No business logic.
- `src/Bayubai.SharedKernel` holds primitives every module may use; it depends on no module.
- `src/Modules/Bayubai.<Module>` is one project per module. Only types in the module's root namespace are public (the `<Module>Module` entry point and contracts); everything else is `internal`. Modules never reference each other; a cross-module call goes through a public interface in the callee's root namespace. Architecture tests enforce this.
- Each module owns one PostgreSQL schema and one DbContext with its own migrations.
- Migrations never run at API startup: `Bayubai.MigrationService` applies them locally, a deploy step applies them in production.
- `src/Bayubai.AppHost`: `LocalStack.cs` is the local run, `AzureDeployment.cs` the production model; `infra/` is its generated Bicep (never edit by hand); `deploy/` holds the bootstrap and deploy scripts.
- `site/` is the public page at `bayubai.com`, served by the Cloudflare Worker `bayubai-site`, deployed from `main` (no build step; `deploy/README.md`).
- `web/packages/api-client` is generated from `openapi.json` (never edit `src/generated/`), `web/packages/i18n` holds every UI string, `web/packages/ui` holds shared components and flows; `web/apps/client` (parent PWA) and `web/apps/studio` (consultant and admin) hold routes and app-specific screens only.

## Standing rules (checked in review)

1. Consultant-owned data: every row a consultant owns implements `IConsultantOwned`, and the module DbContext calls `ApplyConsultantQueryFilters`. Any `IgnoreQueryFilters()` needs a comment saying why the query is safe.
2. Time: NodaTime only (`Instant`, `LocalDateTime` plus an IANA zone id, injected `IClock`). No `DateTime` or `DateTimeOffset` in domain or persistence code.
3. No UI text outside the i18n dictionaries. The API returns stable error codes (`code` in ProblemDetails, e.g. `identity.invite_expired`), never human-readable text.
4. No personal data about parents or children in logs or traces. Log ids only.
5. Account deletion removes all personal data. Every new table holding personal data must be covered by account deletion and by its test.

## Conventions

- Errors: `ApiError(code, status)` plus `.ToProblem()`. Validation: DataAnnotations on the request type plus `.WithRequestValidation<T>()` on the endpoint (the built-in .NET 10 validation does not see types declared in module assemblies).
- Tests: HTTP behaviour in `Bayubai.Api.IntegrationTests` (real PostgreSQL via Testcontainers, fake clock, fake email sender); pure logic in the module's unit test project.
- Tracking: GitHub Issues. Branches `feature/bb-<issue>-<slug>`; spec and plan files `bb-<issue>-<slug>.md` (no issue: `<slug>.md`).
- Writing: plain hyphen `-`, never em or en dashes. Code comments: one dry sentence, why not what.
- Secrets never enter the repo. Local: `dotnet user-secrets`. Production: Key Vault.
- Technology overview for humans: `docs/tech-overview.md` (Russian). When a change adopts a new technology, add a section there in the same change.
- Every endpoint gets `.WithName("<Operation>")`: it becomes the generated hook name (`useGetMe`).
- A new error code needs RU and EN text in `web/packages/i18n/src/locales/*/errors.json`; a test fails otherwise.
- Times in the UI: `formatDateTime` in the viewer's profile zone; another person's local time with `formatLocalTime` in their zone, labelled with the zone id.
- Review: `REVIEW.md` is the checklist for every PR; the `build-test` skill picks the checks for what changed.
- Migrations run after the new API revision is live, so they must be backward compatible (see `REVIEW.md`).
