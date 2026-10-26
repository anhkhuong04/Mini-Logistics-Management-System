import { Button, Card, Table } from 'antd';
import type { IntegrationClient, IntegrationDashboard } from './api';
import { useSession } from '../../shared/auth/SessionContext';
import { formatDateTime } from '../../shared/lib/dateTime';
export function WebhookDeliveryTable({ client, failures, busy, retry }: { client: IntegrationClient; failures: IntegrationDashboard['outboxFailures']; busy: boolean; retry: (kind: 'delivery' | 'outbox', id: string) => void }) {
  const canManage = client.scopes.includes('WebhookManage'); const metrics = client.webhookMetrics;
  const { session } = useSession(); const time = (value?: string | null) => value ? formatDateTime(value, session!.serverTimeZoneId, session!.locale) : '—';
  return <><Card title="Lịch sử delivery"><p>Tối đa 10 delivery gần nhất: {metrics.succeededDeliveries}/{metrics.totalDeliveries} thành công ({metrics.successRate}%). Thời gian trung bình: {metrics.averageLatencyMs ?? '—'} ms.</p>
    <div role="region" aria-label="Lịch sử delivery" tabIndex={0}><Table rowKey="webhookDeliveryId" dataSource={client.recentDeliveries} pagination={false} scroll={{ x: 950 }} columns={[
      { title: 'Sự kiện', dataIndex: 'eventType' }, { title: 'Trạng thái', dataIndex: 'status' }, { title: 'Tạo lúc', render: (_, row) => time(row.createdAtUtc) }, { title: 'Số lần retry', dataIndex: 'retryCount' }, { title: 'HTTP', dataIndex: 'lastResponseStatusCode' }, { title: 'Thời gian (ms)', dataIndex: 'lastDurationMs' }, { title: 'Lần gửi', render: (_, row) => time(row.lastAttemptAtUtc) }, { title: 'Retry tiếp', render: (_, row) => time(row.nextAttemptAtUtc) }, { title: 'Lỗi', dataIndex: 'lastError' },
      { title: 'Thao tác', render: (_, row) => ['Failed', 'DeadLettered'].includes(row.status) && <Button disabled={busy || !canManage} onClick={() => retry('delivery', row.webhookDeliveryId)}>Retry delivery {row.webhookDeliveryId}</Button> },
    ]} /></div></Card>
    <Card title="Khôi phục outbox"><p>Cửa sổ tối đa 100 lỗi gần nhất; chỉ hiển thị mục thuộc client đang chọn.</p><div role="region" aria-label="Lỗi outbox" tabIndex={0}><Table rowKey="outboxMessageId" dataSource={failures.filter(f => f.apiClientId === client.apiClientId)} pagination={{ pageSize: 10 }} scroll={{ x: 800 }} columns={[
      { title: 'Sự kiện', dataIndex: 'eventType' }, { title: 'Trạng thái', dataIndex: 'status' }, { title: 'Retry', dataIndex: 'retryCount' }, { title: 'Retry tiếp', render: (_, row) => time(row.nextAttemptAtUtc) }, { title: 'Lỗi', dataIndex: 'error' }, { title: 'Thao tác', render: (_, row) => <Button disabled={busy || !canManage} onClick={() => retry('outbox', row.outboxMessageId)}>Retry outbox {row.outboxMessageId}</Button> },
    ]} /></div></Card></>;
}
