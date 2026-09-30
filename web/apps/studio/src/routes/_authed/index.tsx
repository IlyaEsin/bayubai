import { Card, PageTitle, SectionTitle, hasRole, roles, useMe } from '@bayubai/ui';
import { createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ClientsList } from '../../dashboard/ClientsList';
import { InvitationCreator } from '../../dashboard/InvitationCreator';
import { InvitationsList } from '../../dashboard/InvitationsList';

export const Route = createFileRoute('/_authed/')({
  component: Dashboard,
});

function Dashboard() {
  const { t } = useTranslation();
  const me = useMe();

  if (!hasRole(me, roles.consultant)) {
    return (
      <Card className="flex flex-col gap-2">
        <SectionTitle>{t('studio:noAccessTitle')}</SectionTitle>
        <p className="text-muted-foreground">{t('studio:noAccessBody')}</p>
      </Card>
    );
  }

  return (
    <>
      <PageTitle>{t('studio:title')}</PageTitle>
      <InvitationCreator me={me} />
      <div className="grid gap-6 lg:grid-cols-2">
        <ClientsList me={me} />
        <InvitationsList me={me} />
      </div>
    </>
  );
}
