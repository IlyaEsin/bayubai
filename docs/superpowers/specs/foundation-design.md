# CareNest: Foundation (sub-project 1)

Status: approved
Date: 2026-09-24

## 1. Context

CareNest is a platform that automates how an independent consultant works with parents. The first domain is infant and toddler sleep (children 0 to 4 years): a parent pays for a service, fills in an intake questionnaire and a multi-day sleep diary, the consultant analyses it and delivers a personalised sleep/wake schedule, then optionally keeps supporting the parent for several weeks.

The platform is built and validated with one pilot consultant, but the domain is modelled for any consultant from day one. Nothing consultant-specific (questionnaires, rules, materials) lives in code; it is loaded into the consultant's account as data.

The whole product is split into sub-projects, each with its own spec, plan and implementation:

| # | Sub-project | Depends on consultant documents |
|---|---|---|
| 1 | **Foundation** (this spec) | No |
| 2 | Engagements: service catalog, engagement lifecycle, manual payment confirmation, extensions and upgrades, children | No |
| 3 | Questionnaires and diary: template builder, mobile filling, sleep and feeding diary | Yes |
| 4 | Analysis: diary summary, rule engine, draft schedule and weaning plan | Yes, plus real cases |
| 5 | Telegram bot, steps 1-2: notifications, Mini App | No |
| 6 | Scheduling and self-booking: call slots, concurrent support cap | No |
| 7 | Telegram bot, steps 3-4: quick-log buttons, chat relay | No |

## 2. Goal of this sub-project

A deployed, empty but production-grade platform skeleton: a parent and a consultant can sign in, set their profile, and a consultant can invite a parent who then becomes their client. Everything later sub-projects build on (module boundaries, multi-consultant isolation, time handling, i18n, auth, CI, deploy) is in place and enforced by tests.

### Non-goals

- Anything that depends on the consultant's documents: questionnaire content, diary fields, analysis rules, the child profile beyond what sub-project 2 defines.
- Engagements, services, payments, scheduling (sub-projects 2 and 6).
- Background jobs, queues, caching, push notifications (sub-project 5).
- Consultant self-registration: consultants are created by the platform admin until a second consultant exists.
- Native mobile apps: the parent app is a PWA plus a Telegram Mini App; React Native is considered only if a PWA limit is hit in practice.

## 3. Repositories

Two repositories:

- **`carenest`** (public, GitHub): the engine and synthetic demo data only. Public as an engineering portfolio.
- **`carenest-private`** (private, GitHub): source documents, anonymised real cases, and each consultant's templates, rules and materials in the platform's import format. Nothing flows from it into the public repo; the platform receives this data only through import into the consultant's account.

License: none (all rights reserved). The code is viewable, not reusable.

### Public repo layout

```
carenest/
├─ src/
│  ├─ CareNest.AppHost/          .NET Aspire orchestration (local run and Azure deploy model)
│  ├─ CareNest.ServiceDefaults/  Aspire defaults: OpenTelemetry, health checks, resilience
│  ├─ CareNest.Api/              host: startup, DI, route groups, OpenAPI
│  ├─ CareNest.SharedKernel/     ids, errors, clock, language, consultant scoping primitives
│  ├─ CareNest.MigrationService/   applies module migrations (local run and deploy step)
│  └─ Modules/
│     └─ CareNest.Identity/      users, sign-in, roles, profiles, invitations
├─ tests/
│  ├─ CareNest.SharedKernel.Tests/
│  ├─ CareNest.Identity.Tests/
│  ├─ CareNest.Api.IntegrationTests/   WebApplicationFactory + Testcontainers PostgreSQL
│  ├─ CareNest.ArchitectureTests/
│  └─ e2e/                              Playwright smoke
├─ web/                          pnpm workspace
│  ├─ apps/client/               parent app: PWA + Telegram Mini App, mobile-first
│  ├─ apps/studio/               consultant and admin app, laptop-first
│  └─ packages/
│     ├─ api-client/             generated from OpenAPI by orval
│     ├─ ui/                     shared components
│     └─ i18n/                   RU/EN dictionaries
├─ infra/                        Bicep generated from the Aspire model, committed and reviewable
├─ docs/superpowers/{specs,plans}/
├─ .claude/                      CLAUDE.md, rules, skills
└─ .github/workflows/
```

## 4. Backend

- **.NET 10 (LTS, supported to November 2028).** .NET 8 reaches end of support in November 2026 and is not used.
- **ASP.NET Core Minimal APIs.** Each module registers its own route group (`/api/identity/...`).
- **Errors** use ProblemDetails (RFC 9457) with a stable machine-readable `code` (e.g. `identity.invite_expired`). The API never returns human-readable text; clients translate codes.
- **Validation** uses DataAnnotations on request types, run by a shared endpoint filter. The built-in .NET 10 Minimal API validation skips request types declared in module assemblies (verified 2026-09-24), so it is not used.
- **OpenAPI** is generated from code and is the single contract for all clients.
- **Modular monolith.** One process, one database. Each module is its own project with a public interface; modules call each other only through that interface. Architecture tests fail the build if a module references another module's internals. No MediatR.
- **Data: PostgreSQL + EF Core (Npgsql).** One schema and one DbContext with its own migrations per module (`identity` in this sub-project). `jsonb` is available for later flexible templates but not used here.
- **Time: NodaTime** with the Npgsql NodaTime plugin. Moments are `Instant` (UTC); a user's time zone is an IANA id; local times are `LocalDateTime` plus zone. `DateTime` is not used in domain or persistence code. A clock abstraction (`IClock`) is injected so tests control time.
- **Multi-consultant isolation.** Every consultant-owned row carries `ConsultantId`. An EF global query filter scoped to the current consultant is applied to every consultant-owned entity; an integration test proves one consultant cannot read another's data. In this sub-project the only consultant-owned entities are invitations and the parent-consultant link.
- **Observability:** OpenTelemetry via Aspire service defaults, exported to Azure Application Insights in production. **Personal data about parents and children is never written to logs or traces**; log statements use ids only.

## 5. Identity

### Approach

Own authentication on ASP.NET Core Identity with a password-less user store. Managed options were ruled out: Azure AD B2C is closed to new customers, and Entra External ID has no Telegram support and no built-in VK or Yandex.

### Sign-in methods

| Method | Mechanism |
|---|---|
| Google | OIDC |
| Yandex ID | OAuth 2.0 (`AspNet.Security.OAuth.Providers`) |
| VK ID | OAuth 2.1 with PKCE; package compatibility with the current VK ID API is verified during planning, otherwise a custom handler |
| Telegram | Login Widget; payload verified by HMAC with the bot token. Mini App `initData` verification comes in sub-project 5 |
| Email | One-time magic link, valid 15 minutes, single use; sent over SMTP via Brevo (Mailpit locally); Azure Communication Services Email was dropped because Microsoft retires it on 30 September 2028 |

No paid methods (SMS, WhatsApp) and no Apple sign-in.

### Sessions

- Web clients (client, studio, Mini App) use HttpOnly, Secure, SameSite=Lax cookies. No tokens in JavaScript.
- Bearer tokens are added only when a native client appears; ASP.NET Core supports both schemes side by side.
- Cookies require the apps and API on one registrable domain (`app.`, `studio.`, `api.` subdomains of a purchased domain). Default Azure hostnames are on different sites and are not used in production.

### Accounts and linking

- One user can have several sign-in methods.
- A method is linked only while the user is signed in and explicitly adds it.
- Accounts are never merged automatically by matching email (Telegram provides no email, and merging on an unverified email enables account takeover).

### Roles

| Role | How it is created |
|---|---|
| Parent | Self-registers on first sign-in |
| Consultant | Created by the platform admin only |
| Admin | Seeded from configuration |

A user may hold more than one role.

### Invitations

1. A consultant creates an invitation and gets a link with a single-use token (default validity 14 days).
2. The parent opens the link and signs in with any method (registering if new).
3. The parent is linked to that consultant as their client.

An expired or used token returns `identity.invite_expired` / `identity.invite_used`. Sub-project 2 uses this mechanism when a consultant creates an engagement for a parent.

### Profile

Display name, UI language (`ru` or `en`), time zone (detected from the browser on first sign-in, editable).

### Account deletion

A user can delete their account. Deletion removes the user, their sign-in methods and their links to consultants. Later sub-projects must extend deletion to every piece of personal data they add; this is a standing rule in `CLAUDE.md`.

## 6. Frontend and languages

- **Stack (both apps):** Vite, React, TypeScript strict, pnpm workspace, TanStack Router, TanStack Query with hooks generated by orval, shadcn/ui (Radix + Tailwind).
- **Parent app (`client`):**
  - Mobile-first layout, touch targets of at least 44px, primary actions within thumb reach.
  - Dark theme from day one, following the system setting by default: parents use it at night.
  - PWA via `vite-plugin-pwa`: manifest, icon, installable. The service worker caches the app shell only; no offline data in this sub-project.
- **Consultant app (`studio`):** laptop-first, usable on a phone. Also hosts admin screens for users with the admin role.
- **i18n:**
  - i18next with RU and EN dictionaries in `packages/i18n`, split by feature namespace.
  - Plurals through `Intl.PluralRules` (Russian has three forms).
  - Language comes from the profile, or from the browser before sign-in.
  - Dates and times are formatted with `Intl` in the user's time zone.
  - Server-sent emails are localised on the server in the recipient's language.
  - Consultant-authored content (questionnaires, materials) is versioned per language by the consultant in later sub-projects; it is never machine-translated.

### Screens in this sub-project

| App | Screens |
|---|---|
| client | Sign-in (provider buttons + email), profile, accept invitation, empty home ("no active consultations") |
| studio | Sign-in, empty consultant dashboard, create invitation, admin: create consultant |

## 7. Tooling, CI and deploy

### Local development

`dotnet run` on `CareNest.AppHost` starts PostgreSQL (container), the API, both Vite apps and Mailpit, with the Aspire dashboard for logs and traces.

### Claude tooling (written for this repo)

- **`CLAUDE.md`** covering stack, commands, module boundaries, and standing rules:
  - consultant filter on consultant-owned data;
  - NodaTime only;
  - no UI text outside i18n;
  - no personal data in logs;
  - account deletion covers all personal data.
- **Tracking:** GitHub Issues. An issue `#12` is keyed `cn-12` in branches (`feature/cn-12-...`) and in spec and plan file names (`docs/superpowers/specs/cn-12-<description>.md`). Documents without an issue use `<description>.md`.
- **`build-test` skill:** backend `dotnet build` / `dotnet test` and frontend `pnpm lint` / `typecheck` / `test`, scoped to what changed.
- **`how-to-test` skill:** Playwright scenarios per issue in `tests/e2e/how-to-test/cn-<n>/` (inside the e2e project, so the scenarios reuse its Playwright install and helpers), run output gitignored.
- **`REVIEW.md`:** project review checklist (the standing rules above plus module boundaries and migration safety).

### CI (GitHub Actions, every PR)

| Area | Checks |
|---|---|
| Backend | Build; unit, integration (Testcontainers) and architecture tests |
| Frontend | Lint, typecheck, Vitest |
| Contracts | i18n key parity (RU and EN must have identical keys); generated API client must match the current OpenAPI document |
| Hygiene | Forbidden-reference guard (the denylist of third-party names is stored as a GitHub Actions secret, so the list itself never appears in the repo); gitleaks; CodeQL; Dependabot |
| E2E | Playwright smoke: email magic-link sign-in, profile update, invitation acceptance. OAuth providers are replaced by a fake provider in the test environment |

`main` is protected: changes land only through PRs with green CI.

### Deploy (Azure)

- **Runtime:** Azure Container Apps for the API, Azure Database for PostgreSQL Flexible Server (Burstable B1ms), Azure Static Web Apps for both frontends, EU region.
- **Pipeline:**
  - A merge to `main` deploys to production via `aspire deploy` (Aspire 13 no longer recommends azd); `infra/` is the committed output of `aspire publish`.
  - No staging environment initially.
  - Static Web Apps preview environments are not used: they live on `*.azurestaticapps.net`, a different site from `api.<domain>`, so sign-in cannot work there.
  - Database migrations run as a separate deploy step (the MigrationService image as a Container Apps job the deploy workflow starts and waits for; migrations must be backward compatible because they run after the new API revision), never at application startup.
- **Secrets and access:**
  - GitHub authenticates to Azure with OIDC federated credentials; no Azure secrets in GitHub.
  - Application secrets (OAuth client secrets, bot token) live in Key Vault, read by Container Apps via managed identity.
  - Locally they live in .NET user-secrets.
  - GitHub secret-scanning push protection is enabled.
- **Operations:** automatic PostgreSQL backups with 7-day retention; an Azure budget alert at 40 USD per month (the API keeps one warm replica; about 31 USD per month in total).
- **Portability:** everything runs in containers, so moving to a Russian VPS (if Russian data-residency law or reachability from Russia becomes a problem) is a redeploy, not a rewrite. To keep it that way, application code uses no Azure SDK, only standard interfaces:
  - secrets reach the app as configuration (environment variables filled from Key Vault references by Container Apps), never through a Key Vault client in code;
  - email goes over SMTP (Brevo SMTP relay in production), so another provider is a settings change;
  - telemetry goes through OpenTelemetry; the Azure Monitor exporter is switched on only in `CareNest.ServiceDefaults` when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set;
  - Azure-specific parts live only in the AppHost publish model, `infra/` and the deploy workflow. An architecture test fails if a module, SharedKernel or the API references an `Azure.*` package.

## 8. Acceptance criteria

1. `dotnet run` on the AppHost brings up the full stack locally with no manual steps beyond user-secrets for real OAuth providers.
2. A parent can sign in with each of Google, Yandex ID, VK ID, Telegram and email magic link against real providers in production.
3. A signed-in user can add a second sign-in method and then sign in with either.
4. A consultant (created by the admin) can create an invitation; a new parent opening it ends up linked to that consultant; a used or expired link shows a translated error.
5. An integration test proves consultant A cannot read consultant B's invitations or clients.
6. Profile language switch changes the UI language immediately; times display in the profile time zone.
7. The parent app installs as a PWA on Android and iOS and respects the system dark theme.
8. Account deletion removes the user and all their links; a test covers it.
9. All CI checks in section 7 run on every PR and block merge on failure.
10. The production deploy is reproducible from `main` with `aspire deploy`, and the budget alert exists.

## 9. Open items for planning

- Resolved in plan 1: `AspNet.Security.OAuth.VkId` 10.0.0 works with the current VK ID API.
- Resolved in plan 3: the domain is bought during delivery (Task 7). Free Static Web Apps with custom domains works with the cookie layout because the session cookie is host-only on `api.<domain>` and `app.`/`studio.` are the same site.
