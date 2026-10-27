import type { ReactNode } from 'react';
import { Alert, Button, Empty, Spin } from 'antd';
import { useI18n } from '../i18n/I18nContext';
export function AsyncContent({
  loading,
  error,
  empty,
  onRetry,
  children,
}: {
  loading?: boolean;
  error?: boolean;
  empty?: boolean;
  onRetry?: () => void;
  children: ReactNode;
}) {
  const { t } = useI18n();
  if (loading)
    return (
      <div className="content-state" role="status" aria-live="polite">
        <Spin /> {t('loading')}
      </div>
    );
  if (error)
    return (
      <Alert
        type="error"
        showIcon
        title={t('loadError')}
        action={onRetry && <Button onClick={onRetry}>{t('retry')}</Button>}
      />
    );
  if (empty) return <Empty className="content-state" description={t('empty')} />;
  return <>{children}</>;
}
