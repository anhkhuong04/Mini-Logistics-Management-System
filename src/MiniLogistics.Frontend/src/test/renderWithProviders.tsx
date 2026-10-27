import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { AppProviders } from '../app/providers/AppProviders';
import type { Locale } from '../shared/i18n/messages';
import { SessionProvider } from '../shared/auth/SessionProvider';
import type { Session } from '../shared/auth/SessionContext';

export function renderWithProviders(
  ui: ReactElement,
  {
    path = '/',
    locale = 'vi-VN',
    session = null,
  }: { path?: string; locale?: Locale; session?: Session | null } = {},
) {
  return {
    user: userEvent.setup(),
    ...render(ui, {
      wrapper: ({ children }) => (
        <AppProviders locale={locale} disableMotion>
          <MemoryRouter initialEntries={[path]}>
            <SessionProvider initialSession={session}>{children}</SessionProvider>
          </MemoryRouter>
        </AppProviders>
      ),
    }),
  };
}
