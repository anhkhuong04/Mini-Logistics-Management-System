import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '../test/renderWithProviders';
import { App } from './App';

describe('application entry', () => {
  it('opens support information and provides a working contact link', async () => {
    const { user } = renderWithProviders(<App />);
    await user.click(screen.getByRole('button', { name: 'Hỗ trợ' }));
    expect(await screen.findByRole('dialog')).toHaveTextContent('Liên hệ hỗ trợ');
    expect(screen.getByRole('link', { name: 'Trang liên hệ' })).toHaveAttribute('href', '/contact');
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});
