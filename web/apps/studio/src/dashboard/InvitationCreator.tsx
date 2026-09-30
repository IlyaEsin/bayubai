import { getListInvitationsQueryKey, useCreateInvitation, type MeResponse } from '@bayubai/api-client';
import { formatDateTime } from '@bayubai/i18n';
import { Button, Card, ErrorAlert, Input, SectionTitle, useNow } from '@bayubai/ui';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

const dayMs = 24 * 60 * 60 * 1000;

export function InvitationCreator({ me }: { me: MeResponse }) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const [copied, setCopied] = useState(false);
  const now = useNow();
  const create = useCreateInvitation({
    mutation: {
      onSuccess: () => {
        setCopied(false);
        return queryClient.invalidateQueries({ queryKey: getListInvitationsQueryKey() });
      },
    },
  });
  const invitation = create.data;

  const copy = async (url: string) => {
    await navigator.clipboard.writeText(url);
    setCopied(true);
  };

  return (
    <Card className="flex flex-col gap-3">
      <Button className="self-start" disabled={create.isPending} onClick={() => create.mutate()}>
        {t('studio:invite.create')}
      </Button>
      <ErrorAlert error={create.error} />
      {invitation && (
        <div className="flex flex-col gap-2">
          <SectionTitle>{t('studio:invite.readyTitle')}</SectionTitle>
          <p className="text-sm text-muted-foreground">{t('studio:invite.hint')}</p>
          <div className="flex flex-wrap gap-2">
            <Input readOnly value={invitation.url} aria-label={t('studio:invite.readyTitle')} className="flex-1" onFocus={(event) => event.target.select()} />
            <Button variant="secondary" onClick={() => void copy(invitation.url)}>
              {copied ? t('common:copied') : t('common:copy')}
            </Button>
          </div>
          <p className="text-sm">
            {t('studio:invite.validDays', {
              count: Math.round((Date.parse(invitation.expiresAt) - now.getTime()) / dayMs),
              date: formatDateTime(invitation.expiresAt, me.timeZone, i18n.language),
            })}
          </p>
        </div>
      )}
    </Card>
  );
}
