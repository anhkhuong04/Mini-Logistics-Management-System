import { createContext, useContext } from 'react';
import type { Locale, MessageKey } from './messages';
export const I18nContext = createContext<{ locale: Locale; t: (key: MessageKey) => string } | null>(null);
export function useI18n() {
  const value = useContext(I18nContext);
  if (!value) throw new Error('I18nProvider is required');
  return value;
}
