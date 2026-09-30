import type { MeResponse } from '@bayubai/api-client';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Suspense } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../app/AppProviders';
import { meQuery } from '../session/session';
import { renderWithProviders, testApi } from '../testing/render';
import { server } from '../testing/server';
import { AppShell } from './Shells';

// AppShell renders ThemeSwitcher, which reads the system preference on mount.
beforeEach(() => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  }));
});

const me: MeResponse = {
  id: '0199a000-0000-7000-8000-000000000001',
  displayName: 'Anna',
  language: 'en',
  timeZone: 'Europe/Moscow',
  roles: ['parent'],
  signInMethods: ['email'],
};

function renderShell(onSignedOut = vi.fn()) {
  const queryClient = createQueryClient();
  queryClient.setQueryData(meQuery.queryKey, me);
  return {
    onSignedOut,
    ...renderWithProviders(
      <Suspense>
        <AppShell nav={null} onSignedOut={onSignedOut}>
          <p>content</p>
        </AppShell>
      </Suspense>,
      'en',
      queryClient,
    ),
  };
}

describe('AppShell sign-out', () => {
  it('clears the session and navigates away on a successful sign-out', async () => {
    server.use(http.post(`${testApi}/api/identity/signout`, () => new HttpResponse(null, { status: 204 })));
    const { onSignedOut, queryClient } = renderShell();

    await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }));

    await waitFor(() => expect(onSignedOut).toHaveBeenCalledTimes(1));
    expect(queryClient.getQueryData(meQuery.queryKey)).toBeUndefined();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('treats a 401 as already signed out and still clears the session', async () => {
    server.use(
      http.post(`${testApi}/api/identity/signout`, () =>
        HttpResponse.json({ status: 401, code: 'identity.not_signed_in' }, { status: 401 }),
      ),
    );
    const { onSignedOut, queryClient } = renderShell();

    await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }));

    await waitFor(() => expect(onSignedOut).toHaveBeenCalledTimes(1));
    expect(queryClient.getQueryData(meQuery.queryKey)).toBeUndefined();
  });

  it('keeps the session and shows the error when sign-out fails for another reason', async () => {
    server.use(
      http.post(`${testApi}/api/identity/signout`, () => HttpResponse.json({ status: 500 }, { status: 500 })),
    );
    const { onSignedOut, queryClient } = renderShell();

    await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Please try again.');
    expect(onSignedOut).not.toHaveBeenCalled();
    expect(queryClient.getQueryData(meQuery.queryKey)).toEqual(me);
  });
});
