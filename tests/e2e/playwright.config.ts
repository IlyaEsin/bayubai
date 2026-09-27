import { defineConfig, devices } from '@playwright/test';
import { studioUrl } from './support/urls';

export default defineConfig({
  testDir: './specs',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  // One shared stack and one cached admin session; parallel runs would also trip the per-email link throttle.
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list']],
  globalSetup: './support/global-setup.ts',
  use: {
    ...devices['Desktop Chrome'],
    locale: 'en-US',
    timezoneId: 'Europe/Moscow',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'smoke' },
    // Same scenarios in a visible, slowed-down browser for a live demo.
    { name: 'walkthrough', use: { headless: false, launchOptions: { slowMo: 700 } } },
    // Per-issue "how to test" scenarios (how-to-test skill), shown in a visible browser.
    { name: 'how-to-test', testDir: './how-to-test', use: { headless: false, launchOptions: { slowMo: 500 } } },
  ],
  // Reuses a running `dotnet run --project src/CareNest.AppHost`; otherwise starts it (Docker must be running).
  webServer: {
    command: 'dotnet run --project ../../src/CareNest.AppHost',
    url: studioUrl,
    reuseExistingServer: !process.env.CI,
    timeout: 600_000,
  },
});
