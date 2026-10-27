import { Alert, Button, Form, Input, Select } from 'antd';
import { SaveOutlined, PlusOutlined } from '@ant-design/icons';
import { useProvinces } from '../../shared/api/referenceData';
import type { ProfileBody } from './api';
export function ShopProfileForm({
  initial,
  pending,
  blocked,
  create,
  onSave,
}: {
  initial?: ProfileBody;
  pending: boolean;
  blocked: boolean;
  create?: boolean;
  onSave: (value: ProfileBody) => void;
}) {
  const reference = useProvinces();
  const [form] = Form.useForm<ProfileBody>();
  const province = Form.useWatch('province', form) ?? initial?.province;
  const required = [{ required: true, whitespace: true, message: 'Vui lòng nhập trường này' }];
  return (
    <>
      {reference.isError && (
        <Alert
          role="alert"
          type="error"
          title="Không tải được địa chỉ"
          action={<Button onClick={() => void reference.refetch()}>Thử lại</Button>}
        />
      )}
      <Form
        form={form}
        layout="vertical"
        id={create ? 'additional-shop' : 'shop-profile'}
        name={create ? 'additional-shop' : 'shop-profile'}
        initialValues={initial ?? { country: 'Vietnam' }}
        onFinish={onSave}
        disabled={pending || blocked}
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
      >
        <Form.Item name="name" label="Tên Shop" rules={[...required, { max: 200 }]}>
          <Input maxLength={200} />
        </Form.Item>
        <Form.Item
          name="phoneNumber"
          label="Điện thoại"
          rules={[...required, { pattern: /^\+?[\d ]{9,20}$/, message: 'Điện thoại không hợp lệ' }]}
        >
          <Input maxLength={20} />
        </Form.Item>
        <Form.Item name="addressLine" label="Địa chỉ đường" rules={[...required, { max: 300 }]}>
          <Input maxLength={300} />
        </Form.Item>
        <Form.Item name="province" label="Tỉnh / Thành phố" rules={required}>
          <Select
            showSearch
            disabled={pending || blocked || !reference.data}
            options={reference.data?.provinces.map((item) => ({ value: item.name, label: item.name }))}
            onChange={() => form.setFieldValue('ward', undefined)}
          />
        </Form.Item>
        <Form.Item name="ward" label="Phường / Xã" rules={required}>
          <Select
            showSearch
            disabled={pending || blocked || !province || !reference.data}
            options={reference.data?.provinces
              .find((item) => item.name === province)
              ?.wards.map((item) => ({ value: item.name, label: item.name }))}
          />
        </Form.Item>
        <Form.Item name="country" label="Quốc gia" rules={[...required, { max: 100 }]}>
          <Input maxLength={100} />
        </Form.Item>
        <Button
          type="primary"
          htmlType="submit"
          icon={create ? <PlusOutlined aria-hidden /> : <SaveOutlined aria-hidden />}
          disabled={blocked || pending || !reference.data}
          loading={pending}
        >
          {create ? 'Tạo Shop mới' : 'Lưu hồ sơ'}
        </Button>
      </Form>
    </>
  );
}
