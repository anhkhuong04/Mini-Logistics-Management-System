import { Alert, Button, Card, Empty, Spin, Table } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { useSearchParams, Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { PageHeader } from '../../shared/ui/PageHeader';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { formatDateTime } from '../../shared/lib/dateTime';
import { useSession } from '../../shared/auth/SessionContext';
import { FileDownloadButton } from '../shipments/FileDownloadButton';
import { codExportUrl } from '../shipments/downloads';
import { ShopSettingsBoundary, type SelectedShop } from './ShopSettingsBoundary';
import { readSettings, settingsKey, type CodReport } from './api';
import { SettingsFilters } from './SettingsFilters';
import { settingsFilters } from './queries';
export function ShopCodReportPage() { return <ShopSettingsBoundary permission="ViewCod">{(shop, user) => <CodWorkspace shop={shop} user={user} />}</ShopSettingsBoundary>; }
function CodWorkspace({ shop, user }: { shop: SelectedShop; user: string }) {
  const [search] = useSearchParams(); let range: { fromUtc?: string; toUtc?: string } | undefined;
  try { range = settingsFilters(search); } catch { /* Display invalid URL before requesting data. */ }
  const params = new URLSearchParams(); if (range?.fromUtc) params.set('fromUtc', range.fromUtc); if (range?.toUtc) params.set('toUtc', range.toUtc);
  const query = useQuery({ queryKey: [...settingsKey(user, shop.shopId), 'cod', params.toString()], queryFn: ({ signal }) => readSettings<CodReport>(shop.shopId, `cod-report?${params}`, signal), enabled: !!range });
  const { session } = useSession(); const report = query.data; const money = (value: string) => formatMoney(decimalString(value), report!.currency);
  return <><PageHeader title="Báo cáo COD" actions={<Button icon={<ReloadOutlined aria-hidden />} loading={query.isFetching} onClick={() => void query.refetch()} disabled={!range}>Làm mới</Button>} />
    <SettingsFilters />{!range ? <Alert role="alert" type="error" title="Bộ lọc không hợp lệ" /> : query.isError ? <Alert role="alert" type="error" title="Không tải được báo cáo COD" /> : query.isPending ? <Spin /> : report && <>
      <div className="settings-summary">{([['Chờ thu', report.pendingCollectionAmount], ['Đã thu (gồm đối soát)', report.collectedAmount], ['Đã đối soát', report.settledAmount], ['Chênh lệch', report.discrepancyAmount]] as const).map(([label, value]) => <Card key={label} title={label}><strong>{money(value)}</strong></Card>)}</div>
      <p>Báo cáo tối đa 10.000 dòng mới nhất theo ngày tạo vận đơn; tổng tiền áp dụng cùng tập dòng theo nghiệp vụ hiện tại.</p>
      {shop.permissions.includes('ExportData') && <FileDownloadButton path={codExportUrl(shop.shopId, range)} mime="text/csv" label="Xuất COD CSV" />}
      {report.rows.length === 0 ? <Empty description="Không có dữ liệu COD" /> : <Table rowKey="shipmentId" dataSource={report.rows} pagination={false} scroll={{ x: 900 }} columns={[
        { title: 'Vận đơn', render: (_, row: CodReport['rows'][number]) => <Link to={`/shipments/${row.shipmentId}?shopId=${shop.shopId}`}>{row.trackingCode}</Link> },
        { title: 'Trạng thái', dataIndex: 'shipmentStatus' }, { title: 'COD', dataIndex: 'codStatus' }, { title: 'Khai báo', render: (_, row) => money(row.declaredAmount) },
        { title: 'Thực thu', render: (_, row) => row.collectedAmount == null ? 'Chưa thu' : money(row.collectedAmount) }, { title: 'Chênh lệch', render: (_, row) => money(row.discrepancyAmount) },
        { title: 'Ngày tạo', render: (_, row) => formatDateTime(row.createdAtUtc, session!.serverTimeZoneId, 'vi-VN') },
      ]} />}
    </>}</>;
}
