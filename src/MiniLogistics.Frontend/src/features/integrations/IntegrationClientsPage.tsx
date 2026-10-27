import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Card, Empty, Modal, Select, Spin, Table } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import { useSession } from '../../shared/auth/SessionContext';
import { requestJson } from '../../shared/api/httpClient';
import { useBrowserCommand } from '../../shared/api/useBrowserCommand';
import { PageHeader } from '../../shared/ui/PageHeader';
import { keepFocusInModal } from '../../shared/ui/modalFocus';
import { formatDateTime } from '../../shared/lib/dateTime';
import { ClientScopeForm } from './ClientScopeForm';
import { OneTimeSecretModal } from './OneTimeSecretModal';
import { WebhookSettingsPanel } from './WebhookSettingsPanel';
import { WebhookDeliveryTable } from './WebhookDeliveryTable';
import { IntegrationAuditPanel } from './IntegrationAuditPanel';
import {
  integrationPath,
  integrationWrite,
  type ClientSecret,
  type IntegrationDashboard,
  type SecurityBody,
} from './api';
import './integrations.css';
type Action = {
  kind: 'create' | 'rotate' | 'active' | 'security' | 'webhook' | 'test' | 'delivery' | 'outbox';
  id?: string;
  body?: unknown;
};
const actionNames = {
  create: 'Tạo API client',
  rotate: 'Rotate API key',
  active: 'Đổi trạng thái client',
  security: 'Cập nhật quyền API',
  webhook: 'Cập nhật webhook',
  test: 'Xếp hàng test webhook',
  delivery: 'Retry delivery',
  outbox: 'Retry outbox',
};
export function IntegrationClientsPage() {
  const { session, permissionVersion } = useSession();
  const [search] = useSearchParams();
  return (
    <IntegrationWorkspace
      key={`${session!.userId}:${permissionVersion}:${search.get('shopId')}:${search.get('clientId')}`}
    />
  );
}
function IntegrationWorkspace() {
  const { session } = useSession();
  const [search, setSearch] = useSearchParams();
  const shopId = search.get('shopId');
  const clientId = search.get('clientId');
  const time = (value?: string | null) =>
    value ? formatDateTime(value, session!.serverTimeZoneId, session!.locale) : '—';
  const valid = !shopId || /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(shopId);
  const query = useQuery({
    queryKey: ['user', session!.userId, 'integrations', shopId],
    enabled: valid,
    retry: false,
    staleTime: 0,
    gcTime: 0,
    queryFn: ({ signal }) =>
      requestJson<IntegrationDashboard>(
        integrationPath(`dashboard${shopId ? `?${new URLSearchParams({ shopId })}` : ''}`),
        { signal },
      ),
  });
  const data = query.data;
  const shop = data?.shops.find((s) => s.shopId === shopId);
  const selected = data?.apiClients.find((c) => c.apiClientId === clientId);
  const [target, setTarget] = useState<Action | null>(null);
  const [secret, setSecret] = useState<ClientSecret | null>(null);
  const signing = useRef('');
  const command = useBrowserCommand();
  useEffect(
    () => () => {
      signing.current = '';
    },
    [],
  );
  useEffect(() => {
    if (!shopId && data?.shops.length)
      setSearch(
        (current) => {
          current.set('shopId', data.shops.find((s) => s.isActive)?.shopId ?? data.shops[0].shopId);
          current.delete('clientId');
          return current;
        },
        { replace: true },
      );
  }, [data, shopId, setSearch]);
  const busy = command.pending || command.blocked || query.isFetching || !shop?.isActive;
  async function refresh() {
    const generation = command.snapshot();
    const result = await query.refetch();
    if (result.isSuccess) command.verified(generation);
  }
  function cancel() {
    signing.current = '';
    setTarget(null);
  }
  async function save() {
    if (!target) return;
    const action = target;
    const password = signing.current;
    signing.current = '';
    setTarget(null);
    const endpoint =
      action.kind === 'create'
        ? 'clients'
        : action.kind === 'delivery'
          ? `deliveries/${action.id}/retry`
          : action.kind === 'outbox'
            ? `outbox/${action.id}/retry`
            : `clients/${action.id}/${({ rotate: 'rotate-key', active: 'active', security: 'security', webhook: 'webhook', test: 'webhook/test' } as const)[action.kind]}`;
    const body =
      action.kind === 'webhook'
        ? { ...(action.body as { url: string }), signingSecret: password }
        : (action.body ?? {});
    const result = await command.run((signal) =>
      integrationWrite<ClientSecret | undefined>(
        endpoint,
        ['active', 'security', 'webhook'].includes(action.kind) ? 'PUT' : 'POST',
        body,
        signal,
      ),
    );
    if (result) {
      if (action.kind === 'create' || action.kind === 'rotate') setSecret(result.value as ClientSecret);
      await query.refetch();
    }
  }
  const request = (kind: Action['kind'], body?: unknown) =>
    setTarget({ kind, id: selected?.apiClientId, body });
  return (
    <div className="integrations-workspace">
      <PageHeader
        title="Quản lý tích hợp"
        actions={
          <Button
            icon={<ReloadOutlined aria-hidden />}
            disabled={command.pending || query.isFetching}
            onClick={() => void refresh()}
          >
            Làm mới
          </Button>
        }
      />
      {command.message && <Alert role="status" title={command.message} />}
      {!valid || query.isError ? (
        <Alert
          type="error"
          role="alert"
          title="Không tải được tích hợp hoặc bạn không có quyền quản lý Shop này."
          action={<Button onClick={() => setSearch({})}>Chọn lại Shop</Button>}
        />
      ) : query.isPending ? (
        <Spin />
      ) : (
        data && (
          <>
            <p>
              Môi trường key mới: <strong>{data.environment}</strong>
            </p>
            {data.granularPermissionEnabled && (
              <p>Danh sách Shop được giới hạn theo phạm vi quản lý đã cấp.</p>
            )}
            <p>
              Múi giờ hiển thị: {session!.serverTimeZoneId}. Cửa sổ sử dụng API và ô nhập thời điểm hết hạn
              dùng UTC.
            </p>
            <label htmlFor="integration-shop">Shop quản lý</label>
            <Select
              id="integration-shop"
              value={shopId ?? undefined}
              disabled={command.pending}
              options={data.shops.map((s) => ({ value: s.shopId, label: s.shopName }))}
              onChange={(id) => {
                signing.current = '';
                setSecret(null);
                setSearch({ shopId: id });
              }}
            />
            {!data.shops.length ? (
              <Empty description="Chưa có Shop trong phạm vi quản lý." />
            ) : (
              shop && (
                <>
                  {!shop.isActive && (
                    <Alert
                      type="warning"
                      title="Shop đang ngừng hoạt động. Chỉ xem lịch sử; không thể thay đổi tích hợp."
                    />
                  )}
                  <Card title="Tạo API client">
                    <ClientScopeForm
                      busy={busy}
                      submit={(body: SecurityBody) => request('create', { ...body, shopId })}
                    />
                  </Card>
                  <div role="region" aria-label="Danh sách API client" tabIndex={0}>
                    <Table
                      rowKey="apiClientId"
                      dataSource={data.apiClients}
                      pagination={{ pageSize: 10 }}
                      scroll={{ x: 900 }}
                      columns={[
                        { title: 'Tên', dataIndex: 'name' },
                        { title: 'Prefix', dataIndex: 'apiKeyPrefix' },
                        { title: 'Hoạt động', render: (_, row) => String(row.isActive) },
                        { title: 'Quyền', render: (_, row) => row.scopes.join(', ') },
                        {
                          title: 'IP',
                          render: (_, row) => row.allowedIpAddresses.join(', ') || 'Không giới hạn',
                        },
                        { title: 'Hết hạn', render: (_, row) => time(row.expiresAtUtc) },
                        { title: 'Dùng gần nhất', render: (_, row) => time(row.lastUsedAtUtc) },
                        {
                          title: 'Thao tác',
                          render: (_, row) => (
                            <Button
                              disabled={command.pending}
                              onClick={() => {
                                signing.current = '';
                                setSecret(null);
                                setSearch({ shopId: shopId!, clientId: row.apiClientId });
                              }}
                            >
                              Chọn {row.name}
                            </Button>
                          ),
                        },
                      ]}
                    />
                  </div>
                  {clientId && !selected && (
                    <Alert
                      type="error"
                      role="alert"
                      title="Client không thuộc Shop đang chọn hoặc quyền đã thay đổi."
                    />
                  )}
                  {selected && (
                    <>
                      <Card title={`Client: ${selected.name}`}>
                        <Button danger disabled={busy} onClick={() => request('rotate')}>
                          Rotate API key
                        </Button>
                        <Button
                          danger={selected.isActive}
                          disabled={busy}
                          onClick={() => request('active', { isActive: !selected.isActive })}
                        >
                          {selected.isActive ? 'Thu hồi API client' : 'Kích hoạt API client'}
                        </Button>
                        <p>Rotate tạo key mới, vô hiệu key cũ và kích hoạt lại client.</p>
                        <ClientScopeForm
                          key={JSON.stringify([
                            selected.scopes,
                            selected.allowedIpAddresses,
                            selected.expiresAtUtc,
                          ])}
                          client={selected}
                          busy={busy}
                          submit={(body) => request('security', body)}
                        />
                      </Card>
                      <WebhookSettingsPanel
                        key={`${selected.apiClientId}:${selected.webhookEndpoint?.updatedAtUtc ?? ''}`}
                        client={selected}
                        busy={busy}
                        submit={(url, value) => {
                          signing.current = value;
                          request('webhook', { url });
                        }}
                        test={() => request('test')}
                      />
                      <WebhookDeliveryTable
                        client={selected}
                        failures={data.outboxFailures}
                        busy={busy}
                        retry={(kind, id) => setTarget({ kind, id })}
                      />
                      <IntegrationAuditPanel client={selected} />
                    </>
                  )}
                </>
              )
            )}
          </>
        )
      )}
      {target && (
        <Modal
          open
          title="Xác nhận thao tác tích hợp?"
          wrapProps={{ onKeyDown: keepFocusInModal }}
          onCancel={cancel}
          onOk={() => void save()}
          okText="Xác nhận"
          cancelText="Đóng"
        >
          <p>
            {actionNames[target.kind]} ·{' '}
            {target.kind === 'create' ? (target.body as SecurityBody).name : selected?.name}
          </p>
          <p>
            {shop?.shopName} · {target.id}
          </p>
          {target.kind === 'active' && (
            <p>
              {(target.body as { isActive: boolean }).isActive
                ? 'Kích hoạt API client'
                : 'Thu hồi API client; key hiện tại sẽ không được chấp nhận.'}
            </p>
          )}
          {target.kind === 'rotate' && <p>Key cũ sẽ hết hiệu lực. Key mới chỉ hiện một lần.</p>}
          {target.kind === 'security' && <pre>{JSON.stringify(target.body, null, 2)}</pre>}
          {target.kind === 'webhook' && (
            <p>Lưu URL mới và signing secret vừa nhập; endpoint sẽ được kích hoạt.</p>
          )}
        </Modal>
      )}
      {secret && (
        <OneTimeSecretModal
          value={secret}
          close={() => {
            const id = secret.apiClientId;
            setSecret(null);
            setSearch({ shopId: shopId!, clientId: id });
          }}
        />
      )}
    </div>
  );
}
