import { Card } from 'antd';
import { Link } from 'react-router';
import { PageHeader } from '../../shared/ui/PageHeader';
import { useI18n } from '../../shared/i18n/I18nContext';
import './public.css';
export function FaqPage() {
  const { locale } = useI18n();
  const en = locale === 'en-US';
  const questions = en
    ? [
        [
          'How do I track a shipment?',
          <>
            Enter your tracking code on the home page or visit <Link to="/tracking">Track shipment</Link> to
            see its status and journey.
          </>,
        ],
        [
          'How long do pickup and delivery take?',
          'Pickup is usually within 2–4 hours after order creation during business hours. Inner-city delivery is the same day or next day; inter-province and remote delivery takes 2–5 working days.',
        ],
        [
          'Is cash on delivery (COD) supported?',
          'COD collection is free on most routes. Shop reconciliation transfers are made three times a week, on Monday, Wednesday and Friday, to the registered bank account.',
        ],
        [
          'What should I do about lost or damaged goods?',
          <>
            Contact <a href="tel:19006885">1900 6885</a> within 48 hours of receiving the goods. Compensation
            depends on the declared value and selected service.
          </>,
        ],
        [
          'Can I integrate LAK with my online store?',
          'LAK provides a documented Partner API for shipment creation, tracking and status webhooks in your e-commerce system.',
        ],
      ]
    : [
        [
          'Làm thế nào để tra cứu vận đơn?',
          <>
            Nhập mã vận đơn trên Trang chủ hoặc truy cập <Link to="/tracking">Tra cứu vận đơn</Link> để xem
            lịch trình và trạng thái giao nhận.
          </>,
        ],
        [
          'Thời gian lấy hàng và giao hàng chuẩn của LAK là bao lâu?',
          'Thời gian lấy hàng thường trong vòng 2–4 giờ kể từ lúc tạo đơn thành công (trong giờ hành chính). Nội thành giao trong ngày hoặc muộn nhất vào ngày hôm sau. Liên tỉnh / vùng xa giao từ 2 đến 5 ngày làm việc.',
        ],
        [
          'LAK có hỗ trợ thu tiền hộ (COD) không và thời gian đối soát như thế nào?',
          'Miễn phí thu tiền hộ (COD) cho hầu hết các tuyến vận chuyển. Chuyển tiền đối soát COD cho Shop định kỳ 3 lần/tuần (Thứ 2, Thứ 4, Thứ 6) qua tài khoản ngân hàng đã đăng ký với LAK.',
        ],
        [
          'Cần làm gì khi hàng hóa bị thất lạc hoặc hư hỏng?',
          <>
            Liên hệ ngay tổng đài <a href="tel:19006885">1900 6885</a> trong vòng 48h kể từ lúc nhận hàng.
            Chính sách đền bù dựa trên giá trị khai báo và loại hình dịch vụ đã chọn.
          </>,
        ],
        [
          'Tôi có thể tích hợp LAK Logistics vào website bán hàng của mình không?',
          'LAK cung cấp Open API (Partner API) đầy đủ tài liệu để tích hợp dịch vụ tạo đơn, tra cứu và webhook cập nhật trạng thái vào hệ thống E-commerce của bạn.',
        ],
      ];
  return (
    <div className="public-page">
      <PageHeader
        title={en ? 'Frequently asked questions' : 'Câu hỏi thường gặp (FAQ)'}
        description={
          en
            ? 'Common questions from LAK customers and partners.'
            : 'Tổng hợp các thắc mắc phổ biến của Khách hàng & Đối tác khi sử dụng dịch vụ của LAK Logistics.'
        }
      />
      {questions.map(([question, answer], index) => (
        <Card key={index}>
          <h2>
            {index + 1}. {question}
          </h2>
          <p>{answer}</p>
        </Card>
      ))}
      <Link to="/contact">
        {en ? 'Still have questions? Contact us' : 'Vẫn còn thắc mắc? Liên hệ với chúng tôi'}
      </Link>
    </div>
  );
}
