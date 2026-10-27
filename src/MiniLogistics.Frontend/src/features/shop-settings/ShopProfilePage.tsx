import { useState } from 'react';
import { Alert, Button, Card, Descriptions, Modal, Spin } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { PageHeader } from '../../shared/ui/PageHeader';
import { keepFocusInModal } from '../../shared/ui/modalFocus';
import { requestJson } from '../../shared/api/httpClient';
import { useShopContext } from '../shop-context';
import { getShopContext } from '../shop-context';
import { ShopSettingsBoundary, type SelectedShop } from './ShopSettingsBoundary';
import { readSettings, settingsKey, shopPath, type Profile, type ProfileBody } from './api';
import { ShopProfileForm } from './ShopProfileForm';
import { useSettingsCommand } from './useSettingsCommand';
import { SettingsFeedback } from './SettingsFeedback';
export function ShopProfilePage() {
  return (
    <ShopSettingsBoundary>
      {(shop, user) => <ProfileWorkspace shop={shop} user={user} />}
    </ShopSettingsBoundary>
  );
}
function ProfileWorkspace({ shop, user }: { shop: SelectedShop; user: string }) {
  const query = useQuery({
    queryKey: [...settingsKey(user, shop.shopId), 'profile'],
    queryFn: ({ signal }) => readSettings<Profile>(shop.shopId, 'profile', signal),
    refetchOnWindowFocus: false,
  });
  const { context, reload } = useShopContext();
  const cache = useQueryClient();
  const navigate = useNavigate();
  const command = useSettingsCommand();
  const [target, setTarget] = useState<{ body: ProfileBody; create: boolean } | null>(null);
  async function refresh() {
    if (command.pending) return;
    const generation = command.snapshot();
    const [result] = await Promise.all([
      query.refetch(),
      getShopContext(shop.shopId, new AbortController().signal),
    ]).catch(() => []);
    if (result?.isSuccess) {
      command.verified(generation);
      reload();
    }
  }
  async function save() {
    if (!target) return;
    const action = target;
    setTarget(null);
    const result = await command.run((signal) =>
      requestJson<{ shopId: string }>(
        action.create ? '/api/v1/app/shops' : `${shopPath(shop.shopId)}/profile`,
        { method: action.create ? 'POST' : 'PUT', body: action.body, signal },
      ),
    );
    if (result) {
      await cache.invalidateQueries({ queryKey: ['user', user] });
      reload();
      if (action.create) navigate(`/profile?shopId=${result.value.shopId}`);
    }
  }
  const canEdit =
    shop.isActive &&
    shop.permissions.includes('ManageShopProfile') &&
    shop.permissions.includes('ViewFullPii');
  return (
    <>
      <PageHeader
        title="Hồ sơ Shop"
        actions={
          <Button
            icon={<ReloadOutlined aria-hidden />}
            disabled={command.pending}
            loading={query.isFetching}
            onClick={() => void refresh()}
          >
            Làm mới
          </Button>
        }
      />
      <SettingsFeedback message={command.message} />
      {query.isError ? (
        <Alert role="alert" type="error" title="Không tải được hồ sơ Shop" />
      ) : query.isPending ? (
        <Spin />
      ) : (
        <Card title="Shop hiện tại">
          {canEdit ? (
            <ShopProfileForm
              key={query.dataUpdatedAt}
              initial={query.data}
              pending={command.pending}
              blocked={command.blocked}
              onSave={(body) => setTarget({ body, create: false })}
            />
          ) : (
            <>
              <Alert type="info" title="Hồ sơ chỉ đọc theo quyền hiện tại" />
              <Descriptions
                column={1}
                items={['name', 'phoneNumber', 'addressLine', 'ward', 'province', 'country'].map((key) => ({
                  key,
                  label: key,
                  children: String(query.data[key as keyof Profile]),
                }))}
              />
            </>
          )}
        </Card>
      )}
      {context?.shops.some((item) => item.isOwner) && (
        <Card title="Tạo thêm Shop">
          <ShopProfileForm
            create
            pending={command.pending}
            blocked={command.blocked}
            onSave={(body) => setTarget({ body, create: true })}
          />
        </Card>
      )}
      {target && (
        <Modal
          open
          title={target.create ? 'Xác nhận tạo Shop?' : 'Xác nhận lưu hồ sơ?'}
          wrapProps={{ onKeyDown: keepFocusInModal }}
          onCancel={() => setTarget(null)}
          onOk={() => void save()}
          okText="Xác nhận"
          cancelText="Đóng"
        >
          <p>{target.body.name}</p>
        </Modal>
      )}
    </>
  );
}
