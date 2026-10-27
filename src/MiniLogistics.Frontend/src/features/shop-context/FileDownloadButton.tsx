import { useEffect, useRef, useState } from 'react';
import { Alert, Button } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { ApiError } from '../../shared/api/httpClient';
import { useSession } from '../../shared/auth/SessionContext';
import { useShopContext } from './ShopContext';
import { useI18n } from '../../shared/i18n/I18nContext';
import { downloadFile } from '../../shared/api/downloads';
type Props = { path: string; mime: 'text/csv' | 'application/pdf'; label: string; disabled?: boolean };
export function FileDownloadButton(props: Props) {
  const { session } = useSession();
  const { context } = useShopContext();
  return <DownloadControl key={`${session?.userId}/${context?.selectedShopId}/${props.path}`} {...props} />;
}
function DownloadControl({ path, mime, label, disabled }: Props) {
  const { locale } = useI18n();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<number | null>(null);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  async function download() {
    if (controller.current && !controller.current.signal.aborted) return;
    const request = new AbortController();
    controller.current = request;
    setPending(true);
    setError(null);
    try {
      await downloadFile(path, mime, request.signal);
    } catch (failure) {
      if (!request.signal.aborted) setError(failure instanceof ApiError ? failure.status : 0);
    } finally {
      if (!request.signal.aborted) {
        setPending(false);
        controller.current = null;
      }
    }
  }
  const message =
    locale === 'en-US'
      ? error === 429
        ? 'Download limit reached. Wait before retrying.'
        : 'Unable to download this file. Check your session, permissions and filters.'
      : error === 429
        ? 'Đã vượt giới hạn tải file. Vui lòng chờ trước khi thử lại.'
        : 'Không thể tải file. Kiểm tra phiên đăng nhập, quyền và bộ lọc.';
  return (
    <span className="file-download">
      <Button
        icon={<DownloadOutlined aria-hidden />}
        loading={pending}
        disabled={disabled || pending}
        onClick={() => {
          void download();
        }}
      >
        {label}
      </Button>
      {error !== null && <Alert role="alert" type="error" title={message} />}
    </span>
  );
}
