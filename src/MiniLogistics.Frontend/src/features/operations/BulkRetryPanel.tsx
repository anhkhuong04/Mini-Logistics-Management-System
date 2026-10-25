import { Button, Space, Table, Typography } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import type { BulkResult } from './api';
export function BulkRetryPanel({ count, disabled, result, onRetry, onClear }: { count: number; disabled: boolean; result?: BulkResult; onRetry: () => void; onClear: () => void }) {
  return <section aria-label="Retry hàng loạt"><Space wrap>
    <Button icon={<ReloadOutlined aria-hidden />} disabled={disabled || count === 0 || count > 50} onClick={onRetry}>Retry tự động các đơn đã chọn ({count}/50)</Button>
    <Button disabled={count === 0} onClick={onClear}>Bỏ chọn</Button>
  </Space>{result && <>
    <Typography.Paragraph role="status">Đã phân công {result.assignedCount}/{result.requestedCount}; bỏ qua {result.skippedCount}. Xem kết quả từng đơn.</Typography.Paragraph>
    <Table size="small" rowKey="shipmentId" dataSource={result.items} scroll={{ x: 620 }} pagination={{ pageSize: 10 }} columns={[
      { title: 'Vận đơn', dataIndex: 'trackingCode', render: (value, row) => value || row.shipmentId },
      { title: 'Kết quả', dataIndex: 'result' }, { title: 'Chi tiết', dataIndex: 'reason' },
    ]} />
  </>}</section>;
}
