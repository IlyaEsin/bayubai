import { useListInvitations, type MeResponse } from '@bayubai/api-client';
import { formatDateTime } from '@bayubai/i18n';
import { Card, ErrorAlert, SectionTitle } from '@bayubai/ui';
import { useTranslation } from 'react-i18next';

// Times are shown in the consultant's own profile zone.
export function InvitationsList({ me }: { me: MeResponse }) {
  const { t, i18n } = useTranslation();
  const invitations = useListInvitations();
  const format = (instant: string) => formatDateTime(instant, me.timeZone, i18n.language);

  return (
    <Card className="flex flex-col gap-3">
      <SectionTitle>{t('studio:invitations.title')}</SectionTitle>
      <ErrorAlert error={invitations.error} />
      {invitations.data?.length === 0 && <p className="text-muted-foreground">{t('studio:invitations.empty')}</p>}
      <ul className="flex flex-col divide-y divide-border">
        {invitations.data?.map((invitation) => (
          <li key={invitation.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
            <span>{t('studio:invitations.created', { date: format(invitation.createdAt) })}</span>
            <span className="text-muted-foreground">{t('studio:invitations.expires', { date: format(invitation.expiresAt) })}</span>
            <span className="rounded-full bg-soft px-3 py-0.5 font-semibold text-strong">{t(`studio:status.${invitation.status}`)}</span>
          </li>
        ))}
      </ul>
    </Card>
  );
}
