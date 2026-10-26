import { useEffect, useState } from 'react';
import { Button, Card } from 'antd';
import { BankOutlined, CarOutlined, InboxOutlined, ProfileOutlined } from '@ant-design/icons';
import { Link } from 'react-router';
import { PageHeader } from '../../shared/ui/PageHeader';
import { useI18n } from '../../shared/i18n/I18nContext';
import { readBanners, safeBannerLink, type PublicBanner } from './api';
import { TrackingSearch } from './TrackingSearch';
import './public.css';
export function HomePage() {
  const { locale } = useI18n(); const en = locale === 'en-US';
  const [banners, setBanners] = useState<PublicBanner[]>([]); const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);
  useEffect(() => { const controller = new AbortController(); void readBanners(controller.signal).then(items => { if (!controller.signal.aborted) setBanners(items.filter(item => Boolean(safeBannerLink(item.imageUrl)))); }).catch(() => {}); return () => controller.abort(); }, []);
  useEffect(() => {
    if (banners.length < 2 || paused) return;
    const timer = window.setInterval(() => setIndex(value => (value + 1) % banners.length), 3000);
    return () => window.clearInterval(timer);
  }, [banners.length, paused]);
  const banner = banners[index]; const link = safeBannerLink(banner?.linkUrl ?? null);
  const services = en ? [['Pickup service', 'Create and manage clear pickup requests.'], ['Shipping service', 'Manage your shop and create shipments in one flow.'], ['Cash on delivery', 'Track collections and COD reconciliation.'], ['Shipment processing', 'Follow shipment status at every operational milestone.']] : [['Dịch vụ lấy hàng', 'Hỗ trợ khởi tạo và xử lý yêu cầu lấy hàng rõ ràng.'], ['Dịch vụ gửi hàng', 'Quản lý thông tin shop và tạo đơn trong một luồng gọn.'], ['Dịch vụ COD', 'Theo dõi tiền thu hộ và đối soát minh bạch hơn.'], ['Xử lý vận đơn', 'Cập nhật trạng thái vận đơn rõ từng mốc nghiệp vụ.']];
  const icons = [<InboxOutlined />, <CarOutlined />, <BankOutlined />, <ProfileOutlined />];
  return <div className="public-page">
    <PageHeader title={en ? 'LAK Logistics' : 'LAK | Nền tảng quản lý logistics'} description={en ? 'Shipment management, tracking and COD reconciliation for your shop.' : 'Nền tảng quản lý vận chuyển, theo dõi đơn hàng và đối soát COD cho shop.'} />
    <section className="public-hero" aria-label={en ? 'LAK services' : 'Dịch vụ LAK'}>{banner ? <>{link ? <a href={link} target="_blank" rel="noopener noreferrer"><img src={banner.imageUrl} alt={banner.title} onError={() => { setBanners(items => items.filter(item => item.id !== banner.id)); setIndex(0); }} /></a> : <img src={banner.imageUrl} alt={banner.title} onError={() => { setBanners(items => items.filter(item => item.id !== banner.id)); setIndex(0); }} />}
      {banners.length > 1 && <div className="public-banner-controls"><Button onClick={() => setIndex((index + banners.length - 1) % banners.length)}>{en ? 'Previous banner' : 'Banner trước'}</Button><span>{index + 1} / {banners.length}</span><Button onClick={() => setIndex((index + 1) % banners.length)}>{en ? 'Next banner' : 'Banner tiếp'}</Button><Button onClick={() => setPaused(value => !value)}>{paused ? (en ? 'Resume banners' : 'Tiếp tục banner') : (en ? 'Pause banners' : 'Tạm dừng banner')}</Button></div>}</> : <img src="/images/landing/hero-banner.png" alt={en ? 'LAK logistics services' : 'Dịch vụ vận chuyển LAK'} />}</section>
    <Card><TrackingSearch /></Card>
    <div className="public-grid"><Card><h2>{en ? 'Start shipping' : 'Bắt đầu tạo đơn'}</h2><p>{en ? 'Create a pickup or shipping order.' : 'Tạo đơn lấy hàng hoặc gửi hàng.'}</p><Link to="/login">{en ? 'Sign in' : 'Đăng nhập'}</Link></Card><Card><h2>{en ? 'Join LAK' : 'Đồng hành cùng LAK'}</h2><p>{en ? 'Manage shipping for your shop.' : 'Quản lý vận chuyển cho shop của bạn.'}</p><Link to="/register-shop">{en ? 'Register your shop' : 'Đăng ký shop'}</Link></Card><Card><h2>{en ? 'Need support?' : 'Bạn cần hỗ trợ?'}</h2><p>{en ? 'Contact our customer support team.' : 'Liên hệ đội ngũ chăm sóc khách hàng.'}</p><Link to="/contact">{en ? 'Contact us' : 'Liên hệ với chúng tôi'}</Link></Card></div>
    <section id="services"><h2>{en ? 'LAK services' : 'Dịch vụ của LAK'}</h2><div className="public-grid public-services">{services.map(([title, description], i) => <Card key={title}><span className="public-service-icon" aria-hidden="true">{icons[i]}</span><h3>{title}</h3><p>{description}</p></Card>)}</div></section>
    <section id="about"><h2>{en ? 'About us' : 'Về chúng tôi'}</h2><p>{en ? 'LAK focuses on practical daily logistics: clear shipment creation, tracking and delivery operations.' : 'LAK hướng tới một giao diện logistics dễ sử dụng hằng ngày: bố cục đơn giản, hình ảnh đúng ngữ cảnh và ưu tiên thao tác thực tế.'}</p><div className="public-grid"><Card><h3>{en ? 'Clear operations' : 'Vận hành rõ ràng'}</h3><p>{en ? 'Create shipments, track progress and coordinate deliveries.' : 'Tập trung vào tạo đơn, tracking và xử lý giao nhận theo một luồng dễ quét.'}</p></Card><Card><h3>{en ? 'Ready to grow' : 'Sẵn sàng mở rộng'}</h3><p>{en ? 'A shared workspace for shops and delivery teams.' : 'Nền tảng vận hành logistics cho shop và đội điều phối.'}</p></Card></div></section>
  </div>;
}
