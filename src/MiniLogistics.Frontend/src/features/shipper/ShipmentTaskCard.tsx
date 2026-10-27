import { Card, Collapse, Descriptions, Tag, Typography } from 'antd';
import type { CollectBody, ShipperShipment, StatusBody } from './api';
import { CodCollectionForm } from './CodCollectionForm';
import { canCollectCod } from './models';
import { ShipmentStatusTag } from '../../shared/ui/ShipmentStatusTag';
import { StatusActionForm } from './StatusActionForm';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { formatDateTime } from '../../shared/lib/dateTime';
export function ShipmentTaskCard({
  shipment,
  busy,
  pending,
  message,
  timeZone,
  onStatus,
  onCollect,
}: {
  shipment: ShipperShipment;
  busy: boolean;
  pending: boolean;
  message?: string;
  timeZone: string;
  onStatus: (body: StatusBody) => void;
  onCollect: (body: CollectBody) => void;
}) {
  return (
    <article className="shipper-task" aria-label={shipment.trackingCode}>
      <Card
        title={<Typography.Title level={2}>{shipment.trackingCode}</Typography.Title>}
        extra={<ShipmentStatusTag status={shipment.status} />}
      >
        <div className="shipper-route">
          <section aria-label="Lấy hàng">
            <Typography.Text strong>Lấy hàng</Typography.Text>
            <p>{shipment.senderName}</p>
            <a href={`tel:${shipment.senderPhone}`}>{shipment.senderPhone}</a>
            <p>{shipment.pickupAddress.fullAddress}</p>
          </section>
          <section aria-label="Giao hàng">
            <Typography.Text strong>Giao hàng</Typography.Text>
            <p>{shipment.receiverName}</p>
            <a href={`tel:${shipment.receiverPhone}`}>{shipment.receiverPhone}</a>
            <p>{shipment.deliveryAddress.fullAddress}</p>
          </section>
        </div>
        <Descriptions
          size="small"
          column={1}
          items={[
            {
              key: 'cod',
              label: 'COD',
              children: (
                <>
                  {formatMoney(decimalString(shipment.codAmount), shipment.currency)}{' '}
                  <Tag>{shipment.codStatus}</Tag>
                </>
              ),
            },
            {
              key: 'fee',
              label: 'Phí vận chuyển',
              children: formatMoney(decimalString(shipment.shippingFeeAmount), shipment.currency),
            },
            {
              key: 'date',
              label: 'Ngày tạo',
              children: formatDateTime(shipment.createdAtUtc, timeZone, 'vi-VN'),
            },
          ]}
        />
        <div className="shipper-task-actions" aria-busy={pending}>
          {canCollectCod(shipment.status, shipment.codStatus) && (
            <CodCollectionForm shipment={shipment} busy={busy} pending={pending} onRequest={onCollect} />
          )}
          <StatusActionForm
            key={`${shipment.status}:${shipment.codStatus}`}
            shipment={shipment}
            busy={busy}
            onRequest={onStatus}
          />
          {pending && <p role="status">Đang xử lý {shipment.trackingCode}…</p>}
          {message && <p role="status">{message}</p>}
        </div>
        <Collapse
          items={[
            {
              key: 'history',
              label: 'Timeline',
              children: (
                <ol className="shipper-timeline">
                  {shipment.trackingHistory.map((history, index) => (
                    <li key={index}>
                      <ShipmentStatusTag status={history.status} />
                      <time>{formatDateTime(history.changedAtUtc, timeZone, 'vi-VN')}</time>
                      <p>
                        {history.changedByUserFound
                          ? `${history.changedByDisplayName}${history.changedByEmail ? ` (${history.changedByEmail})` : ''}`
                          : 'Người dùng không xác định'}
                      </p>
                      <p>{history.note}</p>
                    </li>
                  ))}
                </ol>
              ),
            },
          ]}
        />
      </Card>
    </article>
  );
}
