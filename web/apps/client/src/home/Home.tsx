import { useListMyConsultants } from '@bayubai/api-client';
import { Card, ErrorAlert, PageTitle, SectionTitle, useMe } from '@bayubai/ui';
import { useTranslation } from 'react-i18next';
import { ConsultantCard } from './ConsultantCard';

// Kept out of the route file so TanStack Router's code-splitting still applies to the route module.
export function Home() {
  const { t } = useTranslation();
  const me = useMe();
  const consultants = useListMyConsultants();

  return (
    <>
      <PageTitle>{t('home:greeting', { name: me.displayName })}</PageTitle>
      {consultants.isError && <ErrorAlert error={consultants.error} />}
      {consultants.isSuccess &&
        (consultants.data.length > 0 ? (
          consultants.data.map((consultant) => <ConsultantCard key={consultant.userId} consultant={consultant} />)
        ) : (
          <Card className="flex flex-col gap-2">
            <SectionTitle>{t('home:emptyTitle')}</SectionTitle>
            <p className="text-muted-foreground">{t('home:emptyBody')}</p>
          </Card>
        ))}
    </>
  );
}
