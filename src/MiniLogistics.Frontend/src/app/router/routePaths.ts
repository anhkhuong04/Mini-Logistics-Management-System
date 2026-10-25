export const routePaths = { home: '/', signIn: '/login', contact: '/contact', faq: '/faq' } as const;
export function browserReturnPath(path: string) {
  const basePath = window.__MINI_LOGISTICS_CONFIG__?.basePath ?? '/app';
  return `${basePath}${path.startsWith('/') ? path : `/${path}`}`;
}
