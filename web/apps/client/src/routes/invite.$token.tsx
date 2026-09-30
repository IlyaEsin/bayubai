import { Card, PageTitle, PublicShell, SignInPanel, loadSession } from '@bayubai/ui';
import { Link, createFileRoute, useRouter } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { AcceptInvitation } from '../invite/AcceptInvitation';

export const Route = createFileRoute('/invite/$token')({
  beforeLoad: async ({ context }) => ({ signedIn: (await loadSession(context.queryClient)) !== null }),
  component: Invite,
});

function Invite() {
  const { t } = useTranslation();
  const { token } = Route.useParams();
  const { signedIn } = Route.useRouteContext();
  const router = useRouter();

  return (
    <PublicShell>
      <PageTitle>{t('invite:title')}</PageTitle>
      {signedIn ? (
        <AcceptInvitation
          token={token}
          home={
            <Link to="/" className="inline-flex min-h-11 items-center font-semibold text-accent underline">
              {t('invite:toHome')}
            </Link>
          }
        />
      ) : (
        <>
          <p className="text-muted-foreground">{t('invite:signInFirst')}</p>
          <Card>
            <SignInPanel next={`/invite/${token}`} onSignedIn={(path) => router.history.push(path)} />
          </Card>
        </>
      )}
    </PublicShell>
  );
}
