import { Card } from 'antd';
import { Link } from 'react-router';
import { formatDateTime } from '../../shared/lib/dateTime';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { useSession } from '../../shared/auth/SessionContext';
import { AdminShell, AdminReadState } from './AdminShell';
import { AdminFilters } from './AdminFilters';
import { useAdminRead } from './queries';
import type { Schema } from './api';
export function AdminDashboardPage() {
  const query = useAdminRead<Schema['AdminDashboardResponse']>('dashboard'); const data = query.data; const { session } = useSession();
  const time = (value: string) => formatDateTime(value, session!.serverTimeZoneId, 'vi-VN');
  return <AdminShell title="Tổng quan quản trị" busy={query.isFetching} refresh={() => void query.refetch()}><AdminFilters kind="report" />
    <AdminReadState pending={query.isPending} error={query.isError}>{data && <>
      <p>Vận đơn tạo từ {time(data.fromUtc)} đến {time(data.toUtc)} · {data.province ?? 'Tất cả khu vực'}</p>
      <div className="admin-summary"><Card title="Vận đơn trong kỳ"><p>Đã tạo: {data.shipments.created}</p><p>Chờ phân công: {data.shipments.pendingPickupUnassigned}</p><p>Giao thất bại: {data.shipments.deliveryFailed}</p><p>Tỷ lệ thất bại: {data.shipments.deliveryFailedRate}%</p><Link to="/operations/assignments">Điều phối</Link></Card>
        <Card title="Shipper hiện tại"><p>Active: {data.shippers.active}</p><p>Nhận đơn: {data.shippers.availableForAssignment}</p><p>Vượt tải: {data.shippers.overCapacity}</p><Link to="/admin/users?role=Shipper">Quản lý shipper</Link></Card>
        <Card title="Shop hiện tại"><p>Active: {data.shops.active}</p><p>Inactive: {data.shops.inactive}</p><Link to="/admin/shops">Quản lý Shop</Link></Card>
        <Card title="COD trong kỳ"><p>Chờ thu ({data.cod.pendingCollectionCount}): {formatMoney(decimalString(data.cod.pendingCollectionAmount), data.cod.currency)}</p><p>Chờ đối soát ({data.cod.collectedAwaitingSettlementCount}): {formatMoney(decimalString(data.cod.collectedAwaitingSettlementAmount), data.cod.currency)}</p><p>Đã đối soát ({data.cod.settledCount}): {formatMoney(decimalString(data.cod.settledAmount), data.cod.currency)}</p><Link to="/admin/cod">Báo cáo COD</Link></Card>
        <Card title="Webhook trong kỳ"><p>Failed: {data.webhooks.failed}</p><p>Retry pending: {data.webhooks.retryPending}</p></Card></div>
    </>}</AdminReadState></AdminShell>;
}
