import { Button, Collapse, Space, Table, Tag, Typography } from 'antd';
import type { OperationsShipment, OperationsPageData } from './api';
import { nextStatuses } from './models';
import { ShipmentStatusTag } from '../../shared/ui/ShipmentStatusTag';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { formatDateTime } from '../../shared/lib/dateTime';
export type OperationsAction = 'reassign' | 'cancel-assignment' | 'status' | 'collect';
export function AssignmentTable({
  data,
  busy,
  can,
  messages,
  timeZone,
  onAction,
  onPage,
}: {
  data: OperationsPageData<OperationsShipment>;
  busy: (id: string) => boolean;
  can: (permission: string) => boolean;
  messages: Record<string, string>;
  timeZone: string;
  onAction: (action: OperationsAction, item: OperationsShipment) => void;
  onPage: (page: number, size: number) => void;
}) {
  return (
    <Table
      rowKey="shipmentId"
      dataSource={data.items}
      scroll={{ x: 1040 }}
      pagination={{
        current: data.pageNumber,
        pageSize: data.pageSize,
        total: data.totalCount,
        showSizeChanger: true,
        pageSizeOptions: [10, 25, 50, 100],
        onChange: onPage,
      }}
      expandable={{
        expandedRowRender: (item) => (
          <Collapse
            items={[
              {
                key: 'history',
                label: 'Lịch sử vận đơn',
                children: (
                  <ul>
                    {item.trackingHistory.map((entry, index) => (
                      <li key={index}>
                        {entry.status} — {entry.note} —{' '}
                        {formatDateTime(entry.changedAtUtc, timeZone, 'vi-VN')}
                        {can('audit.view') && ` — ${entry.changedByDisplayName}`}
                      </li>
                    ))}
                  </ul>
                ),
              },
            ]}
          />
        ),
      }}
      columns={[
        {
          title: 'Vận đơn',
          dataIndex: 'trackingCode',
          render: (value, item) => (
            <>
              <Typography.Text strong>{value}</Typography.Text>
              {item.isSlaOverdue && <Tag color="warning">Quá SLA</Tag>}
              {messages[item.shipmentId] && <p role="status">{messages[item.shipmentId]}</p>}
            </>
          ),
        },
        {
          title: 'Người nhận',
          render: (_, item) => (
            <>
              {item.receiverName}
              <br />
              {item.receiverPhone}
              <br />
              {item.deliveryAddress.fullAddress}
            </>
          ),
        },
        { title: 'Lấy hàng', render: (_, item) => item.pickupAddress.fullAddress },
        {
          title: 'Shipper',
          render: (_, item) => (
            <>
              {item.activeShipperName ?? 'Chưa phân công'}
              <br />
              {item.activeShipperPhone}
            </>
          ),
        },
        { title: 'Trạng thái', render: (_, item) => <ShipmentStatusTag status={item.status} /> },
        {
          title: 'COD',
          render: (_, item) => (
            <>
              {formatMoney(decimalString(item.codAmount), item.currency)}
              <br />
              {item.codStatus}
            </>
          ),
        },
        {
          title: 'Phí vận chuyển',
          render: (_, item) => formatMoney(decimalString(item.shippingFeeAmount), item.currency),
        },
        { title: 'Ngày tạo', render: (_, item) => formatDateTime(item.createdAtUtc, timeZone, 'vi-VN') },
        {
          title: 'Thao tác',
          render: (_, item) => (
            <Space orientation="vertical" align="start">
              {item.status === 'Assigned' && can('assignment.reassign') && (
                <Button
                  disabled={busy(item.shipmentId)}
                  aria-label={`Đổi shipper ${item.trackingCode}`}
                  onClick={() => onAction('reassign', item)}
                >
                  Đổi shipper
                </Button>
              )}
              {item.status === 'Assigned' && can('assignment.cancel') && (
                <Button
                  danger
                  disabled={busy(item.shipmentId)}
                  aria-label={`Hủy phân công ${item.trackingCode}`}
                  onClick={() => onAction('cancel-assignment', item)}
                >
                  Hủy phân công
                </Button>
              )}
              {nextStatuses(item.status).length > 0 && can('shipment.status_update') && (
                <Button
                  disabled={busy(item.shipmentId)}
                  aria-label={`Cập nhật ${item.trackingCode}`}
                  onClick={() => onAction('status', item)}
                >
                  Cập nhật trạng thái
                </Button>
              )}
              {item.status === 'Delivered' &&
                item.codStatus === 'PendingCollection' &&
                can('cod.collect') && (
                  <Button
                    disabled={busy(item.shipmentId)}
                    aria-label={`Thu COD ${item.trackingCode}`}
                    onClick={() => onAction('collect', item)}
                  >
                    Thu COD
                  </Button>
                )}
            </Space>
          ),
        },
      ]}
    />
  );
}
