import { settingsFilters } from './queries';
import { useState } from 'react';
import { Alert, Button, Form, Input, InputNumber } from 'antd';
import { FilterOutlined } from '@ant-design/icons';
import { useSearchParams } from 'react-router';
import { utcDateRange } from '../shipments/shipmentFilters';
export function SettingsFilters({ audit = false }: { audit?: boolean }) {
  const [search, setSearch] = useSearchParams(); const [error, setError] = useState(false);
  const initial = { from: search.get('fromUtc')?.slice(0, 10), to: search.get('toUtc')?.slice(0, 10), action: search.get('action') ?? '', limit: Number(search.get('limit') ?? 200) };
  return <>{error && <Alert role="alert" type="error" title="Khoảng ngày không hợp lệ" />}<Form key={search.toString()} name="settings-filters" layout="vertical" initialValues={initial}
    requiredMark={(label, { required: marked }) => <>{marked && <span aria-hidden="true" className="required-mark">*</span>}{label}</>}
    onFinish={(values: typeof initial) => {
      try {
        const range = utcDateRange(values.from ?? '', values.to ?? ''); const next = new URLSearchParams(search);
        for (const key of ['fromUtc', 'toUtc', 'action', 'limit']) next.delete(key);
        if (range.fromUtc) next.set('fromUtc', range.fromUtc); if (range.toUtc) next.set('toUtc', range.toUtc);
        if (audit) { if (values.action) next.set('action', values.action); next.set('limit', String(values.limit)); }
        settingsFilters(next); setError(false); setSearch(next);
      } catch { setError(true); }
    }}>
    <Form.Item name="from" label="Từ ngày (UTC)"><Input type="date" /></Form.Item><Form.Item name="to" label="Đến ngày (UTC)"><Input type="date" /></Form.Item>
    {audit && <><Form.Item name="action" label="Action"><Input maxLength={120} /></Form.Item><Form.Item name="limit" label="Giới hạn dòng" rules={[{ required: true }, { type: 'number', min: 1, max: 500 }]}><InputNumber min={1} max={500} precision={0} /></Form.Item></>}
    <Button htmlType="submit" icon={<FilterOutlined aria-hidden />}>Áp dụng</Button>{' '}<Button onClick={() => { const next = new URLSearchParams(search); for (const key of ['fromUtc', 'toUtc', 'action', 'limit']) next.delete(key); setSearch(next); setError(false); }}>Xóa lọc</Button>
  </Form></>;
}
