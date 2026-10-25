import { useState } from 'react';
import { Button, Form, Input, Modal, Space } from 'antd';
import { EditOutlined, SendOutlined, StopOutlined } from '@ant-design/icons';
import { Link } from 'react-router';
import { useShopContext } from '../shop-context/ShopContext';
import { useShipmentFormCopy } from '../../shared/i18n/shipmentFormCopy';
import { routePaths } from '../../app/router/routePaths';
import type { ShipmentDetail } from './api';
import { cancelShipment, submitShipment } from './commands';
import { useShipmentCommand } from './useShipmentCommand';
import { ShipmentCommandFeedback } from './ShipmentCommandFeedback';
export function ShipmentActions({ shipment, reload }: { shipment: ShipmentDetail; reload(): Promise<{ isSuccess: boolean }> }) {
  const copy = useShipmentFormCopy(); const { context } = useShopContext(); const shop = context?.shops.find(item => item.shopId === context.selectedShopId);
  const command = useShipmentCommand(); const [action, setAction] = useState<'submit' | 'cancel' | null>(null); const [form] = Form.useForm<{ reason: string }>();
  if (!shop?.isActive || !shop.permissions.includes('ManageShipments')) return null;
  const editable = ['Draft', 'PendingPickup'].includes(shipment.status) && shop.permissions.includes('ViewFullPii') && shop.permissions.includes('ViewCod');
  async function confirm() {
    if (!shop) return;
    let reason = '';
    if (action === 'cancel') { try { reason = (await form.validateFields()).reason; } catch { return; } }
    const result = await command.run(async () => {
      if (action === 'cancel') await cancelShipment(shop.shopId, shipment.shipmentId, reason);
      else await submitShipment(shop.shopId, shipment.shipmentId);
    });
    if (result) { setAction(null); form.resetFields(); await reload(); }
    else setAction(null);
  }
  return <section className="shipment-actions" aria-label={copy.edit}>
    <ShipmentCommandFeedback {...command} shopId={shop.shopId} reload={() => { void reload().then(result => { if (result.isSuccess) command.reset(); }); }} />
    <Space wrap>{editable && <Link aria-disabled={command.pending || command.blocked} onClick={event => { if (command.pending || command.blocked) event.preventDefault(); }}
      className="ant-btn ant-btn-default" to={`${routePaths.shipmentCreate}?shopId=${shop.shopId}&shipmentId=${shipment.shipmentId}`}><EditOutlined aria-hidden /> {copy.edit}</Link>}
      {shipment.status === 'Draft' && <Button icon={<SendOutlined aria-hidden />} disabled={command.pending || command.blocked} onClick={() => setAction('submit')}>{copy.submit}</Button>}
      {['Draft', 'PendingPickup', 'Assigned', 'PickingUp'].includes(shipment.status) && <Button danger icon={<StopOutlined aria-hidden />} disabled={command.pending || command.blocked} onClick={() => setAction('cancel')}>{copy.cancel}</Button>}
    </Space>
    <Modal title={action === 'cancel' ? copy.confirmCancel : copy.confirmSubmit} open={action !== null} okText={copy.confirm} cancelText={copy.close}
      confirmLoading={command.pending} closable={!command.pending} maskClosable={!command.pending} cancelButtonProps={{ disabled: command.pending }}
      onCancel={() => { setAction(null); form.resetFields(); }} onOk={() => { void confirm(); }}>
      {action === 'cancel' ? <Form form={form} layout="vertical"><Form.Item name="reason" label={copy.reason} rules={[{ required: true }, { max: 500 }]}><Input.TextArea autoFocus maxLength={500} disabled={command.pending} /></Form.Item></Form> : <p>{copy.previewHint}</p>}
    </Modal>
  </section>;
}
