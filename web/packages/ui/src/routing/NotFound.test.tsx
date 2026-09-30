import { createI18n } from '@bayubai/i18n';
import { render, screen } from '@testing-library/react';
import { createMemoryHistory, createRootRoute, createRoute, createRouter, Outlet, RouterProvider } from '@tanstack/react-router';
import { describe, expect, it } from 'vitest';
import { AppProviders, createQueryClient } from '../app/AppProviders';
import { NotFound } from './NotFound';

// A tiny one-route tree exercises the real router not-found pipeline instead of calling the component in isolation.
function buildRouter() {
  const rootRoute = createRootRoute({
    component: () => (
      <AppProviders queryClient={createQueryClient()} i18n={createI18n('en')}>
        <Outlet />
      </AppProviders>
    ),
    notFoundComponent: NotFound,
  });
  const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: () => <p>home</p> });
  const routeTree = rootRoute.addChildren([indexRoute]);
  return createRouter({ routeTree, history: createMemoryHistory({ initialEntries: ['/missing'] }) });
}

describe('NotFound', () => {
  it('shows a translated message with a link home for an unknown URL', async () => {
    render(<RouterProvider router={buildRouter()} />);

    expect(await screen.findByText('Page not found')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Go to home' })).toHaveAttribute('href', '/');
  });
});
