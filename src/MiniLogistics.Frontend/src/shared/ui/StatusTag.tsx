import type { ReactNode } from 'react';
import { Tag } from 'antd';
export function StatusTag({ tone = 'default', children }: {
  tone?: 'default' | 'success' | 'warning' | 'error' | 'processing'; children: ReactNode;
}) { return <Tag color={tone}>{children}</Tag>; }
