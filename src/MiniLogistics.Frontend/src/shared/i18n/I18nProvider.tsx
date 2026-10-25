import { useLayoutEffect, type ReactNode } from 'react';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import 'dayjs/locale/en';
import { I18nContext } from './I18nContext';
import { messages, type Locale, type MessageKey } from './messages';
export function I18nProvider({ locale, children }: { locale: Locale; children: ReactNode }) {
  useLayoutEffect(() => {
    document.documentElement.lang = locale === 'en-US' ? 'en' : 'vi';
    dayjs.locale(locale === 'en-US' ? 'en' : 'vi');
  }, [locale]);
  return <I18nContext.Provider value={{ locale, t: (key: MessageKey) => messages[locale][key] }}>{children}</I18nContext.Provider>;
}
