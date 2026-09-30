import { useGetProviders } from '@bayubai/api-client';
import { detectTimeZone, toLanguage } from '@bayubai/i18n';
import { useTranslation } from 'react-i18next';
import { Button } from '../components/button';
import { externalSignInUrl, safeNext } from '../session/navigation';
import { EmailSignInForm } from './EmailSignInForm';
import { TelegramLoginButton } from './TelegramLoginButton';

export const emailProvider = 'email';
export const telegramProvider = 'Telegram';

type SignInPanelProps = {
  next: string;
  onSignedIn: (next: string) => void;
};

export function SignInPanel({ next, onSignedIn }: SignInPanelProps) {
  const { t, i18n } = useTranslation();
  const providers = useGetProviders();
  const available = providers.data?.providers ?? [emailProvider];
  const oauth = available.filter((provider) => provider !== emailProvider && provider !== telegramProvider);
  const telegramBot = available.includes(telegramProvider) ? providers.data?.telegramBotName : null;

  const startOAuth = (provider: string) =>
    window.location.assign(
      externalSignInUrl({ provider, next, mode: 'signin', language: toLanguage(i18n.language), timeZone: detectTimeZone() }),
    );

  return (
    <div className="flex flex-col gap-4">
      {oauth.map((provider) => (
        <Button key={provider} variant="secondary" width="full" onClick={() => startOAuth(provider)}>
          {t('auth:continueWith', { method: t(`auth:method.${provider}`) })}
        </Button>
      ))}
      {telegramBot && <TelegramLoginButton botName={telegramBot} mode="signin" onSignedIn={() => onSignedIn(safeNext(next))} />}
      {(oauth.length > 0 || telegramBot) && (
        <div className="flex items-center gap-3 text-sm text-muted-foreground" aria-hidden>
          <span className="h-px flex-1 bg-border" />
          {t('auth:or')}
          <span className="h-px flex-1 bg-border" />
        </div>
      )}
      <EmailSignInForm mode="signin" next={next} />
    </div>
  );
}
