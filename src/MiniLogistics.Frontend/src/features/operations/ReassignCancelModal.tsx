import { Alert, Form, Input, Modal, Typography } from 'antd';
import { keepFocusInOperationsModal } from './modalFocus';
export function ReassignCancelModal({ title, trackingCode, pending, error, onConfirm, onCancel }:
  { title: string; trackingCode: string; pending: boolean; error?: string; onConfirm: (reason: string) => void; onCancel: () => void }) {
  const [form] = Form.useForm<{ reason: string }>();
  return <Modal open title={title} wrapProps={{ onKeyDown: keepFocusInOperationsModal }} onCancel={onCancel} closable={!pending} keyboard={!pending} maskClosable={!pending}
    confirmLoading={pending} cancelButtonProps={{ disabled: pending }} okText="Xác nhận" cancelText="Đóng" onOk={() => form.submit()}>
    <Typography.Paragraph>{trackingCode}</Typography.Paragraph>{error && <Alert type="error" title={error} role="alert" />}
    <Form form={form} layout="vertical" onFinish={({ reason }) => onConfirm(reason)}>
      <Form.Item name="reason" label="Lý do hủy phân công" rules={[{ required: true, whitespace: true, message: 'Nhập lý do' }, { max: 500 }]}><Input.TextArea maxLength={500} /></Form.Item>
    </Form>
  </Modal>;
}
