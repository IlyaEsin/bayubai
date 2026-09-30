import type { MeResponse } from '@bayubai/api-client';
import { renderWithProviders, server, testApi } from '@bayubai/ui/testing';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { ClientsList } from './ClientsList';
import { InvitationCreator } from './InvitationCreator';
import { InvitationsList } from './InvitationsList';

// The consultant lives in Yekaterinburg (UTC+5); her client in Vladivostok (UTC+10).
const consultant: MeResponse = {
  id: 'c1',
  displayName: 'Consultant',
  language: 'ru',
  timeZone: 'Asia/Yekaterinburg',
  roles: ['consultant'],
  signInMethods: ['email'],
};

describe('InvitationCreator', () => {
  it('shows the link and its expiry in the consultant zone with a Russian plural', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-01-05T09:00:00Z'));
    server.use(
      http.post(`${testApi}/api/identity/invitations`, () =>
        HttpResponse.json({ id: 'i1', url: 'http://localhost:5173/invite/abc', expiresAt: '2026-01-19T09:00:00Z' }),
      ),
      http.get(`${testApi}/api/identity/invitations`, () => HttpResponse.json([])),
    );
    renderWithProviders(<InvitationCreator me={consultant} />, 'ru');

    await userEvent.click(screen.getByRole('button', { name: 'Создать приглашение' }));

    expect(await screen.findByDisplayValue('http://localhost:5173/invite/abc')).toBeInTheDocument();
    expect(screen.getByText('Действует 14 дней, до 19 янв. 2026 г., 14:00')).toBeInTheDocument();
  });
});

describe('InvitationsList', () => {
  it('translates statuses and formats times in the consultant zone', async () => {
    server.use(
      http.get(`${testApi}/api/identity/invitations`, () =>
        HttpResponse.json([{ id: 'i1', createdAt: '2026-01-05T09:00:00Z', expiresAt: '2026-01-19T09:00:00Z', status: 'expired' }]),
      ),
    );

    renderWithProviders(<InvitationsList me={consultant} />, 'ru');

    expect(await screen.findByText('Истекло')).toBeInTheDocument();
    expect(screen.getByText('Создано 5 янв. 2026 г., 14:00')).toBeInTheDocument();
  });
});

describe('ClientsList', () => {
  it("shows each client's local time in the client's own zone", async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-01-05T09:00:00Z'));
    server.use(
      http.get(`${testApi}/api/identity/clients`, () =>
        HttpResponse.json([{ userId: 'p1', displayName: 'Anna', language: 'en', timeZone: 'Asia/Vladivostok', linkedAt: '2026-01-05T09:00:00Z' }]),
      ),
    );

    renderWithProviders(<ClientsList me={consultant} />, 'ru');

    expect(await screen.findByText('Anna')).toBeInTheDocument();
    expect(screen.getByText('Клиент с 5 янв. 2026 г., 14:00')).toBeInTheDocument();
    expect(screen.getByText('Сейчас у клиента 19:00 (Asia/Vladivostok)')).toBeInTheDocument();
    // The consultant reads Russian while the client uses English.
    expect(screen.getByText('Язык интерфейса: English')).toBeInTheDocument();
  });

  it('says so when there are no clients yet', async () => {
    server.use(http.get(`${testApi}/api/identity/clients`, () => HttpResponse.json([])));

    renderWithProviders(<ClientsList me={consultant} />);

    expect(await screen.findByText('No clients yet')).toBeInTheDocument();
  });
});
