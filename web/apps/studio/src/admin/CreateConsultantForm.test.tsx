import type { MeResponse } from '@bayubai/api-client';
import { createQueryClient, meQuery } from '@bayubai/ui';
import { renderWithProviders, server, testApi } from '@bayubai/ui/testing';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Suspense } from 'react';
import { describe, expect, it } from 'vitest';
import { CreateConsultantForm } from './CreateConsultantForm';

const admin: MeResponse = {
  id: 'a1',
  displayName: 'Admin',
  language: 'en',
  timeZone: 'Asia/Yekaterinburg',
  roles: ['admin', 'parent'],
  signInMethods: ['email'],
};

describe('CreateConsultantForm', () => {
  it("creates a consultant with the chosen language and the admin's zone by default", async () => {
    let body: Record<string, unknown> | undefined;
    server.use(
      http.post(`${testApi}/api/identity/admin/consultants`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ userId: 'c1' });
      }),
    );
    const queryClient = createQueryClient();
    queryClient.setQueryData(meQuery.queryKey, admin);
    renderWithProviders(
      <Suspense>
        <CreateConsultantForm />
      </Suspense>,
      'en',
      queryClient,
    );

    await userEvent.type(await screen.findByLabelText('Consultant email'), 'consultant@example.test');
    await userEvent.type(screen.getByLabelText('Consultant name'), 'Consultant');
    await userEvent.selectOptions(screen.getByLabelText('Consultant language'), 'ru');
    await userEvent.click(screen.getByRole('button', { name: 'Create consultant' }));

    expect(await screen.findByText(/Consultant created/)).toBeInTheDocument();
    expect(body).toEqual({ email: 'consultant@example.test', displayName: 'Consultant', language: 'ru', timeZone: 'Asia/Yekaterinburg' });
  });
});
