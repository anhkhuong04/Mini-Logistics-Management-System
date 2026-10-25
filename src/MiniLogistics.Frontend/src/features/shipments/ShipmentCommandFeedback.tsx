import { Alert, Button } from 'antd';
import { Link } from 'react-router';
import { ApiError } from '../../shared/api/httpClient';
import { useShipmentFormCopy } from '../../shared/i18n/shipmentFormCopy';
import { useShipmentCopy } from '../../shared/i18n/shipmentCopy';
import { shipmentListUrl } from './shipmentFilters';
export function ShipmentCommandFeedback({ failure, unknown, conflict, shopId, reload }: {
  failure: Error | null; unknown: boolean; conflict: boolean; shopId: string; reload(): void;
}) {
  const copy = useShipmentFormCopy(); const shipment = useShipmentCopy();
  if (!failure) return null;
  return <Alert role="alert" type="error" title={unknown ? copy.uncertain : conflict ? copy.conflict : failure instanceof ApiError && failure.status === 429 ? copy.rateLimit : copy.failed}
    action={unknown ? <Link to={shipmentListUrl(shopId)}>{shipment.back}</Link> : conflict ? <Button onClick={reload}>{copy.reload}</Button> : undefined} />;
}
