import type { ConsultantResponse, MeResponse } from '@bayubai/api-client';
import { createQueryClient, meQuery } from '@bayubai/ui';
import { renderWithProviders, server, testApi } from '@bayubai/ui/testing';
import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { Suspense } from 'react';
import { describe, expect, it } from 'vitest';
import { Home } from './Home';

const me: MeResponse = {
  id: '0199a000-0000-7000-8000-000000000001',
  displayName: 'Anna',
  language: 'en',
  timeZone: 'Europe/Moscow',
  roles: ['parent'],
  signInMethods: ['email'],
};

const consultant: ConsultantResponse = {
  userId: 'c1',
  displayName: 'Consultant',
  timeZone: 'Europe/Moscow',
  linkedAt: '2026-01-01T00:00:00Z',
};

function renderHome(consultants: ConsultantResponse[]) {
  server.use(http.get(`${testApi}/api/identity/me/consultants`, () => HttpResponse.json(consultants)));
  const queryClient = createQueryClient();
  queryClient.setQueryData(meQuery.queryKey, me);
  return renderWithProviders(
    <Suspense>
      <Home />
    </Suspense>,
    'en',
    queryClient,
  );
}

describe('Home', () => {
  it('shows the empty state when the parent has no linked consultant', async () => {
    renderHome([]);

    expect(await screen.findByText('No active consultations')).toBeInTheDocument();
    expect(screen.getByText('When a consultant invites you, the consultation will appear here.')).toBeInTheDocument();
  });

  it('shows the consultant card without the empty state when a consultant is linked', async () => {
    renderHome([consultant]);

    expect(await screen.findByText('Consultant')).toBeInTheDocument();
    expect(screen.queryByText('No active consultations')).not.toBeInTheDocument();
  });

  it('shows no empty-state text while the consultants query is still pending', () => {
    // Never resolves, so the query stays pending for the life of the test.
    server.use(http.get(`${testApi}/api/identity/me/consultants`, () => new Promise(() => {})));
    const queryClient = createQueryClient();
    queryClient.setQueryData(meQuery.queryKey, me);
    renderWithProviders(
      <Suspense>
        <Home />
      </Suspense>,
      'en',
      queryClient,
    );

    expect(screen.queryByText('No active consultations')).not.toBeInTheDocument();
    expect(screen.queryByText('Consultant')).not.toBeInTheDocument();
  });

  it('shows the error alert instead of the empty state when the consultants request fails', async () => {
    // 404 is not retried by the query client's default retry policy, so the error settles immediately.
    server.use(http.get(`${testApi}/api/identity/me/consultants`, () => new HttpResponse(null, { status: 404 })));
    const queryClient = createQueryClient();
    queryClient.setQueryData(meQuery.queryKey, me);
    renderWithProviders(
      <Suspense>
        <Home />
      </Suspense>,
      'en',
      queryClient,
    );

    expect(await screen.findByRole('alert')).toHaveTextContent('Not found');
    expect(screen.queryByText('No active consultations')).not.toBeInTheDocument();
  });
});
