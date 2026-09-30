# Rename CareNest to Bayubai Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every CareNest name in code, packages, configuration, Azure model, deploy scripts, UI text and living docs becomes Bayubai (Russian UI: Баюбай), in one pull request, before the first Azure deploy.

**Architecture:** Mechanical rename in layers: `git mv` for paths, scripted text replacement per layer, then hand edits of human-facing text and regeneration of derived files (`openapi.json`, generated client, `pnpm-lock.yaml`, `infra/`). Each task leaves the repository building and its layer's tests green.

**Tech Stack:** .NET 10, EF Core 10 (`dotnet ef` 10.0.12 local tool), Aspire 13.5.4 (`dotnet aspire` local tool), pnpm 10 workspace in `web/`, Playwright in `tests/e2e/`, Bash (Git Bash on Windows).

**Spec:** `docs/superpowers/specs/rename-to-bayubai.md` (read it first; its tables are the source of truth for every name).

**Branch:** `feature/rename-to-bayubai` already exists and holds the spec commit. Work there; push with `git push -u origin feature/rename-to-bayubai` once, after the final review. `main` is protected by a ruleset: changes land only through a PR with the five required checks green. The GitHub repository is already `IlyaEsin/bayubai` and the local `origin` already points to it.

**Environment notes (Windows):** run shell steps in Git Bash. `core.autocrlf=true`: `sed -i` may rewrite line endings in the working copy; git normalizes on commit, so check `git diff --stat` shows only the expected lines, not whole files. Docker must be running for integration and e2e tests. Before building after the path renames, delete stale `bin/` and `obj/` folders (Step commands below do it).

## Global Constraints

- Brand: Russian UI and emails "Баюбай", English "Bayubai". Code, packages, paths and Azure names use `Bayubai`/`bayubai` (Latin).
- Short prefix `bb`: branches `feature/bb-<n>-<slug>`, spec and plan files `bb-<n>-<slug>.md`, how-to-test folders `tests/e2e/how-to-test/bb-<n>/`, test names `bb-<n>: ...`; Azure short names `bb` (Container Apps environment), `bb-client`, `bb-studio`, `bb-monthly`.
- Static strings (one value for all languages): email `From` display name `Баюбай`; client `index.html` title and PWA `name`/`short_name` `Баюбай`; studio `index.html` title `Баюбай Студия`.
- Local values: database and connection string name `bayubai`, design-time database `bayubai_design`, demo admin `admin@bayubai.local`, default `From` `Баюбай <no-reply@bayubai.local>`, test Telegram bot `bayubai_bot`, update variable `BAYUBAI_UPDATE_OPENAPI`, Data Protection application name `Bayubai`.
- Azure/deploy values: PostgreSQL admin user `bayubai`, resource group `rg-bayubai`, Key Vault `kv-bayubai-<6 hex>`, deploy app `bayubai-deploy`, `repo="IlyaEsin/bayubai"`.
- EF migrations: only namespaces and CLR type names change; migration ids, SQL and the `identity` schema stay; no new migration.
- OpenAPI: only `info.title` changes to `Bayubai.Api | v1`; no operation or schema changes.
- Finished plans 1 and 2 keep the old name plus a one-line note; plan 3 renames Tasks 7-10 only.
- Text uses the plain hyphen `-`, never em or en dashes. Code comments: one dry sentence, why not what.
- Never write the owner's employer names or the pilot consultant's personal name anywhere (the CI `forbidden-references` check fails on them).

## Review Focus

1. **Stale generated infra next to the new one.** `aspire publish` writes folders per resource (`infra/cn/`, `infra/cn-acr/`); after renaming the environment to `bb`, the old folders would stay and be deployed or drift. Pinned in Task 3: delete `infra/` before publishing, then assert no `cn`-named file remains.
2. **EF model snapshot out of step with the renamed CLR types.** The next `migrations add` would then generate a spurious migration. Pinned in Task 1: `dotnet ef migrations has-pending-model-changes` must report no changes.
3. **Connection string name mismatch between the API and the AppHost.** Integration tests set `ConnectionStrings:bayubai` themselves, so they would not notice the AppHost using another name; the local stack and Azure would fail at runtime. Pinned in Task 1 (grep that all three values agree) and Task 4 (full e2e run on the local stack).
4. **Latin "Bayubai" left in Russian text after the blanket replacement.** Pinned in Tasks 1 and 2: grep the Russian email strings and `ru` locale for `Bayubai` and expect nothing.
5. **Deploy identity bound to the old repository name.** The federated credential subject is built from `repo` in `bootstrap.sh`; a stale value breaks every GitHub login to Azure. Pinned in Task 3: grep asserts `repo="IlyaEsin/bayubai"`.

---

## File map

```
CareNest.slnx -> Bayubai.slnx
src/CareNest.{Api,AppHost,MigrationService,ServiceDefaults,SharedKernel}/ -> src/Bayubai.*/  (csproj, .http and every file inside)
src/Modules/CareNest.Identity/ -> src/Modules/Bayubai.Identity/  (incl. Persistence/Migrations/*.cs)
tests/CareNest.{Api.IntegrationTests,ArchitectureTests,Identity.Tests,SharedKernel.Tests}/ -> tests/Bayubai.*/
.github/workflows/backend.yml, deploy.yml
web/package.json, web/apps/{client,studio}/{package.json,index.html,vite.config.ts,src/**}, web/packages/{api-client,i18n,ui}/**, web/pnpm-lock.yaml
web/packages/api-client/openapi.json, src/generated/** (regenerated)
src/Bayubai.AppHost/{AzureDeployment.cs,LocalStack.cs,Templates/web.bicep}, infra/** (regenerated)
deploy/{bootstrap.sh,budget.bicep,README.md}
tests/e2e/{package.json,playwright.config.ts,support/urls.ts,specs/01-sign-in-and-profile.spec.ts}
CLAUDE.md, REVIEW.md, .claude/skills/{build-test,how-to-test}/SKILL.md, docs/tech-overview.md, docs/superpowers/specs/foundation-design.md
docs/superpowers/plans/2026-09-24-foundation-backend.md, 2026-09-25-foundation-web.md, 2026-09-27-foundation-delivery.md
```

---

### Task 1: .NET solution, projects and namespaces

**Files:**
- Rename: `CareNest.slnx`, every folder and file under `src/` and `tests/` (except `tests/e2e/`) whose path contains `CareNest`
- Modify: every text file under `src/`, `tests/` (except `tests/e2e/`), `Bayubai.slnx`, `.github/workflows/backend.yml`, `.github/workflows/deploy.yml`
- Hand edits: `src/Modules/Bayubai.Identity/Email/MagicLinkEmail.cs`, `src/Modules/Bayubai.Identity/Email/EmailOptions.cs`, `tests/Bayubai.Identity.Tests/EmailTests.cs`, `tests/Bayubai.Api.IntegrationTests/EmailSignInTests.cs`
- Regenerate: `web/packages/api-client/openapi.json`, `web/packages/api-client/src/generated/**`, `infra/**`

**Interfaces:**
- Produces: `Bayubai.slnx`; projects `src/Bayubai.AppHost/Bayubai.AppHost.csproj`, `src/Bayubai.Api`, `src/Bayubai.MigrationService`, `src/Modules/Bayubai.Identity`; `IdentityModule.ConnectionStringName = "bayubai"`; `OpenApiContractTests` reads `BAYUBAI_UPDATE_OPENAPI`; OpenAPI title `Bayubai.Api | v1`. Tasks 3 and 4 use the AppHost path; Task 2 consumes the regenerated `openapi.json`.

- [ ] **Step 1: Rename paths**

```bash
cd /c/code/carenest
for d in src/CareNest.* src/Modules/CareNest.* tests/CareNest.*; do git mv "$d" "${d//CareNest/Bayubai}"; done
git ls-files | grep 'CareNest' | grep -v '^docs/' | while read -r f; do mkdir -p "$(dirname "${f//CareNest/Bayubai}")"; git mv "$f" "${f//CareNest/Bayubai}"; done
find src tests -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
git ls-files | grep -i carenest | grep -v '^docs/'
```

Expected: the last command prints nothing (every tracked path outside `docs/` is renamed; `Bayubai.slnx` is at the root).

- [ ] **Step 2: Replace names in text**

```bash
git grep -l -z -I -i carenest -- src tests Bayubai.slnx .github/workflows/backend.yml .github/workflows/deploy.yml ':!tests/e2e' \
  | xargs -0 sed -i -e 's/CareNest/Bayubai/g' -e 's/CARENEST/BAYUBAI/g' -e 's/carenest/bayubai/g'
git grep -n -i carenest -- src tests Bayubai.slnx .github ':!tests/e2e'
```

Expected: the grep prints nothing. `CareNest_Api` in the AppHost becomes `Bayubai_Api` (Aspire generates `Projects.Bayubai_Api` from the new project name).

- [ ] **Step 3: Russian text and the default sender**

The blanket replacement wrote the Latin name into Russian strings. Fix by hand:
- `src/Modules/Bayubai.Identity/Email/MagicLinkEmail.cs`: Russian subject `"Вход в Баюбай"`, Russian body `$"Чтобы войти в Баюбай, откройте ссылку (она действует 15 минут):\n..."` (keep the rest of the line as it is); English stays `"Sign in to Bayubai"` / `"To sign in to Bayubai, ..."`.
- `src/Modules/Bayubai.Identity/Email/EmailOptions.cs`: `public string From { get; set; } = "Баюбай <no-reply@bayubai.local>";`
- `tests/Bayubai.Identity.Tests/EmailTests.cs`: `message.Subject.ShouldBe("Вход в Баюбай");` (the English assertion stays `"Sign in to Bayubai"`).
- `tests/Bayubai.Api.IntegrationTests/EmailSignInTests.cs`: `factory.Emails.SentTo(email)[0].Subject.ShouldBe("Вход в Баюбай");`
- Any other test that asserts the `From` value: change it to the new default.

Check (Review Focus 4):

```bash
git grep -n -E 'Вход в Bayubai|войти в Bayubai' -- src tests
```

Expected: nothing.

- [ ] **Step 4: Build and run the fast suites**

```bash
dotnet build Bayubai.slnx
dotnet test tests/Bayubai.SharedKernel.Tests
dotnet test tests/Bayubai.Identity.Tests
dotnet test tests/Bayubai.ArchitectureTests
```

Expected: build 0 warnings, 0 errors; SharedKernel 22, Identity 41, Architecture 6 passed.

- [ ] **Step 5: The EF model has no pending changes (Review Focus 2)**

```bash
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/Modules/Bayubai.Identity
```

Expected: `No changes have been made to the model since the last migration.` If it reports changes, the snapshot or a `.Designer.cs` still carries an old type name; fix the text, never add a migration.

- [ ] **Step 6: Connection string names agree (Review Focus 3)**

```bash
git grep -n -E 'ConnectionStringName|AddDatabase\(|ConnectionStrings:' -- src tests
```

Expected: `ConnectionStringName = "bayubai"`, `AddDatabase("bayubai")` in both `LocalStack.cs` and `AzureDeployment.cs`, `ConnectionStrings:bayubai` in `ApiFactory.cs`.

- [ ] **Step 7: OpenAPI title, generated client, integration tests**

```bash
dotnet test tests/Bayubai.Api.IntegrationTests
```

Expected: one failure, `OpenApiContractTests`, whose diff is only `info.title` (`CareNest.Api | v1` -> `Bayubai.Api | v1`). Then:

```bash
BAYUBAI_UPDATE_OPENAPI=1 dotnet test tests/Bayubai.Api.IntegrationTests
git diff web/packages/api-client/openapi.json
cd web && pnpm generate:api && cd ..
dotnet test tests/Bayubai.Api.IntegrationTests
```

Expected: the `openapi.json` diff is the single title line; `pnpm generate:api` (still `--filter @carenest/api-client` until Task 2) changes only the title in generated file headers; the final run passes all 80.

- [ ] **Step 8: Regenerate infra**

```bash
dotnet aspire publish --apphost src/Bayubai.AppHost/Bayubai.AppHost.csproj --output-path infra --non-interactive --nologo
git diff --stat -- infra
```

Expected: `Pipeline succeeded`; the diff renames `carenest` to `bayubai` in the database, connection-string and `postgres_user` names, and the `From` display name reads `Bayubai` (Task 3 changes it to `Баюбай` and the environment from `cn` to `bb`); nothing else changes.

- [ ] **Step 9: Commit**

```bash
git add -A src tests Bayubai.slnx .github/workflows web/packages/api-client infra
git status --short
git commit -m "refactor: rename the .NET solution, projects and namespaces to Bayubai"
```

`git status --short` must show no unstaged leftovers except files other tasks own (none expected).

---

### Task 2: Web workspace packages and texts

**Files:**
- Modify: `web/package.json`, `web/apps/{client,studio}/package.json`, `web/packages/{api-client,i18n,ui}/package.json`, every `web/**/*.{ts,tsx,css}` importing `@carenest/*`, `web/packages/api-client/orval.config.ts`, `web/apps/{client,studio}/vite.config.ts`, `web/apps/{client,studio}/index.html`, `web/packages/i18n/src/locales/{ru,en}/{common,auth}.json`, `web/packages/ui/src/auth/{SignInPanel,TelegramLoginButton}.test.tsx`
- Regenerate: `web/pnpm-lock.yaml`

**Interfaces:**
- Consumes: `web/packages/api-client/openapi.json` with title `Bayubai.Api | v1` (Task 1).
- Produces: packages `@bayubai/api-client`, `@bayubai/i18n`, `@bayubai/ui`, `@bayubai/client`, `@bayubai/studio`; root `bayubai-web`; script `generate:api` = `pnpm --filter @bayubai/api-client generate`. Task 5 documents these.

- [ ] **Step 1: Replace package names and code identifiers**

```bash
cd /c/code/carenest
git grep -l -z -I -i carenest -- web ':!web/pnpm-lock.yaml' \
  | xargs -0 sed -i -e 's/@carenest\//@bayubai\//g' -e 's/carenest-web/bayubai-web/g' -e 's/carenest_bot/bayubai_bot/g' \
     -e 's/^  carenest: {/  bayubai: {/' -e 's/src\/CareNest\.Api/src\/Bayubai.Api/g'
git grep -n -i carenest -- web ':!web/pnpm-lock.yaml'
```

Expected: the grep now lists only human-facing text: the two `index.html` titles, the client PWA `name`/`short_name`, and the four locale values.

- [ ] **Step 2: Human-facing text**

- `web/packages/i18n/src/locales/ru/common.json`: `"appName": "Баюбай",`
- `web/packages/i18n/src/locales/en/common.json`: `"appName": "Bayubai",`
- `web/packages/i18n/src/locales/ru/auth.json`: `"title": "Вход в Баюбай",`
- `web/packages/i18n/src/locales/en/auth.json`: `"title": "Sign in to Bayubai",`
- `web/apps/client/index.html`: `<title>Баюбай</title>`
- `web/apps/client/vite.config.ts`: `name: 'Баюбай',` and `short_name: 'Баюбай',`
- `web/apps/studio/index.html`: `<title>Баюбай Студия</title>`

Check (Review Focus 4):

```bash
git grep -n -i -E 'carenest|bayubai' -- web/packages/i18n/src/locales/ru
git grep -n -i carenest -- web ':!web/pnpm-lock.yaml'
```

Expected: both print nothing.

- [ ] **Step 3: Lockfile and checks**

```bash
cd web
pnpm install
pnpm generate:api
git status --short -- packages/api-client/src/generated
pnpm lint && pnpm typecheck && pnpm test && pnpm build
cd ..
```

Expected: `pnpm install` rewrites `pnpm-lock.yaml` importer and link names to `@bayubai/*`; `generate:api` leaves the generated client unchanged; lint and typecheck clean; tests 84 passed (api-client 6, i18n 17, ui 47, client 9, studio 5); build succeeds. If `build` changes `routeTree.gen.ts`, commit it.

- [ ] **Step 4: Commit**

```bash
git add -A web
git commit -m "refactor: rename the web workspace packages to @bayubai and the UI texts to Баюбай"
```

---

### Task 3: Azure model, deploy scripts and infra

**Files:**
- Modify: `src/Bayubai.AppHost/AzureDeployment.cs`, `src/Bayubai.AppHost/Templates/web.bicep`, `deploy/bootstrap.sh`, `deploy/budget.bicep`, `deploy/README.md`, `.github/workflows/deploy.yml`
- Regenerate: `infra/**` (from scratch)

**Interfaces:**
- Consumes: the AppHost project path `src/Bayubai.AppHost/Bayubai.AppHost.csproj` (Task 1).
- Produces: Azure names `bb` (Container Apps environment), `bb-client`, `bb-studio`, `bb-monthly`, `rg-bayubai`, `kv-bayubai-<6 hex>`, `bayubai-deploy`; plan 3 Tasks 7-10 (updated in Task 5) use them.

- [ ] **Step 1: The Azure model**

In `src/Bayubai.AppHost/AzureDeployment.cs`:
- `builder.AddAzureContainerAppEnvironment("cn")` -> `builder.AddAzureContainerAppEnvironment("bb")`
- `.WithEnvironment("Email__From", ReferenceExpression.Create($"Bayubai <no-reply@{domain}>"))` (after Task 1's replacement) -> `.WithEnvironment("Email__From", ReferenceExpression.Create($"Баюбай <no-reply@{domain}>"))`
- confirm `builder.AddParameter("postgres-user", "bayubai", publishValueAsDefault: true)` (Task 1 already replaced it).

In `src/Bayubai.AppHost/Templates/web.bicep`: `name: 'cn-client'` -> `name: 'bb-client'`, `name: 'cn-studio'` -> `name: 'bb-studio'`.

- [ ] **Step 2: Deploy scripts and workflow**

- `deploy/bootstrap.sh`: `repo="IlyaEsin/bayubai"`, `resource_group="rg-bayubai"`, `vault="kv-bayubai-$(printf '%s' "$subscription" | sha256sum | cut -c1-6)"`, `deploy_app="bayubai-deploy"`.
- `deploy/budget.bicep`: `name: 'bb-monthly'`.
- `.github/workflows/deploy.yml`: `--name "cn-$app"` -> `--name "bb-$app"`.
- `deploy/README.md`: every `rg-carenest` -> `rg-bayubai`, `kv-carenest-` -> `kv-bayubai-`, `cn-client`/`cn-studio`/`cn-monthly` -> `bb-client`/`bb-studio`/`bb-monthly`.

```bash
sed -i -e 's/rg-carenest/rg-bayubai/g' -e 's/kv-carenest-/kv-bayubai-/g' -e 's/cn-client/bb-client/g' -e 's/cn-studio/bb-studio/g' -e 's/cn-monthly/bb-monthly/g' deploy/README.md
git grep -n -i -P 'carenest|\bcn-|"cn"' -- deploy .github/workflows src/Bayubai.AppHost
git grep -n 'repo=' -- deploy/bootstrap.sh
```

Expected: the first grep prints nothing; the second prints `repo="IlyaEsin/bayubai"` (Review Focus 5).

- [ ] **Step 3: Regenerate infra from scratch (Review Focus 1)**

```bash
rm -rf infra
dotnet aspire publish --apphost src/Bayubai.AppHost/Bayubai.AppHost.csproj --output-path infra --non-interactive --nologo
ls infra
git add -A infra
dotnet aspire publish --apphost src/Bayubai.AppHost/Bayubai.AppHost.csproj --output-path infra --non-interactive --nologo
git status --porcelain -- infra
git grep -n -i -P 'carenest|cn-acr|\bcn\b' -- infra; ls infra | grep -E '^cn'
```

Expected: `ls infra` shows `bb/` and `bb-acr/` and no `cn*` folder; the second publish changes nothing, so `git status --porcelain -- infra` prints nothing; the last two commands print nothing. `infra/api/api.bicep` shows `value: 'Баюбай <no-reply@${domain_value}>'`.

Check the Key Vault secret parsing still yields the three secrets:

```bash
grep -h -A1 "Microsoft.KeyVault/vaults/secrets@[^']*' existing" infra/*/*.bicep | sed -n "s/^  name: '\(.*\)'$/\1/p" | grep -v '^connectionstrings--' | sort -u
```

Expected: `admin-email`, `email-password`, `email-username`.

- [ ] **Step 4: Lint the scripts and workflows, build**

```bash
bash -n deploy/bootstrap.sh; bash -n deploy/check-secrets.sh; bash -n deploy/run-migrations.sh
actionlint .github/workflows/*.yml
dotnet build Bayubai.slnx
```

Expected: no output from `bash -n` and actionlint (download actionlint v1.7.12 to a scratch folder outside the repo if it is not installed); build 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add -A src/Bayubai.AppHost deploy .github/workflows/deploy.yml infra
git commit -m "refactor: Azure names bb-* and *-bayubai in the model, deploy scripts and infra"
```

---

### Task 4: End-to-end tests on the renamed stack

**Files:**
- Modify: `tests/e2e/package.json`, `tests/e2e/playwright.config.ts`, `tests/e2e/support/urls.ts`, `tests/e2e/specs/01-sign-in-and-profile.spec.ts`

**Interfaces:**
- Consumes: AppHost path `src/Bayubai.AppHost` (Task 1), demo admin `admin@bayubai.local` and database `bayubai` (Task 1), heading "Вход в Баюбай" (Task 2).

- [ ] **Step 1: Rename**

- `tests/e2e/package.json`: `"name": "bayubai-e2e",`
- `tests/e2e/playwright.config.ts`: the comment `dotnet run --project src/Bayubai.AppHost` and `command: 'dotnet run --project ../../src/Bayubai.AppHost',`
- `tests/e2e/support/urls.ts`: `export const adminEmail = 'admin@bayubai.local';`
- `tests/e2e/specs/01-sign-in-and-profile.spec.ts`: `await expect(page.getByRole('heading', { name: 'Вход в Баюбай' })).toBeVisible();`

```bash
git grep -n -i carenest -- tests/e2e
```

Expected: nothing.

- [ ] **Step 2: Run the whole suite (Review Focus 3)**

Make sure no AppHost is running (Playwright must start the renamed one), then from `tests/e2e/`:

```bash
pnpm install
pnpm typecheck
pnpm test
```

Expected: typecheck clean; `7 passed`. The local PostgreSQL container keeps its volume, and the stack starts on a new empty `bayubai` database that the migration service creates. Stop the AppHost and the processes it started afterwards.

- [ ] **Step 3: Commit**

```bash
git add -A tests/e2e
git commit -m "test: e2e runs against the renamed Bayubai stack"
```

---

### Task 5: Documentation, conventions and the final sweep

**Files:**
- Modify: `CLAUDE.md`, `REVIEW.md`, `.claude/skills/build-test/SKILL.md`, `.claude/skills/how-to-test/SKILL.md`, `docs/tech-overview.md`, `docs/superpowers/specs/foundation-design.md`
- Modify (note only): `docs/superpowers/plans/2026-09-24-foundation-backend.md`, `docs/superpowers/plans/2026-09-25-foundation-web.md`
- Modify (note + Tasks 7-10): `docs/superpowers/plans/2026-09-27-foundation-delivery.md`

**Interfaces:**
- Consumes: every name produced by Tasks 1-4.

- [ ] **Step 1: Living documents**

```bash
for f in CLAUDE.md REVIEW.md .claude/skills/build-test/SKILL.md .claude/skills/how-to-test/SKILL.md docs/tech-overview.md docs/superpowers/specs/foundation-design.md; do
  sed -i -e 's/CareNest/Bayubai/g' -e 's/CARENEST/BAYUBAI/g' -e 's/carenest/bayubai/g' \
    -e 's/cn-client/bb-client/g' -e 's/cn-studio/bb-studio/g' -e 's/cn-monthly/bb-monthly/g' \
    -e 's/cn-<n>/bb-<n>/g' -e 's/cn-<issue>/bb-<issue>/g' -e 's/cn-<номер>/bb-<номер>/g' "$f"
done
git grep -n -P '\bcn-|\bcn\b' -- CLAUDE.md REVIEW.md .claude docs/tech-overview.md docs/superpowers/specs/foundation-design.md
```

Expected: the grep prints nothing that still refers to the old prefix or the old Azure environment (fix any remaining `cn` environment mention to `bb` by hand).

Then read each file once and fix by hand what a blind replacement cannot:
- Russian prose about the product itself says «Баюбай» (for example in `docs/tech-overview.md` section 1 and wherever the text names the product rather than a project, package or path); code names, paths and packages stay `Bayubai.*` / `@bayubai/*`.
- `CLAUDE.md` first heading `# Bayubai`, and its first paragraph names the product "Bayubai (Russian: Баюбай)".
- `docs/tech-overview.md`: the email section says the sender is `Баюбай <no-reply@<домен>>`; the Azure section uses `rg-bayubai`, `kv-bayubai-*`, `bb-client`, `bb-studio`.
- `docs/superpowers/specs/foundation-design.md`: product name Bayubai; add one line under its title: "Renamed from CareNest on 2026-09-30 (`rename-to-bayubai.md`)."

- [ ] **Step 2: Historical plans**

Insert as the second line of `docs/superpowers/plans/2026-09-24-foundation-backend.md`, `docs/superpowers/plans/2026-09-25-foundation-web.md` and `docs/superpowers/plans/2026-09-27-foundation-delivery.md` (right after the `#` title line, followed by a blank line):

```markdown

> The project was renamed to Bayubai on 2026-09-30 (`docs/superpowers/specs/rename-to-bayubai.md`); names below are historical.
```

For `2026-09-27-foundation-delivery.md` the note instead reads: "The project was renamed to Bayubai on 2026-09-30 (`docs/superpowers/specs/rename-to-bayubai.md`); Tasks 1-6 below are historical, Tasks 7-10 use the new names."

- [ ] **Step 3: Plan 3 Tasks 7-10**

Rename only between the `### Task 7:` heading and the `## Notes for later sub-projects` heading:

```bash
f=docs/superpowers/plans/2026-09-27-foundation-delivery.md
start=$(grep -n '^### Task 7:' "$f" | cut -d: -f1); end=$(grep -n '^## Notes for later sub-projects' "$f" | cut -d: -f1)
sed -i "${start},${end}{s/CareNest/Bayubai/g;s/carenest/bayubai/g;s/cn-client/bb-client/g;s/cn-studio/bb-studio/g;s/cn-monthly/bb-monthly/g}" "$f"
sed -n "${start},${end}p" "$f" | grep -n -i -P 'carenest|\bcn-'
```

Expected: nothing printed. Then read Tasks 7-10 once: where they say `<domain>`, add that the domain is `bayubai.com`; where they show the sender, it is `Баюбай <no-reply@bayubai.com>`.

- [ ] **Step 4: The final sweep (spec Success criterion)**

```bash
git grep -l -i carenest
```

Expected exactly these files: `docs/superpowers/plans/2026-09-24-foundation-backend.md`, `docs/superpowers/plans/2026-09-25-foundation-web.md`, `docs/superpowers/plans/2026-09-27-foundation-delivery.md` (Tasks 1-6 and its note), `docs/superpowers/plans/rename-to-bayubai.md`, `docs/superpowers/specs/rename-to-bayubai.md`, `docs/superpowers/specs/foundation-design.md` (its one-line rename note). Anything else is a miss: fix it.

- [ ] **Step 5: Full verification**

```bash
dotnet build Bayubai.slnx && dotnet test Bayubai.slnx
cd web && pnpm lint && pnpm typecheck && pnpm test && cd ..
```

Expected: build 0 warnings; backend 149 passed (SharedKernel 22, Identity 41, Architecture 6, Integration 80); web 84 passed. (The e2e run from Task 4 stands unless code changed after it.)

- [ ] **Step 6: Commit**

```bash
git add CLAUDE.md REVIEW.md .claude docs
git commit -m "docs: Bayubai in the living docs, bb prefix, rename notes in the plans"
```

After the final review, push the branch and open the PR (`gh pr create --base main --title "Rename CareNest to Bayubai"`), with the body summarising the spec and the test counts. The owner merges it, then renames the local folder `C:\code\carenest` to `C:\code\bayubai` (and moves the Claude Code project memory to the new path).
