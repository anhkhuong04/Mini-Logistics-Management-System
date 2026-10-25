import { useEffect, useState } from 'react';
import { Alert, Button, Form, Input, Select } from 'antd';
import { Link } from 'react-router';
import { PageHeader } from '../../shared/ui/PageHeader';
import { ApiError } from '../../shared/api/httpClient';
import { useProvinces } from '../../shared/api/referenceData';
import { useSession } from '../../shared/auth/SessionContext';
import { register, type Registration } from './api';
import { postLoginPath } from './redirects';
import { useAuthCopy } from '../../shared/i18n/authCopy';
export function RegisterShopPage() {
  const copy = useAuthCopy();
  const [form] = Form.useForm<Registration>();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const { signedIn } = useSession();
  const province = Form.useWatch('province', form);
  const reference = useProvinces();
  useEffect(() => () => { form.setFieldValue('password', ''); }, [form]);
  async function submit(values: Registration) {
    if (pending) return;
    setPending(true); setError('');
    try {
      const session = await register(values); await signedIn(session);
      window.location.assign(postLoginPath(session.roles ?? []));
    } catch (failure) {
      form.setFieldValue('password', '');
      setError(copy.registerFailure);
      if (failure instanceof ApiError && failure.errors) {
        const names: Array<keyof Registration> = ['fullName', 'email', 'password', 'shopName', 'phoneNumber', 'addressLine', 'province', 'ward', 'country'];
        form.setFields(names.filter(name => failure.errors?.[name]).map(name => ({ name, errors: failure.errors![name] })));
      }
    } finally { setPending(false); }
  }
  const fields: Array<[keyof Registration, string, number]> = [
    ['fullName', copy.fullName, 150], ['email', copy.email, 256], ['shopName', copy.shopName, 200],
    ['phoneNumber', copy.phoneNumber, 100], ['addressLine', copy.addressLine, 300],
  ];
  const wards = reference.data?.provinces?.find(item => item.name === province)?.wards ?? [];
  return <><PageHeader title={copy.register} /><section className="auth-panel">
    {error && <Alert type="error" role="alert" title={error} />}
    {reference.isError && <Alert type="error" title={copy.referenceFailure} action={<Button onClick={() => void reference.refetch()}>{copy.retry}</Button>} />}
    <Form form={form} layout="vertical" disabled={pending} onFinish={submit} initialValues={{ country: 'Vietnam' }}>
      {fields.map(([name, label, max]) => <Form.Item key={name} name={name} label={label} rules={[{ required: true }, { max }, ...(name === 'email' ? [{ type: 'email' as const }] : [])]}>
        <Input autoComplete={name === 'email' ? 'username' : name === 'fullName' ? 'name' : name === 'phoneNumber' ? 'tel' : 'off'} />
      </Form.Item>)}
      <Form.Item name="password" label={copy.password} rules={[{ required: true }, { min: 8, max: 100 },
        { pattern: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$/, message: copy.passwordRule }]}>
        <Input.Password autoComplete="new-password" /></Form.Item>
      <Form.Item name="province" label={copy.province} rules={[{ required: true }]}>
        <Select showSearch disabled={pending || !reference.data} options={reference.data?.provinces?.map(item => ({ value: item.name, label: item.name }))}
          onChange={() => form.setFieldValue('ward', undefined)} /></Form.Item>
      <Form.Item name="ward" label={copy.ward} rules={[{ required: true }]}>
        <Select showSearch disabled={pending || !province} options={wards.map(item => ({ value: item.name, label: item.name }))} /></Form.Item>
      <Form.Item name="country" label={copy.country} rules={[{ required: true }, { max: 100 }]}><Input /></Form.Item>
      <Button type="primary" htmlType="submit" loading={pending} disabled={!reference.data}>{copy.register}</Button>
    </Form><p><Link to="/login">{copy.login}</Link></p>
  </section></>;
}
