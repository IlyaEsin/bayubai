import { expect, test, type Page } from '@playwright/test';
import { newParentPage, signedInParent } from '../support/actors';
import { clientUrl } from '../support/urls';

// The fake provider stands in for Google, Yandex ID and VK ID; this cookie tells it which account the "provider" returns.
async function actAsFakeSubject(page: Page, subject: string) {
  await page.context().addCookies([{ name: 'bb_fake_subject', value: subject, domain: 'localhost', path: '/' }]);
}

test('a parent adds a second sign-in method and can sign in with either', async ({ browser }) => {
  const { page } = await signedInParent(browser);
  const subject = `fake-${Date.now()}`;
  await actAsFakeSubject(page, subject);

  await page.getByRole('link', { name: 'Profile' }).click();
  await page.getByRole('button', { name: 'Add test sign-in' }).click();
  await expect(page.getByRole('list', { name: 'Sign-in methods' })).toContainText('test sign-in');
  await expect(page.getByRole('list', { name: 'Sign-in methods' })).toContainText('Email');
  const name = await page.getByLabel('Name').inputValue();

  const other = await newParentPage(browser);
  await actAsFakeSubject(other, subject);
  await other.goto(`${clientUrl}/sign-in`);
  await other.getByRole('button', { name: 'Continue with test sign-in' }).click();

  await expect(other.getByRole('heading', { name: `Hello, ${name}` })).toBeVisible();
});
