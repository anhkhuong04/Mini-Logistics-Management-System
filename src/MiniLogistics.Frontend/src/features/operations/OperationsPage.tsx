import { useEffect, useRef, useState } from 'react';
import {
  Alert,
  Button,
  Form,
  Input,
  Modal,
  Result,
  Select,
  Space,
  Spin,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import { ReloadOutlined, TruckOutlined, UserAddOutlined } from '@ant-design/icons';
import { useSearchParams } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useSession } from '../../shared/auth/SessionContext';
import { ApiError } from '../../shared/api/httpClient';
import { PageHeader } from '../../shared/ui/PageHeader';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { operationsKey, useOperationsPage, useOperationsShippers } from './queries';
import {
  command,
  type AutoResult,
  type BulkResult,
  type CodCandidate,
  type OperationsShipper,
  type OperationsShipment,
  type PendingShipment,
} from './api';
import {
  nextStatuses,
  normalizeProvince,
  parseOperationsFilters,
  type OperationsFilters as Filters,
} from './models';
import { OperationsFilters } from './OperationsFilters';
import { AssignShipperModal, type AssignmentValues } from './AssignShipperModal';
import { ReassignCancelModal } from './ReassignCancelModal';
import { AssignmentTable } from './AssignmentTable';
import { BulkRetryPanel } from './BulkRetryPanel';
import { CodSettlementPanel } from './CodSettlementPanel';
import { keepFocusInModal } from '../../shared/ui/modalFocus';
import './operations.css';
type Target = {
  kind:
    | 'assign'
    | 'reassign'
    | 'cancel-assignment'
    | 'status'
    | 'collect'
    | 'settle'
    | 'auto-assign'
    | 'bulk';
  id: string;
  code: string;
  province?: string;
  currentShipperId?: string | null;
  status?: string;
  ids?: string[];
};
export function OperationsPage() {
  const { session, permissionVersion } = useSession();
  if (!session?.operationPermissions.includes('operations.shipment.view'))
    return <Result status="403" title="Không có quyền điều phối" />;
  return (
    <OperationsWorkspace
      key={`${session.userId}:${permissionVersion}:${session.operationPermissions.join(',')}`}
      userId={session.userId}
      permissions={session.operationPermissions}
      timeZone={session.serverTimeZoneId}
    />
  );
}
function OperationsWorkspace({
  userId,
  permissions,
  timeZone,
}: {
  userId: string;
  permissions: string[];
  timeZone: string;
}) {
  const [search, setSearch] = useSearchParams();
  const [filterError, setFilterError] = useState(false);
  let filters: Filters | undefined;
  try {
    filters = parseOperationsFilters(search);
  } catch {
    /* Render invalid URL without querying. */
  }
  const can = (permission: string) => permissions.includes(`operations.${permission}`);
  const f = filters ?? { view: 'pending' as const, pageNumber: 1, pageSize: 25, slaOnly: false };
  const valid = !!filters && (f.view !== 'cod' || can('cod.settle'));
  const pending = useOperationsPage<PendingShipment>(
    userId,
    'pending-pickups',
    f,
    valid && f.view === 'pending',
  );
  const shipments = useOperationsPage<OperationsShipment>(
    userId,
    'shipments',
    f,
    valid && f.view === 'operations',
  );
  const cod = useOperationsPage<CodCandidate>(
    userId,
    'cod/settlement-candidates',
    f,
    valid && f.view === 'cod',
  );
  const shippers = useOperationsShippers(userId);
  const cache = useQueryClient();
  const [target, setTarget] = useState<Target | null>(null);
  const [dialogError, setDialogError] = useState<string>();
  const [dialogSearch, setDialogSearch] = useState(search.toString());
  if (dialogSearch !== search.toString()) {
    setDialogSearch(search.toString());
    setTarget(null);
  }
  const [messages, setMessages] = useState<Record<string, string>>({});
  const [bulkResult, setBulkResult] = useState<BulkResult>();
  const [pendingIds, setPendingIds] = useState<Set<string>>(new Set());
  const [blocked, setBlocked] = useState<Set<string>>(new Set());
  const failures = useRef(new Map<string, number>());
  const failureVersion = useRef(0);
  const inFlight = useRef(new Set<string>());
  const alive = useRef(true);
  const requests = useRef(new Set<AbortController>());
  const refreshButton = useRef<HTMLButtonElement>(null);
  const dialogTrigger = useRef<HTMLElement | null>(null);
  useEffect(() => {
    alive.current = true;
    const controllers = requests.current;
    return () => {
      alive.current = false;
      controllers.forEach((controller) => controller.abort());
    };
  }, []);
  const active = f.view === 'operations' ? shipments : f.view === 'cod' ? cod : pending;
  const busy = (id: string) => pendingIds.has(id) || blocked.has(id) || active.isFetching;
  const show = (value: Target) => {
    dialogTrigger.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setDialogError(undefined);
    setTarget(value);
  };
  function replace(next: Filters) {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(next))
      if (value != null && value !== '') params.set(key, String(value));
    try {
      parseOperationsFilters(params);
      setFilterError(false);
      setTarget(null);
      setSearch(params);
    } catch {
      setFilterError(true);
    }
  }
  async function refresh() {
    const verifiedFailures = new Map(failures.current);
    const results = await Promise.all([active.refetch(), shippers.refetch()]);
    if (alive.current && results.every((result) => result.isSuccess)) {
      for (const [id, version] of verifiedFailures)
        if (failures.current.get(id) === version) failures.current.delete(id);
      setBlocked(new Set(failures.current.keys()));
    }
  }
  async function execute(body: unknown = {}) {
    if (!target) return;
    const current = target;
    const ids = current.ids ?? [current.id];
    if (ids.some((id) => inFlight.current.has(id) || blocked.has(id))) return;
    ids.forEach((id) => inFlight.current.add(id));
    setPendingIds(new Set(inFlight.current));
    setDialogError(undefined);
    const controller = new AbortController();
    requests.current.add(controller);
    const resource =
      current.kind === 'bulk'
        ? 'assignments/bulk-retry'
        : `shipments/${encodeURIComponent(current.id)}/${current.kind === 'collect' || current.kind === 'settle' ? 'cod/' : ''}${current.kind}`;
    try {
      const result = await command<BulkResult | AutoResult | undefined>(
        resource,
        current.kind === 'bulk' ? { shipmentIds: ids } : body,
        controller.signal,
      );
      if (!alive.current) return;
      if (current.kind === 'bulk') setBulkResult(result as BulkResult);
      const text = current.kind === 'auto-assign' ? (result as AutoResult).reason : 'Thao tác hoàn tất.';
      const rowMessages =
        current.kind === 'bulk'
          ? (result as BulkResult).items.map((item) => [item.shipmentId, `${item.result}: ${item.reason}`])
          : ids.map((id) => [id, text]);
      setMessages((previous) => ({ ...previous, ...Object.fromEntries(rowMessages) }));
      setTarget(null);
      await cache.invalidateQueries({ queryKey: operationsKey(userId) });
      refreshButton.current?.focus();
    } catch (error) {
      if (!alive.current) return;
      const uncertain =
        !(error instanceof ApiError) ||
        error.status === 0 ||
        error.status >= 500 ||
        error.code === 'Browser.ProtocolError';
      const conflict = error instanceof ApiError && error.status === 409;
      const text = uncertain
        ? 'Chưa xác định kết quả. Làm mới để kiểm tra trước khi thao tác lại.'
        : conflict
          ? 'Dữ liệu hoặc sức chứa đã thay đổi (409). Làm mới trước khi thử lại.'
          : error instanceof ApiError
            ? error.title
            : 'Không thể thực hiện thao tác.';
      setMessages((previous) => ({ ...previous, ...Object.fromEntries(ids.map((id) => [id, text])) }));
      if (uncertain || conflict) {
        for (const id of ids) failures.current.set(id, ++failureVersion.current);
        setBlocked(new Set(failures.current.keys()));
        setTarget(null);
        requestAnimationFrame(() => {
          if (alive.current) refreshButton.current?.focus();
        });
      } else setDialogError(text);
    } finally {
      requests.current.delete(controller);
      ids.forEach((id) => inFlight.current.delete(id));
      if (alive.current) setPendingIds(new Set(inFlight.current));
    }
  }
  const isDialogPending = !!target && (target.ids ?? [target.id]).some((id) => pendingIds.has(id));
  const close = () => {
    if (isDialogPending) return;
    setTarget(null);
    requestAnimationFrame(() => {
      if (!alive.current) return;
      if (dialogTrigger.current?.isConnected) dialogTrigger.current.focus();
      else refreshButton.current?.focus();
    });
  };
  const page = (pageNumber: number, pageSize: number) => replace({ ...f, pageNumber, pageSize });
  return (
    <>
      <PageHeader
        title="Điều phối giao nhận"
        description="Phân công shipper, theo dõi tiến độ và xử lý COD."
        actions={
          <Button
            ref={refreshButton}
            icon={<ReloadOutlined aria-hidden />}
            loading={active.isFetching}
            onClick={() => {
              void refresh();
            }}
          >
            Làm mới
          </Button>
        }
      />
      {!filters || filterError ? (
        <Alert
          role="alert"
          type="error"
          title="Bộ lọc không hợp lệ"
          action={
            <Button onClick={() => replace({ view: 'pending', pageNumber: 1, pageSize: 25, slaOnly: false })}>
              Xóa bộ lọc
            </Button>
          }
        />
      ) : (
        <>
          <Tabs
            activeKey={f.view}
            onChange={(view) =>
              replace({
                ...f,
                view: view as Filters['view'],
                pageNumber: 1,
                status: undefined,
                codStatus: undefined,
                shipperId: undefined,
              })
            }
            items={[
              { key: 'pending', label: 'Chờ phân công', icon: <UserAddOutlined aria-hidden /> },
              { key: 'operations', label: 'Theo dõi vận hành', icon: <TruckOutlined aria-hidden /> },
              ...(can('cod.settle') ? [{ key: 'cod', label: 'Đối soát COD' }] : []),
            ]}
          />
          {f.view === 'cod' && !can('cod.settle') ? (
            <Result status="403" title="Không có quyền đối soát COD" />
          ) : (
            <>
              {f.view !== 'cod' && (
                <OperationsFilters
                  key={search.toString()}
                  filters={f}
                  shippers={shippers.data ?? []}
                  timeZone={timeZone}
                  onApply={replace}
                />
              )}
              {shippers.isError && (
                <Alert role="alert" type="error" title="Không tải được shipper. Làm mới để thử lại." />
              )}
              {active.isError ? (
                <Alert role="alert" type="error" title="Không tải được danh sách. Làm mới để thử lại." />
              ) : active.isPending ? (
                <Spin aria-label="Đang tải workspace" />
              ) : (
                active.data && (
                  <>
                    <Typography.Paragraph role="status">
                      {f.view === 'cod'
                        ? `${active.data.totalCount} khoản COD đã thu; không áp dụng bộ lọc vận đơn.`
                        : `${active.data.totalCount} đơn theo bộ lọc hiện tại; ${shippers.data?.length ?? '…'} shipper đang hoạt động.`}
                    </Typography.Paragraph>
                    {blocked.size > 0 && (
                      <Alert
                        role="alert"
                        type="warning"
                        title="Có thao tác cần kiểm tra lại. Làm mới thành công để mở khóa."
                      />
                    )}
                    {f.view === 'pending' && pending.data && (
                      <PendingTable
                        key={`${search}:${pending.dataUpdatedAt}`}
                        data={pending.data}
                        busy={busy}
                        can={can}
                        messages={messages}
                        shippers={shippers.isError ? undefined : shippers.data}
                        bulkResult={bulkResult}
                        onPage={page}
                        onBulk={(ids) =>
                          show({ kind: 'bulk', id: 'bulk', code: `${ids.length} vận đơn`, ids })
                        }
                        onAction={(kind, item) =>
                          show({
                            kind,
                            id: item.shipmentId,
                            code: item.trackingCode,
                            province: item.pickupProvince,
                          })
                        }
                      />
                    )}
                    {f.view === 'operations' && shipments.data && (
                      <AssignmentTable
                        data={shipments.data}
                        busy={busy}
                        can={can}
                        messages={messages}
                        timeZone={timeZone}
                        onPage={page}
                        onAction={(kind, item) =>
                          show({
                            kind,
                            id: item.shipmentId,
                            code: item.trackingCode,
                            province: item.pickupAddress.province,
                            currentShipperId: item.activeShipperId,
                            status: item.status,
                          })
                        }
                      />
                    )}
                    {f.view === 'cod' && cod.data && (
                      <CodSettlementPanel
                        data={cod.data}
                        busy={busy}
                        messages={messages}
                        timeZone={timeZone}
                        onPage={page}
                        onSettle={(item) =>
                          show({ kind: 'settle', id: item.shipmentId, code: item.trackingCode })
                        }
                      />
                    )}
                  </>
                )
              )}
            </>
          )}
        </>
      )}
      {target && ['assign', 'reassign'].includes(target.kind) && (
        <AssignShipperModal
          key={`${target.kind}:${target.id}`}
          title={target.kind === 'assign' ? 'Phân công shipper' : 'Đổi shipper'}
          trackingCode={target.code}
          province={target.province ?? ''}
          currentShipperId={target.currentShipperId}
          shippers={shippers.isError ? [] : (shippers.data ?? [])}
          reassign={target.kind === 'reassign'}
          pending={isDialogPending}
          error={dialogError}
          onCancel={close}
          onConfirm={(values: AssignmentValues) => {
            void execute(values);
          }}
        />
      )}
      {target?.kind === 'cancel-assignment' && (
        <ReassignCancelModal
          trackingCode={target.code}
          title="Hủy phân công"
          pending={isDialogPending}
          error={dialogError}
          onCancel={close}
          onConfirm={(reason) => {
            void execute({ reason });
          }}
        />
      )}
      {target?.kind === 'status' && (
        <StatusModal
          key={target.id}
          target={target}
          pending={isDialogPending}
          error={dialogError}
          onCancel={close}
          onConfirm={(values) => {
            void execute(values);
          }}
        />
      )}
      {target && ['auto-assign', 'bulk', 'collect', 'settle'].includes(target.kind) && (
        <Modal
          open
          title="Xác nhận thao tác"
          wrapProps={{ onKeyDown: keepFocusInModal }}
          confirmLoading={isDialogPending}
          cancelButtonProps={{ disabled: isDialogPending }}
          keyboard={!isDialogPending}
          maskClosable={!isDialogPending}
          closable={!isDialogPending}
          okText="Xác nhận"
          cancelText="Đóng"
          onCancel={close}
          onOk={() => {
            void execute();
          }}
        >
          <Typography.Paragraph>
            {target.code} —{' '}
            {target.kind === 'collect'
              ? 'Xác nhận đã thu COD theo số tiền khai báo.'
              : target.kind === 'settle'
                ? 'Xác nhận đối soát COD đã thu.'
                : 'Retry tự động; kết quả phụ thuộc vùng làm việc và sức chứa hiện tại.'}
          </Typography.Paragraph>
          {dialogError && <Alert type="error" title={dialogError} role="alert" />}
        </Modal>
      )}
    </>
  );
}
function PendingTable({
  data,
  busy,
  can,
  messages,
  shippers,
  bulkResult,
  onPage,
  onAction,
  onBulk,
}: {
  data: NonNullable<ReturnType<typeof useOperationsPage<PendingShipment>>['data']>;
  busy: (id: string) => boolean;
  can: (permission: string) => boolean;
  messages: Record<string, string>;
  shippers?: OperationsShipper[];
  bulkResult?: BulkResult;
  onPage: (page: number, size: number) => void;
  onAction: (kind: 'assign' | 'auto-assign', item: PendingShipment) => void;
  onBulk: (ids: string[]) => void;
}) {
  const [selection, setSelection] = useState<React.Key[]>([]);
  const selected = data.items
    .filter((item) => selection.includes(item.shipmentId))
    .map((item) => item.shipmentId);
  return (
    <>
      {can('assignment.bulk_retry_auto') && (
        <BulkRetryPanel
          count={selected.length}
          disabled={selected.some(busy)}
          result={bulkResult}
          onRetry={() => onBulk(selected)}
          onClear={() => setSelection([])}
        />
      )}
      <Table
        rowKey="shipmentId"
        dataSource={data.items}
        scroll={{ x: 800 }}
        rowSelection={
          can('assignment.bulk_retry_auto')
            ? {
                selectedRowKeys: selection,
                onChange: setSelection,
                getCheckboxProps: (item) => ({
                  disabled: busy(item.shipmentId),
                  'aria-label': `Chọn ${item.trackingCode}`,
                }),
                preserveSelectedRowKeys: false,
              }
            : undefined
        }
        pagination={{
          current: data.pageNumber,
          pageSize: data.pageSize,
          total: data.totalCount,
          showSizeChanger: true,
          pageSizeOptions: [10, 25, 50, 100],
          onChange: onPage,
        }}
        columns={[
          {
            title: 'Vận đơn',
            dataIndex: 'trackingCode',
            render: (value, item) => (
              <>
                <Typography.Text strong>{value}</Typography.Text>
                {item.isSlaOverdue && <Tag color="warning">Quá SLA</Tag>}
                {messages[item.shipmentId] && <p role="status">{messages[item.shipmentId]}</p>}
              </>
            ),
          },
          { title: 'Người nhận', dataIndex: 'receiverName' },
          { title: 'Lấy hàng', dataIndex: 'pickupProvince' },
          { title: 'Giao hàng', dataIndex: 'deliveryProvince' },
          { title: 'COD', render: (_, item) => formatMoney(decimalString(item.codAmount), item.currency) },
          {
            title: 'Phí vận chuyển',
            render: (_, item) => formatMoney(decimalString(item.shippingFeeAmount), item.currency),
          },
          {
            title: 'Vùng lấy hàng',
            render: (_, item) => {
              if (!shippers) return 'Chưa tải được vùng shipper';
              const count = shippers.filter((shipper) =>
                shipper.workingAreas.some(
                  (area) =>
                    area.isActive &&
                    normalizeProvince(area.province) === normalizeProvince(item.pickupProvince),
                ),
              ).length;
              return shippers.length === 0
                ? 'Chưa có shipper active'
                : count === 0
                  ? 'Chưa có shipper nhận pickup tại vùng này'
                  : `${count} shipper phù hợp; có thể retry tự động`;
            },
          },
          {
            title: 'Thao tác',
            render: (_, item) => (
              <Space wrap>
                {can('assignment.assign') && (
                  <Button
                    disabled={busy(item.shipmentId)}
                    onClick={() => onAction('assign', item)}
                    aria-label={`Phân công ${item.trackingCode}`}
                  >
                    Phân công
                  </Button>
                )}
                {can('assignment.retry_auto') && (
                  <Button
                    disabled={busy(item.shipmentId)}
                    onClick={() => onAction('auto-assign', item)}
                    aria-label={`Retry ${item.trackingCode}`}
                  >
                    Retry tự động
                  </Button>
                )}
              </Space>
            ),
          },
        ]}
      />
    </>
  );
}
function StatusModal({
  target,
  pending,
  error,
  onConfirm,
  onCancel,
}: {
  target: Target;
  pending: boolean;
  error?: string;
  onConfirm: (value: { newStatus: string; note?: string; failureReasonCode?: string }) => void;
  onCancel: () => void;
}) {
  const [form] = Form.useForm<{ newStatus: string; note?: string; failureReasonCode?: string }>();
  const status = Form.useWatch('newStatus', form);
  return (
    <Modal
      open
      title={`Cập nhật ${target.code}`}
      wrapProps={{ onKeyDown: keepFocusInModal }}
      onOk={() => form.submit()}
      onCancel={onCancel}
      okText="Xác nhận"
      cancelText="Đóng"
      confirmLoading={pending}
      cancelButtonProps={{ disabled: pending }}
      closable={!pending}
      keyboard={!pending}
      maskClosable={!pending}
    >
      {error && <Alert role="alert" type="error" title={error} />}
      <Form form={form} layout="vertical" onFinish={onConfirm}>
        <Form.Item
          label="Trạng thái mới"
          name="newStatus"
          rules={[{ required: true, message: 'Chọn trạng thái' }]}
        >
          <Select options={nextStatuses(target.status ?? '').map((value) => ({ value, label: value }))} />
        </Form.Item>
        {status === 'DeliveryFailed' && (
          <Form.Item label="Mã lý do giao thất bại" name="failureReasonCode">
            <Select
              allowClear
              options={[
                'ReceiverUnavailable',
                'WrongAddress',
                'ReceiverRejected',
                'CannotContactReceiver',
                'DamagedParcel',
                'PaymentIssue',
                'WeatherOrAccessIssue',
                'Other',
              ].map((value) => ({ value, label: value }))}
            />
          </Form.Item>
        )}
        <Form.Item
          label="Ghi chú"
          name="note"
          rules={[
            { required: status === 'DeliveryFailed', whitespace: true, message: 'Nhập lý do giao thất bại' },
            { max: 500 },
          ]}
        >
          <Input.TextArea maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
