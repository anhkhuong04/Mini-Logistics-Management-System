import { useState } from 'react';
import { Button, Modal, Result, Typography } from 'antd';
import { Link, Route, Routes } from 'react-router';
import { AppLayout } from './layouts/AppLayout';
import { PageHeader } from '../shared/ui/PageHeader';
import { useI18n } from '../shared/i18n/I18nContext';
import { routePaths } from './router/routePaths';
import { LoginPage } from '../features/auth/LoginPage';
import { RegisterShopPage } from '../features/auth/RegisterShopPage';
import { useSession } from '../shared/auth/SessionContext';
import { RequireSession } from '../shared/auth/RequireSession';
import { LegacyFeatureRoute } from './router/LegacyFeatureRoute';
import { navigationFor } from './router/navigation';
import { ShopContextProvider } from '../features/shop-context/ShopContextProvider';
import { ShopContextSelector } from '../features/shop-context/ShopContextSelector';
import { useShopContext } from '../features/shop-context/ShopContext';
import { useAuthCopy } from '../shared/i18n/authCopy';
import { ShipmentListPage } from '../features/shipments/ShipmentListPage';
import { ShipmentDetailPage } from '../features/shipments/ShipmentDetailPage';

function Welcome() {
  const { t } = useI18n();
  const [isHelpOpen, setHelpOpen] = useState(false);
  const { session, isLoading, error } = useSession();

  return (
    <>
      <PageHeader title="Mini Logistics" description={t('description')} />
      {session?.roles.includes('Shop') && !isLoading && !error && <ShopContextSelector />}
      <section className="welcome-panel" aria-labelledby="welcome-heading">
        <Typography.Title level={2} id="welcome-heading">{t('welcome')}</Typography.Title>
        <Typography.Paragraph type="secondary">{t('welcomeDetail')}</Typography.Paragraph>
        <div className="welcome-actions">
          {!session && <Link className="ant-btn ant-btn-primary" to={routePaths.signIn}>{t('signIn')}</Link>}
          <Button onClick={() => setHelpOpen(true)}>{t('support')}</Button>
        </div>
        <div className="welcome-links"><a href={routePaths.faq}>{t('faq')}</a></div>
      </section>
      <Modal title={t('contactTitle')} open={isHelpOpen} onCancel={() => setHelpOpen(false)} footer={null}>
        <Typography.Paragraph>{t('supportDescription')}</Typography.Paragraph>
        <a href={routePaths.contact}>{t('contact')}</a>
      </Modal>
    </>
  );
}

function NotFound() {
  const { t } = useI18n();
  return <><PageHeader title={t('missingTitle')} />
    <Result status="404" subTitle={t('missingDescription')}
      extra={<Link className="ant-btn ant-btn-default" to={routePaths.home}>{t('backHome')}</Link>} />
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
  return <AppLayout navigation={navigationFor(isLoading || error ? null : session, context, locale)} sessionTools={tools}>
    {logoutError && <Result status="warning" title={copy.logoutFailure}
      extra={<Button onClick={() => { void logout().then(() => setLogoutError(false)).catch(() => setLogoutError(true)); }}>{copy.retry}</Button>} />}
    <Routes>
    <Route path={routePaths.home} element={<Welcome />} />
    <Route path="/login" element={<LoginPage />} />
    <Route path="/register-shop" element={<RegisterShopPage />} />
    <Route path={routePaths.shipments} element={<RequireSession roles={['Shop']}><ShipmentListPage /></RequireSession>} />
    <Route path={routePaths.shipmentDetailPattern} element={<RequireSession roles={['Shop']}><ShipmentDetailPage /></RequireSession>} />
    {['/dashboard', '/profile', '/shop/notifications'].map(path =>
      <Route key={path} path={path} element={<RequireSession roles={['Shop']}><LegacyFeatureRoute
        shopPermission="ViewShipments" /></RequireSession>} />)}
    <Route path="/shipments/create" element={<RequireSession roles={['Shop']}><LegacyFeatureRoute shopPermission="ManageShipments" /></RequireSession>} />
    <Route path="/shop/cod-report" element={<RequireSession roles={['Shop']}><LegacyFeatureRoute shopPermission="ViewCod" /></RequireSession>} />
    <Route path="/shop/audit" element={<RequireSession roles={['Shop']}><LegacyFeatureRoute shopPermission="ViewAudit" /></RequireSession>} />
    <Route path="/shop/staff" element={<RequireSession roles={['Shop']}><LegacyFeatureRoute ownerOnly /></RequireSession>} />
    <Route path="/operations/*" element={<RequireSession roles={['Admin', 'Operator']}><LegacyFeatureRoute /></RequireSession>} />
    <Route path="/shipper/*" element={<RequireSession roles={['Shipper']}><LegacyFeatureRoute /></RequireSession>} />
    <Route path="/admin/*" element={<RequireSession roles={['Admin']}><LegacyFeatureRoute /></RequireSession>} />
    <Route path="/partner/integrations" element={<RequireSession roles={['Admin', 'Shop', 'IntegrationAdmin']}><LegacyFeatureRoute /></RequireSession>} />
    <Route path="*" element={<NotFound />} />
  </Routes></AppLayout>;
}
export function App() { return <ShopContextProvider><AppContent /></ShopContextProvider>; }
