import { browserReturnPath, routePaths } from '../../app/router/routePaths';
const legacy = /^\/(?:dashboard|operations\/assignments?|shipper\/shipments|partner\/integrations|shipments(?:\/create|\/[0-9a-f-]{36})?|profile|shop\/(?:staff|notifications|cod-report|audit)|admin(?:\/(?:dashboard|users|shops|hubs|system-config|cod|audit-logs|banners))?)(?:\?|$)/i;
function migratedDestination(value: string) {
  return /^\/(?:dashboard|operations\/assignments?|shipments(?:\/create|\/[0-9a-f-]{36})?)(?:\?|$)/i.test(value) ? browserReturnPath(value) : value;
}
export function safeReturnUrl(value: string | null): string | null {
  if (!value || value.includes('\\') || value.split('?')[0].includes('%') || Array.from(value).some(character => character.charCodeAt(0) <= 32) || value.startsWith('//')) return null;
  if (value === '/app/' || value === '/app') return value;
  if (value.startsWith('/app/')) value = value.slice(4);
  return legacy.test(value) ? migratedDestination(value) : null;
}
export function postLoginPath(roles: string[]) {
  if (roles.some(role => role === 'Admin' || role === 'Operator')) return browserReturnPath(routePaths.operations);
  if (roles.includes('Shipper')) return '/shipper/shipments';
  if (roles.includes('Shop')) return browserReturnPath('/dashboard');
  if (roles.includes('IntegrationAdmin')) return '/partner/integrations';
  return '/app/';
}
