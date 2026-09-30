import { useCallback, useEffect, useState } from 'react';

export type ThemePreference = 'system' | 'light' | 'dark';

export type ResolvedTheme = 'light' | 'dark';

export const themePreferences: readonly ThemePreference[] = ['system', 'light', 'dark'];

// The same key is read by the inline script in each app's index.html before the first paint.
export const themeStorageKey = 'bb.theme';

const darkQuery = '(prefers-color-scheme: dark)';

export function readThemePreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(themeStorageKey);
    return stored === 'light' || stored === 'dark' ? stored : 'system';
  } catch {
    return 'system';
  }
}

export function resolveTheme(preference: ThemePreference, systemPrefersDark: boolean): ResolvedTheme {
  if (preference === 'system') {
    return systemPrefersDark ? 'dark' : 'light';
  }

  return preference;
}

export function applyTheme(preference: ThemePreference): void {
  document.documentElement.dataset.theme = resolveTheme(preference, window.matchMedia(darkQuery).matches);
}

export function useThemePreference(): [ThemePreference, (preference: ThemePreference) => void] {
  const [preference, setPreference] = useState(readThemePreference);

  useEffect(() => {
    applyTheme(preference);
    if (preference !== 'system') {
      return;
    }

    // Parents use the app at night, so "system" must follow the OS switching to dark while the app is open.
    const media = window.matchMedia(darkQuery);
    const follow = () => applyTheme('system');
    media.addEventListener('change', follow);
    return () => media.removeEventListener('change', follow);
  }, [preference]);

  const choose = useCallback((next: ThemePreference) => {
    try {
      if (next === 'system') {
        localStorage.removeItem(themeStorageKey);
      } else {
        localStorage.setItem(themeStorageKey, next);
      }
    } catch {
      // Private mode can block storage; the choice still applies for this visit.
    }

    setPreference(next);
  }, []);

  return [preference, choose];
}
