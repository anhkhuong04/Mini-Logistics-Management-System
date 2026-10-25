import { useState } from 'react';
import { Button, Modal, Result, Typography } from 'antd';
import { Link, Route, Routes } from 'react-router';
import { AppLayout } from './layouts/AppLayout';
import { PageHeader } from '../shared/ui/PageHeader';
import { useI18n } from '../shared/i18n/I18nContext';
import { routePaths } from './router/routePaths';

function Welcome() {
  const { t } = useI18n();
  const [isHelpOpen, setHelpOpen] = useState(false);

  return (
    <>
      <PageHeader title="Mini Logistics" description={t('description')} />
      <section className="welcome-panel" aria-labelledby="welcome-heading">
        <Typography.Title level={2} id="welcome-heading">{t('welcome')}</Typography.Title>
        <Typography.Paragraph type="secondary">{t('welcomeDetail')}</Typography.Paragraph>
        <div className="welcome-actions">
          <Button type="primary" href={routePaths.signIn}>{t('signIn')}</Button>
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

export function App() {
  return <AppLayout><Routes>
    <Route path={routePaths.home} element={<Welcome />} />
    <Route path="*" element={<NotFound />} />
  </Routes></AppLayout>;
}
