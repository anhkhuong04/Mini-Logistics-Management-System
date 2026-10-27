import { useShipmentCopy } from '../i18n/shipmentCopy';
import { StatusTag } from './StatusTag';
import type { ShipmentStatus } from '../model/shipmentFilters';
export function ShipmentStatusTag({ status }: { status: ShipmentStatus }) {
  const copy = useShipmentCopy();
  const tone =
    status === 'Delivered'
      ? 'success'
      : status === 'DeliveryFailed'
        ? 'error'
        : ['Returned', 'PendingPickup'].includes(status)
          ? 'warning'
          : ['Cancelled', 'Draft'].includes(status)
            ? 'default'
            : 'processing';
  return <StatusTag tone={tone}>{copy.labels[status]}</StatusTag>;
}
