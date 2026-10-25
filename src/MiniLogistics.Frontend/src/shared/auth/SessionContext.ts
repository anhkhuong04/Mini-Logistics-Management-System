import { createContext, useContext } from 'react';
import type { components } from '../api/generated/browser-api';
export type Session = components['schemas']['BrowserSession'];
export type SessionState = { session: Session | null; isLoading: boolean; error: Error | null;
  reload(): Promise<void>; logout(): Promise<void>; signedIn(session: Session): Promise<void>; permissionVersion: number };
export const SessionContext = createContext<SessionState | null>(null);
export function useSession() {
  const value = useContext(SessionContext);
  if (!value) throw new Error('SessionProvider is required');
  return value;
}
