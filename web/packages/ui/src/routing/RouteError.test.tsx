import { ApiProblem } from '@bayubai/api-client';
import { createI18n } from '@bayubai/i18n';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryHistory, createRootRoute, createRoute, createRouter, Outlet, RouterProvider } from '@tanstack/react-router';
import { describe, expect, it } from 'vitest';
import { AppProviders, createQueryClient } from '../app/AppProviders';
import { RouteError } from './RouteError';

// A tiny one-route tree exercises the real router error pipeline instead of calling the component in isolation.
function buildRouter(loader: () => unknown) {
  const rootRoute = createRootRoute({
    component: () => (
      <AppProviders queryClient={createQueryClient()} i18n={createI18n('en')}>
        <Outlet />
      </AppProviders>
    ),
  });
  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    loader,
    errorComponent: RouteError,
    component: () => <p>home</p>,
  });
  const routeTree = rootRoute.addChildren([indexRoute]);
  return createRouter({ routeTree, history: createMemoryHistory({ initialEntries: ['/'] }) });
}

describe('RouteError', () => {
  it('shows the translated message for a failed load', async () => {
    const router = buildRouter(() => {
      throw new ApiProblem(500, undefined);
    });

    render(<RouterProvider router={router} />);

    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('retries the load when the button is pressed', async () => {
    let attempts = 0;
    const router = buildRouter(() => {
      attempts += 1;
      if (attempts === 1) {
        throw new ApiProblem(500, undefined);
      }
    });

    render(<RouterProvider router={router} />);
    await screen.findByRole('button', { name: 'Try again' });

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    await waitFor(() => expect(screen.getByText('home')).toBeInTheDocument());
    expect(attempts).toBe(2);
  });
});
