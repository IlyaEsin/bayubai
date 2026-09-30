import { getGetMeQueryKey, useDeleteMe, useGetProviders, useUpdateMe, type MeResponse } from '@bayubai/api-client';
import { detectTimeZone, languages, toLanguage } from '@bayubai/i18n';
import { useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { emailProvider, telegramProvider } from '../auth/SignInPanel';
import { EmailSignInForm } from '../auth/EmailSignInForm';
import { TelegramLoginButton } from '../auth/TelegramLoginButton';
import { Button } from '../components/button';
import { Field, Input, Select, fieldAria } from '../components/form';
import { Alert, Card, PageTitle, SectionTitle } from '../components/layout';
import { ErrorAlert } from '../errors/ErrorAlert';
import { invalidFields } from '../errors/problem';
import { externalSignInUrl } from '../session/navigation';
import { resetSession, useMe } from '../session/session';

const profilePath = '/profile';

export const timeZoneListId = 'time-zones';

function timeZoneOptions(): string[] {
  return typeof Intl.supportedValuesOf === 'function' ? Intl.supportedValuesOf('timeZone') : [];
}

// Shared by ProfileView and any other form that asks for an IANA zone, so the list of options is defined once.
export function TimeZoneOptions() {
  return (
    <datalist id={timeZoneListId}>
      {timeZoneOptions().map((zone) => (
        <option key={zone} value={zone} />
      ))}
    </datalist>
  );
}

function ProfileForm({ me }: { me: MeResponse }) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const [displayName, setDisplayName] = useState(me.displayName);
  const [language, setLanguage] = useState(me.language);
  const [timeZone, setTimeZone] = useState(me.timeZone);
  const update = useUpdateMe();
  const invalid = invalidFields(update.error);
  const fieldError = (field: string) => (invalid.has(field) ? t('errors:field.invalid') : undefined);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    update.mutate(
      { data: { displayName, language, timeZone } },
      {
        onSuccess: (saved) => {
          queryClient.setQueryData(getGetMeQueryKey(), saved);
          // Spec acceptance 6: the language switch applies at once, without a reload.
          void i18n.changeLanguage(saved.language);
        },
      },
    );
  };

  return (
    <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
      <Field id="displayName" label={t('profile:displayName')} error={fieldError('displayName')}>
        <Input
          {...fieldAria('displayName', { error: fieldError('displayName') })}
          autoComplete="name"
          maxLength={100}
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
        />
      </Field>
      <Field id="language" label={t('common:language.label')} error={fieldError('language')}>
        <Select {...fieldAria('language', { error: fieldError('language') })} value={language} onChange={(event) => setLanguage(event.target.value)}>
          {languages.map((option) => (
            <option key={option} value={option}>
              {t(`common:language.${option}`)}
            </option>
          ))}
        </Select>
      </Field>
      <Field id="timeZone" label={t('profile:timeZone')} hint={t('profile:timeZoneHint')} error={fieldError('timeZone')}>
        <Input
          {...fieldAria('timeZone', { error: fieldError('timeZone'), hint: t('profile:timeZoneHint') })}
          list={timeZoneListId}
          autoComplete="off"
          value={timeZone}
          onChange={(event) => setTimeZone(event.target.value)}
        />
        <TimeZoneOptions />
      </Field>
      <ErrorAlert error={update.error} />
      {update.isSuccess && <Alert tone="success">{t('common:saved')}</Alert>}
      <Button type="submit" className="self-start" disabled={update.isPending}>
        {t('common:save')}
      </Button>
    </form>
  );
}

function SignInMethods({ me }: { me: MeResponse }) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const providers = useGetProviders();
  const available = providers.data?.providers ?? [];
  const oauthToAdd = available.filter(
    (provider) => provider !== emailProvider && provider !== telegramProvider && !me.signInMethods.includes(provider),
  );
  const telegramBot =
    available.includes(telegramProvider) && !me.signInMethods.includes(telegramProvider) ? providers.data?.telegramBotName : null;

  const link = (provider: string) =>
    window.location.assign(
      externalSignInUrl({ provider, next: profilePath, mode: 'link', language: toLanguage(i18n.language), timeZone: detectTimeZone() }),
    );

  return (
    <Card className="flex flex-col gap-4">
      <SectionTitle>{t('profile:methods.title')}</SectionTitle>
      <ul className="flex flex-wrap gap-2" aria-label={t('profile:methods.title')}>
        {me.signInMethods.map((method) => (
          <li key={method} className="rounded-full bg-soft px-3 py-1 text-sm font-semibold text-strong">
            {t(`auth:method.${method}`)}
          </li>
        ))}
      </ul>
      {oauthToAdd.map((provider) => (
        <Button key={provider} variant="secondary" width="full" onClick={() => link(provider)}>
          {t('profile:methods.add', { method: t(`auth:method.${provider}`) })}
        </Button>
      ))}
      {telegramBot && (
        <TelegramLoginButton
          botName={telegramBot}
          mode="link"
          onSignedIn={() => void queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() })}
        />
      )}
      <EmailSignInForm mode="link" next={profilePath} />
    </Card>
  );
}

function DeleteAccount({ onDeleted }: { onDeleted: () => void }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [confirming, setConfirming] = useState(false);
  const remove = useDeleteMe({
    mutation: {
      onSuccess: async () => {
        await resetSession(queryClient);
        onDeleted();
      },
    },
  });

  return (
    <Card className="flex flex-col gap-3">
      <SectionTitle>{t('profile:delete.title')}</SectionTitle>
      <p className="text-sm text-muted-foreground">{t('profile:delete.warning')}</p>
      <ErrorAlert error={remove.error} />
      {confirming ? (
        <div className="flex flex-wrap gap-2">
          <Button variant="danger" disabled={remove.isPending} onClick={() => remove.mutate()}>
            {t('profile:delete.confirm')}
          </Button>
          <Button variant="ghost" onClick={() => setConfirming(false)}>
            {t('common:cancel')}
          </Button>
        </div>
      ) : (
        <Button variant="secondary" className="self-start" onClick={() => setConfirming(true)}>
          {t('profile:delete.start')}
        </Button>
      )}
    </Card>
  );
}

export function ProfileView({ onDeleted }: { onDeleted: () => void }) {
  const { t } = useTranslation();
  const me = useMe();

  return (
    <div className="flex flex-col gap-6">
      <PageTitle>{t('profile:title')}</PageTitle>
      <Card>
        <ProfileForm me={me} />
      </Card>
      <SignInMethods me={me} />
      <DeleteAccount onDeleted={onDeleted} />
    </div>
  );
}
