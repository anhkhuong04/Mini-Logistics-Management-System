import { useState } from 'react';
import { Button, Modal, Space, Typography } from 'antd';

export function App() {
  const [isHelpOpen, setHelpOpen] = useState(false);

  return (
    <main>
      <Typography.Title>Mini Logistics</Typography.Title>
      <Typography.Paragraph>Quản lý vận đơn và theo dõi giao nhận.</Typography.Paragraph>
      <Space>
        <Button type="primary" href="/login">Đăng nhập</Button>
        <Button onClick={() => setHelpOpen(true)}>Hỗ trợ</Button>
      </Space>
      <Modal title="Liên hệ hỗ trợ" open={isHelpOpen} onCancel={() => setHelpOpen(false)} footer={null}>
        <Typography.Paragraph>
          Tìm thông tin hỗ trợ tại <a href="/contact">Trang liên hệ</a>.
        </Typography.Paragraph>
      </Modal>
    </main>
  );
}
