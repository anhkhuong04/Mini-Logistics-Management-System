import { useEffect, useState } from 'react';
import { Alert, Button, Card, Empty, Spin, Tag, Timeline } from 'antd';
import { useSearchParams } from 'react-router';
import { PageHeader } from '../../shared/ui/PageHeader';
import { useI18n } from '../../shared/i18n/I18nContext';
import { useShipmentCopy } from '../../shared/i18n/shipmentCopy';
import { formatDateTime } from '../../shared/lib/dateTime';
import { readTracking, TrackingReadError, type PublicTracking } from './api';
import { TrackingSearch } from './TrackingSearch';
import './public.css';
const englishMessages: Record<string, string> = {
  SHIPMENT_DRAFT: 'The shipment was saved as a draft.', SHIPMENT_PENDING_PICKUP: 'The shipment is awaiting pickup.',
  SHIPMENT_ASSIGNED: 'The shipment has been assigned.', SHIPMENT_PICKING_UP: 'The courier is on the way to collect the shipment.',
  SHIPMENT_PICKED_UP: 'The shipment has been picked up.', SHIPMENT_IN_TRANSIT: 'The shipment is in transit.',
  SHIPMENT_DELIVERING: 'The shipment is being delivered.', SHIPMENT_DELIVERED: 'The shipment has been delivered successfully.',
  SHIPMENT_DELIVERY_FAILED: 'The delivery attempt was unsuccessful.', SHIPMENT_RETURNED: 'The shipment has been returned.', SHIPMENT_CANCELLED: 'The shipment has been cancelled.',
  SHIPMENT_DELIVERY_FAILED_RECEIVER_UNAVAILABLE: 'The receiver is not available to accept delivery.', SHIPMENT_DELIVERY_FAILED_WRONG_ADDRESS: 'The delivery address needs verification.',
  SHIPMENT_DELIVERY_FAILED_RECEIVER_REJECTED: 'The receiver declined the shipment.', SHIPMENT_DELIVERY_FAILED_CANNOT_CONTACT_RECEIVER: 'The receiver could not be contacted.',
  SHIPMENT_DELIVERY_FAILED_DAMAGED_PARCEL: 'The parcel needs inspection before delivery can continue.', SHIPMENT_DELIVERY_FAILED_PAYMENT_ISSUE: 'Delivery could not be completed due to a payment issue.',
  SHIPMENT_DELIVERY_FAILED_WEATHER_OR_ACCESS: 'Weather or access conditions prevented delivery.', SHIPMENT_STATUS_UPDATED: 'The shipment status has been updated.',
};
export function TrackingPage() {
  const { locale } = useI18n(); const en = locale === 'en-US'; const { labels } = useShipmentCopy();
  const [params, setParams] = useSearchParams(); const code = params.get('code')?.trim() ?? '';
  const [revision, setRevision] = useState(0);
  const [state, setState] = useState<{ code: string; data?: PublicTracking; error?: unknown; pending: boolean }>({ code: '', pending: false });
  useEffect(() => {
    if (!code || code.length > 30) return;
    const controller = new AbortController();
    void readTracking(code, controller.signal).then(data => { if (!controller.signal.aborted) setState({ code, data, pending: false }); }).catch((error: unknown) => { if (!controller.signal.aborted) setState({ code, error, pending: false }); });
    return () => controller.abort();
  }, [code, revision]);
  // A URL change immediately hides old results, even before the next request resolves.
  const current = state.code === code ? state : { code, pending: Boolean(code) };
  const pending = code.length <= 30 && current.pending;
  const invalid = code.length > 30;
  const error = current.error;
  const errorText = invalid ? (en ? 'Enter a tracking code of at most 30 characters.' : 'Vui lòng nhập mã vận đơn tối đa 30 ký tự.') : error instanceof TrackingReadError && error.status === 404 ? (en ? 'Shipment not found. Please check your tracking code.' : 'Không tìm thấy vận đơn. Vui lòng kiểm tra lại mã vận đơn.') : error instanceof TrackingReadError && error.status === 429 ? (error.retryAfter ? (en ? `Too many requests. Try again after ${error.retryAfter} seconds.` : `Bạn tra cứu quá nhanh. Vui lòng thử lại sau ${error.retryAfter} giây.`) : (en ? 'Too many requests. Please try again later.' : 'Bạn tra cứu quá nhanh. Vui lòng thử lại sau.')) : (en ? 'Unable to load tracking. Please try again.' : 'Không thể tải thông tin vận đơn. Vui lòng thử lại.');
  const data = !invalid && current.data;
  const date = (value: string) => formatDateTime(value, data ? data.serverTimeZoneId : 'UTC', locale);
  function search(next: string) {
    if (next === code) { setState({ code, pending: true }); setRevision(value => value + 1); }
    else { const updated = new URLSearchParams(params); updated.set('code', next); setParams(updated); }
  }
  return <div className="public-page"><PageHeader title={en ? 'Track your shipment' : 'Tra cứu vận đơn'} description={en ? 'Follow the status and journey of your shipment.' : 'Theo dõi trạng thái và hành trình vận đơn của bạn.'} />
    <Card><TrackingSearch key={code} initialCode={code} pending={pending} onSearch={search} /><p>{en ? 'Public tracking shows a safe summary. Personal contact details and full addresses are hidden.' : 'Tra cứu công khai chỉ hiển thị thông tin tóm tắt. Số điện thoại và địa chỉ đầy đủ được ẩn.'}</p></Card>
    <section aria-live="polite" aria-busy={pending} aria-label={en ? 'Tracking result' : 'Kết quả tra cứu'}>
      {pending ? <Spin description={en ? 'Loading tracking…' : 'Đang tra cứu…'}><div style={{ minHeight: 100 }} /></Spin> : invalid || error ? <Alert type="warning" showIcon title={errorText} action={!invalid && <Button onClick={() => search(code)}>{en ? 'Try again' : 'Thử lại'}</Button>} /> : data ? <Card>
        <h2>{data.trackingCode}</h2><Tag color={data.currentStatus === 'Delivered' ? 'success' : ['Cancelled', 'Returned', 'DeliveryFailed'].includes(data.currentStatus) ? 'error' : 'processing'}>{labels[data.currentStatus]}</Tag>
        <dl className="public-tracking-summary"><div><dt>{en ? 'From' : 'Nơi gửi'}</dt><dd>{data.pickupProvince}</dd></div><div><dt>{en ? 'To' : 'Nơi nhận'}</dt><dd>{data.deliveryProvince}</dd></div><div><dt>{en ? 'Sender' : 'Người gửi'}</dt><dd>{data.senderName || '—'}</dd></div><div><dt>{en ? 'Receiver' : 'Người nhận'}</dt><dd>{data.receiverName || '—'}</dd></div><div><dt>{en ? 'Created at' : 'Ngày tạo'}</dt><dd>{date(data.createdAtUtc)}</dd></div><div><dt>{en ? 'Last updated' : 'Cập nhật lần cuối'}</dt><dd>{date(data.lastUpdatedAtUtc)}</dd></div></dl>
        <h3>{en ? 'Shipment journey' : 'Hành trình vận đơn'}</h3><Timeline items={[...data.timeline].sort((a, b) => Date.parse(b.changedAtUtc) - Date.parse(a.changedAtUtc)).map(item => ({ content: <><strong>{labels[item.status]}</strong><p>{en ? englishMessages[item.messageCode] ?? (item.locale === locale ? item.message : 'The shipment status has been updated.') : item.message}</p><time dateTime={item.changedAtUtc}>{date(item.changedAtUtc)}</time></> }))} />
      </Card> : <Empty description={en ? 'Enter a tracking code to see shipment details.' : 'Nhập mã vận đơn để xem kết quả tra cứu.'} />}
    </section>
  </div>;
}
