import { screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../test/renderWithProviders';
import { App } from './App';
afterEach(() => vi.unstubAllGlobals());
it('renders the public landing without loading workspace navigation', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json([])));
  renderWithProviders(<App />);
  expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('LAK');
  expect(screen.getByRole('navigation', { name: 'Điều hướng công khai' })).toBeInTheDocument();
  expect(screen.queryByRole('menu')).not.toBeInTheDocument();
});

it('keeps the system error route distinct from missing pages without reflecting error details', async () => {
  renderWithProviders(<App />, { path: '/Error?detail=private-stack-trace', locale: 'en-US' });
  expect(await screen.findByRole('heading', { level: 1, name: 'An error occurred' })).toHaveFocus();
  expect(screen.getByText('We could not process this request. Try again or return to the home page.')).toBeVisible();
  expect(screen.getByRole('link', { name: 'Back to home' })).toHaveAttribute('href', '/');
  expect(screen.queryByText(/private-stack-trace/)).not.toBeInTheDocument();
});

it.each(['/FAQ', '/faq/', '/error/'])('keeps public layout for router-equivalent path %s', async path => {
  renderWithProviders(<App />, { path, locale: 'en-US' });
  expect(screen.getByRole('navigation', { name: 'Public navigation' })).toBeInTheDocument();
  expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  expect(await screen.findByRole('heading', { level: 1 })).toBeVisible();
});
