import { AppShell, loadSession, navLinkClass } from '@bayubai/ui';
import { Link, Outlet, createFileRoute, redirect, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

export const Route = createFileRoute('/_authed')({
  beforeLoad: async ({ context, location }) => {
    if (!(await loadSession(context.queryClient))) {
      throw redirect({ to: '/sign-in', search: { next: location.href } });
    }
  },
  component: AuthedLayout,
});

function AuthedLayout() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  return (
    <AppShell
      onSignedOut={() => void navigate({ to: '/sign-in', search: {} })}
      nav={
        <>
          <Link to="/" className={navLinkClass} activeOptions={{ exact: true }}>
            {t('common:home')}
          </Link>
          <Link to="/profile" className={navLinkClass}>
            {t('common:profile')}
          </Link>
        </>
      }
    >
      <Outlet />
    </AppShell>
  );
}
