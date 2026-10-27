import { browserReturnPath, routePaths } from '../../shared/routing/routePaths';
const legacy =
  /^\/(?:dashboard|operations\/assignments?|shipper\/shipments|partner\/integrations|shipments(?:\/create|\/[0-9a-f-]{36})?|profile|shop\/(?:staff|notifications|cod-report|audit)|admin(?:\/(?:dashboard|users|shops|hubs|system-config|cod|audit-logs|banners))?)(?:\?|$)/i;
function migratedDestination(value: string) {
  return /^\/(?:partner\/integrations|admin(?:\/(?:dashboard|users|shops|hubs|system-config|cod|audit-logs|banners))?|profile|shop\/(?:staff|notifications|cod-report|audit)|dashboard|operations\/assignments?|shipper\/shipments|shipments(?:\/create|\/[0-9a-f-]{36})?)(?:\?|$)/i.test(
    value,
  )
    ? browserReturnPath(value)
    : value;
}
export function safeReturnUrl(value: string | null): string | null {
  if (
    !value ||
    value.includes('\\') ||
    value.split('?')[0].includes('%') ||
    Array.from(value).some((character) => character.charCodeAt(0) <= 32) ||
    value.startsWith('//')
  )
    return null;
  if (value === '/app/' || value === '/app') return value;
  if (value.startsWith('/app/')) value = value.slice(4);
  return legacy.test(value) ? migratedDestination(value) : null;
}
export function postLoginPath(roles: string[]) {
  if (roles.some((role) => role === 'Admin' || role === 'Operator'))
    return browserReturnPath(routePaths.operations);
  if (roles.includes('Shipper')) return browserReturnPath(routePaths.shipper);
  if (roles.includes('Shop')) return browserReturnPath('/dashboard');
  if (roles.includes('IntegrationAdmin')) return browserReturnPath('/partner/integrations');
  return '/app/';
}
