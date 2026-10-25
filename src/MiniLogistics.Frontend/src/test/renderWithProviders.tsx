import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { App, ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';

export function renderWithProviders(ui: ReactElement) {
  return {
    user: userEvent.setup(),
    ...render(<ConfigProvider locale={viVN} theme={{ token: { motion: false } }}><App>{ui}</App></ConfigProvider>),
  };
}
