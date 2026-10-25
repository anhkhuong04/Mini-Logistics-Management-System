import type { ReactNode } from 'react';
import { Layout } from 'antd';
import { useI18n } from '../../shared/i18n/I18nContext';
import { LanguageLinks } from './LanguageLinks';
import styles from './AppLayout.module.css';
export function PublicLayout({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  return <Layout className={styles.shell}>
    <a className="skip-link" href="#main-content">{t('skip')}</a>
    <Layout.Header className={styles.header}><a className={styles.brand} href="/">Mini Logistics</a>
      <div className={styles.headerTools}><LanguageLinks /></div>
    </Layout.Header>
    <Layout.Content id="main-content" tabIndex={-1} className={styles.content}><div className={styles.main}>{children}</div></Layout.Content>
    <Layout.Footer className={styles.footer}>{t('footer')}</Layout.Footer>
  </Layout>;
}
