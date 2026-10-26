import type { components } from '../../shared/api/generated/browser-api';
import { requestJson } from '../../shared/api/httpClient';
export type Schema = components['schemas'];
export const adminKey = (user: string) => ['user', user, 'administration'] as const;
export const adminPath = (part: string) => `/api/v1/app/admin/${part}`;
export const readAdmin = <T,>(part: string, signal: AbortSignal) => requestJson<T>(adminPath(part), { signal });
export const writeAdmin = <T,>(part: string, method: 'POST' | 'PUT' | 'DELETE', body: unknown, signal: AbortSignal) => requestJson<T>(adminPath(part), { method, body, signal });
