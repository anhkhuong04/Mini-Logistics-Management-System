import { useEffect, useMemo, useRef } from 'react';
import { Alert, Button, Card, Descriptions, Form, Input, InputNumber, Select, Space, Spin } from 'antd';
import { SaveOutlined, SendOutlined } from '@ant-design/icons';
import { useProvinces } from '../../shared/api/referenceData';
import { useShipmentFormCopy } from '../../shared/i18n/shipmentFormCopy';
import { useI18n } from '../../shared/i18n/I18nContext';
import { decimalString, formatMoney } from '../../shared/lib/money';
import { quoteInput, toShipmentWrite, type ShipmentWrite } from './shipmentFormMapping';
import { useShippingQuote } from './quote/useShippingQuote';

export type ShipmentFormMode = 'send' | 'draft';
export function ShipmentForm({
  initialValues,
  editingStatus,
  pending,
  blocked,
  fieldErrors,
  onSubmit,
}: {
  initialValues: ShipmentWrite;
  editingStatus?: string;
  pending: boolean;
  blocked: boolean;
  fieldErrors?: Record<string, string[]>;
  onSubmit(values: ShipmentWrite, mode: ShipmentFormMode): Promise<void>;
}) {
  const copy = useShipmentFormCopy();
  const { locale } = useI18n();
  const [form] = Form.useForm<ShipmentWrite>();
  const mode = useRef<ShipmentFormMode>('send');
  const reference = useProvinces();
  const values = Form.useWatch([], { form, preserve: true }) as ShipmentWrite | undefined;
  const body = useMemo(() => quoteInput(values), [values]);
  const quote = useShippingQuote(body);
  useEffect(() => {
    if (fieldErrors)
      form.setFields(
        Object.entries(fieldErrors).map(([name, errors]) => ({
          name: name.split('.') as Parameters<typeof form.setFields>[0][number]['name'],
          errors,
        })),
      );
  }, [fieldErrors, form]);
  const required = [{ required: true, message: copy.required }];
  const text = (name: keyof ShipmentWrite, label: string, maximum: number, phone = false) => (
    <Form.Item
      name={name}
      label={label}
      rules={[
        ...required,
        { max: maximum },
        ...(phone ? [{ pattern: /^\+?[\d ]{9,20}$/, message: copy.required }] : []),
      ]}
    >
      <Input maxLength={maximum} autoComplete="off" />
    </Form.Item>
  );
  const address = (name: 'pickupAddress' | 'deliveryAddress', label: string) => {
    const province = values?.[name]?.province ?? initialValues[name]?.province;
    const wards = reference.data?.provinces.find((item) => item.name === province)?.wards ?? [];
    return (
      <fieldset className="address-fields">
        <legend>{label}</legend>
        <div className="filter-grid">
          <Form.Item name={[name, 'street']} label={copy.street} rules={[...required, { max: 200 }]}>
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name={[name, 'province']} label={copy.province} rules={required}>
            <Select
              showSearch
              disabled={pending || blocked || !reference.data}
              options={reference.data?.provinces.map((item) => ({ value: item.name, label: item.name }))}
              onChange={() => form.setFieldValue([name, 'ward'], undefined)}
            />
          </Form.Item>
          <Form.Item name={[name, 'ward']} label={copy.ward} rules={required}>
            <Select
              showSearch
              disabled={pending || blocked || !province}
              options={wards.map((item) => ({ value: item.name, label: item.name }))}
            />
          </Form.Item>
          <Form.Item name={[name, 'country']} label={copy.country} rules={[...required, { max: 100 }]}>
            <Input maxLength={100} />
          </Form.Item>
        </div>
      </fieldset>
    );
  };
  const numeric: Array<[keyof ShipmentWrite, string, boolean]> = [
    ['weightKg', copy.weight, true],
    ['lengthCm', copy.length, true],
    ['widthCm', copy.width, true],
    ['heightCm', copy.height, true],
    ['goodsValueAmount', copy.goods, false],
    ['codAmount', copy.cod, false],
  ];
  const preview = quote.data;
  const money = (value: string) => formatMoney(decimalString(value), preview!.currency, locale);
  return (
    <>
      {reference.isError && (
        <Alert
          role="alert"
          type="error"
          title={copy.referenceError}
          action={<Button onClick={() => void reference.refetch()}>{copy.retry}</Button>}
        />
      )}
      <Form
        form={form}
        name="shipment"
        layout="vertical"
        initialValues={initialValues}
        disabled={pending || blocked}
        requiredMark={(label, { required: isRequired }) => (
          <>
            {isRequired && (
              <span aria-hidden="true" className="required-mark">
                *
              </span>
            )}
            {label}
          </>
        )}
        onFinish={(input) => {
          void onSubmit(toShipmentWrite(input), mode.current);
        }}
        onFinishFailed={({ errorFields }) => {
          if (errorFields[0]) form.scrollToField(errorFields[0].name, { focus: true });
        }}
      >
        <div className="shipment-form-grid">
          <div>
            <Card title={copy.sender}>
              <div className="filter-grid">
                {text('senderName', copy.senderName, 100)}
                {text('senderPhone', copy.senderPhone, 20, true)}
              </div>
              {address('pickupAddress', copy.pickup)}
            </Card>
            <Card title={copy.receiver}>
              <div className="filter-grid">
                {text('receiverName', copy.receiverName, 100)}
                {text('receiverPhone', copy.receiverPhone, 20, true)}
              </div>
              {address('deliveryAddress', copy.delivery)}
            </Card>
            <Card title={copy.parcel}>
              <div className="filter-grid">
                {numeric.map(([name, label, positive]) => (
                  <Form.Item
                    key={name}
                    name={name}
                    label={label}
                    rules={[
                      ...required,
                      {
                        validator: (_, value: unknown) =>
                          typeof value === 'string' &&
                          /^\d+(?:\.\d+)?$/.test(value) &&
                          (!positive || /[1-9]/.test(value))
                            ? Promise.resolve()
                            : Promise.reject(new Error(positive ? copy.positive : copy.decimal)),
                      },
                    ]}
                  >
                    <InputNumber<string>
                      stringMode
                      controls={false}
                      min={positive ? '0.001' : '0'}
                      style={{ width: '100%' }}
                    />
                  </Form.Item>
                ))}
                {text('currency', copy.currency, 3)}
              </div>
              <Form.Item name="note" label={copy.note} rules={[{ max: 500 }]}>
                <Input.TextArea maxLength={500} rows={3} />
              </Form.Item>
            </Card>
          </div>
          <aside aria-label={copy.preview}>
            <Card title={copy.preview}>
              {!body ? (
                <p>{copy.previewEmpty}</p>
              ) : quote.isPending ? (
                <Spin description={copy.checking} />
              ) : quote.error ? (
                <Alert
                  role="alert"
                  type="warning"
                  title={copy.previewError}
                  action={<Button onClick={() => void quote.refetch()}>{copy.retry}</Button>}
                />
              ) : (
                preview && (
                  <Descriptions
                    column={1}
                    items={[
                      { key: 'base', label: copy.base, children: money(preview.baseFeeAmount) },
                      { key: 'extra', label: copy.extra, children: money(preview.extraWeightFeeAmount) },
                      {
                        key: 'insurance',
                        label: copy.insurance,
                        children: money(preview.insuranceFeeAmount),
                      },
                      { key: 'return', label: copy.returnFee, children: money(preview.returnFeeAmount) },
                      {
                        key: 'volumetric',
                        label: copy.volumetric,
                        children: `${preview.volumetricWeightKg} kg`,
                      },
                      {
                        key: 'chargeable',
                        label: copy.chargeable,
                        children: `${preview.chargeableWeightKg} kg`,
                      },
                      {
                        key: 'total',
                        label: copy.total,
                        children: <strong>{money(preview.shippingFeeAmount)}</strong>,
                      },
                    ]}
                  />
                )
              )}
              <p>{copy.previewHint}</p>
            </Card>
          </aside>
        </div>
        <Space wrap className="shipment-form-actions">
          <Button
            type="primary"
            icon={<SendOutlined aria-hidden />}
            htmlType="submit"
            loading={pending}
            disabled={blocked || pending || !reference.data}
            onClick={() => {
              mode.current = 'send';
            }}
          >
            {editingStatus === 'Draft' ? copy.submit : editingStatus ? copy.save : copy.create}
          </Button>
          {(!editingStatus || editingStatus === 'Draft') && (
            <Button
              icon={<SaveOutlined aria-hidden />}
              disabled={blocked || pending || !reference.data}
              onClick={() => {
                mode.current = 'draft';
                form.submit();
              }}
            >
              {copy.draft}
            </Button>
          )}
        </Space>
      </Form>
    </>
  );
}
