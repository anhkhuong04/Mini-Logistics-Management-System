import { Route, Routes } from 'react-router';
import { Result } from 'antd';
import { AdminIdentityScope } from './AdminShell';
import { AdminDashboardPage } from './AdminDashboardPage';
import { AdminCodPage } from './AdminCodPage';
import { AdminAuditPage } from './AdminAuditPage';
import { AdminUsersPage } from './AdminUsersPage';
import { AdminShopsPage } from './AdminShopsPage';
import { AdminHubsPage } from './AdminHubsPage';
import { AdminSystemConfigPage } from './AdminSystemConfigPage';
import { AdminBannersPage } from './AdminBannersPage';
export function AdministrationRoutes() { return <AdminIdentityScope><Routes>
  <Route index element={<AdminDashboardPage />} /><Route path="dashboard" element={<AdminDashboardPage />} />
  <Route path="cod" element={<AdminCodPage />} /><Route path="audit-logs" element={<AdminAuditPage />} />
  <Route path="users" element={<AdminUsersPage />} /><Route path="shops" element={<AdminShopsPage />} />
  <Route path="hubs" element={<AdminHubsPage />} /><Route path="system-config" element={<AdminSystemConfigPage />} />
  <Route path="banners" element={<AdminBannersPage />} /><Route path="*" element={<Result status="404" />} />
</Routes></AdminIdentityScope>; }
