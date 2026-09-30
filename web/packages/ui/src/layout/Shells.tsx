import { ApiProblem } from '@bayubai/api-client';
import { languages } from '@bayubai/i18n';
import { useEffect, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../components/button';
import { Select } from '../components/form';
import { ErrorAlert } from '../errors/ErrorAlert';
import { cn } from '../lib';
import { useMe, useSignOut } from '../session/session';
import { ThemeSwitcher } from '../theme/ThemeSwitcher';

// A text wordmark until a real logo exists.
function Wordmark() {
  const { t } = useTranslation();
  return <span className="text-xl font-bold tracking-tight text-accent">{t('common:appName')}</span>;
}

function LanguageSwitcher() {
  const { t, i18n } = useTranslation();
  return (
    <Select
      aria-label={t('common:language.label')}
      className="w-auto"
      value={i18n.language}
      onChange={(event) => void i18n.changeLanguage(event.target.value)}
    >
      {languages.map((option) => (
        <option key={option} value={option}>
          {t(`common:language.${option}`)}
        </option>
      ))}
    </Select>
  );
}

type Width = 'narrow' | 'wide';

const widths: Record<Width, string> = { narrow: 'max-w-xl', wide: 'max-w-5xl' };

// Before sign-in the language follows the browser and can be switched here; it is sent with the sign-in request.
export function PublicShell({ children, width = 'narrow' }: { children: ReactNode; width?: Width }) {
  return (
    <div className="flex min-h-dvh flex-col">
      <header className={cn('mx-auto flex w-full items-center justify-between gap-3 px-4 py-3', widths[width])}>
        <Wordmark />
        <div className="flex items-center gap-2">
          <LanguageSwitcher />
          <ThemeSwitcher />
        </div>
      </header>
      <main className={cn('mx-auto flex w-full flex-1 flex-col gap-6 px-4 pb-10', widths[width])}>{children}</main>
    </div>
  );
}

type AppShellProps = {
  nav: ReactNode;
  onSignedOut: () => void;
  children: ReactNode;
  width?: Width;
};

export function AppShell({ nav, onSignedOut, children, width = 'narrow' }: AppShellProps) {
  const { t, i18n } = useTranslation();
  const me = useMe();
  const signOut = useSignOut(onSignedOut);
  // A 401 means the session was already gone, so it is not shown as a failure; every other error is.
  const signOutError = signOut.error instanceof ApiProblem && signOut.error.status === 401 ? undefined : signOut.error;

  // After sign-in the profile language wins over the browser language.
  useEffect(() => {
    if (i18n.language !== me.language) {
      void i18n.changeLanguage(me.language);
    }
  }, [i18n, me.language]);

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="border-b border-border">
        <div className={cn('mx-auto flex w-full flex-wrap items-center justify-between gap-3 px-4 py-3', widths[width])}>
          <Wordmark />
          <nav aria-label={t('common:mainNav')} className="flex flex-wrap items-center gap-1">
            {nav}
          </nav>
          <div className="flex flex-col items-end gap-2">
            <div className="flex items-center gap-2">
              <ThemeSwitcher />
              <Button variant="ghost" onClick={() => signOut.mutate()} disabled={signOut.isPending}>
                {t('common:signOut')}
              </Button>
            </div>
            <ErrorAlert error={signOutError} />
          </div>
        </div>
      </header>
      <main className={cn('mx-auto flex w-full flex-1 flex-col gap-6 px-4 py-6', widths[width])}>{children}</main>
    </div>
  );
}

export const navLinkClass =
  'inline-flex min-h-11 items-center rounded-full px-4 font-semibold text-foreground hover:bg-muted aria-[current=page]:bg-soft aria-[current=page]:text-strong';
