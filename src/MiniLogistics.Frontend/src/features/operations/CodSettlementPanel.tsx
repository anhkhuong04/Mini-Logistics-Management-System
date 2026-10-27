import { Button, Table } from 'antd';
import type { CodCandidate, OperationsPageData } from './api';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { formatDateTime } from '../../shared/lib/dateTime';
export function CodSettlementPanel({
  data,
  busy,
  messages,
  timeZone,
  onSettle,
  onPage,
}: {
  data: OperationsPageData<CodCandidate>;
  busy: (id: string) => boolean;
  messages: Record<string, string>;
  timeZone: string;
  onSettle: (item: CodCandidate) => void;
  onPage: (page: number, size: number) => void;
}) {
  return (
    <Table
      rowKey="shipmentId"
      dataSource={data.items}
      scroll={{ x: 720 }}
      pagination={{
        current: data.pageNumber,
        pageSize: data.pageSize,
        total: data.totalCount,
        showSizeChanger: true,
        pageSizeOptions: [10, 25, 50, 100],
        onChange: onPage,
      }}
      columns={[
        { title: 'Vận đơn', dataIndex: 'trackingCode' },
        {
          title: 'Người nhận',
          render: (_, item) => (
            <>
              {item.receiverName}
              <details>
                <summary>Thông tin liên hệ</summary>
                {item.receiverPhone}
                <br />
                {item.collectedByEmail}
              </details>
            </>
          ),
        },
        {
          title: 'COD đã thu',
          render: (_, item) => formatMoney(decimalString(item.codAmount), item.currency),
        },
        {
          title: 'Thời điểm thu',
          render: (_, item) =>
            item.collectedAtUtc ? formatDateTime(item.collectedAtUtc, timeZone, 'vi-VN') : 'Chưa ghi nhận',
        },
        { title: 'Người thu', render: (_, item) => item.collectedByName ?? 'Không còn thông tin người dùng' },
        { title: 'Kết quả', render: (_, item) => <span role="status">{messages[item.shipmentId]}</span> },
        {
          title: 'Thao tác',
          render: (_, item) => (
            <Button
              disabled={busy(item.shipmentId)}
              onClick={() => onSettle(item)}
              aria-label={`Đối soát ${item.trackingCode}`}
            >
              Đối soát
            </Button>
          ),
        },
      ]}
    />
  );
}
