import { useStartEmailSignIn } from '@bayubai/api-client';
import { detectTimeZone, toLanguage } from '@bayubai/i18n';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../components/button';
import { Field, Input, fieldAria } from '../components/form';
import { Alert } from '../components/layout';
import { ErrorAlert } from '../errors/ErrorAlert';
import { invalidFields } from '../errors/problem';
import { emailCallbackUrl, type SignInMode } from '../session/navigation';

type EmailSignInFormProps = {
  mode: SignInMode;
  next: string;
};

export function EmailSignInForm({ mode, next }: EmailSignInFormProps) {
  const { t, i18n } = useTranslation();
  const [email, setEmail] = useState('');
  const [sentTo, setSentTo] = useState<string | null>(null);
  const start = useStartEmailSignIn();
  const id = `email-${mode}`;
  const error = invalidFields(start.error).has('email') ? t('errors:field.invalid') : undefined;

  const submit = (event: FormEvent) => {
    event.preventDefault();
    start.mutate(
      {
        data: {
          email,
          callbackUrl: emailCallbackUrl(next),
          language: toLanguage(i18n.language),
          timeZone: detectTimeZone(),
          mode,
        },
      },
      { onSuccess: () => setSentTo(email) },
    );
  };

  if (sentTo) {
    return (
      <Alert tone="info" className="flex flex-col gap-2">
        <p className="font-semibold">{t('auth:email.sentTitle')}</p>
        <p>{t('auth:email.sentBody', { email: sentTo })}</p>
        <Button
          variant="ghost"
          className="self-start px-0"
          onClick={() => {
            setSentTo(null);
            start.reset();
          }}
        >
          {t('auth:email.retry')}
        </Button>
      </Alert>
    );
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3" noValidate>
      <Field id={id} label={t('auth:email.label')} error={error}>
        <Input
          {...fieldAria(id, { error })}
          type="email"
          autoComplete="email"
          inputMode="email"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
        />
      </Field>
      <ErrorAlert error={start.error} />
      <Button type="submit" width="full" disabled={start.isPending || email.trim() === ''}>
        {mode === 'link' ? t('profile:methods.addEmail') : t('auth:email.submit')}
      </Button>
    </form>
  );
}
