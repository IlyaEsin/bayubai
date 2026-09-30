import { configureApi } from '@bayubai/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { emailCallbackUrl, externalSignInUrl, safeNext } from './navigation';

describe('safeNext', () => {
  it.each(['/', '/invite/abc', '/profile?tab=1'])('keeps the in-app path %s', (path) => {
    expect(safeNext(path)).toBe(path);
  });

  it.each([
    'https://evil.example/',
    '//evil.example/x',
    '/\\evil.example',
    'javascript:alert(1)',
    '',
    undefined,
    42,
    '/\t/evil.example',
    '/\n/evil.example',
    '/\r/evil.example',
  ])('replaces %s with home', (value) => {
    expect(safeNext(value)).toBe('/');
  });
});

describe('callback URLs', () => {
  afterEach(() => configureApi({ baseUrl: '' }));

  it('carries the next path through the magic link', () => {
    expect(emailCallbackUrl('/invite/abc', 'https://app.example.test')).toBe('https://app.example.test/auth/email?next=%2Finvite%2Fabc');
  });

  it('never carries a foreign next path', () => {
    expect(emailCallbackUrl('https://evil.example', 'https://app.example.test')).toBe('https://app.example.test/auth/email?next=%2F');
  });

  it('builds the API start URL for an OAuth provider', () => {
    configureApi({ baseUrl: 'https://api.example.test' });

    const url = new URL(externalSignInUrl({ provider: 'Google', next: '/profile', mode: 'link', language: 'ru', timeZone: 'Europe/Moscow' }));

    expect(url.origin + url.pathname).toBe('https://api.example.test/api/identity/external/Google/start');
    expect(url.searchParams.get('returnUrl')).toBe(`${window.location.origin}/auth/external?next=%2Fprofile`);
    expect(url.searchParams.get('mode')).toBe('link');
    expect(url.searchParams.get('language')).toBe('ru');
    expect(url.searchParams.get('timeZone')).toBe('Europe/Moscow');
  });
});
