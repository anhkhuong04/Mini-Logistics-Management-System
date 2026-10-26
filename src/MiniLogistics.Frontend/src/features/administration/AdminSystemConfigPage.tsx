import { useState } from 'react';
import { Alert, Button, Card, Form, Input, Select, Table } from 'antd';
import { useBrowserCommand } from '../../shared/api/useBrowserCommand';
import { AdminShell, AdminReadState, AdminConfirm } from './AdminShell';
import { useAdminRead } from './queries';
import { writeAdmin, type Schema } from './api';
type Fee = Schema['BrowserAdminFeeRequest'];
const defaults: Fee = { routeType: 'InterRegion', baseWeightKg: '1', baseFeeAmount: '20000', extraWeightStepKg: '0.5', extraStepFeeAmount: '5000', minimumWeightKg: null, maximumWeightKg: null, insuranceFreeThreshold: '1000000', insuranceMaximumValue: '20000000', insuranceRate: '0.005', returnFeeRate: '0.5' };
const fields = ['baseWeightKg', 'baseFeeAmount', 'extraWeightStepKg', 'extraStepFeeAmount', 'minimumWeightKg', 'maximumWeightKg', 'insuranceFreeThreshold', 'insuranceMaximumValue', 'insuranceRate', 'returnFeeRate'] as const;
const labels: Record<string, string> = { routeType: 'Loại tuyến', version: 'Phiên bản', isActive: 'Đang áp dụng', province: 'Tỉnh / Thành phố', region: 'Vùng', createdAtUtc: 'Ngày tạo UTC', baseWeightKg: 'Khối lượng cơ bản (kg)', baseFeeAmount: 'Phí cơ bản', extraWeightStepKg: 'Bước khối lượng thêm (kg)', extraStepFeeAmount: 'Phí mỗi bước thêm', minimumWeightKg: 'Khối lượng tối thiểu (kg)', maximumWeightKg: 'Khối lượng tối đa (kg)', insuranceFreeThreshold: 'Ngưỡng miễn bảo hiểm', insuranceMaximumValue: 'Giá trị bảo hiểm tối đa', insuranceRate: 'Tỷ lệ bảo hiểm', returnFeeRate: 'Tỷ lệ phí hoàn' };
export function AdminSystemConfigPage() {
  const query = useAdminRead<Schema['AdminSystemConfigurationResponse']>('configuration', false); const command = useBrowserCommand();
  const [target, setTarget] = useState<{ part: string; body: Fee | Schema['BrowserAdminRouteRequest'] } | null>(null);
  const [fee, setFee] = useState<Fee>(defaults); const [revision, setRevision] = useState(0);
  const busy = command.pending || command.blocked;
  async function refresh() { const generation = command.snapshot(); if ((await query.refetch()).isSuccess) command.verified(generation); }
  async function save() { if (!target) return; const action = target; setTarget(null); if (await command.run(signal => writeAdmin(`configuration/${action.part}`, action.part === 'routes' ? 'PUT' : 'POST', action.body, signal))) await query.refetch(); }
  return <AdminShell title="Cấu hình hệ thống" refresh={() => void refresh()} busy={command.pending || query.isFetching} message={command.message}>
    <p>Thay đổi tạo phiên bản mới và làm mới cache. Vận đơn đã tạo giữ nguyên snapshot giá.</p>
    {query.data?.routeRegions.length === 0 && <Alert type="warning" title="Chưa có vùng tuyến đã cấu hình. Khi tạo cấu hình đầu tiên, chỉ các tỉnh đã cấu hình được dùng để phân tuyến. Cần chuẩn bị đủ danh sách tỉnh phục vụ." />}
    <AdminReadState pending={query.isPending} error={query.isError}>
      <Card title="Lịch sử vùng tuyến"><Table rowKey="configId" dataSource={query.data?.routeRegions} scroll={{ x: 700 }} columns={['province', 'region', 'version', 'isActive', 'createdAtUtc'].map(key => ({ title: labels[key] ?? key, key, render: (_, row) => String(row[key as keyof typeof row] ?? '') }))} /></Card>
      <Card title="Lịch sử biểu phí"><Table rowKey="feeRuleId" dataSource={query.data?.feeRules} scroll={{ x: 1400 }} columns={[...['routeType', 'version', 'isActive', ...fields].map(key => ({ title: labels[key] ?? key, key, render: (_: unknown, row: Schema['FeeRuleConfigResponse']) => String(row[key as keyof typeof row] ?? '') })), { title: 'Thao tác', render: (_, row) => <Button disabled={busy} onClick={() => { setFee({ ...row, reason: '' }); setRevision(value => value + 1); }}>Dùng làm bản mới</Button> }]} /></Card>
    </AdminReadState>
    <Card title="Phiên bản vùng tuyến mới"><Form<Schema['BrowserAdminRouteRequest']> requiredMark={(label, info) => <>{info.required && <span aria-hidden="true">* </span>}{label}</>} layout="vertical" disabled={busy} onFinish={body => setTarget({ part: 'routes', body })}>
      <Form.Item name="province" label="Tỉnh / Thành phố" rules={[{ required: true }, { max: 100 }]}><Input maxLength={100} /></Form.Item>
      <Form.Item name="region" label="Vùng" rules={[{ required: true }, { max: 120 }]}><Input maxLength={120} /></Form.Item>
      <Form.Item name="reason" label="Lý do"><Input maxLength={500} /></Form.Item><Button htmlType="submit">Lưu vùng tuyến</Button>
    </Form></Card>
    <Card title="Phiên bản biểu phí mới"><Form<Fee> key={revision} requiredMark={(label, info) => <>{info.required && <span aria-hidden="true">* </span>}{label}</>} layout="vertical" initialValues={fee} disabled={busy} onFinish={body => setTarget({ part: 'fees', body: { ...body, minimumWeightKg: body.minimumWeightKg || null, maximumWeightKg: body.maximumWeightKg || null } })}>
      <Form.Item name="routeType" label="Loại tuyến" rules={[{ required: true }]}><Select options={['IntraProvince', 'IntraRegion', 'InterRegion'].map(value => ({ value }))} /></Form.Item>
      {fields.map(name => <Form.Item key={name} name={name} label={labels[name]} rules={[{ required: name !== 'minimumWeightKg' && name !== 'maximumWeightKg' }, { pattern: /^\d+(\.\d+)?$/, message: 'Nhập số thập phân không âm' }]}><Input inputMode="decimal" maxLength={30} /></Form.Item>)}
      <Form.Item name="reason" label="Lý do biểu phí"><Input maxLength={500} /></Form.Item><Button htmlType="submit">Lưu biểu phí mới</Button>
    </Form></Card>
    {target && <AdminConfirm title="Tạo phiên bản cấu hình mới?" cancel={() => setTarget(null)} confirm={() => void save()}><pre>{JSON.stringify(target.body, null, 2)}</pre></AdminConfirm>}
  </AdminShell>;
}
