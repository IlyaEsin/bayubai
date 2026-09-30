import { useCompleteTelegramSignIn, type TelegramCompleteRequestAuth } from '@bayubai/api-client';
import { detectTimeZone, toLanguage } from '@bayubai/i18n';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useEffectEvent, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorAlert } from '../errors/ErrorAlert';
import { resetSession } from '../session/session';
import type { SignInMode } from '../session/navigation';

declare global {
  interface Window {
    cnTelegramAuth?: (auth: TelegramCompleteRequestAuth) => void;
  }
}

export const telegramWidgetUrl = 'https://telegram.org/js/telegram-widget.js?22';

type TelegramLoginButtonProps = {
  botName: string;
  mode: SignInMode;
  onSignedIn: () => void;
};

// The official Login Widget renders its own button in an iframe and hands the signed payload to a global callback.
export function TelegramLoginButton({ botName, mode, onSignedIn }: TelegramLoginButtonProps) {
  const { i18n } = useTranslation();
  const queryClient = useQueryClient();
  const container = useRef<HTMLDivElement>(null);
  const complete = useCompleteTelegramSignIn();

  const onAuth = useEffectEvent((auth: TelegramCompleteRequestAuth) => {
    complete.mutate(
      { data: { auth, mode, language: toLanguage(i18n.language), timeZone: detectTimeZone() } },
      {
        onSuccess: async () => {
          await resetSession(queryClient);
          onSignedIn();
        },
      },
    );
  });

  useEffect(() => {
    const element = container.current;
    if (!element) {
      return;
    }

    window.cnTelegramAuth = (auth) => onAuth(auth);
    const script = document.createElement('script');
    script.src = telegramWidgetUrl;
    script.async = true;
    script.setAttribute('data-telegram-login', botName);
    script.setAttribute('data-size', 'large');
    script.setAttribute('data-onauth', 'cnTelegramAuth(user)');
    element.appendChild(script);

    return () => {
      element.replaceChildren();
      delete window.cnTelegramAuth;
    };
  }, [botName]);

  return (
    <div className="flex flex-col items-center gap-2">
      <div ref={container} className="min-h-11" />
      <ErrorAlert error={complete.error} />
    </div>
  );
}
