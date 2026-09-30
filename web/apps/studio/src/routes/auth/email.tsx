import { CompleteEmailSignIn, PublicShell } from '@bayubai/ui';
import { createFileRoute, useRouter } from '@tanstack/react-router';

export const Route = createFileRoute('/auth/email')({
  validateSearch: (search: Record<string, unknown>): { token?: string; next?: string } => ({
    token: typeof search.token === 'string' ? search.token : undefined,
    next: typeof search.next === 'string' ? search.next : undefined,
  }),
  component: EmailCallback,
});

function EmailCallback() {
  const { token, next } = Route.useSearch();
  const router = useRouter();

  return (
    <PublicShell>
      <CompleteEmailSignIn token={token} next={next ?? '/'} onSignedIn={(path) => router.history.push(path)} />
    </PublicShell>
  );
}
