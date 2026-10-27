import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Card, Form, Input, Modal, Table, Tag } from 'antd';
import { EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { PageHeader } from '../../shared/ui/PageHeader';
import { keepFocusInModal } from '../../shared/ui/modalFocus';
import { requestJson } from '../../shared/api/httpClient';
import { useShopContext } from '../shop-context';
import { ShopSettingsBoundary } from './ShopSettingsBoundary';
import { readSettings, settingsKey, shopPath, type Staff, type CreateStaff, type StaffAccess } from './api';
import { SettingsFeedback } from './SettingsFeedback';
import { useSettingsCommand } from './useSettingsCommand';
import { StaffPermissionForm } from './StaffPermissionForm';
import { presets } from './models';
export function ShopStaffPage() {
  return (
    <ShopSettingsBoundary ownerOnly>
      {(shop, user) => <StaffWorkspace shopId={shop.shopId} user={user} />}
    </ShopSettingsBoundary>
  );
}
function StaffWorkspace({ shopId, user }: { shopId: string; user: string }) {
  const query = useQuery({
    queryKey: [...settingsKey(user, shopId), 'staff'],
    queryFn: ({ signal }) => readSettings<Staff[]>(shopId, 'staff', signal),
  });
  const cache = useQueryClient();
  const { reload } = useShopContext();
  const command = useSettingsCommand();
  const [editing, setEditing] = useState<Staff | null>(null);
  const [confirm, setConfirm] = useState<{ staffUserId?: string; access: StaffAccess } | null>(null);
  const [form] = Form.useForm<CreateStaff & { isActive: boolean }>();
  const secret = useRef<CreateStaff | null>(null);
  useEffect(
    () => () => {
      secret.current = null;
    },
    [],
  );
  async function refresh() {
    if (command.pending) return;
    const generation = command.snapshot();
    const result = await query.refetch();
    if (result.isSuccess) {
      command.verified(generation);
      reload();
    }
  }
  async function save() {
    if (!confirm) return;
    const action = confirm;
    const create = secret.current;
    secret.current = null;
    setConfirm(null);
    const result = await command.run((signal) =>
      requestJson<Staff>(`${shopPath(shopId)}/staff${action.staffUserId ? `/${action.staffUserId}` : ''}`, {
        method: action.staffUserId ? 'PUT' : 'POST',
        body: action.staffUserId ? action.access : create,
        signal,
      }),
    );
    if (result) {
      setEditing(null);
      form.resetFields();
      await cache.invalidateQueries({ queryKey: ['user', user] });
      reload();
    }
  }
  function close() {
    secret.current = null;
    form.setFieldValue('password', '');
    setConfirm(null);
  }
  return (
    <>
      <PageHeader
        title="Nhân viên Shop"
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
        <Alert role="alert" type="error" title="Không tải được nhân viên" />
      ) : (
        <Table
          rowKey="userId"
          dataSource={query.data}
          loading={query.isPending}
          pagination={false}
          scroll={{ x: 700 }}
          columns={[
            { title: 'Họ tên', dataIndex: 'fullName' },
            { title: 'Email', dataIndex: 'email' },
            { title: 'Điện thoại', dataIndex: 'phoneNumber' },
            { title: 'Vai trò', dataIndex: 'role' },
            { title: 'Quyền', render: (_, row: Staff) => row.permissions.join(', ') },
            {
              title: 'Hoạt động',
              render: (_, row: Staff) => <Tag>{row.isActive ? 'Active' : 'Inactive'}</Tag>,
            },
            {
              title: 'Thao tác',
              render: (_, row: Staff) => (
                <Button
                  icon={<EditOutlined aria-hidden />}
                  disabled={command.pending || command.blocked || query.isFetching}
                  onClick={() => {
                    secret.current = null;
                    setEditing(row);
                    form.resetFields();
                    form.setFieldsValue({
                      role: row.role,
                      permissions: row.permissions,
                      isActive: row.isActive,
                    });
                  }}
                >
                  Sửa {row.fullName}
                </Button>
              ),
            },
          ]}
        />
      )}
      <Card title={editing ? `Sửa quyền ${editing.fullName}` : 'Tạo nhân viên'}>
        <Form
          form={form}
          name="shop-staff"
          layout="vertical"
          initialValues={{ role: 'Viewer', permissions: presets.Viewer, isActive: true }}
          disabled={command.pending || command.blocked}
          requiredMark={(label, { required: marked }) => (
            <>
              {marked && (
                <span aria-hidden="true" className="required-mark">
                  *
                </span>
              )}
              {label}
            </>
          )}
          onFinish={(values) => {
            if (!editing) {
              secret.current = values;
              form.setFieldValue('password', '');
            }
            setConfirm({
              staffUserId: editing?.userId,
              access: { role: values.role, permissions: values.permissions, isActive: values.isActive },
            });
          }}
        >
          {!editing && (
            <>
              <Form.Item
                name="fullName"
                label="Họ tên"
                rules={[{ required: true, whitespace: true }, { max: 200 }]}
              >
                <Input maxLength={200} />
              </Form.Item>
              <Form.Item
                name="email"
                label="Email nhân viên"
                rules={[{ required: true, type: 'email' }, { max: 256 }]}
              >
                <Input autoComplete="off" maxLength={256} />
              </Form.Item>
              <Form.Item
                name="phoneNumber"
                label="Điện thoại nhân viên"
                rules={[{ required: true }, { pattern: /^\+?[\d ]{9,20}$/ }]}
              >
                <Input maxLength={20} />
              </Form.Item>
              <Form.Item
                name="password"
                label="Mật khẩu khởi tạo"
                extra="8–100 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
                rules={[
                  { required: true },
                  { min: 8, max: 100 },
                  {
                    pattern: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$/,
                    message: 'Mật khẩu cần chữ hoa, chữ thường, số và ký tự đặc biệt',
                  },
                ]}
              >
                <Input.Password autoComplete="new-password" maxLength={100} />
              </Form.Item>
            </>
          )}
          <StaffPermissionForm editing={!!editing} />
          <Button
            type="primary"
            htmlType="submit"
            icon={<PlusOutlined aria-hidden />}
            loading={command.pending}
            disabled={command.pending || command.blocked}
          >
            {editing ? 'Lưu quyền' : 'Tạo nhân viên'}
          </Button>
          {editing && (
            <Button
              disabled={command.pending}
              onClick={() => {
                setEditing(null);
                form.resetFields();
              }}
            >
              Hủy sửa
            </Button>
          )}
        </Form>
      </Card>
      {confirm && (
        <Modal
          open
          title={confirm.staffUserId ? 'Xác nhận thay đổi quyền/hoạt động?' : 'Xác nhận tạo nhân viên?'}
          wrapProps={{ onKeyDown: keepFocusInModal }}
          onCancel={close}
          onOk={() => void save()}
          okText="Xác nhận"
          cancelText="Đóng"
        >
          <p>{confirm.access.role}</p>
          <p>{confirm.access.permissions.join(', ')}</p>
          {confirm.staffUserId && <p>{confirm.access.isActive ? 'Active' : 'Inactive'}</p>}
        </Modal>
      )}
    </>
  );
}
