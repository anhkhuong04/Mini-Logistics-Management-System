import { useState } from 'react';
import { Button, Card, Form, Input, Switch, Table } from 'antd';
import { useBrowserCommand } from '../../shared/api/useBrowserCommand';
import { AdminShell, AdminReadState, AdminConfirm } from './AdminShell';
import { AdminFilters } from './AdminFilters';
import { useAdminRead } from './queries';
import { writeAdmin, type Schema } from './api';
type Hub = Schema['AdminHubResponse']; type Body = Schema['BrowserAdminHubRequest'];
export function AdminHubsPage() {
  const query = useAdminRead<Hub[]>('hubs'); const command = useBrowserCommand(); const [editing, setEditing] = useState<Hub | null>(null); const [target, setTarget] = useState<{ row?: Hub; body?: Body; active?: boolean; reason?: string } | null>(null);
  async function refresh() { const generation = command.snapshot(); const result = await query.refetch(); if (result.isSuccess) command.verified(generation); }
  async function save() {
    if (!target) return; const action = target; setTarget(null); const active = action.active !== undefined;
    const result = await command.run(signal => writeAdmin(`hubs${action.row ? `/${action.row.hubId}${active ? '/active' : ''}` : ''}`, action.row ? 'PUT' : 'POST', active ? { isActive: action.active, reason: action.reason } : action.body, signal));
    if (result) { setEditing(null); await query.refetch(); }
  }
  const busy = command.pending || command.blocked;
  return <AdminShell title="Quản trị Hub" refresh={() => void refresh()} busy={command.pending || query.isFetching} message={command.message}><AdminFilters kind="hubs" /><AdminReadState pending={query.isPending} error={query.isError}>
    <Table rowKey="hubId" dataSource={query.data} pagination={false} scroll={{ x: 1000 }} columns={[{ title: 'Mã', dataIndex: 'code' }, { title: 'Tên', dataIndex: 'name' }, { title: 'Tỉnh', dataIndex: 'province' }, { title: 'Phường', dataIndex: 'ward' }, { title: 'Địa chỉ', dataIndex: 'addressLine' }, { title: 'Sorting', render: (_, row: Hub) => String(row.isRegionalSortingHub) }, { title: 'Active', render: (_, row) => String(row.isActive) }, { title: 'Vùng làm việc active', dataIndex: 'activeWorkingAreaCount' }, { title: 'Thao tác', render: (_, row) => <><Button disabled={busy || query.isFetching} onClick={() => setEditing(row)}>Sửa {row.code}</Button><Button disabled={busy || query.isFetching} onClick={() => setTarget({ row, active: !row.isActive })}>Đổi hoạt động {row.code}</Button></> }]} /></AdminReadState>
    <Card title={editing ? `Sửa Hub ${editing.code}` : 'Tạo Hub'}><HubForm key={editing?.hubId ?? 'new'} initial={editing ?? undefined} busy={busy} submit={body => setTarget({ row: editing ?? undefined, body })} />{editing && <Button disabled={command.pending} onClick={() => setEditing(null)}>Hủy sửa</Button>}</Card>
    {target && <AdminConfirm reason={target.reason} changeReason={target.active !== undefined ? reason => setTarget({ ...target, reason }) : undefined} title="Xác nhận thay đổi Hub?" cancel={() => setTarget(null)} confirm={() => void save()}><p>{target.body?.name ?? target.row?.name}</p><p>{target.active !== undefined ? String(target.active) : target.body?.province}</p></AdminConfirm>}
  </AdminShell>;
}
function HubForm({ initial, busy, submit }: { initial?: Body; busy: boolean; submit: (value: Body) => void }) {
  return <Form<Body> name="admin-hub" className="admin-editor" requiredMark={(label, info) => <>{info.required && <span aria-hidden="true">* </span>}{label}</>} layout="vertical" initialValues={initial ?? { country: 'Vietnam', isRegionalSortingHub: false }} onFinish={submit} disabled={busy}>
    {([['code', 'Mã Hub', 40], ['name', 'Tên Hub', 200], ['province', 'Tỉnh / Thành phố', 100], ['ward', 'Phường / Xã', 100], ['addressLine', 'Địa chỉ', 300], ['country', 'Quốc gia', 100]] as const).map(([key, label, max]) => <Form.Item key={key} name={key} label={label} rules={[{ required: !['ward', 'addressLine'].includes(key) }, { max }]}><Input maxLength={max} /></Form.Item>)}
    <Form.Item name="isRegionalSortingHub" label="Hub phân loại vùng" valuePropName="checked"><Switch /></Form.Item><Button htmlType="submit" type="primary" disabled={busy}>Lưu Hub</Button>
  </Form>;
}
