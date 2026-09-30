import type { ConsultantResponse } from '@bayubai/api-client';
import { formatLocalTime } from '@bayubai/i18n';
import { Card, SectionTitle, useNow } from '@bayubai/ui';
import { useTranslation } from 'react-i18next';

// The consultant may live in another zone, so the parent sees her local time before writing late at night.
export function ConsultantCard({ consultant }: { consultant: ConsultantResponse }) {
  const { t, i18n } = useTranslation();
  const now = useNow();

  return (
    <Card className="flex flex-col gap-1">
      <SectionTitle>{t('home:consultant.title')}</SectionTitle>
      <p className="text-lg font-semibold text-strong">{consultant.displayName}</p>
      <p className="text-sm text-muted-foreground">
        {t('home:consultant.localTime', {
          time: formatLocalTime(now, consultant.timeZone, i18n.language),
          zone: consultant.timeZone,
        })}
      </p>
    </Card>
  );
}
