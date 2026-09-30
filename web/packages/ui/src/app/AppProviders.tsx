import { ApiProblem } from '@bayubai/api-client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { i18n } from 'i18next';
import { useEffect, type ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';

// Client errors (4xx) are answers, not glitches, so only network failures and 5xx are retried.
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        refetchOnWindowFocus: false,
        retry: (failureCount, error) =>
          failureCount < 2 && !(error instanceof ApiProblem && error.status >= 400 && error.status < 500),
      },
    },
  });
}

type AppProvidersProps = {
  queryClient: QueryClient;
  i18n: i18n;
  children: ReactNode;
};

export function AppProviders({ queryClient, i18n, children }: AppProvidersProps) {
  useEffect(() => {
    const sync = (language: string) => {
      document.documentElement.lang = language;
    };
    sync(i18n.language);
    i18n.on('languageChanged', sync);
    return () => i18n.off('languageChanged', sync);
  }, [i18n]);

  return (
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>{children}</I18nextProvider>
    </QueryClientProvider>
  );
}
