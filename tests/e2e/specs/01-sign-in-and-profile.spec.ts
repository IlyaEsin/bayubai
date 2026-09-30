import { expect, test } from '@playwright/test';
import { signedInParent } from '../support/actors';
import { latestMagicLink } from '../support/mail';
import { clientUrl } from '../support/urls';

test('a parent signs in with an email link and updates the profile', async ({ browser }) => {
  const { page } = await signedInParent(browser, 'Asia/Yekaterinburg');

  await page.getByRole('link', { name: 'Profile' }).click();
  await expect(page.getByLabel('Time zone')).toHaveValue('Asia/Yekaterinburg');
  await page.getByLabel('Name').fill('Anna');
  await page.getByLabel('Language').selectOption('ru');
  await page.getByRole('button', { name: 'Save' }).click();

  // Acceptance 6: the language switch applies immediately.
  await expect(page.getByRole('button', { name: 'Сохранить' })).toBeVisible();
  await page.getByRole('link', { name: 'Главная' }).click();
  await expect(page.getByRole('heading', { name: 'Здравствуйте, Anna' })).toBeVisible();
  await expect(page.getByText('Нет активных консультаций')).toBeVisible();
});

test('the sign-in page speaks the browser language before sign-in', async ({ browser }) => {
  const page = await (await browser.newContext({ locale: 'ru-RU' })).newPage();

  await page.goto(`${clientUrl}/sign-in`);

  await expect(page.getByRole('heading', { name: 'Вход в Баюбай' })).toBeVisible();
});

test('the theme follows the system and remembers an explicit choice', async ({ browser }) => {
  const page = await (await browser.newContext({ colorScheme: 'dark', locale: 'en-US' })).newPage();
  await page.goto(`${clientUrl}/sign-in`);
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

  await page.getByRole('button', { name: 'Light' }).click();
  await page.reload();

  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await expect(page.getByRole('button', { name: 'Light' })).toHaveAttribute('aria-pressed', 'true');
});

test('a magic link opened in another browser does not sign in', async ({ browser }) => {
  const requester = await (await browser.newContext({ locale: 'en-US' })).newPage();
  await requester.goto(`${clientUrl}/sign-in`);
  const email = `other-browser-${Date.now()}@example.test`;
  await requester.getByLabel('Email').fill(email);
  await requester.getByRole('button', { name: 'Email me a sign-in link' }).click();
  await expect(requester.getByText('Check your email')).toBeVisible();
  const link = await latestMagicLink(requester.request, email);

  const elsewhere = await (await browser.newContext({ locale: 'en-US' })).newPage();
  await elsewhere.goto(link);

  await expect(elsewhere.getByRole('alert')).toContainText('Open the link in the same browser');
  // Not signed in there: the failed-sign-in screen offers a way back, not the app.
  await expect(elsewhere.getByRole('link', { name: 'Back to sign-in' })).toBeVisible();

  // The link stays usable in the browser that requested it.
  await requester.goto(link);
  await expect(requester.getByRole('heading', { name: /Hello, other-browser-/ })).toBeVisible();
});
