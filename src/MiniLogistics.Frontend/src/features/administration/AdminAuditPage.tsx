import { Table } from 'antd';
import { AdminShell, AdminReadState } from './AdminShell';
import { AdminFilters } from './AdminFilters';
import { useAdminRead } from './queries';
import type { Schema } from './api';
export function AdminAuditPage() {
  const query = useAdminRead<Schema['AdminAuditLogResponse'][]>('audit');
  return <AdminShell title="Nhật ký quản trị" refresh={() => void query.refetch()} busy={query.isFetching}><AdminFilters kind="audit" /><p>Nhật ký chỉ đọc, tối đa 500 dòng theo bộ lọc. Các trường nhạy cảm được che.</p><AdminReadState pending={query.isPending} error={query.isError}><Table rowKey="auditLogId" dataSource={query.data} pagination={{ pageSize: 25, showSizeChanger: false }} scroll={{ x: 1000 }} columns={[
    { title: 'UTC', dataIndex: 'createdAtUtc' }, { title: 'Actor', dataIndex: 'actorUserId' }, { title: 'Vai trò', dataIndex: 'actorRole' }, { title: 'Action', dataIndex: 'action' }, { title: 'Đối tượng', dataIndex: 'targetType' }, { title: 'ID', dataIndex: 'targetId' }, { title: 'Thay đổi', render: (_, row) => <><pre>{row.oldValueJson}</pre><pre>{row.newValueJson}</pre></> }, { title: 'Lý do', dataIndex: 'reason' }, { title: 'IP', dataIndex: 'ipAddress' }, { title: 'User agent', dataIndex: 'userAgent' },
  ]} /></AdminReadState></AdminShell>;
}
