# Rename CareNest to Bayubai

Status: approved design, 2026-09-30. Owner decisions recorded in the brainstorming session of that date.

## Goal

The product brand is **Bayubai** (Russian: **Баюбай**), with the domain `bayubai.com` (`bayubai.app` held as protection). The owner does not want the brand to differ from the repository and code names, so everything named CareNest is renamed before the first deploy. After the first deploy some names (Key Vault, resource group, database) become expensive or impossible to change, which is why the rename comes before `deploy/bootstrap.sh` (plan 3, Task 7 Step 4).

Success: `git grep -i carenest` finds the old name only in the finished plans 1 and 2, in the one-line rename notes, and in this spec; the backend, web and e2e suites pass; `infra/` is regenerated with the new names; the product shows "Баюбай" to Russian users and "Bayubai" to English users.

## Decisions

| Topic | Decision |
|---|---|
| Brand in the UI and emails | "Баюбай" in Russian, "Bayubai" in English |
| Short prefix | `bb`: issues, branches `feature/bb-<n>-<slug>`, spec and plan files `bb-<n>-<slug>.md`, how-to-test folders `tests/e2e/how-to-test/bb-<n>/`, test names `bb-<n>: ...` |
| Approach | One pull request, mechanical: `git mv` for paths, scripted text replacement, then manual edits of human-facing text, then regeneration |
| Documentation | Living documents renamed; finished plans 1 and 2 keep the old name with a one-line note; plan 3 renames Tasks 7-10 only (Tasks 1-6 are history) |
| GitHub | Repositories already renamed by the owner to `IlyaEsin/bayubai` (public) and `IlyaEsin/bayubai-private`; local `origin` already updated |
| Local folder | `C:\code\carenest` is renamed by the owner after this PR is merged, together with moving the Claude Code project memory |

## Technical names

| Now | After |
|---|---|
| `CareNest.slnx` | `Bayubai.slnx` |
| projects and folders `CareNest.Api`, `.AppHost`, `.MigrationService`, `.ServiceDefaults`, `.SharedKernel`, `Modules/CareNest.Identity`, `tests/CareNest.*` | `Bayubai.*`, same layout |
| namespaces `CareNest.*` | `Bayubai.*` |
| `Projects.CareNest_Api`, `Projects.CareNest_MigrationService` (Aspire generated) | `Projects.Bayubai_Api`, `Projects.Bayubai_MigrationService` |
| EF migrations (`*.Designer.cs`, model snapshot) | namespaces and type names only; migration ids and SQL unchanged; schema `identity` unchanged |
| npm packages `@carenest/api-client`, `@carenest/i18n`, `@carenest/ui`, `@carenest/client`, `@carenest/studio`, and `carenest-e2e` | `@bayubai/*`, `bayubai-e2e`; `web/pnpm-lock.yaml` regenerated |
| database and connection string name `carenest`, design-time `carenest_design` | `bayubai`, `bayubai_design` |
| Azure PostgreSQL admin user `carenest` | `bayubai` |
| Data Protection application name `CareNest` | `Bayubai` (signs local sessions out once; no production data exists) |
| `CARENEST_UPDATE_OPENAPI` | `BAYUBAI_UPDATE_OPENAPI` |
| OpenAPI `info.title` `CareNest.Api \| v1` | `Bayubai.Api \| v1`; `openapi.json` and the generated client are regenerated; no operation or schema changes |
| local demo admin `admin@carenest.local`, default `From` `no-reply@carenest.local`, test bot `carenest_bot` | `admin@bayubai.local`, `no-reply@bayubai.local`, `bayubai_bot` |
| resource group `rg-carenest`, Key Vault `kv-carenest-<6 hex>`, deploy app `carenest-deploy`, `repo="IlyaEsin/carenest"` in `deploy/bootstrap.sh` | `rg-bayubai`, `kv-bayubai-<6 hex>`, `bayubai-deploy`, `repo="IlyaEsin/bayubai"` |
| Container Apps environment `cn`, Static Web Apps `cn-client` and `cn-studio`, budget `cn-monthly` | `bb`, `bb-client`, `bb-studio`, `bb-monthly` |
| `infra/` | regenerated with `dotnet aspire publish`; its diff contains only the renames above |

The local PostgreSQL container keeps its data volume; the new database name means the local stack starts on a fresh, empty `bayubai` database, and the old `carenest` database stays unused in the volume.

## Human-facing text

| Where | Russian | English |
|---|---|---|
| i18n `common.appName` | Баюбай | Bayubai |
| i18n `auth.title` | Вход в Баюбай | Sign in to Bayubai |
| magic-link email subject and body (`MagicLinkEmail.cs`) | Вход в Баюбай, "Чтобы войти в Баюбай, ..." | Sign in to Bayubai, "To sign in to Bayubai, ..." |

Static strings shown before i18n loads, one value for every language:
- email `From` display name in the Azure model: `Баюбай <no-reply@{domain}>`;
- client `index.html` title and PWA `name`/`short_name`: `Баюбай`;
- studio `index.html` title: `Баюбай Студия`.

e2e assertions on these texts change with them (for example the sign-in heading).

## Documentation

- Renamed throughout: `CLAUDE.md` (including the `bb` prefix in Tracking and the how-to-test command), `docs/tech-overview.md`, `deploy/README.md`, `REVIEW.md`, `.claude/skills/build-test/SKILL.md`, `.claude/skills/how-to-test/SKILL.md`, `docs/superpowers/specs/foundation-design.md`.
- `docs/superpowers/plans/2026-09-24-foundation-backend.md` and `2026-09-25-foundation-web.md`: unchanged except a first-line note "The project was renamed to Bayubai on 2026-09-30 (`docs/superpowers/specs/rename-to-bayubai.md`); names below are historical."
- `docs/superpowers/plans/2026-09-27-foundation-delivery.md`: the same note, and Tasks 7 to 10 updated to the new names (repository, resource names, Key Vault, domain `bayubai.com`, `From` address).

## Out of scope

- Buying domains, Azure, Brevo: owner actions already in progress (plan 3, Task 7).
- Renaming the local folder and moving Claude Code memory: after merge, by the owner.
- Git history: old commits keep the old name.

## Verification

1. `dotnet build Bayubai.slnx`: 0 warnings; `dotnet test Bayubai.slnx`: all pass, including the architecture tests (assembly names change with the projects) and the OpenAPI contract test after regeneration.
2. No new EF migration is needed: `dotnet ef migrations has-pending-model-changes` (or an equivalent `migrations add` dry check) reports no changes.
3. In `web/`: `pnpm install`, `pnpm generate:api`, `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build`.
4. In `tests/e2e/`: `pnpm test` against the renamed AppHost.
5. `dotnet aspire publish` twice: identical output, and the `infra/` diff shows only renamed names.
6. `bash -n` on the deploy scripts; actionlint on the workflows.
7. The forbidden-references guard with the owner's list passes (run by CI on the PR).
8. `git grep -i carenest` lists only the places named in Success.
