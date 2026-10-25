import { useState } from 'react';
import { Alert, Modal, Result } from 'antd';
import { useNavigate, useSearchParams } from 'react-router';
import { ApiError } from '../../shared/api/httpClient';
import { useSession } from '../../shared/auth/SessionContext';
import { useShipmentFormCopy } from '../../shared/i18n/shipmentFormCopy';
import { PageHeader } from '../../shared/ui/PageHeader';
import { AsyncContent } from '../../shared/ui/AsyncContent';
import { useShopContext } from '../shop-context/ShopContext';
import { ShopReadBoundary } from '../shop-context/ShopReadBoundary';
import { ShopContextSelector } from '../shop-context/ShopContextSelector';
import { routePaths } from '../../app/router/routePaths';
import { isShipmentId, useShipment } from './queries';
import { ReadError } from './ReadError';
import { fromShipment, type ShipmentWrite } from './shipmentFormMapping';
import { createShipment, updateShipment, submitShipment } from './commands';
import { ShipmentForm, type ShipmentFormMode } from './ShipmentForm';
import { useShipmentCommand } from './useShipmentCommand';
import { ShipmentCommandFeedback } from './ShipmentCommandFeedback';

export function ShipmentFormPage() {
  const [search] = useSearchParams(); const { session } = useSession(); const { context } = useShopContext();
  // A new identity/Shop/edit target remounts all form and mutation state, including masked inputs.
  const shop = context?.shops.find(item => item.shopId === context.selectedShopId);
  return <ShipmentFormWorkspace key={`${session?.userId}/${context?.selectedShopId}/${search.get('shipmentId') ?? ''}/${shop?.isActive}/${shop?.permissions.join(',')}`} />;
}
function ShipmentFormWorkspace() {
  const copy = useShipmentFormCopy(); const [search] = useSearchParams(); const id = search.get('shipmentId');
  const editing = useShipment(id ?? ''); const { context } = useShopContext(); const navigate = useNavigate();
  const shop = context?.shops.find(item => item.shopId === context.selectedShopId);
  const command = useShipmentCommand(); const [confirmation, setConfirmation] = useState<ShipmentWrite | null>(null);
  const [revision, setRevision] = useState(0);
  async function save(values: ShipmentWrite, mode: ShipmentFormMode) {
    if (!shop) return;
    const outcome = await command.run(async () => {
      if (!id) return createShipment(shop.shopId, values, mode === 'draft');
      const updated = await updateShipment(shop.shopId, id, values);
      if (mode === 'send' && editing.data?.status === 'Draft') {
        // Updating the draft is already committed, even if the subsequent submit fails.
        await command.invalidate();
        return submitShipment(shop.shopId, id);
      }
      return updated;
    });
    if (outcome) navigate(`${routePaths.shipmentDetail(outcome.value.shipmentId)}?shopId=${shop.shopId}`);
    setConfirmation(null);
  }
  async function submit(values: ShipmentWrite, mode: ShipmentFormMode) {
    if (id && editing.data?.status === 'Draft' && mode === 'send') setConfirmation(values);
    else await save(values, mode);
  }
  const initial: ShipmentWrite = id && editing.data ? fromShipment(editing.data) : {
    senderName: shop?.name ?? '', senderPhone: shop?.permissions.includes('ViewFullPii') ? shop.phoneNumber : '', receiverName: '', receiverPhone: '',
    pickupAddress: { street: shop?.permissions.includes('ViewFullPii') ? shop.addressLine : '', ward: shop?.ward ?? '', province: shop?.province ?? '', country: shop?.country ?? 'Vietnam' },
    deliveryAddress: { street: '', ward: '', province: '', country: 'Vietnam' }, weightKg: '1', lengthCm: '10', widthCm: '10', heightCm: '10', goodsValueAmount: '0', codAmount: '0', currency: 'VND', note: null,
  };
  const fullSnapshot = shop?.permissions.includes('ViewFullPii') && shop.permissions.includes('ViewCod');
  return <><PageHeader title={id ? copy.edit : copy.create} /><ShopReadBoundary><ShopContextSelector showInactive={false} />
    {!shop?.isActive || !shop.permissions.includes('ManageShipments') ? <Result status="403" title={copy.denied} />
      : id && (!isShipmentId(id) || search.getAll('shipmentId').length !== 1) ? <Result status="404" />
      : id && !fullSnapshot ? <Alert role="alert" type="warning" title={copy.limited} />
      : id && editing.error ? <ReadError error={editing.error} onRetry={() => void editing.refetch()} />
      : id && editing.isPending ? <AsyncContent loading>{null}</AsyncContent>
      : id && !['Draft', 'PendingPickup'].includes(editing.data!.status) ? <Alert role="alert" type="warning" title={copy.uneditable} />
      : <>
        <ShipmentCommandFeedback {...command} unknown={command.unknown || !id && command.conflict} conflict={Boolean(id) && command.conflict} shopId={shop.shopId}
          reload={() => { if (id) void editing.refetch().then(result => { if (result.isSuccess) { setRevision(value => value + 1); command.reset(); } }); }} />
        <ShipmentForm key={revision} initialValues={initial} editingStatus={id ? editing.data?.status : undefined} pending={command.pending} blocked={command.blocked}
          fieldErrors={command.failure instanceof ApiError ? command.failure.errors : undefined} onSubmit={submit} />
      </>}
    <Modal title={copy.confirmSubmit} open={confirmation !== null} confirmLoading={command.pending} okText={copy.confirm} cancelText={copy.close}
      closable={!command.pending} maskClosable={!command.pending} cancelButtonProps={{ disabled: command.pending }}
      onCancel={() => setConfirmation(null)} onOk={() => { if (confirmation) void save(confirmation, 'send'); }}><p>{copy.previewHint}</p></Modal>
  </ShopReadBoundary></>;
}
