import { useState } from 'react';
import { Button, Input } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router';
import { useI18n } from '../../shared/i18n/I18nContext';
export function TrackingSearch({
  initialCode = '',
  pending = false,
  onSearch,
}: {
  initialCode?: string;
  pending?: boolean;
  onSearch?: (code: string) => void;
}) {
  const [code, setCode] = useState(initialCode);
  const navigate = useNavigate();
  const { locale } = useI18n();
  const en = locale === 'en-US';
  return (
    <form
      className="public-tracking-search"
      onSubmit={(event) => {
        event.preventDefault();
        const normalized = code.trim();
        if (!normalized) return;
        if (onSearch) onSearch(normalized);
        else navigate(`/tracking?${new URLSearchParams({ code: normalized })}`);
      }}
    >
      <label htmlFor="public-tracking-code">{en ? 'Tracking code' : 'Mã vận đơn'}</label>
      <div className="public-search-controls">
        <Input
          id="public-tracking-code"
          value={code}
          onChange={(event) => setCode(event.target.value)}
          maxLength={30}
          required
          autoComplete="off"
          placeholder={en ? 'Enter your tracking code' : 'Nhập mã đơn hàng bạn cần tra cứu'}
        />
        <Button type="primary" htmlType="submit" icon={<SearchOutlined aria-hidden />} loading={pending}>
          {en ? 'Track shipment' : 'Tra cứu vận đơn'}
        </Button>
      </div>
    </form>
  );
}
