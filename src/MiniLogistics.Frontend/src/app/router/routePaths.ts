export const routePaths = { home: '/', signIn: '/login', contact: '/contact', faq: '/faq', dashboard: '/dashboard', shipments: '/shipments',
  shipmentCreate: '/shipments/create',
  operations: '/operations/assignments', operationsAlias: '/operations/assignment',
  shipper: '/shipper/shipments',
  shopProfile: '/profile', shopStaff: '/shop/staff', shopNotifications: '/shop/notifications', shopCodReport: '/shop/cod-report', shopAudit: '/shop/audit',
  shipmentDetailPattern: '/shipments/:id', shipmentDetail: (id: string) => `/shipments/${encodeURIComponent(id)}` } as const;
export function browserReturnPath(path: string) {
  const basePath = window.__MINI_LOGISTICS_CONFIG__?.basePath ?? '/app';
  return `${basePath}${path.startsWith('/') ? path : `/${path}`}`;
}
