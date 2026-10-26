import { Card, Table } from 'antd';
import type { IntegrationClient } from './api';
import { useSession } from '../../shared/auth/SessionContext';
import { formatDateTime } from '../../shared/lib/dateTime';
const titles: Record<string, string> = { bucketUtc: 'Thời gian UTC', requestCount: 'Tổng request', successCount: 'Thành công', errorCount: 'Lỗi', createdAtUtc: 'UTC', method: 'HTTP method', path: 'Endpoint', statusCode: 'HTTP status', errorCode: 'Mã lỗi', errorMessage: 'Thông tin lỗi' };
export function IntegrationAuditPanel({ client }: { client: IntegrationClient }) {
  const usage = client.usage;
  const { session } = useSession(); const time = (value?: string | null) => value ? formatDateTime(value, session!.serverTimeZoneId, session!.locale) : '—';
  return <><Card title="Nhật ký credential"><p>Tối đa 10 hành động gần nhất.</p><div role="region" aria-label="Nhật ký credential" tabIndex={0}><Table rowKey="auditId" dataSource={client.recentCredentialAudits} pagination={false} scroll={{ x: 800 }} columns={[
    { title: 'Action', dataIndex: 'action' }, { title: 'Actor', dataIndex: 'actorUserId' }, { title: 'Kết quả', render: (_, row) => row.isSuccess ? 'Thành công' : 'Thất bại' }, { title: 'Mã lỗi', dataIndex: 'errorCode' }, { title: 'Thời gian', render: (_, row) => time(row.occurredAtUtc) },
  ]} /></div></Card>
    <Card title="Sử dụng API">{usage ? <><p>{usage.totalRequests} requests · {usage.successfulRequests} thành công · {usage.failedRequests} lỗi</p>
      {([['Theo ngày UTC', usage.daily], ['Theo giờ UTC', usage.hourly]] as const).map(([title, rows]) => <div role="region" aria-label={title} tabIndex={0} key={title}><Table title={() => title} rowKey="bucketUtc" dataSource={rows} pagination={{ pageSize: 7 }} scroll={{ x: 600 }} columns={['bucketUtc', 'requestCount', 'successCount', 'errorCount'].map(key => ({ title: titles[key], key, render: (_, row) => String(row[key as keyof typeof row]) }))} /></div>)}
      <div role="region" aria-label="Request lỗi gần nhất" tabIndex={0}><Table rowKey={row => `${row.createdAtUtc}:${row.method}:${row.path}`} dataSource={usage.latestFailedRequests} pagination={false} scroll={{ x: 800 }} columns={['createdAtUtc', 'method', 'path', 'statusCode', 'errorCode', 'errorMessage'].map(key => ({ title: titles[key], key, render: (_, row) => String(row[key as keyof typeof row] ?? '') }))} /></div>
    </> : <p>Chưa có dữ liệu sử dụng API.</p>}</Card></>;
}
