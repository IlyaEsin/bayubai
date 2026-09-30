import { ApiProblem, useCompleteEmailSignIn } from '@bayubai/api-client';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useEffectEvent, useRef, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, PageTitle } from '../components/layout';
import { ErrorAlert } from '../errors/ErrorAlert';
import { safeNext } from '../session/navigation';
import { resetSession } from '../session/session';

type CompleteProps = {
  next: string;
  onSignedIn: (next: string) => void;
};

function Failed({ error, next }: { error: unknown; next: string }) {
  const { t } = useTranslation();
  return (
    <Card className="flex flex-col gap-4">
      <PageTitle>{t('auth:failedTitle')}</PageTitle>
      <ErrorAlert error={error} />
      <a
        className="inline-flex min-h-11 items-center font-semibold text-accent underline"
        href={`/sign-in?next=${encodeURIComponent(safeNext(next))}`}
      >
        {t('auth:backToSignIn')}
      </a>
    </Card>
  );
}

function Completing(): ReactNode {
  const { t } = useTranslation();
  return (
    <p role="status" className="text-muted-foreground">
      {t('auth:completing')}
    </p>
  );
}

// Landing page of the magic link: posts the token once, even under StrictMode's double effects.
export function CompleteEmailSignIn({ token, next, onSignedIn }: CompleteProps & { token: string | undefined }) {
  const queryClient = useQueryClient();
  const { mutate, error } = useCompleteEmailSignIn();
  const started = useRef(false);

  const succeed = useEffectEvent(async () => {
    await resetSession(queryClient);
    onSignedIn(safeNext(next));
  });

  useEffect(() => {
    if (started.current || !token) {
      return;
    }

    started.current = true;
    mutate({ data: { token } }, { onSuccess: () => void succeed() });
  }, [token, mutate]);

  if (!token) {
    return <Failed error={new ApiProblem(400, 'identity.magic_link_invalid')} next={next} />;
  }

  return error ? <Failed error={error} next={next} /> : <Completing />;
}

// Landing page after an OAuth round trip: the API appends ?error=<code> when it could not sign the user in.
export function CompleteExternalSignIn({ error, next, onSignedIn }: CompleteProps & { error: string | undefined }) {
  const queryClient = useQueryClient();

  const succeed = useEffectEvent(async () => {
    await resetSession(queryClient);
    onSignedIn(safeNext(next));
  });

  useEffect(() => {
    if (!error) {
      void succeed();
    }
  }, [error]);

  return error ? <Failed error={new ApiProblem(400, error)} next={next} /> : <Completing />;
}
