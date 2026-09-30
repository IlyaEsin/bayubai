import { renderWithProviders, server, testApi } from '@bayubai/ui/testing';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { AcceptInvitation } from './AcceptInvitation';

const home = <a href="/">home</a>;

describe('AcceptInvitation', () => {
  it('accepts with the token from the link', async () => {
    let token: unknown;
    server.use(
      http.post(`${testApi}/api/identity/invitations/accept`, async ({ request }) => {
        token = ((await request.json()) as { token: string }).token;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<AcceptInvitation token="inv-123" home={home} />);

    await userEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));

    expect(await screen.findByText('Invitation accepted')).toBeInTheDocument();
    expect(token).toBe('inv-123');
  });

  it.each([
    [409, 'identity.invite_used', 'This invitation has already been used'],
    [410, 'identity.invite_expired', 'The invitation has expired. Ask the consultant for a new one.'],
    [404, 'identity.invite_not_found', 'Invitation not found. Please check the link.'],
  ])('shows a translated error for %i %s', async (status, code, message) => {
    server.use(http.post(`${testApi}/api/identity/invitations/accept`, () => HttpResponse.json({ status, code }, { status })));
    renderWithProviders(<AcceptInvitation token="inv-123" home={home} />);

    await userEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
  });
});
