import { Input, InputNumber, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useImportCopy } from '../../shared/i18n/importCopy';
import { useShipmentFormCopy } from '../../shared/i18n/shipmentFormCopy';
import { useI18n } from '../../shared/i18n/I18nContext';
import { decimalString, formatMoney } from '../../shared/lib/money';
import type { ImportDraft, ImportPreview } from './api';
export type EditableImportRow = ImportPreview['rows'][number] & { edited?: boolean };
export function ImportPreviewTable({
  rows,
  disabled,
  onChange,
}: {
  rows: EditableImportRow[];
  disabled: boolean;
  onChange(index: number, field: keyof ImportDraft, value: string): void;
}) {
  const copy = useImportCopy();
  const form = useShipmentFormCopy();
  const { locale } = useI18n();
  const fields: Array<[keyof ImportDraft, string, boolean?]> = [
    ['clientOrderCode', copy.code],
    ['receiverName', form.receiverName],
    ['receiverPhone', form.receiverPhone],
    ['deliveryStreet', form.street],
    ['deliveryProvince', form.province],
    ['deliveryWard', form.ward],
    ['deliveryCountry', form.country],
    ['weightKg', form.weight, true],
    ['lengthCm', form.length, true],
    ['widthCm', form.width, true],
    ['heightCm', form.height, true],
    ['goodsValueAmount', form.goods, true],
    ['codAmount', form.cod, true],
    ['note', form.note],
  ];
  const columns: ColumnsType<EditableImportRow> = [
    { title: copy.row, key: 'row', width: 70, render: (_, row) => row.draft.rowNumber },
    ...fields.map(([key, label, numeric]) => ({
      title: label,
      key,
      width: 180,
      render: (_: unknown, row: EditableImportRow) => {
        const index = rows.indexOf(row);
        const name = `${copy.row} ${row.draft.rowNumber} · ${label}`;
        return numeric ? (
          <InputNumber<string>
            stringMode
            controls={false}
            aria-label={name}
            disabled={disabled}
            value={String(row.draft[key] ?? '')}
            onChange={(value) => onChange(index, key, value ?? '')}
          />
        ) : (
          <Input
            aria-label={name}
            disabled={disabled}
            value={String(row.draft[key] ?? '')}
            onChange={(event) => onChange(index, key, event.target.value)}
          />
        );
      },
    })),
    {
      title: form.preview,
      key: 'fee',
      width: 160,
      render: (_, row) =>
        row.edited
          ? '—'
          : row.shippingFeeAmount === null
            ? '—'
            : formatMoney(decimalString(row.shippingFeeAmount), row.currency, locale),
    },
    {
      title: copy.errors,
      key: 'errors',
      width: 250,
      render: (_, row) =>
        row.edited ? (
          <Tag>{copy.edited}</Tag>
        ) : (
          <>
            <Tag color={row.isValid ? 'success' : 'error'}>{row.isValid ? copy.valid : copy.invalid}</Tag>
            <ul>
              {row.errors.map((error, index) => (
                <li key={index}>{error}</li>
              ))}
            </ul>
          </>
        ),
    },
  ];
  return (
    <div className="data-table-region" role="region" aria-label={copy.preview} tabIndex={0}>
      <Table
        rowKey={(row) => String(row.draft.rowNumber)}
        columns={columns}
        dataSource={rows}
        scroll={{ x: 3100 }}
        pagination={{ pageSize: 10, showSizeChanger: false }}
      />
    </div>
  );
}
