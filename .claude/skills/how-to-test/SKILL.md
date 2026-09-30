---
name: how-to-test
description: Write and run a Playwright "how to test" scenario for a GitHub issue in tests/e2e/how-to-test/bb-<n>/, so the owner can watch the change work in a real browser against the local Aspire stack. Use when an issue's change is ready to be shown.
---

# How to test

1. Find the issue number from the branch (`feature/bb-<n>-...`) or ask.
2. Write `tests/e2e/how-to-test/bb-<n>/<what-it-shows>.spec.ts`:
   - Reuse the helpers in `tests/e2e/support/`: `signedInParent`, `signedInConsultant`, `createInvitation` from `actors.ts`, `signInWithEmail` and `latestMagicLink` from `mail.ts`, and the URLs and `uniqueEmail` from `urls.ts`.
   - Name every test `bb-<n>: <what the person sees>`. Assert on what a person sees (role, label, text), never on CSS classes.
   - Use `en-US` UI text, as in `tests/e2e/specs/`.
3. Run from `tests/e2e/`: `pnpm typecheck`, then `pnpm how-to-test bb-<n>`.
   - A visible, slowed-down browser opens.
   - Playwright starts the AppHost if it is not running (Docker must be running).
   - Traces of failures land in `test-results/` (gitignored).
4. Commit the scenario with the change; it documents how to verify the issue later.
5. Tell the owner the command to watch it themselves:

   ```bash
   cd tests/e2e && pnpm how-to-test bb-<n>
   ```
