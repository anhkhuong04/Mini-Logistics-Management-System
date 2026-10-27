import { useEffect, useRef, useState } from 'react';
import { ApiError } from './httpClient';
export function useBrowserCommand() {
  const active = useRef(true);
  const controller = useRef<AbortController | null>(null);
  const sending = useRef(false);
  const locked = useRef(false);
  const [pending, setPending] = useState(false);
  const [blocked, setBlocked] = useState(false);
  const [message, setMessage] = useState('');
  const generation = useRef(0);
  useEffect(() => {
    active.current = true;
    return () => {
      active.current = false;
      controller.current?.abort();
    };
  }, []);
  async function run<T>(write: (signal: AbortSignal) => Promise<T>): Promise<{ value: T } | undefined> {
    if (sending.current || locked.current) return;
    sending.current = true;
    setPending(true);
    setMessage('');
    controller.current = new AbortController();
    try {
      const value = await write(controller.current.signal);
      if (!active.current) return;
      setMessage('Đã lưu thay đổi.');
      return { value };
    } catch (error) {
      if (!active.current) return;
      if (
        !(error instanceof ApiError) ||
        error.status === 0 ||
        error.status >= 500 ||
        error.status === 409 ||
        error.code === 'Browser.ProtocolError'
      ) {
        generation.current++;
        locked.current = true;
        setBlocked(true);
        setMessage(
          'Chưa xác định kết quả hoặc dữ liệu đã đổi. Làm mới và kiểm tra dữ liệu trước khi thao tác lại.',
        );
      } else setMessage(error instanceof ApiError ? error.title : 'Không thể lưu thay đổi.');
    } finally {
      sending.current = false;
      if (active.current) setPending(false);
    }
  }
  function verified(expected: number) {
    if (active.current && !sending.current && generation.current === expected) {
      locked.current = false;
      setBlocked(false);
    }
  }
  return { run, verified, snapshot: () => generation.current, pending, blocked, message };
}
