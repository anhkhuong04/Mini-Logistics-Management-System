import { Card } from 'antd';
import { EnvironmentOutlined, MailOutlined, PhoneOutlined } from '@ant-design/icons';
import { Link } from 'react-router';
import { PageHeader } from '../../shared/ui/PageHeader';
import { useI18n } from '../../shared/i18n/I18nContext';
import './public.css';
export function ContactPage() {
  const { locale } = useI18n(); const en = locale === 'en-US';
  return <div className="public-page"><PageHeader title={en ? 'Contact us' : 'Thông tin liên hệ'} description={en ? 'Questions about our services, shipment tracking or working with LAK? Contact our support team.' : 'Chào mừng bạn đến với LAK Logistics. Liên hệ qua các kênh dưới đây để được hỗ trợ dịch vụ, tra cứu vận đơn hoặc hợp tác cùng chúng tôi.'} />
    <div className="public-grid"><Card><h2><PhoneOutlined aria-hidden /> {en ? 'Customer support' : 'Tổng đài CSKH'}</h2><a href="tel:19006885">1900 6885</a><p>{en ? 'Available 8:00–22:00 every day' : 'Hỗ trợ từ 8:00–22:00 hằng ngày'}</p></Card>
      <Card><h2><MailOutlined aria-hidden /> {en ? 'Support email' : 'Email hỗ trợ'}</h2><a href="mailto:support@laklogistics.vn">support@laklogistics.vn</a><p>{en ? 'Response within 24 working hours' : 'Phản hồi trong vòng 24h làm việc'}</p></Card>
      <Card><h2><EnvironmentOutlined aria-hidden /> {en ? 'Head office' : 'Trụ sở chính'}</h2><p>{en ? 'LAK Building, 123 Nguyễn Trãi' : 'Tòa nhà LAK, 123 Nguyễn Trãi'}</p><p>{en ? 'Bến Thành Ward, District 1, Hồ Chí Minh City' : 'Phường Bến Thành, Quận 1, TP. Hồ Chí Minh'}</p></Card></div>
    <Card><h2>{en ? 'Need help with a shipment?' : 'Bạn cần hỗ trợ vận đơn?'}</h2><p>{en ? 'Have your tracking code ready when contacting support.' : 'Vui lòng chuẩn bị mã vận đơn khi liên hệ đội ngũ CSKH.'}</p><Link to="/tracking">{en ? 'Track shipment' : 'Tra cứu vận đơn'}</Link> · <Link to="/faq">{en ? 'Frequently asked questions' : 'Câu hỏi thường gặp'}</Link></Card>
  </div>;
}
