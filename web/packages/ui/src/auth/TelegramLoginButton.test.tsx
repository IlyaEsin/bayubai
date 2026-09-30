import { waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { renderWithProviders, testApi } from '../testing/render';
import { server } from '../testing/server';
import { TelegramLoginButton, telegramWidgetUrl } from './TelegramLoginButton';

describe('TelegramLoginButton', () => {
  it('loads the widget for the configured bot', () => {
    const { container } = renderWithProviders(<TelegramLoginButton botName="bayubai_bot" mode="signin" onSignedIn={vi.fn()} />);

    const script = container.querySelector('script');
    expect(script).toHaveAttribute('src', telegramWidgetUrl);
    expect(script).toHaveAttribute('data-telegram-login', 'bayubai_bot');
    expect(script).toHaveAttribute('data-onauth', 'bbTelegramAuth(user)');
  });

  it('sends the widget payload to the API and reports success', async () => {
    let body: Record<string, unknown> | undefined;
    server.use(
      http.post(`${testApi}/api/identity/telegram/complete`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const onSignedIn = vi.fn();
    renderWithProviders(<TelegramLoginButton botName="bayubai_bot" mode="link" onSignedIn={onSignedIn} />);

    window.bbTelegramAuth?.({ id: 42, first_name: 'Anna', auth_date: 1767603600, hash: 'abc' });

    await waitFor(() => expect(onSignedIn).toHaveBeenCalled());
    expect(body).toMatchObject({ auth: { id: 42, first_name: 'Anna' }, mode: 'link', language: 'en' });
  });

  it('removes the global callback on unmount', () => {
    const { unmount } = renderWithProviders(<TelegramLoginButton botName="bayubai_bot" mode="signin" onSignedIn={vi.fn()} />);

    expect(window.bbTelegramAuth).toBeDefined();

    unmount();

    expect(window.bbTelegramAuth).toBeUndefined();
  });
});
