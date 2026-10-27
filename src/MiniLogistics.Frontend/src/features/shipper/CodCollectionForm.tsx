import { Button, Form, Input } from 'antd';
import { WalletOutlined } from '@ant-design/icons';
import type { CollectBody, ShipperShipment } from './api';
const cents = (value: string) => {
  const [whole, fraction = ''] = value.split('.');
  return BigInt(whole + fraction.padEnd(2, '0'));
};

export function CodCollectionForm({
  shipment,
  busy,
  pending,
  onRequest,
}: {
  shipment: ShipperShipment;
  busy: boolean;
  pending: boolean;
  onRequest: (body: CollectBody) => void;
}) {
  const [form] = Form.useForm<CollectBody>();
  return (
    <Form
      form={form}
      name={`shipper-cod-${shipment.shipmentId}`}
      layout="vertical"
      initialValues={{ collectedAmount: shipment.codAmount }}
      onFinish={onRequest}
    >
      <Form.Item
        name="collectedAmount"
        label="Số thực thu"
        rules={[
          {
            validator: (_, value: unknown) =>
              typeof value === 'string' && /^\d{1,16}(?:\.\d{1,2})?$/.test(value)
                ? Promise.resolve()
                : Promise.reject(new Error('Nhập số tiền không âm, tối đa 16 số nguyên và 2 số lẻ')),
          },
        ]}
      >
        <Input inputMode="decimal" disabled={busy} />
      </Form.Item>
      <Form.Item
        name="note"
        label="Ghi chú thu COD"
        dependencies={['collectedAmount']}
        rules={[
          { max: 500 },
          {
            validator: (_, note: unknown) => {
              const amount = form.getFieldValue('collectedAmount');
              const valid = typeof amount === 'string' && /^\d{1,16}(?:\.\d{1,2})?$/.test(amount);
              const differs = valid && cents(amount) !== cents(shipment.codAmount);
              return differs && (typeof note !== 'string' || !note.trim())
                ? Promise.reject(new Error('Nhập ghi chú khi lệch COD'))
                : Promise.resolve();
            },
          },
        ]}
      >
        <Input.TextArea disabled={busy} rows={2} maxLength={500} />
      </Form.Item>
      <Button
        type="primary"
        htmlType="submit"
        icon={<WalletOutlined aria-hidden />}
        disabled={busy}
        loading={pending}
        aria-label={`Thu COD ${shipment.trackingCode}`}
      >
        Xác nhận đã thu COD
      </Button>
    </Form>
  );
}
