import { useRef, useState, type KeyboardEvent, type ReactNode } from 'react';
import { Button, Drawer, Layout, Menu } from 'antd';
import { CloseOutlined, HomeOutlined, MenuOutlined, TruckOutlined } from '@ant-design/icons';
import { Link, useLocation } from 'react-router';
import { useI18n } from '../../shared/i18n/I18nContext';
import { routePaths } from '../router/routePaths';
import { LanguageLinks } from './LanguageLinks';
import styles from './AppLayout.module.css';
export interface NavigationItem { key: string; label: string; to: string; icon?: ReactNode }
export function AppLayout({ children, navigation }: { children: ReactNode; navigation?: NavigationItem[] }) {
  const { t } = useI18n();
  const { pathname } = useLocation();
  const [open, setOpen] = useState(false);
  const trigger = useRef<HTMLButtonElement>(null);
  const content = useRef<HTMLElement>(null);
  const navigated = useRef(false);
  function keepFocusInDrawer(event: KeyboardEvent<HTMLElement>) {
    if (event.key !== 'Tab') return;
    const controls = Array.from(event.currentTarget.querySelectorAll<HTMLElement>(
      'a[href], button:not([disabled]), [tabindex="0"]',
    )).filter(element => element.getClientRects().length > 0);
    const first = controls[0];
    const last = controls.at(-1);
    // Keep Tab from leaving the last control for browser chrome, which has no focusin event.
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  }
  const items = (navigation ?? [{ key: 'home', label: t('home'), to: routePaths.home, icon: <HomeOutlined /> }])
    .map(item => ({ key: item.to, icon: item.icon, label: <Link to={item.to} onClick={() => {
      navigated.current = true;
      setOpen(false);
    }}>{item.label}</Link> }));
  const menu = <Menu mode="inline" aria-label={t('navigation')} selectedKeys={[pathname]} items={items} />;
  return <Layout className={styles.shell}>
    <a className="skip-link" href="#main-content">{t('skip')}</a>
    <Layout.Header className={styles.header}>
      <Button ref={trigger} className={styles.mobileToggle} icon={<MenuOutlined />} aria-label={t('openMenu')}
        aria-expanded={open} aria-controls="mobile-navigation" onClick={() => {
          navigated.current = false;
          setOpen(true);
        }} />
      <Link className={styles.brand} to={routePaths.home}><TruckOutlined aria-hidden />Mini Logistics</Link>
      <div className={styles.headerTools}><LanguageLinks /></div>
    </Layout.Header>
    <Layout className={styles.body}>
      <Layout.Sider width={224} className={styles.sider}><nav aria-label={t('navigation')}>{menu}</nav></Layout.Sider>
      <Layout>
        <Layout.Content ref={content} id="main-content" tabIndex={-1} className={styles.content}><div className={styles.main}>{children}</div></Layout.Content>
        <Layout.Footer className={styles.footer}>{t('footer')}</Layout.Footer>
      </Layout>
    </Layout>
    <Drawer placement="left" open={open} onClose={() => setOpen(false)} closable={false} onKeyDown={keepFocusInDrawer}
      focusable={{ focusTriggerAfterClose: false }}
      size={288} styles={{ wrapper: { maxWidth: 'calc(100vw - 24px)' } }}
      title={t('navigation')}
      extra={<Button icon={<CloseOutlined />} aria-label={t('closeMenu')} onClick={() => setOpen(false)} />}
      afterOpenChange={visible => {
        if (!visible) {
          if (navigated.current) content.current?.querySelector<HTMLElement>('h1[tabindex="-1"]')?.focus();
          else trigger.current?.focus();
          navigated.current = false;
        }
      }}>
      <nav id="mobile-navigation" aria-label={t('navigation')}>{menu}</nav>
    </Drawer>
  </Layout>;
}
