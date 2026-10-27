import { useState } from 'react';
import { Alert, Button, Checkbox, Form, Input } from 'antd';
import { Link, useSearchParams } from 'react-router';
import { PageHeader } from '../../shared/ui/PageHeader';
import { ApiError } from '../../shared/api/httpClient';
import { useSession } from '../../shared/auth/SessionContext';
import { login, type LoginRequest } from './api';
import { postLoginPath, safeReturnUrl } from './redirects';
import { useAuthCopy } from '../../shared/i18n/authCopy';
export function LoginPage() {
  const copy = useAuthCopy();
  const [form] = Form.useForm<LoginRequest>();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const { signedIn } = useSession();
  const [search] = useSearchParams();
  async function submit(values: LoginRequest) {
    if (pending) return;
    setPending(true);
    setError('');
    try {
      const session = await login(values);
      await signedIn(session);
      window.location.assign(safeReturnUrl(search.get('returnUrl')) ?? postLoginPath(session.roles ?? []));
    } catch (failure) {
      form.setFieldValue('password', '');
      setError(
        failure instanceof ApiError && failure.code === 'Auth.LockedOut'
          ? copy.locked
          : failure instanceof ApiError && failure.status === 401
            ? copy.credentials
            : failure instanceof ApiError && failure.status === 403
              ? copy.inactive
              : copy.loginFailure,
      );
    } finally {
      setPending(false);
    }
  }
  return (
    <>
      <PageHeader title={copy.login} />
      <section className="auth-panel">
        {error && <Alert type="error" role="alert" title={error} />}
        <Form
          form={form}
          layout="vertical"
          onFinish={submit}
          disabled={pending}
          initialValues={{ rememberMe: false }}
        >
          <Form.Item
            label={copy.email}
            name="email"
            rules={[{ required: true }, { type: 'email' }, { max: 256 }]}
          >
            <Input autoComplete="username" />
          </Form.Item>
          <Form.Item label={copy.password} name="password" rules={[{ required: true }, { max: 100 }]}>
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Form.Item name="rememberMe" valuePropName="checked">
            <Checkbox>{copy.remember}</Checkbox>
          </Form.Item>
          <Button type="primary" htmlType="submit" loading={pending}>
            {copy.login}
          </Button>
        </Form>
        <p>
          <Link to="/register-shop">{copy.register}</Link>
        </p>
      </section>
    </>
  );
}
