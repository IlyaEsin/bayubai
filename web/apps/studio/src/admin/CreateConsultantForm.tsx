import { useCreateConsultant } from '@bayubai/api-client';
import { languages } from '@bayubai/i18n';
import {
  Alert,
  Button,
  Card,
  ErrorAlert,
  Field,
  Input,
  PageTitle,
  Select,
  TimeZoneOptions,
  fieldAria,
  invalidFields,
  timeZoneListId,
  useMe,
} from '@bayubai/ui';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

export function CreateConsultantForm() {
  const { t } = useTranslation();
  const me = useMe();
  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  // Parents see the consultant's local time, so the zone is asked for up front; the admin's own zone is the likely default.
  const [language, setLanguage] = useState(me.language);
  const [timeZone, setTimeZone] = useState(me.timeZone);
  const create = useCreateConsultant();
  const invalid = invalidFields(create.error);
  const fieldError = (field: string) => (invalid.has(field) ? t('errors:field.invalid') : undefined);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    create.mutate({ data: { email, displayName, language, timeZone } });
  };

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <PageTitle>{t('studio:admin.title')}</PageTitle>
      <Card>
        <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
          <Field id="consultantEmail" label={t('studio:admin.email')} error={fieldError('email')}>
            <Input {...fieldAria('consultantEmail', { error: fieldError('email') })} type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
          </Field>
          <Field id="consultantName" label={t('studio:admin.displayName')} error={fieldError('displayName')}>
            <Input
              {...fieldAria('consultantName', { error: fieldError('displayName') })}
              maxLength={100}
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
            />
          </Field>
          <Field id="consultantLanguage" label={t('studio:admin.language')} error={fieldError('language')}>
            <Select {...fieldAria('consultantLanguage', { error: fieldError('language') })} value={language} onChange={(event) => setLanguage(event.target.value)}>
              {languages.map((option) => (
                <option key={option} value={option}>
                  {t(`common:language.${option}`)}
                </option>
              ))}
            </Select>
          </Field>
          <Field id="consultantTimeZone" label={t('studio:admin.timeZone')} error={fieldError('timeZone')}>
            <Input
              {...fieldAria('consultantTimeZone', { error: fieldError('timeZone') })}
              list={timeZoneListId}
              autoComplete="off"
              value={timeZone}
              onChange={(event) => setTimeZone(event.target.value)}
            />
            <TimeZoneOptions />
          </Field>
          <ErrorAlert error={create.error} />
          {create.isSuccess && <Alert tone="success">{t('studio:admin.created', { email: create.variables.data.email })}</Alert>}
          <Button type="submit" className="self-start" disabled={create.isPending}>
            {t('studio:admin.submit')}
          </Button>
        </form>
      </Card>
    </div>
  );
}
