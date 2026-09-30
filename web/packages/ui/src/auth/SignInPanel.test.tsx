import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { renderWithProviders, testApi } from '../testing/render';
import { server } from '../testing/server';
import { SignInPanel } from './SignInPanel';

function providers(list: string[], telegramBotName: string | null = null) {
  server.use(http.get(`${testApi}/api/identity/providers`, () => HttpResponse.json({ providers: list, telegramBotName })));
}

describe('SignInPanel', () => {
  it('shows a button for each configured OAuth provider and the email form', async () => {
    providers(['email', 'Google', 'Yandex']);

    renderWithProviders(<SignInPanel next="/" onSignedIn={vi.fn()} />);

    expect(await screen.findByRole('button', { name: 'Continue with Google' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continue with Yandex ID' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /VK ID/ })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
  });

  it('asks for a magic link with the callback, next path, language and browser time zone', async () => {
    providers(['email']);
    let sent: Record<string, unknown> | undefined;
    server.use(
      http.post(`${testApi}/api/identity/email/start`, async ({ request }) => {
        sent = (await request.json()) as Record<string, unknown>;
        return new HttpResponse(null, { status: 202 });
      }),
    );
    renderWithProviders(<SignInPanel next="/invite/abc" onSignedIn={vi.fn()} />, 'ru');

    await userEvent.type(screen.getByLabelText('Email'), 'anna@example.test');
    await userEvent.click(screen.getByRole('button', { name: 'Получить ссылку для входа' }));

    expect(await screen.findByText('Проверьте почту')).toBeInTheDocument();
    expect(sent).toMatchObject({
      email: 'anna@example.test',
      callbackUrl: `${window.location.origin}/auth/email?next=%2Finvite%2Fabc`,
      language: 'ru',
      mode: 'signin',
    });
    expect(typeof sent?.timeZone).toBe('string');
  });

  it('shows the translated error and marks the field when the API rejects the email', async () => {
    providers(['email']);
    server.use(
      http.post(`${testApi}/api/identity/email/start`, () =>
        HttpResponse.json({ status: 400, code: 'validation_failed', errors: { email: ['invalid'] } }, { status: 400 }),
      ),
    );
    renderWithProviders(<SignInPanel next="/" onSignedIn={vi.fn()} />);

    await userEvent.type(screen.getByLabelText('Email'), 'Anna <anna@example.test>');
    await userEvent.click(screen.getByRole('button', { name: 'Email me a sign-in link' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Please check the entered data');
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
  });

  it('sanitizes a hostile next path before handing it to onSignedIn after a Telegram sign-in', async () => {
    providers(['email', 'Telegram'], 'bayubai_bot');
    server.use(http.post(`${testApi}/api/identity/telegram/complete`, () => new HttpResponse(null, { status: 204 })));
    const onSignedIn = vi.fn();
    renderWithProviders(<SignInPanel next="https://evil.example" onSignedIn={onSignedIn} />);

    await waitFor(() => expect(window.cnTelegramAuth).toBeDefined());
    window.cnTelegramAuth?.({ id: 42, first_name: 'Anna', auth_date: 1767603600, hash: 'abc' });

    await waitFor(() => expect(onSignedIn).toHaveBeenCalledWith('/'));
  });
});
