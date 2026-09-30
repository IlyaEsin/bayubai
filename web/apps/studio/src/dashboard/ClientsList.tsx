import { useListClients, type MeResponse } from '@bayubai/api-client';
import { formatDateTime, formatLocalTime } from '@bayubai/i18n';
import { Card, ErrorAlert, SectionTitle, useNow } from '@bayubai/ui';
import { useTranslation } from 'react-i18next';

// Clients may live in other zones and read another language: "since" is in the consultant's zone, "local time" in the client's.
export function ClientsList({ me }: { me: MeResponse }) {
  const { t, i18n } = useTranslation();
  const clients = useListClients();
  const now = useNow();

  return (
    <Card className="flex flex-col gap-3">
      <SectionTitle>{t('studio:clients.title')}</SectionTitle>
      <ErrorAlert error={clients.error} />
      {clients.data?.length === 0 && <p className="text-muted-foreground">{t('studio:clients.empty')}</p>}
      <ul className="flex flex-col divide-y divide-border">
        {clients.data?.map((client) => (
          <li key={client.userId} className="flex flex-col gap-0.5 py-2">
            <span className="font-semibold">{client.displayName}</span>
            <span className="text-sm text-muted-foreground">
              {t('studio:clients.since', { date: formatDateTime(client.linkedAt, me.timeZone, i18n.language) })}
            </span>
            <span className="text-sm text-muted-foreground">
              {t('studio:clients.localTime', { time: formatLocalTime(now, client.timeZone, i18n.language), zone: client.timeZone })}
            </span>
            <span className="text-sm text-muted-foreground">
              {t('studio:clients.language', { language: t(`common:language.${client.language}`) })}
            </span>
          </li>
        ))}
      </ul>
    </Card>
  );
}
