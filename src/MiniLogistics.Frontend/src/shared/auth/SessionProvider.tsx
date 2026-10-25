import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiError, onAccessFailure, refreshCsrf, requestJson, resetCsrf } from '../api/httpClient';
import { SessionContext, type Session } from './SessionContext';

export function SessionProvider({ children, initialSession }: { children: ReactNode; initialSession?: Session | null }) {
  const queryClient = useQueryClient();
  const [session, setSession] = useState<Session | null>(initialSession ?? null);
  const [isLoading, setLoading] = useState(initialSession === undefined);
  const [error, setError] = useState<Error | null>(null);
  const [permissionVersion, setPermissionVersion] = useState(0);
  const generation = useRef(0);
  const request = useRef<AbortController | null>(null);
  const reading = useRef(false);
  const deniedPaths = useRef(new Set<string>());
  const sessionUserId = useRef(initialSession?.userId ?? null);
  const clear = useCallback(() => {
    generation.current++;
    request.current?.abort();
    setSession(null);
    sessionUserId.current = null;
    setLoading(false);
    void queryClient.cancelQueries();
    queryClient.clear();
    resetCsrf();
  }, [queryClient]);
  const read = useCallback(async () => {
    const current = ++generation.current;
    request.current?.abort();
    const controller = new AbortController(); request.current = controller;
    reading.current = true;
    try {
      const value = await requestJson<Session>('/api/v1/app/auth/me', { signal: controller.signal });
      if (current === generation.current) {
        if (sessionUserId.current && sessionUserId.current !== value.userId) {
          void queryClient.cancelQueries(); queryClient.clear(); resetCsrf(); deniedPaths.current.clear();
        }
        sessionUserId.current = value.userId;
        setSession(value);
      }
    } catch (failure) {
      if (controller.signal.aborted) return;
      if (failure instanceof ApiError && [401, 403].includes(failure.status)) clear();
      else if (current === generation.current) setError(failure as Error);
    } finally { reading.current = false; if (!controller.signal.aborted) setLoading(false); }
  }, [clear, queryClient]);
  const reload = useCallback(async () => { setLoading(true); setError(null); await read(); }, [read]);
  const cancel = useCallback(() => { generation.current++; request.current?.abort(); }, []);
  useEffect(() => {
    // Bootstrap synchronizes with the server; read updates state after its awaited network response.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (initialSession === undefined) void read();
    return cancel;
  }, [initialSession, read, cancel]);
  useEffect(() => onAccessFailure((status, path) => {
    if (reading.current) return;
    if (status === 401) { clear(); setLoading(false); }
    else {
      if (deniedPaths.current.has(path)) return;
      deniedPaths.current.add(path);
      void queryClient.cancelQueries(); queryClient.clear();
      setPermissionVersion(version => version + 1);
      void reload();
    }
  }), [clear, queryClient, reload]);
  const signedIn = async (value: Session) => {
    clear();
    deniedPaths.current.clear();
    sessionUserId.current = value.userId;
    setSession(value); setLoading(false); setError(null);
    await refreshCsrf();
  };
  const logout = async () => {
    // Clear sensitive state immediately, even if logout delivery fails.
    clear(); setLoading(true);
    try { await requestJson('/api/v1/app/auth/logout', { method: 'POST' }); await refreshCsrf(); }
    finally { setLoading(false); }
  };
  return <SessionContext.Provider value={{ session, isLoading, error, reload, signedIn, logout, permissionVersion }}>{children}</SessionContext.Provider>;
}
