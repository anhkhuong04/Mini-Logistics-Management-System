import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Card, Form, Input, InputNumber, Switch, Table, Upload } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { requestForm } from '../../shared/api/httpClient';
import { useBrowserCommand } from '../../shared/api/useBrowserCommand';
import { AdminShell, AdminReadState, AdminConfirm } from './AdminShell';
import { useAdminRead } from './queries';
import { adminPath, writeAdmin, type Schema } from './api';
type Banner = Schema['BannerDto'];
type Body = { title: string; imageUrl: string; linkUrl?: string | null; sortOrder: number; isActive: boolean };
export function AdminBannersPage() {
  const query = useAdminRead<Banner[]>('banners', false); const command = useBrowserCommand(); const [form] = Form.useForm<Body>();
  const [editing, setEditing] = useState<Banner | null>(null); const [target, setTarget] = useState<{ row?: Banner; kind: 'save' | 'toggle' | 'delete'; body?: Body } | null>(null);
  const [uploading, setUploading] = useState(false); const [uploadError, setUploadError] = useState(''); const upload = useRef<AbortController | null>(null);
  const image = Form.useWatch('imageUrl', form); const busy = command.pending || command.blocked || uploading;
  useEffect(() => () => upload.current?.abort(), []);
  async function selectFile(file: File) {
    if (upload.current) return false;
    form.setFieldValue('imageUrl', ''); setUploadError('');
    if (file.size > 5 * 1024 * 1024 || !/\.(png|jpe?g|webp|svg|gif)$/i.test(file.name)) { setUploadError('Ảnh phải thuộc PNG/JPG/JPEG/WEBP/SVG/GIF, tối đa 5 MiB.'); return false; }
    const controller = new AbortController(); upload.current = controller; setUploading(true);
    const body = new FormData(); body.append('file', file);
    try { const result = await requestForm<Schema['BannerImageResponse']>(adminPath('banners/images'), body, { signal: controller.signal }); if (!controller.signal.aborted) form.setFieldValue('imageUrl', result.imageUrl); }
    catch { if (!controller.signal.aborted) setUploadError('Upload thất bại. Chọn lại ảnh trước khi lưu banner.'); }
    finally { if (!controller.signal.aborted) setUploading(false); upload.current = null; }
    return false;
  }
  async function refresh() { const generation = command.snapshot(); if ((await query.refetch()).isSuccess) command.verified(generation); }
  async function save() {
    if (!target) return; const action = target; setTarget(null);
    const result = await command.run(signal => writeAdmin(`banners${action.row ? `/${action.row.id}${action.kind === 'toggle' ? '/toggle' : ''}` : ''}`, action.kind === 'delete' ? 'DELETE' : action.kind === 'toggle' || !action.row ? 'POST' : 'PUT', action.body, signal));
    if (result) { setEditing(null); form.resetFields(); await query.refetch(); }
  }
  return <AdminShell title="Quản trị banner" refresh={() => void refresh()} busy={busy || query.isFetching} message={command.message}>
    <AdminReadState pending={query.isPending} error={query.isError}><Table rowKey="id" dataSource={query.data} scroll={{ x: 850 }} columns={[
      { title: 'Ảnh', render: (_, row) => <img src={row.imageUrl} alt={row.title} width={100} /> }, { title: 'Tiêu đề', dataIndex: 'title' }, { title: 'Liên kết', dataIndex: 'linkUrl' }, { title: 'Thứ tự', dataIndex: 'sortOrder' }, { title: 'Hoạt động', render: (_, row) => String(row.isActive) },
      { title: 'Thao tác', render: (_, row) => <><Button disabled={busy} onClick={() => { setEditing(row); form.setFieldsValue({ ...row, sortOrder: Number(row.sortOrder) }); }}>Sửa {row.title}</Button><Button disabled={busy} onClick={() => setTarget({ row, kind: 'toggle' })}>Đổi hoạt động {row.title}</Button><Button danger disabled={busy} onClick={() => setTarget({ row, kind: 'delete' })}>Xóa {row.title}</Button></> },
    ]} /></AdminReadState>
    <Card title={editing ? 'Sửa banner' : 'Tạo banner'}><Form<Body> form={form} requiredMark={(label, info) => <>{info.required && <span aria-hidden="true">* </span>}{label}</>} layout="vertical" initialValues={{ sortOrder: 0, isActive: true }} disabled={busy} onFinish={body => setTarget({ row: editing ?? undefined, kind: 'save', body })}>
      <Form.Item name="title" label="Tiêu đề banner" rules={[{ required: true }, { max: 200 }]}><Input maxLength={200} /></Form.Item>
      <Upload accept=".png,.jpg,.jpeg,.webp,.svg,.gif" showUploadList={false} beforeUpload={selectFile} disabled={busy}><Button icon={<UploadOutlined />} loading={uploading}>Chọn ảnh banner</Button></Upload>
      {uploadError && <Alert type="error" role="alert" title={uploadError} />}
      <Form.Item name="imageUrl" label="Đường dẫn ảnh" rules={[{ required: true, message: 'Upload ảnh thành công trước khi lưu' }]}><Input readOnly /></Form.Item>
      {image && <img src={image} alt="Xem trước banner" style={{ maxWidth: '100%', maxHeight: 220 }} />}
      <Form.Item name="linkUrl" label="Liên kết banner" rules={[{ max: 1000 }]}><Input maxLength={1000} /></Form.Item>
      <Form.Item name="sortOrder" label="Thứ tự banner" rules={[{ required: true }]}><InputNumber min={0} max={2147483647} precision={0} /></Form.Item>
      {!editing && <Form.Item name="isActive" label="Hoạt động khi tạo" valuePropName="checked"><Switch /></Form.Item>}
      <Button htmlType="submit" type="primary" disabled={busy || !image}>Lưu banner</Button>
      {editing && <Button disabled={busy} onClick={() => { setEditing(null); form.resetFields(); }}>Hủy sửa banner</Button>}
    </Form></Card>
    {target && <AdminConfirm title={target.kind === 'delete' ? 'Xóa banner?' : 'Xác nhận banner?'} cancel={() => setTarget(null)} confirm={() => void save()}><p>{target.body?.title ?? target.row?.title}</p><p>{target.body?.imageUrl ?? target.row?.imageUrl}</p></AdminConfirm>}
  </AdminShell>;
}
