import type { MeResponse } from '@bayubai/api-client';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Suspense } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../app/AppProviders';
import { meQuery } from '../session/session';
import { renderWithProviders, testApi } from '../testing/render';
import { server } from '../testing/server';
import { ProfileView } from './ProfileView';

const me: MeResponse = {
  id: '0199a000-0000-7000-8000-000000000001',
  displayName: 'Anna',
  language: 'en',
  timeZone: 'Europe/Moscow',
  roles: ['parent'],
  signInMethods: ['email'],
};

function renderProfile(onDeleted = vi.fn()) {
  server.use(http.get(`${testApi}/api/identity/providers`, () => HttpResponse.json({ providers: ['email', 'Google'], telegramBotName: null })));
  // The guarded route has already loaded the session by the time the profile renders.
  const queryClient = createQueryClient();
  queryClient.setQueryData(meQuery.queryKey, me);
  return renderWithProviders(
    <Suspense>
      <ProfileView onDeleted={onDeleted} />
    </Suspense>,
    'en',
    queryClient,
  );
}

describe('ProfileView', () => {
  it('switches the interface language as soon as the profile is saved', async () => {
    server.use(
      http.put(`${testApi}/api/identity/me`, async ({ request }) =>
        HttpResponse.json({ ...me, ...((await request.json()) as object) }),
      ),
    );
    const { i18n } = renderProfile();

    await userEvent.selectOptions(await screen.findByLabelText('Language'), 'ru');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(i18n.language).toBe('ru'));
    expect(await screen.findByRole('button', { name: 'Сохранить' })).toBeInTheDocument();
  });

  it('offers to link providers the user does not have yet', async () => {
    renderProfile();

    expect(await screen.findByRole('button', { name: 'Add Google' })).toBeInTheDocument();
    expect(screen.getByRole('listitem')).toHaveTextContent('Email');
  });

  it('deletes the account only after confirmation', async () => {
    const deleted = vi.fn();
    server.use(
      http.delete(`${testApi}/api/identity/me`, () => {
        deleted();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const onDeleted = vi.fn();
    const { queryClient } = renderProfile(onDeleted);

    await userEvent.click(await screen.findByRole('button', { name: 'Delete account' }));
    expect(deleted).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Yes, delete permanently' }));

    await waitFor(() => expect(onDeleted).toHaveBeenCalled());
    expect(deleted).toHaveBeenCalledTimes(1);
    // Nothing of the deleted account may linger for the next person on this device.
    expect(queryClient.getQueryData(meQuery.queryKey)).toBeUndefined();
  });
});
