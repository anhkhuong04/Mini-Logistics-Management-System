import type { ReactNode } from 'react';
import { Alert, Button, Empty, Input, Modal, Space, Spin } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { Link } from 'react-router';
import { useSession } from '../../shared/auth/SessionContext';
import { PageHeader } from '../../shared/ui/PageHeader';
import { keepFocusInModal } from '../../shared/ui/modalFocus';
import './administration.css';
const links = [
  ['Tổng quan', '/admin/dashboard'],
  ['COD', '/admin/cod'],
  ['Nhật ký', '/admin/audit-logs'],
  ['Người dùng', '/admin/users'],
  ['Shop', '/admin/shops'],
  ['Hub', '/admin/hubs'],
  ['Cấu hình', '/admin/system-config'],
  ['Banner', '/admin/banners'],
];
export function AdminShell({
  title,
  busy,
  refresh,
  message,
  children,
}: {
  title: string;
  busy?: boolean;
  refresh?: () => void;
  message?: string;
  children: ReactNode;
}) {
  return (
    <div className="admin-workspace">
      <PageHeader
        title={title}
        actions={
          refresh && (
            <Button icon={<ReloadOutlined aria-hidden />} disabled={busy} onClick={refresh}>
              Làm mới
            </Button>
          )
        }
      />
      <nav aria-label="Quản trị">
        <Space wrap>
          {links.map(([label, path]) => (
            <Link key={path} to={path}>
              {label}
            </Link>
          ))}
        </Space>
      </nav>
      {message && <Alert role="status" title={message} />}
      {children}
    </div>
  );
}
export function AdminReadState({
  pending,
  error,
  empty,
  children,
}: {
  pending: boolean;
  error: boolean;
  empty?: boolean;
  children: ReactNode;
}) {
  return error ? (
    <Alert role="alert" type="error" title="Không tải được dữ liệu quản trị" />
  ) : pending ? (
    <Spin />
  ) : empty ? (
    <Empty description="Không có dữ liệu phù hợp" />
  ) : (
    children
  );
}
export function AdminConfirm({
  title = 'Xác nhận thay đổi?',
  children,
  cancel,
  confirm,
  reason,
  changeReason,
}: {
  title?: string;
  children: ReactNode;
  cancel: () => void;
  confirm: () => void;
  reason?: string;
  changeReason?: (value: string) => void;
}) {
  return (
    <Modal
      open
      title={title}
      wrapProps={{ onKeyDown: keepFocusInModal }}
      onCancel={cancel}
      onOk={confirm}
      okText="Xác nhận"
      cancelText="Đóng"
    >
      {children}
      {changeReason && (
        <label>
          Lý do thay đổi
          <Input
            value={reason ?? ''}
            maxLength={500}
            onChange={(event) => changeReason(event.target.value)}
          />
        </label>
      )}
    </Modal>
  );
}
export function AdminIdentityScope({ children }: { children: ReactNode }) {
  const { session, permissionVersion } = useSession();
  return <div key={`${session?.userId}:${permissionVersion}:${session?.roles.join(',')}`}>{children}</div>;
}
