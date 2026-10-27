import { screen } from '@testing-library/react';
import { expect, it } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { PublicLayout } from './PublicLayout';

it.each([
  { roles: ['Shop', 'Shipper', 'Operator'], destination: '/app/operations/assignments' },
  { roles: ['Shop', 'Shipper'], destination: '/app/shipper/shipments' },
])('uses the established login destination for mixed roles $roles', ({ roles, destination }) => {
  renderWithProviders(
    <PublicLayout>
      <h1>Public page</h1>
    </PublicLayout>,
    {
      locale: 'en-US',
      session: {
        userId: 'user',
        displayName: 'User',
        roles,
        operationPermissions: [],
        locale: 'en-US',
        serverTimeZoneId: 'UTC',
      },
    },
  );
  expect(screen.getByRole('link', { name: 'Workspace' })).toHaveAttribute('href', destination);
});
