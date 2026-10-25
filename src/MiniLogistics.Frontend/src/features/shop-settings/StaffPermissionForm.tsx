import { permissionNames, presets } from './models';
import { Checkbox, Form, Select, Switch } from 'antd';
import type { StaffAccess } from './api';
export function StaffPermissionForm({ editing }: { editing: boolean }) {
  const form = Form.useFormInstance<StaffAccess>(); const role = Form.useWatch('role', form);
  return <><Form.Item name="role" label="Vai trò" rules={[{ required: true }]}><Select options={Object.keys(presets).map(value => ({ value, label: value }))} onChange={value => form.setFieldValue('permissions', [...presets[value]])} /></Form.Item>
    <Form.Item name="permissions" label="Quyền Shop" rules={[{ validator: (_, values: string[] | undefined) => values?.includes('ViewShipments') ? Promise.resolve() : Promise.reject(new Error('Phải có ViewShipments')) }]}>
      <Checkbox.Group options={permissionNames.map(value => ({ value, label: value, disabled: role !== 'Custom' || value === 'ViewShipments' }))} />
    </Form.Item>{editing && <Form.Item name="isActive" label="Nhân viên hoạt động" valuePropName="checked"><Switch /></Form.Item>}</>;
}
