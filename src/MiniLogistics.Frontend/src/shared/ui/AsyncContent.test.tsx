import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { AsyncContent } from './AsyncContent';
import { StatusTag } from './StatusTag';

describe('shared content states', () => {
  it('announces loading and keeps stale content hidden', () => {
    renderWithProviders(
      <AsyncContent loading>
        <p>Previous customer data</p>
      </AsyncContent>,
    );
    expect(screen.getByRole('status')).toHaveTextContent('Đang tải');
    expect(screen.queryByText('Previous customer data')).not.toBeInTheDocument();
  });

  it('shows a safe error and retries only after a user action', async () => {
    const retry = vi.fn();
    const { user } = renderWithProviders(
      <AsyncContent error onRetry={retry}>
        <p>Private data</p>
      </AsyncContent>,
    );
    expect(screen.getByRole('alert')).toHaveTextContent('Không thể tải dữ liệu');
    expect(retry).not.toHaveBeenCalled();
    expect(screen.queryByText('Private data')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(retry).toHaveBeenCalledOnce();
  });

  it('distinguishes empty content from loaded content and gives status a text label', () => {
    const { rerender } = renderWithProviders(
      <AsyncContent empty>
        <p>Loaded</p>
      </AsyncContent>,
    );
    expect(screen.getByText('Chưa có dữ liệu')).toBeInTheDocument();
    expect(screen.queryByText('Loaded')).not.toBeInTheDocument();
    rerender(
      <AsyncContent>
        <StatusTag tone="success">Đã giao</StatusTag>
      </AsyncContent>,
    );
    expect(screen.getByText('Đã giao')).toBeInTheDocument();
  });
});
