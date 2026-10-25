import { Alert } from 'antd';
export function SettingsFeedback({ message }: { message: string }) { return message ? <Alert role="status" title={message} /> : null; }
