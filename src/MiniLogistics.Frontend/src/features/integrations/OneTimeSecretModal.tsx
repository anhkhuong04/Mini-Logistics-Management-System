import { useState } from 'react';
import { Alert, Button, Input, Modal } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import { keepFocusInModal } from '../../shared/ui/modalFocus';
import type { ClientSecret } from './api';
export function OneTimeSecretModal({ value, close }: { value: ClientSecret; close: () => void }) {
  const [feedback, setFeedback] = useState('');
  async function copy() {
    try { await navigator.clipboard.writeText(value.apiKey); setFeedback('Đã sao chép API key.'); }
    catch { setFeedback('Không thể sao chép. Chọn và sao chép key trực tiếp.'); }
  }
  return <Modal open title="API key chỉ hiển thị một lần" onCancel={close} wrapProps={{ onKeyDown: keepFocusInModal }} footer={<Button onClick={close}>Đã lưu key, đóng</Button>}>
    <Alert type="warning" title="Lưu key ở nơi an toàn. Đóng hộp thoại sẽ xóa key khỏi giao diện; không thể xem lại key này." />
    <label htmlFor="one-time-api-key">API key mới</label><Input.Password id="one-time-api-key" value={value.apiKey} readOnly autoComplete="off" />
    <Button icon={<CopyOutlined aria-hidden />} onClick={() => void copy()}>Sao chép API key</Button>{feedback && <p role="status">{feedback}</p>}
  </Modal>;
}
