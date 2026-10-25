import { useState, type ReactNode } from 'react';
import { App, ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import enUS from 'antd/locale/en_US';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '../../shared/i18n/I18nProvider';
import type { Locale } from '../../shared/i18n/messages';
import { antdTheme } from '../theme/antdTheme';
export function AppProviders({ children, locale = 'vi-VN', disableMotion = false }: {
  children: ReactNode; locale?: Locale; disableMotion?: boolean;
}) {
  const [queryClient] = useState(() => new QueryClient());
  return <QueryClientProvider client={queryClient}><I18nProvider locale={locale}>
    <ConfigProvider locale={locale === 'en-US' ? enUS : viVN}
      theme={{ ...antdTheme, token: { ...antdTheme.token, ...(disableMotion ? { motion: false } : {}) } }}>
      <App>{children}</App>
    </ConfigProvider>
  </I18nProvider></QueryClientProvider>;
}
