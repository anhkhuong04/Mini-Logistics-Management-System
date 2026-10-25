export type ApiProblem = { status: number; title: string; code: string; traceId: string; errors?: Record<string, string[]> };
export class ApiError extends Error implements ApiProblem {
  constructor(public status: number, public code: string, public title: string, public traceId = '', public errors?: Record<string, string[]>) {
    super(title); this.name = 'ApiError';
  }
}
export type RequestOptions = { method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'; body?: unknown; signal?: AbortSignal };
let csrf: string | undefined;
let csrfRequest: Promise<string> | undefined;
let csrfGeneration = 0;
const listeners = new Set<(status: number) => void>();
export function onAccessFailure(listener: (status: number) => void) { listeners.add(listener); return () => { listeners.delete(listener); }; }
export function resetCsrf() { csrf = undefined; csrfRequest = undefined; csrfGeneration++; }
export async function refreshCsrf() {
  resetCsrf();
  await getCsrf();
}
async function getCsrf() {
  if (csrf) return csrf;
  if (!csrfRequest) {
    const generation = csrfGeneration;
    const pending = requestJson<{ requestToken: string }>('/api/v1/app/auth/csrf').then(value => {
      if (generation === csrfGeneration) csrf = value.requestToken;
      return value.requestToken;
    }).finally(() => { if (generation === csrfGeneration) csrfRequest = undefined; });
    csrfRequest = pending;
  }
  return csrfRequest;
}
function assertLocal(path: string) {
  if (!path.startsWith('/') || path.startsWith('//') || path.includes('\\')) throw new ApiError(0, 'Browser.ProtocolError', 'Invalid request URL');
}
async function send(path: string, options: RequestOptions, form?: FormData) {
  assertLocal(path);
  const method = options.method ?? (form ? 'POST' : 'GET');
  const headers = new Headers({ Accept: 'application/json, application/problem+json' });
  if (method !== 'GET') headers.set('X-CSRF-TOKEN', await getCsrf());
  if (!form && options.body !== undefined) headers.set('Content-Type', 'application/json');
  const response = await fetch(path, { method, headers, credentials: 'same-origin', cache: 'no-store', signal: options.signal,
    body: form ?? (options.body === undefined ? undefined : JSON.stringify(options.body)) });
  if (response.status === 401 || response.status === 403) for (const listener of listeners) listener(response.status);
  return response;
}
async function json<T>(response: Response): Promise<T> {
  if (response.status === 204) return undefined as T;
  const contentType = response.headers.get('Content-Type') ?? '';
  if (!contentType.includes('application/json') && !contentType.includes('application/problem+json'))
    throw new ApiError(response.status, 'Browser.ProtocolError', 'Unexpected server response');
  let body: unknown;
  try { body = await response.json(); }
  catch { throw new ApiError(response.status, 'Browser.ProtocolError', 'Invalid server response'); }
  if (!response.ok) {
    const problem = body as Partial<ApiProblem>;
    throw new ApiError(response.status, problem.code ?? 'Browser.HttpError', problem.title ?? 'Request failed', problem.traceId, problem.errors);
  }
  return body as T;
}
export async function requestJson<T>(path: string, options: RequestOptions = {}): Promise<T> { return json<T>(await send(path, options)); }
export async function requestForm<T>(path: string, form: FormData, options: Omit<RequestOptions, 'body'> = {}): Promise<T> {
  return json<T>(await send(path, options, form));
}
export async function requestDownload(path: string, signal?: AbortSignal) {
  const response = await send(path, { signal });
  if (!response.ok) return json<never>(response);
  if ((response.headers.get('Content-Type') ?? '').includes('text/html')) throw new ApiError(response.status, 'Browser.ProtocolError', 'Unexpected download response');
  const disposition = response.headers.get('Content-Disposition') ?? '';
  let name = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1] ?? /filename="?([^";]+)/i.exec(disposition)?.[1] ?? 'download';
  try { name = decodeURIComponent(name); } catch { name = 'download'; }
  const fileName = Array.from(name.split(/[\\/]/).at(-1)!).filter(character => character.charCodeAt(0) >= 32 && character.charCodeAt(0) !== 127).join('') || 'download';
  return { blob: await response.blob(), fileName };
}
