import { useState } from 'react';
import { Alert, Badge, Button, Card, Checkbox, Empty, Modal, Spin } from 'antd';
import { CheckOutlined, ReloadOutlined, SaveOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router';
import { PageHeader } from '../../shared/ui/PageHeader';
import { keepFocusInModal } from '../../shared/ui/modalFocus';
import { requestJson } from '../../shared/api/httpClient';
import { ShopSettingsBoundary, type SelectedShop } from './ShopSettingsBoundary';
import { readSettings, settingsKey, shopPath, type Notification, type Preferences } from './api';
import { useSettingsCommand } from './useSettingsCommand';
import { SettingsFeedback } from './SettingsFeedback';
const events = [
  'shipment.assigned',
  'shipment.picked_up',
  'shipment.delivered',
  'shipment.delivery_failed',
  'shipment.returned',
  'cod.collected',
  'cod.settled',
];
export function ShopNotificationsPage() {
  return (
    <ShopSettingsBoundary>
      {(shop, user) => <NotificationsWorkspace shop={shop} user={user} />}
    </ShopSettingsBoundary>
  );
}
function NotificationsWorkspace({ shop, user }: { shop: SelectedShop; user: string }) {
  const key = settingsKey(user, shop.shopId);
  const canManage = shop.permissions.includes('ManageNotifications');
  const list = useQuery({
    queryKey: [...key, 'notifications'],
    queryFn: ({ signal }) => readSettings<Notification[]>(shop.shopId, 'notifications?limit=100', signal),
  });
  const prefs = useQuery({
    queryKey: [...key, 'notification-preferences'],
    queryFn: ({ signal }) => readSettings<Preferences>(shop.shopId, 'notifications/preferences', signal),
    enabled: canManage,
    refetchOnWindowFocus: false,
  });
  const cache = useQueryClient();
  const command = useSettingsCommand();
  const [target, setTarget] = useState<string[] | null>(null);
  async function refresh() {
    if (command.pending) return;
    const generation = command.snapshot();
    const results = await Promise.all([list.refetch(), ...(canManage ? [prefs.refetch()] : [])]);
    if (results.every((result) => result.isSuccess)) command.verified(generation);
  }
  async function save(id?: string) {
    const enabledEventTypes = target;
    setTarget(null);
    const result = await command.run((signal) =>
      requestJson(`${shopPath(shop.shopId)}/notifications/${id ? `${id}/read` : 'preferences'}`, {
        method: id ? 'POST' : 'PUT',
        body: id ? {} : { enabledEventTypes },
        signal,
      }),
    );
    if (result) await cache.invalidateQueries({ queryKey: id ? [...key, 'notifications'] : key });
  }
  const busy = command.pending || command.blocked;
  return (
    <>
      <PageHeader
        title="Thông báo Shop"
        actions={
          <Button
            icon={<ReloadOutlined aria-hidden />}
            disabled={command.pending}
            loading={list.isFetching || (canManage && prefs.isFetching)}
            onClick={() => void refresh()}
          >
            Làm mới
          </Button>
        }
      />
      <SettingsFeedback message={command.message} />
      {canManage &&
        (prefs.isError ? (
          <Alert role="alert" type="error" title="Không tải được tùy chọn thông báo" />
        ) : prefs.isPending ? (
          <Spin />
        ) : (
          <PreferencesForm
            key={prefs.dataUpdatedAt}
            value={prefs.data.enabledEventTypes}
            busy={busy || prefs.isFetching}
            onSave={setTarget}
          />
        ))}
      {list.isError ? (
        <Alert role="alert" type="error" title="Không tải được thông báo" />
      ) : list.isPending ? (
        <Spin />
      ) : (
        <>
          <p>
            <Badge count={list.data.filter((item) => !item.isRead).length} showZero /> chưa đọc trong tối đa
            100 thông báo mới nhất.
          </p>
          {list.data.length === 0 ? (
            <Empty description="Không có thông báo" />
          ) : (
            list.data.map((item) => (
              <Card key={item.notificationId} title={item.title} className="settings-notice">
                <p>{item.message}</p>
                {item.shipmentId && (
                  <Link to={`/shipments/${item.shipmentId}?shopId=${shop.shopId}`}>Xem vận đơn</Link>
                )}
                {!item.isRead && (
                  <Button
                    icon={<CheckOutlined aria-hidden />}
                    disabled={busy || list.isFetching}
                    onClick={() => void save(item.notificationId)}
                    aria-label={`Đã đọc ${item.title}`}
                  >
                    Đã đọc
                  </Button>
                )}
              </Card>
            ))
          )}
        </>
      )}
      {target && (
        <Modal
          open
          title="Xác nhận tùy chọn thông báo?"
          wrapProps={{ onKeyDown: keepFocusInModal }}
          onOk={() => void save()}
          onCancel={() => setTarget(null)}
          okText="Xác nhận"
          cancelText="Đóng"
        >
          <p>{target.length ? target.join(', ') : 'Tắt tất cả thông báo'}</p>
        </Modal>
      )}
    </>
  );
}
function PreferencesForm({
  value,
  busy,
  onSave,
}: {
  value: string[];
  busy: boolean;
  onSave: (value: string[]) => void;
}) {
  const [selected, setSelected] = useState(value);
  return (
    <Card title="Tùy chọn thông báo">
      <Checkbox.Group
        aria-label="Loại thông báo"
        options={events}
        value={selected}
        disabled={busy}
        onChange={(values) => setSelected(values as string[])}
      />
      <Button icon={<SaveOutlined aria-hidden />} disabled={busy} onClick={() => onSave(selected)}>
        Lưu tùy chọn
      </Button>
    </Card>
  );
}
