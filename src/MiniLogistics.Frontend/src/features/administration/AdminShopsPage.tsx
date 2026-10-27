import { useState } from 'react';
import { Button, Table } from 'antd';
import { useBrowserCommand } from '../../shared/api/useBrowserCommand';
import { AdminShell, AdminReadState, AdminConfirm } from './AdminShell';
import { useAdminRead } from './queries';
import { writeAdmin, type Schema } from './api';
export function AdminShopsPage() {
  const query = useAdminRead<Schema['GetAdminShopResponse'][]>('shops', false);
  const command = useBrowserCommand();
  const [reason, setReason] = useState('');
  const [target, setTarget] = useState<Schema['GetAdminShopResponse'] | null>(null);
  async function refresh() {
    const generation = command.snapshot();
    const result = await query.refetch();
    if (result.isSuccess) command.verified(generation);
  }
  async function save() {
    if (!target) return;
    const row = target;
    setTarget(null);
    const result = await command.run((signal) =>
      writeAdmin(`shops/${row.shopId}/active`, 'PUT', { isActive: !row.isActive, reason }, signal),
    );
    if (result) await query.refetch();
  }
  return (
    <AdminShell
      title="Quản trị Shop"
      refresh={() => void refresh()}
      busy={command.pending || query.isFetching}
      message={command.message}
    >
      <AdminReadState pending={query.isPending} error={query.isError}>
        <Table
          rowKey="shopId"
          dataSource={query.data}
          pagination={false}
          scroll={{ x: 1000 }}
          columns={[
            { title: 'Shop', dataIndex: 'name' },
            { title: 'Chủ Shop', dataIndex: 'ownerFullName' },
            { title: 'Email chủ Shop', dataIndex: 'ownerEmail' },
            { title: 'Điện thoại', dataIndex: 'phoneNumber' },
            {
              title: 'Địa chỉ',
              render: (_, row: Schema['GetAdminShopResponse']) =>
                [row.addressLine, row.ward, row.province].join(', '),
            },
            { title: 'Trạng thái', render: (_, row) => (row.isActive ? 'Active' : 'Inactive') },
            {
              title: 'Thao tác',
              render: (_, row) => (
                <Button
                  disabled={command.pending || command.blocked || query.isFetching}
                  onClick={() => {
                    setReason('');
                    setTarget(row);
                  }}
                >
                  Đổi hoạt động {row.name}
                </Button>
              ),
            },
          ]}
        />
      </AdminReadState>
      {target && (
        <AdminConfirm
          reason={reason}
          changeReason={setReason}
          title="Xác nhận hoạt động Shop?"
          cancel={() => setTarget(null)}
          confirm={() => void save()}
        >
          <p>{target.name}</p>
          <p>{target.isActive ? 'Tạm ngừng Shop' : 'Kích hoạt Shop'}</p>
        </AdminConfirm>
      )}
    </AdminShell>
  );
}
