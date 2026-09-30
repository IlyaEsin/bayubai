import { configureApi } from '@bayubai/api-client';
import { createI18n, type Language } from '@bayubai/i18n';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { AppProviders, createQueryClient } from '../app/AppProviders';

// MSW in Node needs absolute URLs, so tests pin the API to a fixed origin.
export const testApi = 'http://api.test';

export function renderWithProviders(ui: ReactElement, language: Language = 'en', queryClient = createQueryClient()) {
  configureApi({ baseUrl: testApi });
  const i18n = createI18n(language);
  return { queryClient, i18n, ...render(<AppProviders queryClient={queryClient} i18n={i18n}>{ui}</AppProviders>) };
}
