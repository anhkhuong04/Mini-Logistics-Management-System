import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../../shared/api/httpClient';
import { sendAction, type ShipperAction } from './api';
import { shipperKey } from './queries';
export function useShipperCommands(userId: string) {
  const cache = useQueryClient();
  const alive = useRef(true);
  const requests = useRef(new Set<AbortController>());
  const sending = useRef(new Set<string>());
  const failures = useRef(new Map<string, number>());
  const version = useRef(0);
  const [pending, setPending] = useState(new Set<string>());
  const [blocked, setBlocked] = useState(new Set<string>());
  const [messages, setMessages] = useState<Record<string, string>>({});
  const [notice, setNotice] = useState('');
  useEffect(() => {
    alive.current = true;
    const controllers = requests.current;
    return () => {
      alive.current = false;
      controllers.forEach((controller) => controller.abort());
    };
  }, []);
  async function execute(action: ShipperAction) {
    if (sending.current.has(action.key) || failures.current.has(action.key)) return;
    sending.current.add(action.key);
    setPending(new Set(sending.current));
    setMessages((previous) => ({ ...previous, [action.key]: '' }));
    const controller = new AbortController();
    requests.current.add(controller);
    try {
      await sendAction(action, controller.signal);
      if (!alive.current) return;
      const text =
        action.kind === 'availability'
          ? 'Đã cập nhật trạng thái nhận đơn.'
          : `${action.code}: ${action.kind === 'collect' ? 'Đã xác nhận thu COD.' : 'Đã cập nhật trạng thái.'}`;
      setMessages((previous) => ({ ...previous, [action.key]: text }));
      setNotice(text);
      await cache.invalidateQueries({ queryKey: shipperKey(userId) });
    } catch (error) {
      if (!alive.current) return;
      const unknown =
        !(error instanceof ApiError) ||
        error.status === 0 ||
        error.status >= 500 ||
        error.code === 'Browser.ProtocolError';
      const conflict = error instanceof ApiError && error.status === 409;
      const text = unknown
        ? 'Chưa xác định kết quả. Làm mới để kiểm tra trước khi thao tác lại.'
        : conflict
          ? 'Dữ liệu đã thay đổi (409). Làm mới trước khi thử lại.'
          : error instanceof ApiError
            ? error.title
            : 'Không thể thực hiện thao tác.';
      setMessages((previous) => ({ ...previous, [action.key]: text }));
      if (unknown || conflict) {
        failures.current.set(action.key, ++version.current);
        setBlocked(new Set(failures.current.keys()));
        void cache.invalidateQueries({ queryKey: shipperKey(userId), refetchType: 'none' });
      }
    } finally {
      requests.current.delete(controller);
      sending.current.delete(action.key);
      if (alive.current) setPending(new Set(sending.current));
    }
  }
  function unlockVerified(snapshot: Map<string, number>, verified: (key: string) => boolean) {
    if (!alive.current) return;
    for (const [key, failureVersion] of snapshot)
      if (verified(key) && failures.current.get(key) === failureVersion) failures.current.delete(key);
    setBlocked(new Set(failures.current.keys()));
  }
  return {
    execute,
    messages,
    notice,
    pending,
    blocked,
    busy: (key: string) => pending.has(key) || blocked.has(key),
    snapshotFailures: () => new Map(failures.current),
    unlockVerified,
  };
}
