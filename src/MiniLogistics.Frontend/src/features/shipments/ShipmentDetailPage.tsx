import { ArrowLeftOutlined } from '@ant-design/icons';
import { Card, Descriptions, Result, Timeline, Typography } from 'antd';
import { Link, useLocation, useParams } from 'react-router';
import { useSession } from '../../shared/auth/SessionContext';
import { useI18n } from '../../shared/i18n/I18nContext';
import { useShipmentCopy } from '../../shared/i18n/shipmentCopy';
import { formatDateTime } from '../../shared/lib/dateTime';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { PageHeader } from '../../shared/ui/PageHeader';
import { AsyncContent } from '../../shared/ui/AsyncContent';
import { useShopContext } from '../shop-context/ShopContext';
import { ShopReadBoundary } from '../shop-context/ShopReadBoundary';
import { isShipmentId, useShipment } from './queries';
import { ShipmentStatusTag } from './ShipmentStatusTag';
import { ReadError } from './ReadError';
import { shipmentListUrl } from './shipmentFilters';
import { routePaths } from '../../app/router/routePaths';
import { ShipmentActions } from './ShipmentActions';
import { FileDownloadButton } from './FileDownloadButton';
import { labelUrl } from './downloads';
export function ShipmentDetailPage() {
  const { id = '' } = useParams(); const query = useShipment(id); const data = query.data;
  const location = useLocation();
  const { context } = useShopContext(); const { session } = useSession(); const { locale } = useI18n(); const copy = useShipmentCopy();
  const cod = context?.shops.find(shop => shop.shopId === context.selectedShopId)?.permissions.includes('ViewCod');
  const money = (value: string) => formatMoney(decimalString(value), data!.currency, locale);
  const savedSearch = typeof location.state?.listSearch === 'string' ? new URLSearchParams(location.state.listSearch) : null;
  if (context && savedSearch) savedSearch.set('shopId', context.selectedShopId);
  const back = context ? savedSearch ? `${routePaths.shipments}?${savedSearch}` : shipmentListUrl(context.selectedShopId) : routePaths.shipments;
  return <><PageHeader title={copy.detail} actions={context && <Link to={back}><ArrowLeftOutlined aria-hidden /> {copy.back}</Link>} />
    <ShopReadBoundary>{!isShipmentId(id) ? <Result status="404" title={copy.missing} /> : query.error ? <ReadError error={query.error} onRetry={() => void query.refetch()} />
      : <AsyncContent loading={query.isPending}>{data && <><ShipmentActions key={`${session!.userId}/${context!.selectedShopId}/${id}`} shipment={data} reload={() => query.refetch()} /><div className="detail-grid">
        <Card title={data.trackingCode}><ShipmentStatusTag status={data.status} />
          {data.status !== 'Draft' && context?.shops.find(shop => shop.shopId === context.selectedShopId)?.permissions.includes('ViewFullPii') && <FileDownloadButton path={labelUrl(context.selectedShopId, id)} mime="application/pdf" label={locale === 'en-US' ? 'Download shipping label' : 'Tải nhãn vận chuyển'} />}
          <Descriptions column={1} items={[
            { key: 'sender', label: copy.sender, children: `${data.senderName} · ${data.senderPhone}` },
            { key: 'receiver', label: copy.receiver, children: `${data.receiverName} · ${data.receiverPhone}` },
            { key: 'pickup', label: copy.pickup, children: data.pickupAddress.fullAddress },
            { key: 'delivery', label: copy.delivery, children: data.deliveryAddress.fullAddress },
            { key: 'created', label: copy.created, children: formatDateTime(data.createdAtUtc, session!.serverTimeZoneId, locale) },
            { key: 'note', label: copy.note, children: data.note || '—' },
          ]} />
        </Card>
        <Card title={copy.parcel}><Descriptions column={1} items={[
          { key: 'route', label: locale === 'en-US' ? 'Route' : 'Phân tuyến', children: data.routeType },
          { key: 'weight', label: copy.weight, children: `${data.weightKg} kg` }, { key: 'chargeable', label: copy.chargeable, children: `${data.chargeableWeightKg} kg` },
          { key: 'volumetric', label: locale === 'en-US' ? 'Volumetric weight' : 'Khối lượng quy đổi', children: `${data.volumetricWeightKg} kg` },
          { key: 'dimensions', label: copy.dimensions, children: `${data.parcelLengthCm} × ${data.parcelWidthCm} × ${data.parcelHeightCm} cm` },
          { key: 'goods', label: copy.goods, children: money(data.goodsValueAmount) },
          { key: 'base', label: copy.base, children: money(data.baseFeeAmount) }, { key: 'extra', label: copy.extra, children: money(data.extraWeightFeeAmount) },
          { key: 'insurance', label: copy.insurance, children: money(data.insuranceFeeAmount) }, { key: 'return', label: copy.returnFee, children: money(data.returnFeeAmount) },
          { key: 'total', label: copy.fee, children: <Typography.Text strong>{money(data.shippingFeeAmount)}</Typography.Text> },
          ...(cod ? [{ key: 'cod', label: 'COD', children: money(data.codAmount) }] : []),
        ]} /></Card>
        <Card title={copy.history} className="history-card"><Timeline items={data.trackingHistory.map((history, index) => ({ key: index,
          content: <><ShipmentStatusTag status={history.status} /><p>{formatDateTime(history.changedAtUtc, session!.serverTimeZoneId, locale)} · {history.changedByDisplayName}{history.changedByEmail ? ` (${history.changedByEmail})` : ''}</p><p>{history.note}</p></> }))} /></Card>
      </div></>}</AsyncContent>}
    </ShopReadBoundary></>;
}
