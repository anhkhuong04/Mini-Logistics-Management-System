import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { AppProviders } from '../app/providers/AppProviders';
import type { Locale } from '../shared/i18n/messages';

export function renderWithProviders(ui: ReactElement, { path = '/', locale = 'vi-VN' }: { path?: string; locale?: Locale } = {}) {
  return {
    user: userEvent.setup(),
    ...render(ui, { wrapper: ({ children }) => <AppProviders locale={locale} disableMotion>
      <MemoryRouter initialEntries={[path]}>{children}</MemoryRouter>
    </AppProviders> }),
  };
}
