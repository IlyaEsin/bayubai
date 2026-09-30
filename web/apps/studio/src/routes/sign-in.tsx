import { Card, PageTitle, PublicShell, SignInPanel, safeNext } from '@bayubai/ui';
import { createFileRoute, useRouter } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

export const Route = createFileRoute('/sign-in')({
  validateSearch: (search: Record<string, unknown>): { next?: string } => ({
    next: typeof search.next === 'string' ? search.next : undefined,
  }),
  component: SignIn,
});

function SignIn() {
  const { t } = useTranslation();
  const { next } = Route.useSearch();
  const router = useRouter();

  return (
    <PublicShell>
      <div className="flex flex-col gap-1">
        <PageTitle>{t('auth:title')}</PageTitle>
        <p className="text-muted-foreground">{t('auth:subtitle')}</p>
      </div>
      <Card>
        <SignInPanel next={safeNext(next)} onSignedIn={(path) => router.history.push(path)} />
      </Card>
    </PublicShell>
  );
}
