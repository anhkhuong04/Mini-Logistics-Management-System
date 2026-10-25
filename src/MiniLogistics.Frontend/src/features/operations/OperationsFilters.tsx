import { useState } from 'react';
import { Alert, Button, Checkbox, Collapse, Form, Input, InputNumber, Select, Space } from 'antd';
import { FilterOutlined } from '@ant-design/icons';
import { shipmentStatuses } from '../shipments/shipmentFilters';
import { operationsDateLabel, operationsDateRange } from './dateFilters';
import type { OperationsFilters as Filters } from './models';
import type { OperationsShipper } from './api';
export function OperationsFilters({ filters, shippers, timeZone, onApply }: { filters: Filters; shippers: OperationsShipper[]; timeZone: string; onApply: (value: Filters) => void }) {
  const [invalid, setInvalid] = useState(false);
  return <Form name="operations-filters" className="operations-filters" layout="vertical" initialValues={{ ...filters, fromDate: operationsDateLabel(filters.fromUtc, timeZone), toDate: operationsDateLabel(filters.toUtc, timeZone) }}
    onFinish={(values: Record<string, string | boolean>) => {
      try {
        const { fromDate, toDate, ...fields } = values;
        const dates = operationsDateRange(String(fromDate ?? ''), String(toDate ?? ''), timeZone);
        if (fromDate === operationsDateLabel(filters.fromUtc, timeZone)) dates.fromUtc = filters.fromUtc;
        if (toDate === operationsDateLabel(filters.toUtc, timeZone)) dates.toUtc = filters.toUtc;
        setInvalid(false); onApply({ ...filters, ...fields, ...dates,
          minCodAmount: fields.minCodAmount == null || fields.minCodAmount === '' ? undefined : fields.minCodAmount,
          maxCodAmount: fields.maxCodAmount == null || fields.maxCodAmount === '' ? undefined : fields.maxCodAmount,
          pageNumber: 1 } as Filters);
      } catch { setInvalid(true); }
    }}>
    {invalid && <Alert role="alert" type="error" title="Khoảng ngày không hợp lệ" />}
    <Form.Item name="searchText" label="Tìm vận đơn / người nhận"><Input maxLength={100} allowClear /></Form.Item>
    <Form.Item name="province" label="Tỉnh / Thành phố"><Input maxLength={100} allowClear /></Form.Item>
    <Collapse defaultActiveKey={[filters.status, filters.codStatus, filters.shipperId, filters.fromUtc, filters.toUtc, filters.minCodAmount, filters.maxCodAmount, filters.slaOnly].some(Boolean) ? ['advanced'] : []}
      items={[{ key: 'advanced', label: 'Bộ lọc nâng cao', children: <div className="operations-advanced">
    {filters.view === 'operations' && <>
      <Form.Item name="status" label="Trạng thái"><Select allowClear options={shipmentStatuses.filter(value => !['Draft', 'PendingPickup', 'Returned', 'Cancelled'].includes(value)).map(value => ({ value, label: value }))} /></Form.Item>
      <Form.Item name="codStatus" label="Trạng thái COD"><Select allowClear options={['PendingCollection', 'Collected', 'Settled'].map(value => ({ value, label: value }))} /></Form.Item>
      <Form.Item name="shipperId" label="Shipper"><Select allowClear options={shippers.map(item => ({ value: item.userId, label: item.fullName }))} /></Form.Item>
    </>}
    <Form.Item name="fromDate" label={`Từ ngày (${timeZone})`} rules={[{ validator: (_, value: string) => !value || /^\d{4}-\d{2}-\d{2}$/.test(value) ? Promise.resolve() : Promise.reject(new Error('Ngày không hợp lệ')) }]}><Input type="date" /></Form.Item>
    <Form.Item name="toDate" label={`Đến ngày (${timeZone})`}><Input type="date" /></Form.Item>
    <Form.Item name="minCodAmount" label="COD tối thiểu"><InputNumber stringMode min="0" /></Form.Item>
    <Form.Item name="maxCodAmount" label="COD tối đa"><InputNumber stringMode min="0" /></Form.Item>
    <Form.Item name="slaOnly" valuePropName="checked"><Checkbox>Chỉ đơn quá SLA</Checkbox></Form.Item>
    </div> }]} />
    <Space wrap><Button icon={<FilterOutlined aria-hidden />} htmlType="submit">Áp dụng</Button>
      <Button onClick={() => onApply({ view: filters.view, pageNumber: 1, pageSize: filters.pageSize, slaOnly: false })}>Xóa bộ lọc</Button></Space>
  </Form>;
}
