---
name: build-test
description: Build and test Bayubai scoped to what changed - backend build and tests, web lint/typecheck/tests, OpenAPI and infra drift. Use before saying a change works, before a commit, and before opening a PR.
---

# Build and test

1. List what changed: `git status --short` and `git diff --name-only origin/main...HEAD`.
2. Run every row whose paths match, from the repository root unless a folder is given. Docker must be running for integration tests.

| Changed paths | Run |
|---|---|
| `src/**`, `tests/Bayubai.*/**`, `*.props`, `global.json` | `dotnet build Bayubai.slnx` (0 warnings), `dotnet test Bayubai.slnx` |
| an endpoint, request/response type or error code | `dotnet test tests/Bayubai.Api.IntegrationTests`; if `OpenApiContractTests` fails on purpose: `BAYUBAI_UPDATE_OPENAPI=1 dotnet test tests/Bayubai.Api.IntegrationTests`, then in `web/` `pnpm generate:api` |
| `src/Bayubai.AppHost/**` | `dotnet tool restore`, `dotnet aspire publish --apphost src/Bayubai.AppHost/Bayubai.AppHost.csproj --output-path infra`, commit `infra/` if it changed |
| `web/**` | in `web/`: `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm build` (the build regenerates `routeTree.gen.ts`; commit it if it changed) |
| a user flow (sign-in, profile, invitations) or `src/Bayubai.AppHost/LocalStack.cs` | in `tests/e2e/`: `pnpm test` |
| `.github/workflows/**` | `actionlint .github/workflows/*.yml` if installed |

3. Report the exact counts from the output (for example "Integration 78 passed"). Never say green without having the output in front of you; if a check could not run, say which and why.
