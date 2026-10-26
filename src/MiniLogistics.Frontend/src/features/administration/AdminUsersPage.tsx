import { useEffect, useRef, useState } from 'react';
import { Button, Card, Form, Input, InputNumber, Select, Switch, Table, Pagination } from 'antd';
import { useSearchParams } from 'react-router';
import { useBrowserCommand } from '../../shared/api/useBrowserCommand';
import { AdminShell, AdminReadState, AdminConfirm } from './AdminShell';
import { AdminFilters } from './AdminFilters';
import { useAdminRead } from './queries';
import { writeAdmin, type Schema } from './api';
type User = Schema['GetAdminUserResponse'];
type Target = { kind: 'create' | 'active' | 'password' | 'capacity' | 'working-areas'; user?: User; body: Record<string, unknown> };
export function AdminUsersPage() {
  const query = useAdminRead<{ items: User[]; totalCount: number; pageNumber: number; pageSize: number }>('users'); const hubs = useAdminRead<Schema['AdminHubResponse'][]>('hubs', false);
  const [search, setSearch] = useSearchParams(); const command = useBrowserCommand(); const [editor, setEditor] = useState<{ kind: 'password' | 'capacity' | 'working-areas'; user: User } | null>(null); const [target, setTarget] = useState<Target | null>(null); const secret = useRef('');
  useEffect(() => () => { secret.current = ''; }, []);
  async function refresh() { const generation = command.snapshot(); const reads = await Promise.all([query.refetch(), hubs.refetch()]); if (reads.every(r => r.isSuccess)) command.verified(generation); }
  function cancel() { secret.current = ''; setTarget(null); }
  async function save() {
    if (!target) return; const action = target; const password = secret.current; secret.current = ''; setTarget(null);
    const body = action.kind === 'create' ? { ...action.body, password } : action.kind === 'password' ? { ...action.body, newPassword: password } : action.body;
    const part = action.kind === 'create' ? 'users' : `users/${action.user!.userId}/${action.kind}`;
    const result = await command.run(signal => writeAdmin(part, action.kind === 'create' ? 'POST' : 'PUT', body, signal));
    if (result) { setEditor(null); await query.refetch(); }
  }
  const busy = command.pending || command.blocked;
  return <AdminShell title="Người dùng nội bộ" busy={command.pending || query.isFetching} refresh={() => void refresh()} message={command.message}><AdminFilters kind="users" />
    <AdminReadState pending={query.isPending} error={query.isError}><Table rowKey="userId" dataSource={query.data?.items} pagination={false} scroll={{ x: 1000 }} columns={[
      { title: 'Họ tên', dataIndex: 'fullName' }, { title: 'Email', dataIndex: 'email' }, { title: 'Điện thoại', dataIndex: 'phoneNumber' }, { title: 'Vai trò', render: (_, row: User) => row.roles.join(', ') }, { title: 'Hoạt động', render: (_, row) => row.isActive ? 'Active' : 'Inactive' }, { title: 'Sức chứa', dataIndex: 'maxActiveShipments' }, { title: 'Vùng làm việc', render: (_, row) => row.workingAreas.map(a => [a.hubName, a.ward, a.zoneCode].filter(Boolean).join(' / ')).join('; ') },
      { title: 'Thao tác', render: (_, row) => <div>{row.roles.some(r => ['Shipper', 'Operator'].includes(r)) && <><Button disabled={busy || query.isFetching} onClick={() => setTarget({ kind: 'active', user: row, body: { isActive: !row.isActive } })}>Đổi hoạt động {row.fullName}</Button><Button disabled={busy || query.isFetching} onClick={() => setEditor({ kind: 'password', user: row })}>Đổi mật khẩu {row.fullName}</Button></>}{row.roles.includes('Shipper') && <><Button disabled={busy || query.isFetching} onClick={() => setEditor({ kind: 'capacity', user: row })}>Sức chứa {row.fullName}</Button><Button disabled={busy || query.isFetching || !hubs.data} onClick={() => setEditor({ kind: 'working-areas', user: row })}>Vùng {row.fullName}</Button></>}</div> },
    ]} />{query.data && <Pagination current={query.data.pageNumber} pageSize={query.data.pageSize} total={query.data.totalCount} showSizeChanger pageSizeOptions={[10, 25, 50, 100]} onChange={(page, size) => { const next = new URLSearchParams(search); next.set('pageNumber', String(page)); next.set('pageSize', String(size)); setSearch(next); }} />}</AdminReadState>
    {editor ? <Card title={`${editor.kind}: ${editor.user.fullName}`}><UserActionForm key={`${editor.kind}:${editor.user.userId}`} kind={editor.kind} user={editor.user} hubs={hubs.data ?? []} busy={busy} submit={(body, password) => { secret.current = password ?? ''; setTarget({ ...editor, body }); }} /><Button onClick={() => setEditor(null)} disabled={command.pending}>Hủy sửa</Button></Card>
      : <Card title="Tạo người dùng nội bộ"><CreateInternalForm busy={busy} submit={body => { const { password, ...publicFields } = body; secret.current = password; setTarget({ kind: 'create', body: publicFields }); }} /></Card>}
    {target && <AdminConfirm reason={String(target.body.reason ?? "")} changeReason={target.kind === "active" ? reason => setTarget({ ...target, body: { ...target.body, reason } }) : undefined} title={target.kind === 'password' ? 'Xác nhận đổi mật khẩu?' : 'Xác nhận thay đổi người dùng?'} cancel={cancel} confirm={() => void save()}><p>{target.user?.fullName ?? String(target.body.fullName)}</p><p>{target.kind}</p><p>{target.kind === 'active' ? String(target.body.isActive) : target.kind === 'capacity' ? `${target.body.maxActiveShipments} đơn; nhận tự động: ${target.body.isAvailableForAssignment}` : target.kind === 'create' ? String(target.body.role) : target.kind === 'working-areas' ? 'Thay thế danh sách vùng đã chọn.' : String(target.body.reason ?? '')}</p></AdminConfirm>}
  </AdminShell>;
}
function CreateInternalForm({ busy, submit }: { busy: boolean; submit: (body: Schema['BrowserAdminUserRequest']) => void }) {
  const [form] = Form.useForm<Schema['BrowserAdminUserRequest']>();
  return <Form className="admin-editor" form={form} name="internal-user" requiredMark={(label, info) => <>{info.required && <span aria-hidden="true">* </span>}{label}</>} layout="vertical" initialValues={{ role: 'Shipper' }} disabled={busy} onFinish={values => { submit(values); form.setFieldValue('password', ''); }}>
    <Form.Item name="fullName" label="Họ tên" rules={[{ required: true }, { max: 150 }]}><Input maxLength={150} /></Form.Item><Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}><Input maxLength={256} /></Form.Item><Form.Item name="phoneNumber" label="Điện thoại" rules={[{ required: true }, { pattern: /^\+?[\d ]{9,20}$/ }]}><Input maxLength={20} /></Form.Item>
    <Form.Item name="password" label="Mật khẩu khởi tạo" extra="8–100 ký tự, có chữ hoa/thường, số và ký tự đặc biệt." rules={[{ required: true }, { min: 8, max: 100 }, { pattern: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$/ }]}><Input.Password autoComplete="new-password" /></Form.Item>
    <Form.Item name="role" label="Vai trò" rules={[{ required: true }]}><Select options={['Shipper', 'Operator'].map(value => ({ value, label: value }))} /></Form.Item><Button htmlType="submit" type="primary" disabled={busy}>Tạo người dùng</Button>
  </Form>;
}
function UserActionForm({ kind, user, hubs, busy, submit }: { kind: 'password' | 'capacity' | 'working-areas'; user: User; hubs: Schema['AdminHubResponse'][]; busy: boolean; submit: (body: Record<string, unknown>, password?: string) => void }) {
  const [form] = Form.useForm<{ newPassword: string; reason: string; maxActiveShipments: number; isAvailableForAssignment: boolean; areas: { hubId: string; ward?: string; zoneCode?: string }[] }>();
  return <Form className="admin-editor" form={form} requiredMark={(label, info) => <>{info.required && <span aria-hidden="true">* </span>}{label}</>} layout="vertical" name="admin-user-action" disabled={busy} initialValues={{ maxActiveShipments: user.maxActiveShipments, isAvailableForAssignment: user.isAvailableForAssignment, areas: user.workingAreas.map(a => ({ hubId: a.hubId, ward: a.ward, zoneCode: a.zoneCode })) }} onFinish={values => {
    if (kind === 'password') { submit({ reason: values.reason }, values.newPassword); form.setFieldValue('newPassword', ''); }
    else submit(kind === 'capacity' ? { maxActiveShipments: values.maxActiveShipments, isAvailableForAssignment: values.isAvailableForAssignment } : { areas: values.areas });
  }}>
    {kind === 'password' && <><Form.Item name="newPassword" label="Mật khẩu mới" rules={[{ required: true }, { min: 8, max: 100 }]}><Input.Password autoComplete="new-password" /></Form.Item><Form.Item name="reason" label="Lý do" rules={[{ max: 500 }]}><Input maxLength={500} /></Form.Item></>}
    {kind === 'capacity' && <><Form.Item name="maxActiveShipments" label="Giới hạn đơn active" rules={[{ required: true }, { type: 'number', min: 1, max: 200 }]}><InputNumber min={1} max={200} precision={0} /></Form.Item><Form.Item name="isAvailableForAssignment" label="Nhận phân công tự động" valuePropName="checked"><Switch /></Form.Item></>}
    {kind === 'working-areas' && <Form.List name="areas">{(fields, { add, remove }) => <>{fields.map(field => <div key={field.key}><Form.Item name={[field.name, 'hubId']} label="Hub" rules={[{ required: true }]}><Select options={hubs.map(h => ({ value: h.hubId, label: `${h.code} — ${h.name}`, disabled: !h.isActive }))} /></Form.Item><Form.Item name={[field.name, 'ward']} label="Phường / Xã"><Input maxLength={100} /></Form.Item><Form.Item name={[field.name, 'zoneCode']} label="Zone"><Input maxLength={50} /></Form.Item><Button onClick={() => remove(field.name)}>Bỏ vùng</Button></div>)}<Button onClick={() => add({})}>Thêm vùng</Button></>}</Form.List>}
    <Button type="primary" htmlType="submit" disabled={busy}>Lưu thay đổi</Button>
  </Form>;
}
