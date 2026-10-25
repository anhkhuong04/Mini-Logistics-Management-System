import { Button, Form, Input, Select } from 'antd';
import { useShipmentCopy } from '../../shared/i18n/shipmentCopy';
import { failureReasons, nextShipperStatuses } from './models';
import type { ShipperShipment, StatusBody } from './api';
export function StatusActionForm({ shipment, busy, onRequest }: { shipment: ShipperShipment; busy: boolean; onRequest: (body: StatusBody) => void }) {
  const [form] = Form.useForm<StatusBody>(); const selected = Form.useWatch('newStatus', form); const copy = useShipmentCopy();
  const next = nextShipperStatuses(shipment.status);
  if (!next.length) return null;
  return <Form form={form} name={`shipper-status-${shipment.shipmentId}`} layout="vertical" onFinish={onRequest}>
    <Form.Item name="newStatus" label="Trạng thái mới" rules={[{ required: true, message: 'Chọn trạng thái mới' }]}>
      <Select showSearch optionFilterProp="label" disabled={busy} options={next.map(value => ({ value, label: copy.labels[value] }))} />
    </Form.Item>
    {selected === 'DeliveryFailed' && <Form.Item name="failureReasonCode" label="Mã lý do" rules={[{ required: true, message: 'Chọn mã lý do' }]}>
      <Select showSearch optionFilterProp="label" disabled={busy} options={Object.entries(failureReasons).map(([value, label]) => ({ value, label }))} />
    </Form.Item>}
    <Form.Item name="note" label="Ghi chú" rules={[{ required: selected === 'DeliveryFailed', whitespace: true, message: 'Nhập lý do giao thất bại' }, { max: 500 }]}><Input.TextArea disabled={busy} rows={2} maxLength={500} /></Form.Item>
    <Button type="primary" htmlType="submit" disabled={busy} aria-label={`Cập nhật ${shipment.trackingCode}`}>Cập nhật trạng thái</Button>
  </Form>;
}
