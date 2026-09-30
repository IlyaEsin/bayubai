import { CompleteExternalSignIn, PublicShell } from '@bayubai/ui';
import { createFileRoute, useRouter } from '@tanstack/react-router';

export const Route = createFileRoute('/auth/external')({
  validateSearch: (search: Record<string, unknown>): { error?: string; next?: string } => ({
    error: typeof search.error === 'string' ? search.error : undefined,
    next: typeof search.next === 'string' ? search.next : undefined,
  }),
  component: ExternalCallback,
});

function ExternalCallback() {
  const { error, next } = Route.useSearch();
  const router = useRouter();

  return (
    <PublicShell>
      <CompleteExternalSignIn error={error} next={next ?? '/'} onSignedIn={(path) => router.history.push(path)} />
    </PublicShell>
  );
}
