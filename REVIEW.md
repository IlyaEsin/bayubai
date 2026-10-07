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
- [ ] No business logic in `Bayubai.Api`.
- [ ] No Azure SDK outside `Bayubai.AppHost` and `Bayubai.ServiceDefaults`.

## Migrations (they run after the new API revision is live)

- [ ] The new code works against the old schema and the old code against the new one (expand, then contract in a later release): add nullable or defaulted columns; never rename or drop a column or table in the same release that stops using it.
- [ ] No long locks on big tables (no table rewrite, e.g. adding a non-null column without a default).
- [ ] Data migrations are idempotent.

## Contract and deploy

- [ ] An API change updated `openapi.json` and the generated client in the same PR; every endpoint has `.WithName(...)`.
- [ ] A change to the AppHost's Azure model regenerated `infra/`; the `infra/` diff is what was intended.
- [ ] A new secret is in Key Vault before the PR that references it (`deploy/README.md`); no secret in code, config or workflow files.
- [ ] Cookies stay `HttpOnly`, `Secure`, `SameSite=Lax`; CORS origins come from `Frontend:Origins` only.
- [ ] Every `uses:` in a workflow is pinned to a full commit SHA with the version in a comment (`@<sha> # v4.4.0`); Dependabot keeps both up to date.

## Docs

- [ ] A newly adopted technology has a section in `docs/tech-overview.md` (Russian).
- [ ] Text uses the plain hyphen `-`; comments are one dry sentence, why not what.
