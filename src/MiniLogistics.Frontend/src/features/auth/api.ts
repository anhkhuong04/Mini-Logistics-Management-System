import type { components } from '../../shared/api/generated/browser-api';
import { requestJson } from '../../shared/api/httpClient';
import type { Session } from '../../shared/auth/SessionContext';
export type LoginRequest = components['schemas']['BrowserLoginRequest'];
export type Registration = components['schemas']['RegisterShopCommand'];
export type ProvinceData = components['schemas']['BrowserProvinceData'];
export const login = (body: LoginRequest) => requestJson<Session>('/api/v1/app/auth/login', { method: 'POST', body });
export const register = (body: Registration) => requestJson<Session>('/api/v1/app/auth/register-shop', { method: 'POST', body });
