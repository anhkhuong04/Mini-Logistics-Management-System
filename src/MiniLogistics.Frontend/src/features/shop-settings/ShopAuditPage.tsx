import { Alert, Button, Empty, Spin, Table } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { useSearchParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { PageHeader } from '../../shared/ui/PageHeader';
import { formatDateTime } from '../../shared/lib/dateTime';
import { useSession } from '../../shared/auth/SessionContext';
import { ShopSettingsBoundary } from './ShopSettingsBoundary';
import { readSettings, settingsKey, type Audit } from './api';
import { SettingsFilters } from './SettingsFilters';
import { settingsFilters } from './queries';
export function ShopAuditPage() {
  return (
    <ShopSettingsBoundary permission="ViewAudit">
      {(shop, user) => <AuditWorkspace shopId={shop.shopId} user={user} />}
    </ShopSettingsBoundary>
  );
}
function AuditWorkspace({ shopId, user }: { shopId: string; user: string }) {
  const [search] = useSearchParams();
  let valid = true;
  const params = new URLSearchParams();
  try {
    for (const [key, value] of Object.entries(settingsFilters(search)))
      if (value != null) params.set(key, String(value));
  } catch {
    valid = false;
  }
  const query = useQuery({
    queryKey: [...settingsKey(user, shopId), 'audit', params.toString()],
    queryFn: ({ signal }) => readSettings<Audit[]>(shopId, `audit?${params}`, signal),
    enabled: valid,
  });
  const { session } = useSession();
  return (
    <>
      <PageHeader
        title="Nhật ký Shop"
        actions={
          <Button
            icon={<ReloadOutlined aria-hidden />}
            loading={query.isFetching}
            disabled={!valid}
            onClick={() => void query.refetch()}
          >
            Làm mới
          </Button>
        }
      />
      <SettingsFilters audit />
      <p>Nhật ký chỉ đọc, tối đa 500 dòng mới nhất theo bộ lọc.</p>
      {!valid ? (
        <Alert role="alert" type="error" title="Bộ lọc không hợp lệ" />
      ) : query.isError ? (
        <Alert role="alert" type="error" title="Không tải được nhật ký" />
      ) : query.isPending ? (
        <Spin />
      ) : query.data.length === 0 ? (
        <Empty description="Không có hoạt động phù hợp" />
      ) : (
        <Table
          rowKey="auditLogId"
          dataSource={query.data}
          pagination={false}
          scroll={{ x: 850 }}
          columns={[
            {
              title: 'Thời gian',
              render: (_, row: Audit) => formatDateTime(row.createdAtUtc, session!.serverTimeZoneId, 'vi-VN'),
            },
            { title: 'Action', dataIndex: 'action' },
            { title: 'Target', render: (_, row) => `${row.targetType} ${row.targetId}` },
            {
              title: 'Thay đổi',
              render: (_, row) => (
                <>
                  <pre>{row.oldValueJson}</pre>
                  <pre>{row.newValueJson}</pre>
                </>
              ),
            },
            { title: 'Lý do', dataIndex: 'reason' },
          ]}
        />
      )}
    </>
  );
}
