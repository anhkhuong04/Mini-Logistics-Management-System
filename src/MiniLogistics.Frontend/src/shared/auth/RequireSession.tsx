import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';
import { Result } from 'antd';
import { useSession } from './SessionContext';
import { AsyncContent } from '../ui/AsyncContent';
import { browserReturnPath } from '../../app/router/routePaths';
import { useAuthCopy } from '../i18n/authCopy';
export function RequireSession({ children, roles }: { children: ReactNode; roles?: string[] }) {
  const { session, isLoading, error, reload } = useSession();
  const location = useLocation();
  const copy = useAuthCopy();
  if (isLoading || error) return <AsyncContent loading={isLoading} error={Boolean(error)} onRetry={() => void reload()}>{null}</AsyncContent>;
  if (!session) return <Navigate replace to={`/login?returnUrl=${encodeURIComponent(browserReturnPath(`${location.pathname}${location.search}`))}`} />;
  if (roles && !roles.some(role => session.roles?.includes(role))) return <Result status="403" title={copy.forbidden} />;
  return <>{children}</>;
}
