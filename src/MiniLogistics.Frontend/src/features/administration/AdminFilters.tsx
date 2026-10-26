import { useState } from 'react';
import { Alert, Button, Form, Input, Select, InputNumber } from 'antd';
import { useSearchParams } from 'react-router';
import { useSession } from '../../shared/auth/SessionContext';
import { operationsDateRange, operationsDateLabel } from '../operations/dateFilters';
export function AdminFilters({ kind }: { kind: 'report' | 'cod' | 'audit' | 'users' | 'hubs' }) {
  const [search, setSearch] = useSearchParams(); const { session } = useSession(); const [error, setError] = useState(false);
  const zone = session!.serverTimeZoneId;
  const initial = { ...Object.fromEntries(search), from: operationsDateLabel(search.get('fromUtc') ?? undefined, zone), to: operationsDateLabel(search.get('toUtc') ?? undefined, zone), limit: Number(search.get('limit') ?? 200) };
  return <><Form key={search.toString()} requiredMark={(label, info) => <>{info.required && <span aria-hidden="true">* </span>}{label}</>} layout="vertical" className="admin-filters" initialValues={initial} onFinish={values => {
    try {
      const next = new URLSearchParams();
      if (['report', 'cod', 'audit'].includes(kind)) for (const [key, value] of Object.entries(operationsDateRange(values.from ?? '', values.to ?? '', zone))) if (value) next.set(key, value);
      for (const key of ['province', 'status', 'shipperId', 'minAmount', 'maxAmount', 'actorUserId', 'action', 'targetType', 'searchText', 'role', 'isActive', 'isRegionalSortingHub', 'limit']) if (values[key] != null && values[key] !== '') next.set(key, String(values[key]));
      setError(false); setSearch(next);
    } catch { setError(true); }
  }}>
    {['report', 'cod', 'audit'].includes(kind) && <><Form.Item name="from" label="Từ ngày (múi giờ server)"><Input type="date" /></Form.Item><Form.Item name="to" label="Đến ngày (múi giờ server)"><Input type="date" /></Form.Item></>}
    {['report', 'cod', 'hubs'].includes(kind) && <Form.Item name="province" label="Tỉnh / Thành phố"><Input maxLength={100} /></Form.Item>}
    {kind === 'cod' && <><Form.Item name="status" label="Trạng thái COD"><Select allowClear options={['PendingCollection', 'Collected', 'Settled'].map(value => ({ value, label: value }))} /></Form.Item>
      <Form.Item name="shipperId" label="Shipper ID"><Input /></Form.Item><Form.Item name="minAmount" label="COD tối thiểu"><Input inputMode="decimal" /></Form.Item><Form.Item name="maxAmount" label="COD tối đa"><Input inputMode="decimal" /></Form.Item></>}
    {kind === 'audit' && <><Form.Item name="action" label="Action"><Input maxLength={120} /></Form.Item><Form.Item name="targetType" label="Loại đối tượng"><Input maxLength={80} /></Form.Item><Form.Item name="actorUserId" label="Actor ID"><Input /></Form.Item><Form.Item name="limit" label="Giới hạn dòng"><InputNumber min={1} max={500} /></Form.Item></>}
    {['users', 'hubs'].includes(kind) && <><Form.Item name="searchText" label="Tìm kiếm"><Input maxLength={100} /></Form.Item><Form.Item name="isActive" label="Hoạt động"><Select allowClear options={[{ value: 'true', label: 'Active' }, { value: 'false', label: 'Inactive' }]} /></Form.Item></>}
    {kind === 'users' && <Form.Item name="role" label="Vai trò"><Select allowClear options={['Admin', 'Operator', 'Shipper', 'Shop', 'IntegrationAdmin'].map(value => ({ value, label: value }))} /></Form.Item>}
    {kind === 'hubs' && <Form.Item name="isRegionalSortingHub" label="Hub phân loại vùng"><Select allowClear options={[{ value: 'true', label: 'Có' }, { value: 'false', label: 'Không' }]} /></Form.Item>}
    <SpaceButtons reset={() => { setSearch({}); setError(false); }} />
  </Form>{error && <Alert role="alert" type="error" title="Khoảng ngày không hợp lệ" />}</>;
}
function SpaceButtons({ reset }: { reset: () => void }) { return <div><Button htmlType="submit">Áp dụng</Button>{' '}<Button onClick={reset}>Xóa lọc</Button></div>; }
