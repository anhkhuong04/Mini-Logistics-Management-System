import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Empty, Form, Input, Progress, Space, Statistic, Table } from 'antd';
import { ArrowRightOutlined, CheckCircleOutlined, ClockCircleOutlined, ExclamationCircleOutlined, InboxOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { Link, useSearchParams } from 'react-router';
import { PageHeader } from '../../shared/ui/PageHeader';
import { AsyncContent } from '../../shared/ui/AsyncContent';
import { useDashboardCopy } from '../../shared/i18n/dashboardCopy';
import { useShipmentCopy } from '../../shared/i18n/shipmentCopy';
import { useI18n } from '../../shared/i18n/I18nContext';
import { useSession } from '../../shared/auth/SessionContext';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { formatDateTime } from '../../shared/lib/dateTime';
import { useShopContext } from '../shop-context/ShopContext';
import { ShopContextSelector } from '../shop-context/ShopContextSelector';
import { ShopReadBoundary } from '../shop-context/ShopReadBoundary';
import { readShipmentFilters, shipmentListUrl, utcDateRange, type ShipmentStatus } from '../shipments/shipmentFilters';
import { useShipments } from '../shipments/queries';
import { ShipmentStatusTag } from '../shipments/ShipmentStatusTag';
import { routePaths } from '../../app/router/routePaths';
import { groupStatuses } from './statusDistribution';
import { useDashboard } from './queries';
import type { ShipmentItem } from '../shipments/api';

export function ShopDashboardPage() {
  const copy = useDashboardCopy(), shipmentCopy = useShipmentCopy(); const { locale } = useI18n(); const { session } = useSession();
  const { context } = useShopContext(); const shop = context?.shops.find(item => item.shopId === context.selectedShopId);
  const [search, setSearch] = useSearchParams(); const [form] = Form.useForm<{ fromDate: string; toDate: string }>();
  const [dateError, setDateError] = useState(false);
  const filters = useMemo(() => { try { return readShipmentFilters(search); } catch { return null; } }, [search]);
  const range = useMemo(() => filters ? { fromUtc: filters.fromUtc, toUtc: filters.toUtc } : null, [filters]);
  const query = useDashboard(range);
  const latestFilters = useMemo(() => filters ? { ...filters, status: undefined, trackingCode: undefined, receiverName: undefined,
    receiverPhone: undefined, minCodAmount: undefined, maxCodAmount: undefined, sortBy: 'CreatedAt' as const, sortDirection: 'Descending' as const, pageNumber: 1, pageSize: 5 } : null, [filters]);
  const latest = useShipments(latestFilters); const kpi = query.data;
  useEffect(() => { form.setFieldsValue({ fromDate: range?.fromUtc?.slice(0, 10) ?? '', toDate: range?.toUtc?.slice(0, 10) ?? '' }); }, [form, range]);
  function apply(from: string, to: string) {
    try { const dates = utcDateRange(from, to); const next = new URLSearchParams(); if (context) next.set('shopId', context.selectedShopId);
      if (dates.fromUtc) next.set('fromUtc', dates.fromUtc); if (dates.toUtc) next.set('toUtc', dates.toUtc); setDateError(false); setSearch(next);
    } catch { setDateError(true); }
  }
  function preset(days: number) {
    const today = new Date().toISOString().slice(0, 10); const start = new Date(`${today}T00:00:00Z`);
    start.setUTCDate(start.getUTCDate() + 1 - days); apply(start.toISOString().slice(0, 10), today);
  }
  const canCod = shop?.permissions.includes('ViewCod');
  const money = (value: string, currency = kpi!.currency) => formatMoney(decimalString(value), currency, locale);
  const link = (status?: ShipmentStatus) => shipmentListUrl(context!.selectedShopId, { ...range, status });
  const cards = kpi ? [
    { label: copy.total, value: kpi.totalShipments, icon: <InboxOutlined aria-hidden />, to: link() },
    { label: copy.pending, value: kpi.countByStatus.PendingPickup ?? 0, icon: <ClockCircleOutlined aria-hidden />, to: link('PendingPickup') },
    { label: copy.delivered, value: kpi.deliveredShipments, icon: <CheckCircleOutlined aria-hidden />, to: link('Delivered') },
    { label: copy.failed, value: kpi.deliveryFailedShipments, icon: <ExclamationCircleOutlined aria-hidden />, to: link('DeliveryFailed') },
  ] : [];
  const groups = kpi ? groupStatuses(kpi.countByStatus) : [];
  return <><PageHeader title={copy.title} description={copy.description} actions={<Space wrap>
    <Button icon={<ReloadOutlined aria-hidden />} disabled={!range || !context} onClick={() => { void query.refetch(); void latest.refetch(); }}>{shipmentCopy.refresh}</Button>
    {shop?.isActive && shop.permissions.includes('ManageShipments') && <Link className="ant-btn ant-btn-primary" to={`${routePaths.shipmentCreate}?shopId=${shop.shopId}`}><PlusOutlined aria-hidden /> {copy.create}</Link>}
  </Space>} />
    <ShopReadBoundary><ShopContextSelector showInactive={false} />
      <Form form={form} layout="vertical" className="filter-panel" aria-label={copy.range} onFinish={values => apply(values.fromDate, values.toDate)}>
        <Space wrap className="date-presets"><Button onClick={() => apply('', '')}>{copy.all}</Button><Button onClick={() => preset(7)}>{copy.days7}</Button><Button onClick={() => preset(30)}>{copy.days30}</Button></Space>
        <div className="filter-primary"><Form.Item name="fromDate" label={shipmentCopy.from}><Input type="date" /></Form.Item>
          <Form.Item name="toDate" label={shipmentCopy.to}><Input type="date" /></Form.Item><Button htmlType="submit">{shipmentCopy.apply}</Button></div>
      </Form>
      {(!range || dateError) && <Alert role="alert" type="error" title={shipmentCopy.invalid} />}
      {query.error ? <Alert type="error" role="alert" title={copy.kpiError} action={<Button onClick={() => void query.refetch()}>{shipmentCopy.retry}</Button>} />
        : range && <AsyncContent loading={query.isPending}>{kpi && <>
          <section aria-label={copy.summary} className="metric-grid">{cards.map(card => <Link key={card.label} to={card.to} aria-label={`${card.label}: ${card.value}`}>
            <Card><Statistic title={<span>{card.icon} {card.label}</span>} value={card.value} /></Card></Link>)}</section>
          <div className="dashboard-overview"><Card title={copy.distribution}>
            {groups.length === 0 ? <Empty description={copy.empty} /> : <ul className="distribution-list">{groups.map(group =>
              <li key={group.key}><div><span>{group.key === 'processing' ? copy.processing : shipmentCopy.labels[group.status]}</span><strong>{group.count}</strong></div>
                <Progress percent={group.percent} showInfo={false} strokeColor={group.color} aria-label={group.key === 'processing' ? copy.processing : shipmentCopy.labels[group.status]} />
              </li>)}</ul>}
          </Card><Card title={canCod ? copy.finance : copy.fees} extra={canCod && <a href={`/shop/cod-report?shopId=${shop!.shopId}`}>{copy.codReport}</a>}>
            <dl className="money-list">
              {canCod && <><div><dt>{copy.pendingCod}</dt><dd>{money(kpi.pendingCodAmount)}</dd></div><div><dt>{copy.collectedCod}</dt><dd>{money(kpi.collectedCodAmount)}</dd></div></>}
              <div><dt>{copy.fees}</dt><dd>{money(kpi.totalShippingFee)}</dd></div>
            </dl><p>{copy.period}</p>
          </Card></div>
          <Card title={copy.latest} extra={<Link to={link()}>{copy.allShipments} <ArrowRightOutlined aria-hidden /></Link>}>
            {latest.error ? <Alert type="error" role="alert" title={copy.latestError} action={<Button onClick={() => void latest.refetch()}>{shipmentCopy.retry}</Button>} />
              : <AsyncContent loading={latest.isPending}>{latest.data?.items.length === 0 ? <Empty description={copy.empty} /> :
                <div className="data-table-region" role="region" aria-label={copy.latest} tabIndex={0}><Table pagination={false} rowKey="shipmentId" dataSource={latest.data?.items ?? []} scroll={{ x: 640 }} columns={[
                  { title: shipmentCopy.tracking, dataIndex: 'trackingCode', key: 'tracking', render: (text, item) => <><Link to={`${routePaths.shipmentDetail(item.shipmentId)}?shopId=${context!.selectedShopId}`}>{text}</Link>
                    <small className="cell-note">{formatDateTime(item.createdAtUtc, session!.serverTimeZoneId, locale)}</small></> },
                  { title: shipmentCopy.receiver, dataIndex: 'receiverName', key: 'receiver' },
                  { title: shipmentCopy.status, dataIndex: 'status', key: 'status', render: status => <ShipmentStatusTag status={status} /> },
                  ...(canCod ? [{ title: 'COD', dataIndex: 'codAmount', key: 'cod', render: (value: string, item: ShipmentItem) => money(value, item.currency) }] : []),
                  { title: shipmentCopy.fee, dataIndex: 'shippingFeeAmount', key: 'fee', render: (value: string, item: ShipmentItem) => money(value, item.currency) },
                ]} /></div>}
              </AsyncContent>}
          </Card>
        </>}</AsyncContent>}
    </ShopReadBoundary></>;
}
