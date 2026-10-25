import { Button, Result } from 'antd';
import { ApiError } from '../../shared/api/httpClient';
import { useShipmentCopy } from '../../shared/i18n/shipmentCopy';
export function ReadError({ error, onRetry }: { error: Error; onRetry(): void }) {
  const copy = useShipmentCopy();
  const status = error instanceof ApiError ? error.status : 500;
  return <Result status={status === 403 ? '403' : status === 404 ? '404' : 'error'}
    title={status === 403 ? copy.denied : status === 404 ? copy.missing : (copy.list + ' · ' + copy.retry)}
    extra={status !== 403 && status !== 404 && <Button onClick={onRetry}>{copy.retry}</Button>} />;
}
