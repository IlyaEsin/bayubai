import { configureApi } from '@bayubai/api-client';
import { createI18n, detectBrowserLanguage } from '@bayubai/i18n';
import { AppProviders, createQueryClient, NotFound, RouteError } from '@bayubai/ui';
import { RouterProvider, createRouter } from '@tanstack/react-router';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { routeTree } from './routeTree.gen';
import './styles.css';

configureApi({ baseUrl: import.meta.env.VITE_API_BASE_URL ?? '' });

const queryClient = createQueryClient();
const i18n = createI18n(detectBrowserLanguage());
const router = createRouter({
  routeTree,
  context: { queryClient },
  scrollRestoration: true,
  defaultErrorComponent: RouteError,
  defaultNotFoundComponent: NotFound,
});

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppProviders queryClient={queryClient} i18n={i18n}>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>,
);
