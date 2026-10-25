import { useRef, useState } from 'react';
import { Alert, Button, Card, Empty, Form, Input, Modal, Pagination, Result, Select, Space, Spin, Tabs, Tag, Typography } from 'antd';
import { FilterOutlined, ReloadOutlined, TruckOutlined } from '@ant-design/icons';
import { useSearchParams } from 'react-router';
import { useSession } from '../../shared/auth/SessionContext';
import { PageHeader } from '../../shared/ui/PageHeader';
import { keepFocusInModal } from '../../shared/ui/modalFocus';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { readShipperFilters, shipperSearch, stages, codStatuses, type ShipperFilters } from './models';
import { useShipperQueries } from './queries';
import { useShipperCommands } from './useShipperCommands';
import { ShipmentTaskCard } from './ShipmentTaskCard';
import type { ShipperAction } from './api';
import './shipper.css';

export function ShipperWorkspacePage() {
  const { session, permissionVersion } = useSession();
  if (!session?.roles.includes('Shipper')) return <Result status="403" title="Không có quyền Shipper" />;
  return <ShipperWorkspace key={`${session.userId}:${permissionVersion}:${session.roles.join(',')}`} userId={session.userId} timeZone={session.serverTimeZoneId} />;
}
function ShipperWorkspace({ userId, timeZone }: { userId: string; timeZone: string }) {
  const [search, setSearch] = useSearchParams();
  let parsed: ShipperFilters | undefined; try { parsed = readShipperFilters(search); } catch { /* Invalid URL is displayed before the list. */ }
  const filters = parsed ?? { pageNumber: 1, pageSize: 20 };
  const { workspace, profile, areas, summary } = useShipperQueries(userId, filters, !!parsed);
  const commands = useShipperCommands(userId); const [target, setTarget] = useState<ShipperAction | null>(null);
  const [lastSearch, setLastSearch] = useState(search.toString());
  if (lastSearch !== search.toString()) { setLastSearch(search.toString()); setTarget(null); }
  const refreshButton = useRef<HTMLButtonElement>(null); const trigger = useRef<HTMLElement | null>(null);
  const stageLabels = { Pickup: 'Lấy hàng', Transit: 'Trung chuyển', Delivery: 'Giao hàng', CodPending: 'COD chờ thu' };
  const ask = (action: ShipperAction) => { trigger.current = document.activeElement instanceof HTMLElement ? document.activeElement : null; setTarget(action); };
  function close() {
    setTarget(null);
    requestAnimationFrame(() => { if (trigger.current?.isConnected && !trigger.current.matches(':disabled')) trigger.current.focus(); else refreshButton.current?.focus(); });
  }
  async function refresh() {
    const failures = commands.snapshotFailures();
    const [pageRead, profileRead] = await Promise.all([workspace.refetch(), profile.refetch(), summary.refetch(), areas.refetch()]);
    commands.unlockVerified(failures, key => key === '@availability' ? profileRead.isSuccess : pageRead.isSuccess);
  }
  function replace(next: Partial<ShipperFilters>) { setTarget(null); setSearch(shipperSearch(next)); }
  const isRefreshing = workspace.isFetching || profile.isFetching || summary.isFetching || areas.isFetching;
  return <div className="shipper-workspace">
    <PageHeader title="Đơn được phân công" description="Cập nhật hành trình và theo dõi COD của bạn."
      actions={<Button ref={refreshButton} icon={<ReloadOutlined aria-hidden />} loading={isRefreshing} onClick={() => { void refresh(); }}>Làm mới</Button>} />
    {commands.notice && <p role="status">{commands.notice}</p>}
    {profile.isError ? <Alert role="alert" type="error" title="Không tải được hồ sơ shipper. Làm mới để thử lại." /> : profile.isPending ? <Spin aria-label="Đang tải hồ sơ shipper" /> : <Card>
      <div className="shipper-profile"><div><Typography.Title level={2}>{profile.data.fullName}</Typography.Title>
        <Typography.Text>Tải vận chuyển: <strong>{profile.data.activeShipmentCount}/{profile.data.maxActiveShipments}</strong></Typography.Text>
        <p>{profile.data.isAvailableForAssignment ? 'Đang nhận phân công tự động' : 'Tạm dừng phân công tự động'}</p></div>
        <Button icon={<TruckOutlined aria-hidden />} disabled={profile.isFetching || commands.busy('@availability')} loading={commands.pending.has('@availability')}
          onClick={() => ask({ kind: 'availability', key: '@availability', available: !profile.data.isAvailableForAssignment })}>
          {profile.data.isAvailableForAssignment ? 'Tạm dừng nhận đơn' : 'Nhận đơn tự động'}</Button></div>
      {commands.messages['@availability'] && <p role="status">{commands.messages['@availability']}</p>}
      {areas.isError ? <Alert role="alert" type="error" title="Không tải được vùng làm việc." /> : areas.isPending ? <p>Đang tải vùng làm việc…</p> : areas.data.length === 0 ? <p>Chưa được gán hub làm việc</p>
        : <Space wrap>{areas.data.map(area => <Tag key={area.workingAreaId}>{area.hubName} — {[area.province, area.ward, area.zoneCode].filter(Boolean).join(', ')}</Tag>)}</Space>}
    </Card>}
    {summary.isError ? <Alert role="alert" type="error" title="Không tải được báo cáo COD. Làm mới để thử lại." /> : summary.isPending ? <Spin aria-label="Đang tải báo cáo COD" /> : <section aria-label="Báo cáo COD" className="shipper-cod-summary">
      <Typography.Title level={2}>Báo cáo COD ngày {summary.data.businessDate} (UTC)</Typography.Title>
      <div className="shipper-summary-grid"><Card title="COD cần thu"><strong>{formatMoney(decimalString(summary.data.pendingCollectionAmount), summary.data.currency)}</strong><p>{summary.data.pendingCollectionCount} đơn</p></Card>
        <Card title="COD đã thu trong ngày"><strong>{formatMoney(decimalString(summary.data.collectedTodayAmount), summary.data.currency)}</strong><p>{summary.data.collectedTodayCount} đơn</p></Card></div>
    </section>}
    {!parsed ? <Alert role="alert" type="error" title="Bộ lọc không hợp lệ" action={<Button onClick={() => replace({ pageNumber: 1, pageSize: 20 })}>Xóa lọc</Button>} /> : <>
      <Tabs activeKey={filters.stage ?? 'all'} onChange={stage => replace({ ...filters, stage: stage === 'all' ? undefined : stage as ShipperFilters['stage'], codStatus: undefined, pageNumber: 1 })}
        items={[{ key: 'all', label: 'Tất cả' }, ...stages.map(stage => ({ key: stage, label: stageLabels[stage] }))]} />
      <Form key={search.toString()} name="shipper-filters" className="shipper-filters" layout="vertical" initialValues={filters}
        onFinish={(values: Partial<ShipperFilters>) => replace({ ...filters, ...values, pageNumber: 1 })}>
        <Form.Item name="searchText" label="Tìm kiếm"><Input maxLength={100} placeholder="Mã vận đơn đầy đủ, tên hoặc SĐT" allowClear /></Form.Item>
        <Form.Item name="codStatus" label="COD"><Select allowClear disabled={filters.stage === 'CodPending'} placeholder={filters.stage === 'CodPending' ? 'Chờ thu theo nhóm trạng thái' : 'Tất cả COD'} options={codStatuses.map(value => ({ value, label: value }))} /></Form.Item>
        <Form.Item name="businessDate" label="Ngày báo cáo COD (UTC)"><Input type="date" /></Form.Item>
        <Space wrap><Button htmlType="submit" icon={<FilterOutlined aria-hidden />}>Lọc</Button><Button onClick={() => replace({ pageNumber: 1, pageSize: filters.pageSize })}>Xóa lọc</Button></Space>
      </Form>
      {workspace.isError ? <Alert role="alert" type="error" title="Không tải được vận đơn. Làm mới để thử lại." /> : workspace.isPending ? <Spin aria-label="Đang tải vận đơn" /> : <>
        <p role="status">{workspace.data.totalCount} đơn khớp lọc; {workspace.data.items.filter(shipment => shipment.status === 'Delivering').length} đơn đang giao trên trang này.</p>
        {workspace.data.items.length === 0 ? <Empty description="Không có đơn cần xử lý" /> : <div className="shipper-task-grid">{workspace.data.items.map(shipment =>
          <ShipmentTaskCard key={`${search}:${shipment.shipmentId}:${shipment.status}:${shipment.codStatus}`} shipment={shipment} timeZone={timeZone}
            busy={workspace.isFetching || commands.busy(shipment.shipmentId)} pending={commands.pending.has(shipment.shipmentId)} message={commands.messages[shipment.shipmentId]}
            onStatus={body => ask({ kind: 'status', key: shipment.shipmentId, code: shipment.trackingCode, body })}
            onCollect={() => ask({ kind: 'collect', key: shipment.shipmentId, code: shipment.trackingCode })} />)}</div>}
        <Pagination className="shipper-pagination" current={workspace.data.pageNumber} pageSize={workspace.data.pageSize} total={workspace.data.totalCount} showSizeChanger pageSizeOptions={[10, 20, 50, 100]}
          onChange={(pageNumber, pageSize) => replace({ ...filters, pageNumber, pageSize })} />
      </>}
    </>}
    {target && <Modal open title={target.kind === 'collect' ? 'Xác nhận đã thu COD?' : target.kind === 'status' ? 'Xác nhận cập nhật trạng thái?' : target.available ? 'Bật nhận đơn tự động?' : 'Tạm dừng nhận đơn?'}
      wrapProps={{ onKeyDown: keepFocusInModal }} okText="Xác nhận" cancelText="Đóng" onCancel={close} onOk={() => { const action = target; close(); void commands.execute(action); }}>
      {target.kind === 'availability' ? <p>Chỉ thay đổi việc tự động nhận đơn mới; các đơn đang được phân công vẫn thuộc trách nhiệm của bạn.</p>
        : <><p>{target.code}</p><p>{target.kind === 'collect' ? 'Chỉ xác nhận sau khi đã nhận đủ số tiền COD khai báo.' : `Trạng thái mới: ${target.body.newStatus}`}</p>{target.kind === 'status' && <p>{target.body.note}</p>}</>}
    </Modal>}
  </div>;
}
