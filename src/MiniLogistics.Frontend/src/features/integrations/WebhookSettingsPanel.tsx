import { Alert, Button, Card, Form, Input } from 'antd';
import type { IntegrationClient } from './api';
export function WebhookSettingsPanel({
  client,
  busy,
  submit,
  test,
}: {
  client: IntegrationClient;
  busy: boolean;
  submit: (url: string, secret: string) => void;
  test: () => void;
}) {
  const [form] = Form.useForm<{ url: string; signingSecret: string }>();
  const permitted = client.scopes.includes('WebhookManage');
  return (
    <Card title="Webhook">
      <p>Endpoint hoạt động: {client.webhookEndpoint?.isActive ? 'Có' : 'Chưa có'}</p>
      {!permitted && <Alert type="info" title="Client cần quyền WebhookManage để cấu hình hoặc gửi test." />}
      <Form
        form={form}
        name="integration-webhook"
        layout="vertical"
        disabled={busy || !permitted}
        initialValues={{ url: client.webhookEndpoint?.url ?? '' }}
        requiredMark={(label, info) => (
          <>
            {info.required && <span aria-hidden="true">* </span>}
            {label}
          </>
        )}
        onFinish={(v) => {
          submit(v.url, v.signingSecret);
          form.setFieldValue('signingSecret', '');
        }}
      >
        <Form.Item
          name="url"
          label="URL webhook"
          rules={[
            { required: true },
            { max: 500 },
            { pattern: /^https:\/\//, message: 'Webhook phải dùng HTTPS' },
          ]}
        >
          <Input maxLength={500} placeholder="https://example.com/webhook" />
        </Form.Item>
        <Form.Item
          name="signingSecret"
          label="Signing secret mới"
          extra="Tối thiểu 32 byte UTF-8. Luôn nhập secret khi lưu; không hiển thị secret đã lưu."
          rules={[
            { required: true },
            { max: 200 },
            {
              validator: (_, value: string) =>
                !value || new TextEncoder().encode(value).length >= 32
                  ? Promise.resolve()
                  : Promise.reject(new Error('Signing secret cần ít nhất 32 byte UTF-8')),
            },
          ]}
        >
          <Input.Password autoComplete="new-password" maxLength={200} />
        </Form.Item>
        <Button type="primary" htmlType="submit" disabled={busy || !permitted}>
          Lưu và kích hoạt webhook
        </Button>
      </Form>
      <Button disabled={busy || !permitted || !client.webhookEndpoint?.isActive} onClick={test}>
        Xếp hàng test webhook
      </Button>
      <p>Test được xếp hàng để gửi; kết quả gửi được cập nhật trong lịch sử delivery.</p>
    </Card>
  );
}
