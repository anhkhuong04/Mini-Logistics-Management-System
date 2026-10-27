import { useLayoutEffect, useRef, type ReactNode } from 'react';
import { Typography } from 'antd';
import { useLocation } from 'react-router';
export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  const heading = useRef<HTMLHeadingElement>(null);
  const { key } = useLocation();
  useLayoutEffect(() => {
    document.title = title === 'Mini Logistics' ? title : `${title} · Mini Logistics`;
    heading.current?.focus({ preventScroll: true });
  }, [title, key]);
  return (
    <div className="page-header">
      <div>
        <Typography.Title ref={heading} level={1} tabIndex={-1}>
          {title}
        </Typography.Title>
        {description && <Typography.Paragraph type="secondary">{description}</Typography.Paragraph>}
      </div>
      {actions && <div className="page-actions">{actions}</div>}
    </div>
  );
}
