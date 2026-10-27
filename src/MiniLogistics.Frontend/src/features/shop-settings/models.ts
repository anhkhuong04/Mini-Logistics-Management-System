export const permissionNames = [
  'ViewShipments',
  'ManageShipments',
  'ViewFullPii',
  'ExportData',
  'ViewCod',
  'ViewAudit',
  'ManageIntegrations',
  'ManageNotifications',
  'ManageShopProfile',
];
export const presets: Record<string, string[]> = {
  Manager: permissionNames,
  Operator: ['ViewShipments', 'ManageShipments', 'ViewFullPii', 'ViewCod', 'ManageNotifications'],
  Analyst: ['ViewShipments', 'ExportData', 'ViewCod', 'ViewAudit'],
  Viewer: ['ViewShipments'],
  Custom: ['ViewShipments'],
};
