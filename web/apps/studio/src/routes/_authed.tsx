import { AppShell, hasRole, loadSession, navLinkClass, roles, useMe } from '@bayubai/ui';
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
  const me = useMe();
  const navigate = useNavigate();

  return (
    <AppShell
      width="wide"
      onSignedOut={() => void navigate({ to: '/sign-in', search: {} })}
      nav={
        <>
          <Link to="/" className={navLinkClass} activeOptions={{ exact: true }}>
            {t('studio:title')}
          </Link>
          {hasRole(me, roles.admin) && (
            <Link to="/admin/consultants" className={navLinkClass}>
              {t('studio:admin.nav')}
            </Link>
          )}
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
