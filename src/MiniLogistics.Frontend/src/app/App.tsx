import { lazy, Suspense, useState } from 'react';
const AdministrationRoutes = lazy(() => import('../features/administration/AdministrationRoutes').then(module => ({ default: module.AdministrationRoutes })));
import { Button, Result, Spin } from 'antd';
import { Link, Route, Routes, useLocation } from 'react-router';
import { PublicLayout } from './layouts/PublicLayout';
import { AppLayout } from './layouts/AppLayout';
import { PageHeader } from '../shared/ui/PageHeader';
import { useI18n } from '../shared/i18n/I18nContext';
import { routePaths } from './router/routePaths';
const LoginPage = lazy(() => import('../features/auth/LoginPage').then(module => ({ default: module.LoginPage })));
const RegisterShopPage = lazy(() => import('../features/auth/RegisterShopPage').then(module => ({ default: module.RegisterShopPage })));
import { useSession } from '../shared/auth/SessionContext';
import { RequireSession } from '../shared/auth/RequireSession';
const IntegrationClientsPage = lazy(() => import('../features/integrations/IntegrationClientsPage').then(module => ({ default: module.IntegrationClientsPage })));
import { navigationFor } from './router/navigation';
import { ShopContextProvider } from '../features/shop-context/ShopContextProvider';
import { useShopContext } from '../features/shop-context/ShopContext';
import { useAuthCopy } from '../shared/i18n/authCopy';
const ShipmentListPage = lazy(() => import('../features/shipments/ShipmentListPage').then(module => ({ default: module.ShipmentListPage })));
const ShipmentDetailPage = lazy(() => import('../features/shipments/ShipmentDetailPage').then(module => ({ default: module.ShipmentDetailPage })));
const ShopDashboardPage = lazy(() => import('../features/dashboard/ShopDashboardPage').then(module => ({ default: module.ShopDashboardPage })));
const ShipmentFormPage = lazy(() => import('../features/shipments/ShipmentFormPage').then(module => ({ default: module.ShipmentFormPage })));
const OperationsPage = lazy(() => import('../features/operations/OperationsPage').then(module => ({ default: module.OperationsPage })));
const ShipperWorkspacePage = lazy(() => import('../features/shipper/ShipperWorkspacePage').then(module => ({ default: module.ShipperWorkspacePage })));
const ShopProfilePage = lazy(() => import('../features/shop-settings/ShopProfilePage').then(module => ({ default: module.ShopProfilePage })));
const ShopStaffPage = lazy(() => import('../features/shop-settings/ShopStaffPage').then(module => ({ default: module.ShopStaffPage })));
const ShopCodReportPage = lazy(() => import('../features/shop-settings/ShopCodReportPage').then(module => ({ default: module.ShopCodReportPage })));
const ShopNotificationsPage = lazy(() => import('../features/shop-settings/ShopNotificationsPage').then(module => ({ default: module.ShopNotificationsPage })));
const ShopAuditPage = lazy(() => import('../features/shop-settings/ShopAuditPage').then(module => ({ default: module.ShopAuditPage })));

const HomePage = lazy(() => import('../features/public/HomePage').then(module => ({ default: module.HomePage })));
const FaqPage = lazy(() => import('../features/public/FaqPage').then(module => ({ default: module.FaqPage })));
const ContactPage = lazy(() => import('../features/public/ContactPage').then(module => ({ default: module.ContactPage })));
const TrackingPage = lazy(() => import('../features/public/TrackingPage').then(module => ({ default: module.TrackingPage })));

function NotFound() {
  const { t } = useI18n();
  return <><PageHeader title={t('missingTitle')} />
    <Result status="404" subTitle={t('missingDescription')}
      extra={<Link className="ant-btn ant-btn-default" to={routePaths.home}>{t('backHome')}</Link>} />
  </>;
}

function SystemError() {
  const { locale } = useI18n();
  const english = locale === 'en-US';
  return <>
    <PageHeader title={english ? 'An error occurred' : 'Đã xảy ra lỗi'}
      description={english ? 'We could not process this request. Try again or return to the home page.' : 'Hệ thống chưa thể xử lý yêu cầu hiện tại. Vui lòng thử lại hoặc quay về trang chủ.'} />
    <Link className="ant-btn ant-btn-default" to={routePaths.home}>{english ? 'Back to home' : 'Về trang chủ'}</Link>
  </>;
}

function AppContent() {
  const { session, logout, isLoading, error } = useSession();
  const { context } = useShopContext();
  const copy = useAuthCopy();
  const { locale } = useI18n();
  const [logoutError, setLogoutError] = useState(false);
  const tools = session && !isLoading && !error && <div className="session-tools"><span>{session.displayName}</span>
    <Button onClick={() => { void logout().catch(() => setLogoutError(true)); }}>{copy.logout}</Button></div>;
  const pathname = useLocation().pathname.toLowerCase().replace(/\/+$/, '') || '/';
  const publicRoute = ['/', '/faq', '/contact', '/tracking', '/login', '/register-shop', '/error'].includes(pathname);
  const content = <Suspense fallback={<Spin aria-label="Đang tải trang" />}>
    {logoutError && <Result status="warning" title={copy.logoutFailure}
      extra={<Button onClick={() => { void logout().then(() => setLogoutError(false)).catch(() => setLogoutError(true)); }}>{copy.retry}</Button>} />}
    <Routes>
    <Route path={routePaths.home} element={<HomePage />} />
    <Route path="/faq" element={<FaqPage />} /><Route path="/contact" element={<ContactPage />} /><Route path="/tracking" element={<TrackingPage />} />
    <Route path="/Error" element={<SystemError />} />
    <Route path="/login" element={<LoginPage />} />
    <Route path="/register-shop" element={<RegisterShopPage />} />
    <Route path={routePaths.shipments} element={<RequireSession roles={['Shop']}><ShipmentListPage /></RequireSession>} />
    <Route path={routePaths.shipmentDetailPattern} element={<RequireSession roles={['Shop']}><ShipmentDetailPage /></RequireSession>} />
    <Route path={routePaths.dashboard} element={<RequireSession roles={['Shop']}><ShopDashboardPage /></RequireSession>} />
    <Route path={routePaths.shopProfile} element={<RequireSession roles={['Shop']}><ShopProfilePage /></RequireSession>} />
    <Route path={routePaths.shopNotifications} element={<RequireSession roles={['Shop']}><ShopNotificationsPage /></RequireSession>} />
    <Route path={routePaths.shipmentCreate} element={<RequireSession roles={['Shop']}><ShipmentFormPage /></RequireSession>} />
    <Route path={routePaths.shopCodReport} element={<RequireSession roles={['Shop']}><ShopCodReportPage /></RequireSession>} />
    <Route path={routePaths.shopAudit} element={<RequireSession roles={['Shop']}><ShopAuditPage /></RequireSession>} />
    <Route path={routePaths.shopStaff} element={<RequireSession roles={['Shop']}><ShopStaffPage /></RequireSession>} />
    <Route path={routePaths.operations} element={<RequireSession roles={['Admin', 'Operator']}><OperationsPage /></RequireSession>} />
    <Route path={routePaths.operationsAlias} element={<RequireSession roles={['Admin', 'Operator']}><OperationsPage /></RequireSession>} />
    <Route path={routePaths.shipper} element={<RequireSession roles={['Shipper']}><ShipperWorkspacePage /></RequireSession>} />
    <Route path="/admin/*" element={<RequireSession roles={['Admin']}><AdministrationRoutes /></RequireSession>} />
    <Route path="/partner/integrations" element={<RequireSession roles={['Admin', 'Shop', 'IntegrationAdmin']}><IntegrationClientsPage /></RequireSession>} />
    <Route path="*" element={<NotFound />} />
  </Routes></Suspense>;
  return publicRoute ? <PublicLayout sessionTools={<>{session?.roles.includes('Shop') && <span>{context?.shops.find(shop => shop.shopId === context.selectedShopId)?.name}</span>}{tools}</>}>{content}</PublicLayout> : <AppLayout navigation={navigationFor(isLoading || error ? null : session, context, locale)} sessionTools={tools}>{content}</AppLayout>;
}
export function App() { return <ShopContextProvider><AppContent /></ShopContextProvider>; }
