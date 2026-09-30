import { ApiProblem } from '@bayubai/api-client';
import type { i18n } from 'i18next';

// Prefers the API's error code, then a status message, so status-only answers (middleware 401, 500) still read well.
export function problemMessage(i18n: i18n, error: unknown): string {
  if (error instanceof ApiProblem) {
    if (error.status === 0) {
      return i18n.t('errors:network');
    }

    if (error.code && i18n.exists(`errors:${error.code}`)) {
      return i18n.t(`errors:${error.code}`);
    }

    if (i18n.exists(`errors:status.${error.status}`)) {
      return i18n.t(`errors:status.${error.status}`);
    }
  }

  return i18n.t('errors:unknown');
}

export function invalidFields(error: unknown): ReadonlySet<string> {
  return new Set(error instanceof ApiProblem ? Object.keys(error.errors) : []);
}
