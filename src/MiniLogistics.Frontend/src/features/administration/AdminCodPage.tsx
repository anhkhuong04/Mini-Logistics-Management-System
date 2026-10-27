import { useState } from 'react';
import { Table, Button, Card } from 'antd';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { requestJson } from '../../shared/api/httpClient';
import { useBrowserCommand } from '../../shared/api/useBrowserCommand';
import { AdminShell, AdminReadState, AdminConfirm } from './AdminShell';
import { AdminFilters } from './AdminFilters';
import { useAdminRead } from './queries';
import type { Schema } from './api';
export function AdminCodPage() {
  const query = useAdminRead<Schema['AdminCodReportResponse']>('cod-report');
  const command = useBrowserCommand();
  const [target, setTarget] = useState<Schema['AdminCodTransactionResponse'] | null>(null);
  async function refresh() {
    const generation = command.snapshot();
    const result = await query.refetch();
    if (result.isSuccess) command.verified(generation);
  }
  async function settle() {
    if (!target) return;
    const id = target.shipmentId;
    setTarget(null);
    const result = await command.run((signal) =>
      requestJson(`/api/v1/app/operations/shipments/${id}/cod/settle`, { method: 'POST', body: {}, signal }),
    );
    if (result) await query.refetch();
  }
  const data = query.data;
  const money = (v: string) => formatMoney(decimalString(v), data!.summary.currency);
  return (
    <AdminShell
      title="COD quản trị"
      busy={query.isFetching || command.pending}
      refresh={() => void refresh()}
      message={command.message}
    >
      <AdminFilters kind="cod" />
      <AdminReadState pending={query.isPending} error={query.isError}>
        {data && (
          <>
            <div className="admin-summary">
              <Card title="Chờ thu">
                {data.summary.pendingCollectionCount} đơn · {money(data.summary.pendingCollectionAmount)}
              </Card>
              <Card title="Chờ đối soát">
                {data.summary.collectedAwaitingSettlementCount} đơn ·{' '}
                {money(data.summary.collectedAwaitingSettlementAmount)}
              </Card>
              <Card title="Đã đối soát">
                {data.summary.settledCount} đơn · {money(data.summary.settledAmount)}
              </Card>
            </div>
            <Table
              rowKey="shipmentId"
              dataSource={data.items}
              pagination={{ pageSize: 25, showSizeChanger: false }}
              scroll={{ x: 900 }}
              columns={[
                { title: 'Vận đơn', dataIndex: 'trackingCode' },
                { title: 'Người nhận', dataIndex: 'receiverName' },
                { title: 'Điện thoại', dataIndex: 'receiverPhone' },
                { title: 'Tỉnh', dataIndex: 'province' },
                { title: 'Shipper', dataIndex: 'shipperName' },
                { title: 'COD', dataIndex: 'codStatus' },
                { title: 'Tiền', render: (_, row) => money(row.amount) },
                {
                  title: 'Thao tác',
                  render: (_, row) =>
                    row.codStatus === 'Collected' && (
                      <Button
                        disabled={command.pending || command.blocked || query.isFetching}
                        onClick={() => setTarget(row)}
                      >
                        Đối soát {row.trackingCode}
                      </Button>
                    ),
                },
              ]}
            />
            {[
              ['Theo shipper', data.byShipper],
              ['Theo tỉnh', data.byProvince],
            ].map(([label, rows]) => (
              <Card key={String(label)} title={String(label)}>
                <Table
                  rowKey="groupKey"
                  dataSource={rows as Schema['AdminCodGroupSummary'][]}
                  pagination={false}
                  scroll={{ x: 600 }}
                  columns={[
                    { title: 'Nhóm', dataIndex: 'groupKey' },
                    { title: 'Số đơn', dataIndex: 'shipmentCount' },
                    { title: 'Chờ thu', render: (_, row) => money(row.pendingAmount) },
                    { title: 'Đã thu', render: (_, row) => money(row.collectedAmount) },
                    { title: 'Đối soát', render: (_, row) => money(row.settledAmount) },
                    { title: 'Tỷ lệ thu', dataIndex: 'collectedRate' },
                  ]}
                />
              </Card>
            ))}
            <Card title="Theo ngày UTC">
              <Table
                rowKey="day"
                dataSource={data.byDay}
                pagination={false}
                scroll={{ x: 600 }}
                columns={[
                  { title: 'Ngày', dataIndex: 'day' },
                  { title: 'Số đơn', dataIndex: 'shipmentCount' },
                  { title: 'Chờ thu', render: (_, row) => money(row.pendingAmount) },
                  { title: 'Đã thu', render: (_, row) => money(row.collectedAmount) },
                  { title: 'Đối soát', render: (_, row) => money(row.settledAmount) },
                ]}
              />
            </Card>
          </>
        )}
      </AdminReadState>
      {target && (
        <AdminConfirm
          title="Xác nhận đối soát COD?"
          cancel={() => setTarget(null)}
          confirm={() => void settle()}
        >
          <p>{target.trackingCode}</p>
          <p>{money(target.amount)}</p>
        </AdminConfirm>
      )}
    </AdminShell>
  );
}
