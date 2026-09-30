import { getListMyConsultantsQueryKey, useAcceptInvitation } from '@bayubai/api-client';
import { Button, Card, ErrorAlert, SectionTitle } from '@bayubai/ui';
import { useQueryClient } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

type AcceptInvitationProps = {
  token: string;
  home: ReactNode;
};

// An explicit button, not an auto-accept on load: StrictMode or a reload would otherwise fire a second accept.
export function AcceptInvitation({ token, home }: AcceptInvitationProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const accept = useAcceptInvitation({
    mutation: { onSuccess: () => queryClient.invalidateQueries({ queryKey: getListMyConsultantsQueryKey() }) },
  });

  if (accept.isSuccess) {
    return (
      <Card className="flex flex-col gap-3">
        <SectionTitle>{t('invite:acceptedTitle')}</SectionTitle>
        <p>{t('invite:acceptedBody')}</p>
        {home}
      </Card>
    );
  }

  return (
    <Card className="flex flex-col gap-3">
      <ErrorAlert error={accept.error} />
      <Button width="full" disabled={accept.isPending} onClick={() => accept.mutate({ data: { token } })}>
        {t('invite:accept')}
      </Button>
    </Card>
  );
}
