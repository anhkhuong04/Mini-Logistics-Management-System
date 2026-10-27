import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Collapse, Form, Input, Select, Space, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  ClearOutlined,
  FilterOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
} from '@ant-design/icons';
import { Link, useSearchParams } from 'react-router';
import { useSession } from '../../shared/auth/SessionContext';
import { useI18n } from '../../shared/i18n/I18nContext';
import { useShipmentCopy } from '../../shared/i18n/shipmentCopy';
import { formatDateTime } from '../../shared/lib/dateTime';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { PageHeader } from '../../shared/ui/PageHeader';
import { AsyncContent } from '../../shared/ui/AsyncContent';
import { routePaths } from '../../shared/routing/routePaths';
import { useShopContext } from '../shop-context';
import { ShopContextSelector } from '../shop-context';
import { ShopReadBoundary } from '../shop-context';
import { ShipmentStatusTag } from '../../shared/ui/ShipmentStatusTag';
import { ReadError } from './ReadError';
import { useShipments } from './queries';
import {
  readShipmentFilters,
  shipmentSearch,
  shipmentStatuses,
  sortFields,
  type ShipmentFilters,
} from '../../shared/model/shipmentFilters';
import { utcDateRange } from '../../shared/lib/dateRange';
import type { ShipmentItem } from './api';
import { useShipmentFormCopy } from '../../shared/i18n/shipmentFormCopy';
import { ShipmentImportPanel } from '../shipment-import';
import { FileDownloadButton } from '../shop-context';
import { exportShipmentsUrl } from '../../shared/api/downloads';

type FilterForm = ShipmentFilters & { fromDate?: string; toDate?: string };
export function ShipmentListPage() {
  const copy = useShipmentCopy();
  const formCopy = useShipmentFormCopy();
  const { locale } = useI18n();
  const { session } = useSession();
  const { context } = useShopContext();
  const [search, setSearch] = useSearchParams();
  const selected = context?.shops.find((shop) => shop.shopId === context.selectedShopId);
  const fullPii = selected?.permissions.includes('ViewFullPii') ?? false,
    cod = selected?.permissions.includes('ViewCod') ?? false;
  const { filters, invalid } = useMemo(() => {
    try {
      return { filters: readShipmentFilters(search), invalid: false };
    } catch {
      return { filters: null, invalid: true };
    }
  }, [search]);
  const query = useShipments(filters);
  const [form] = Form.useForm<FilterForm>();
  const [formError, setFormError] = useState(false);
  useEffect(() => {
    form.resetFields();
    if (filters)
      form.setFieldsValue({
        ...filters,
        fromDate: filters.fromUtc?.slice(0, 10),
        toDate: filters.toUtc?.slice(0, 10),
      });
  }, [filters, form]);
  function replace(next: Partial<ShipmentFilters>) {
    const params = shipmentSearch(next);
    if (context) params.set('shopId', context.selectedShopId);
    setSearch(params);
  }
  function apply(values: FilterForm) {
    try {
      const dates = utcDateRange(values.fromDate ?? '', values.toDate ?? '');
      const next = { ...values, ...dates, pageNumber: 1, pageSize: filters?.pageSize ?? 25 };
      if (values.fromDate === filters?.fromUtc?.slice(0, 10)) next.fromUtc = filters?.fromUtc;
      if (values.toDate === filters?.toUtc?.slice(0, 10)) next.toUtc = filters?.toUtc;
      const { fromDate: _from, toDate: _to, ...request } = next;
      void _from;
      void _to;
      const parsed = readShipmentFilters(shipmentSearch(request));
      setFormError(false);
      replace(parsed);
    } catch {
      setFormError(true);
    }
  }
  const money = (value: string, currency: string) => formatMoney(decimalString(value), currency, locale);
  const columns: ColumnsType<ShipmentItem> = [
    {
      title: copy.tracking,
      dataIndex: 'trackingCode',
      key: 'tracking',
      render: (text, item) => (
        <Link
          state={{ listSearch: search.toString() }}
          to={`${routePaths.shipmentDetail(item.shipmentId)}?shopId=${encodeURIComponent(context!.selectedShopId)}`}
        >
          {text}
        </Link>
      ),
    },
    { title: copy.receiver, dataIndex: 'receiverName', key: 'receiver' },
    {
      title: copy.status,
      dataIndex: 'status',
      key: 'status',
      render: (status) => <ShipmentStatusTag status={status} />,
    },
    ...(cod
      ? [
          {
            title: 'COD',
            dataIndex: 'codAmount',
            key: 'cod',
            render: (value: string, item: ShipmentItem) => money(value, item.currency),
          },
        ]
      : []),
    {
      title: copy.fee,
      dataIndex: 'shippingFeeAmount',
      key: 'fee',
      render: (value, item) => money(value, item.currency),
    },
    {
      title: copy.created,
      dataIndex: 'createdAtUtc',
      key: 'created',
      render: (value) => formatDateTime(value, session!.serverTimeZoneId, locale),
    },
  ];
  const advanced = (
    <div className="filter-grid">
      {fullPii && (
        <>
          <Form.Item name="receiverName" label={copy.receiver}>
            <Input />
          </Form.Item>
          <Form.Item name="receiverPhone" label={copy.receiverPhone}>
            <Input />
          </Form.Item>
        </>
      )}
      <Form.Item name="fromDate" label={copy.from}>
        <Input type="date" />
      </Form.Item>
      <Form.Item name="toDate" label={copy.to}>
        <Input type="date" />
      </Form.Item>
      {cod && (
        <>
          <Form.Item name="minCodAmount" label={copy.minCod}>
            <Input inputMode="decimal" />
          </Form.Item>
          <Form.Item name="maxCodAmount" label={copy.maxCod}>
            <Input inputMode="decimal" />
          </Form.Item>
        </>
      )}
      <Form.Item name="sortBy" label={copy.sort}>
        <Select
          options={sortFields
            .filter((field) => (fullPii || field !== 'ReceiverName') && (cod || field !== 'CodAmount'))
            .map((value) => ({
              value,
              label: {
                CreatedAt: copy.created,
                TrackingCode: copy.tracking,
                ReceiverName: copy.receiver,
                CodAmount: 'COD',
                ShippingFee: copy.fee,
              }[value],
            }))}
        />
      </Form.Item>
      <Form.Item name="sortDirection" label={copy.direction}>
        <Select
          options={[
            { value: 'Descending', label: copy.descending },
            { value: 'Ascending', label: copy.ascending },
          ]}
        />
      </Form.Item>
    </div>
  );
  return (
    <>
      <PageHeader
        title={copy.list}
        actions={
          <Space wrap>
            <Button
              icon={<ReloadOutlined aria-hidden />}
              onClick={() => void query.refetch()}
              disabled={!filters || !context}
            >
              {copy.refresh}
            </Button>
            {selected?.isActive && selected.permissions.includes('ManageShipments') && (
              <Link
                className="ant-btn ant-btn-primary"
                to={`${routePaths.shipmentCreate}?shopId=${selected.shopId}`}
              >
                <PlusOutlined aria-hidden /> {formCopy.create}
              </Link>
            )}
          </Space>
        }
      />
      <ShopReadBoundary>
        <ShopContextSelector showInactive={false} />
        <Space wrap className="shipment-file-actions">
          {selected?.permissions.includes('ExportData') && filters && (
            <FileDownloadButton
              path={exportShipmentsUrl(selected.shopId, filters)}
              mime="text/csv"
              label={locale === 'en-US' ? 'Export shipments CSV' : 'Xuất CSV vận đơn'}
            />
          )}
        </Space>
        <ShipmentImportPanel />
        {(invalid || formError) && (
          <Alert
            type="error"
            role="alert"
            title={copy.invalid}
            action={
              <Button
                icon={<ClearOutlined aria-hidden />}
                onClick={() => {
                  setFormError(false);
                  replace({});
                }}
              >
                {copy.reset}
              </Button>
            }
          />
        )}
        <Form form={form} layout="vertical" onFinish={apply} className="filter-panel">
          <div className="filter-primary">
            <Form.Item name="trackingCode" label={copy.tracking}>
              <Input prefix={<SearchOutlined aria-hidden />} allowClear />
            </Form.Item>
            <Form.Item name="status" label={copy.status}>
              <Select
                allowClear
                placeholder={copy.all}
                options={shipmentStatuses.map((value) => ({ value, label: copy.labels[value] }))}
              />
            </Form.Item>
            <Space wrap>
              <Button type="primary" icon={<SearchOutlined aria-hidden />} htmlType="submit">
                {copy.search}
              </Button>
              <Button
                icon={<ClearOutlined aria-hidden />}
                onClick={() => {
                  setFormError(false);
                  replace({});
                }}
              >
                {copy.reset}
              </Button>
            </Space>
          </div>
          <Collapse
            items={[
              {
                key: 'advanced',
                label: (
                  <span>
                    <FilterOutlined aria-hidden /> {copy.advanced}
                  </span>
                ),
                children: advanced,
              },
            ]}
          />
        </Form>
        {query.error ? (
          <ReadError error={query.error} onRetry={() => void query.refetch()} />
        ) : (
          !invalid && (
            <AsyncContent loading={query.isPending}>
              <div role="region" aria-label={copy.list} tabIndex={0} className="data-table-region">
                <Table<ShipmentItem>
                  rowKey="shipmentId"
                  columns={columns}
                  loading={query.isFetching}
                  dataSource={query.data?.items ?? []}
                  scroll={{ x: 900 }}
                  locale={{ emptyText: copy.empty }}
                  pagination={{
                    current: filters?.pageNumber ?? 1,
                    pageSize: filters?.pageSize ?? 25,
                    total: query.data?.totalCount ?? 0,
                    showSizeChanger: true,
                    pageSizeOptions: [10, 25, 50, 100],
                    onChange: (pageNumber, pageSize) =>
                      replace({
                        ...filters!,
                        pageNumber: pageSize !== filters?.pageSize ? 1 : pageNumber,
                        pageSize,
                      }),
                  }}
                />
              </div>
            </AsyncContent>
          )
        )}
      </ShopReadBoundary>
    </>
  );
}
