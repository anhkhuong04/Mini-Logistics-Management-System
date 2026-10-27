import { Alert, Form, Input, Modal, Select, Typography } from 'antd';
import type { OperationsShipper } from './api';
import { shipperOptions } from './models';
import { keepFocusInModal } from '../../shared/ui/modalFocus';
export type AssignmentValues = { shipperId: string; note?: string; reason?: string };
export function AssignShipperModal({
  title,
  trackingCode,
  province,
  shippers,
  currentShipperId,
  reassign = false,
  pending,
  error,
  onConfirm,
  onCancel,
}: {
  title: string;
  trackingCode: string;
  province: string;
  shippers: OperationsShipper[];
  currentShipperId?: string | null;
  reassign?: boolean;
  pending: boolean;
  error?: string;
  onConfirm: (values: AssignmentValues) => void;
  onCancel: () => void;
}) {
  const [form] = Form.useForm<AssignmentValues>();
  const selected = Form.useWatch('shipperId', form);
  const options = shipperOptions(
    province,
    shippers.filter((item) => item.userId !== currentShipperId),
  );
  const choice = options.find((item) => item.userId === selected);
  return (
    <Modal
      open
      title={title}
      wrapProps={{ onKeyDown: keepFocusInModal }}
      onCancel={onCancel}
      closable={!pending}
      maskClosable={!pending}
      keyboard={!pending}
      confirmLoading={pending}
      cancelButtonProps={{ disabled: pending }}
      okText="Xác nhận"
      cancelText="Đóng"
      onOk={() => form.submit()}
    >
      <Typography.Paragraph>
        {trackingCode} — Lấy hàng tại {province}
      </Typography.Paragraph>
      {error && <Alert type="error" title={error} role="alert" />}
      <Form
        name={reassign ? 'operations-reassign' : 'operations-assign'}
        form={form}
        layout="vertical"
        onFinish={onConfirm}
      >
        <Form.Item
          name="shipperId"
          label="Chọn shipper"
          rules={[{ required: true, message: 'Chọn shipper để tiếp tục' }]}
        >
          <Select
            showSearch
            optionFilterProp="label"
            options={options.map((item) => ({
              value: item.userId,
              disabled: item.atCapacity,
              label: `${item.fullName} (${item.activeShipmentCount}/${item.maxActiveShipments}) — ${item.matchesArea ? 'Đúng vùng' : 'Khác vùng'}${item.isAvailableForAssignment ? '' : ' — Tạm ngừng nhận đơn'}`,
            }))}
          />
        </Form.Item>
        {choice && (
          <Typography.Paragraph>
            Liên hệ: {choice.phoneNumber ?? 'Chưa cập nhật'}. Vùng làm việc:{' '}
            {choice.workingAreas
              .filter((area) => area.isActive)
              .map((area) => area.province)
              .join(', ') || 'Chưa cấu hình'}
            . Tải hiện tại: {choice.activeShipmentCount}/{choice.maxActiveShipments}.
          </Typography.Paragraph>
        )}
        {choice && !choice.matchesArea && (
          <Alert type="warning" title="Shipper ngoài vùng lấy hàng. Xác nhận phân công thủ công." showIcon />
        )}
        {choice && !choice.isAvailableForAssignment && (
          <Alert
            type="warning"
            title="Shipper đang tạm ngừng nhận đơn tự động; kiểm tra trước khi phân công thủ công."
            showIcon
          />
        )}
        <Form.Item
          name={reassign ? 'reason' : 'note'}
          label={reassign ? 'Lý do đổi shipper' : 'Ghi chú'}
          rules={[{ required: reassign, whitespace: true, message: 'Nhập lý do' }, { max: 500 }]}
        >
          <Input.TextArea maxLength={500} rows={3} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
