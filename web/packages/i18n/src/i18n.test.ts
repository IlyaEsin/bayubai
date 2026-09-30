import { ErrorCode } from '@bayubai/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createI18n, detectBrowserLanguage, detectTimeZone, formatDateTime, formatLocalTime, resources } from './index';

const pluralSuffix = /_(zero|one|two|few|many|other)$/;

function baseKeys(dictionary: Record<string, string>): string[] {
  return [...new Set(Object.keys(dictionary).map((key) => key.replace(pluralSuffix, '')))].sort();
}

describe('dictionaries', () => {
  it('have the same namespaces in every language', () => {
    expect(Object.keys(resources.ru).sort()).toEqual(Object.keys(resources.en).sort());
  });

  // Plural forms differ by language (Russian has one/few/many), so parity compares keys without the plural suffix.
  it.each(Object.keys(resources.en))('have identical keys in namespace %s', (namespace) => {
    const ru = resources.ru[namespace as keyof typeof resources.ru] as Record<string, string>;
    const en = resources.en[namespace as keyof typeof resources.en] as Record<string, string>;

    expect(baseKeys(ru)).toEqual(baseKeys(en));
  });

  it('translate every error code the API publishes', () => {
    for (const code of Object.values(ErrorCode)) {
      expect(resources.ru.errors, code).toHaveProperty([code]);
      expect(resources.en.errors, code).toHaveProperty([code]);
    }
  });
});

describe('createI18n', () => {
  it('uses the three Russian plural forms', () => {
    const t = createI18n('ru').t;

    const phrase = (count: number) => t('studio:invite.validDays', { count, date: 'X' });

    expect([1, 2, 5, 14, 21].map(phrase)).toEqual([
      'Действует 1 день, до X',
      'Действует 2 дня, до X',
      'Действует 5 дней, до X',
      'Действует 14 дней, до X',
      'Действует 21 день, до X',
    ]);
  });

  it('looks up error codes that contain dots', () => {
    expect(createI18n('en').t('errors:identity.invite_expired')).toBe('The invitation has expired. Ask the consultant for a new one.');
  });
});

describe('detectBrowserLanguage', () => {
  it('takes the first supported base language', () => {
    expect(detectBrowserLanguage(['de-DE', 'ru-RU', 'en'])).toBe('ru');
  });

  it('falls back to English', () => {
    expect(detectBrowserLanguage(['de-DE'])).toBe('en');
  });
});

describe('formatDateTime', () => {
  it('shows the instant in the given time zone', () => {
    expect(formatDateTime('2026-01-05T09:00:00Z', 'Asia/Yekaterinburg', 'en')).toBe('Jan 5, 2026, 2:00 PM');
  });

  it('falls back to UTC for an unknown zone instead of throwing', () => {
    expect(formatDateTime('2026-01-05T09:00:00Z', 'Mars/Olympus', 'en')).toBe('Jan 5, 2026, 9:00 AM');
  });
});

describe('formatLocalTime', () => {
  it('shows the same moment as local time in two zones', () => {
    const now = new Date('2026-01-05T09:00:00Z');

    expect(formatLocalTime(now, 'Europe/Moscow', 'ru')).toBe('12:00');
    expect(formatLocalTime(now, 'Asia/Vladivostok', 'ru')).toBe('19:00');
  });
});

describe('detectTimeZone', () => {
  afterEach(() => vi.restoreAllMocks());

  it('falls back to UTC when the browser cannot name its zone', () => {
    vi.spyOn(Intl.DateTimeFormat.prototype, 'resolvedOptions').mockReturnValue({ timeZone: 'Etc/Unknown' } as Intl.ResolvedDateTimeFormatOptions);

    expect(detectTimeZone()).toBe('UTC');
  });
});
