import type { ReactNode } from 'react';
import { Layout } from 'antd';
import { Link } from 'react-router';
import { TruckOutlined } from '@ant-design/icons';
import { useSession } from '../../shared/auth/SessionContext';
import { useI18n } from '../../shared/i18n/I18nContext';
import { LanguageLinks } from './LanguageLinks';
import { postLoginPath } from '../../features/auth/redirects';
import styles from './AppLayout.module.css';
export function PublicLayout({ children, sessionTools }: { children: ReactNode; sessionTools?: ReactNode }) {
  const { t, locale } = useI18n();
  const { session } = useSession();
  const english = locale === 'en-US';
  return (
    <Layout className={styles.shell}>
      <a className="skip-link" href="#main-content">
        {t('skip')}
      </a>
      <Layout.Header className={styles.header}>
        <Link className={styles.brand} to="/">
          <TruckOutlined aria-hidden />
          Mini Logistics
        </Link>
        <div className={styles.headerTools}>
          <LanguageLinks />
          {sessionTools}
        </div>
        <nav
          aria-label={english ? 'Public navigation' : 'Điều hướng công khai'}
          className="public-navigation"
        >
          <Link to="/">{english ? 'Home' : 'Trang chủ'}</Link>
          <Link to="/tracking">{english ? 'Track shipment' : 'Tra cứu'}</Link>
          <Link to="/faq">FAQ</Link>
          <Link to="/contact">{english ? 'Contact' : 'Liên hệ'}</Link>
          {session ? (
            <a href={postLoginPath(session.roles)}>{english ? 'Workspace' : 'Không gian làm việc'}</a>
          ) : (
            <>
              <Link to="/login">{english ? 'Sign in' : 'Đăng nhập'}</Link>
              <Link to="/register-shop">{english ? 'Register Shop' : 'Đăng ký Shop'}</Link>
            </>
          )}
        </nav>
      </Layout.Header>
      <Layout.Content id="main-content" tabIndex={-1} className={styles.content}>
        <div className={styles.main}>{children}</div>
      </Layout.Content>
      <Layout.Footer className={styles.footer}>{t('footer')}</Layout.Footer>
    </Layout>
  );
}
