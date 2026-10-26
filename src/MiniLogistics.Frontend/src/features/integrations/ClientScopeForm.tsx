import { Button, Checkbox, Form, Input } from 'antd';
import { scopes, type IntegrationClient, type SecurityBody } from './api';
export function ClientScopeForm({ client, busy, submit }: { client?: IntegrationClient; busy: boolean; submit: (body: SecurityBody) => void }) {
  return <Form<{ name: string; scopes: string[]; ips: string; expiry: string }> name={client ? 'integration-security' : 'integration-create'} layout="vertical" disabled={busy}
    requiredMark={(label, info) => <>{info.required && <span aria-hidden="true">* </span>}{label}</>}
    initialValues={{ scopes: client?.scopes ?? scopes, ips: client?.allowedIpAddresses.join('\n') ?? '', expiry: client?.expiresAtUtc ?? '' }}
    onFinish={v => submit({ name: v.name, scopes: v.scopes, allowedIpAddresses: (v.ips ?? '').split(/[,\n]/).map(ip => ip.trim()).filter(Boolean), expiresAtUtc: v.expiry?.trim() || null })}>
    {!client && <Form.Item name="name" label="Tên API client" rules={[{ required: true, whitespace: true }, { max: 150 }]}><Input maxLength={150} autoComplete="off" /></Form.Item>}
    <Form.Item name="scopes" label="Quyền API" rules={[{ required: true, type: 'array', min: 1 }]}><Checkbox.Group options={scopes} /></Form.Item>
    <Form.Item name="ips" label="IP cho phép (mỗi dòng hoặc dấu phẩy)" extra="Để trống nếu không giới hạn IP."><Input.TextArea maxLength={2000} rows={3} /></Form.Item>
    <Form.Item name="expiry" label="Hết hạn (UTC, ISO 8601)" extra="Để trống nếu không hết hạn." rules={[{ validator: (_, value: string) => !value || (/(Z|[+-]\d\d:\d\d)$/.test(value) && Number.isFinite(Date.parse(value))) ? Promise.resolve() : Promise.reject(new Error('Nhập thời điểm ISO 8601 có Z hoặc offset')) }]}><Input placeholder="2027-01-01T00:00:00Z" maxLength={40} /></Form.Item>
    <Button type="primary" htmlType="submit" disabled={busy}>{client ? 'Lưu quyền API' : 'Tạo API client'}</Button>
  </Form>;
}
