import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Card, Input, Space, Spin, Table, Upload } from 'antd';
import { CheckOutlined, DownloadOutlined, LoadingOutlined, UploadOutlined } from '@ant-design/icons';
import { Link } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useSession } from '../../shared/auth/SessionContext';
import { useImportCopy } from '../../shared/i18n/importCopy';
import { useShopContext } from '../shop-context/ShopContext';
import { useShipmentCommand } from '../shipments/useShipmentCommand';
import { isShipmentId } from '../shipments/queries';
import { FileDownloadButton } from '../shipments/FileDownloadButton';
import { importErrorsUrl, saveBlob } from '../shipments/downloads';
import { routePaths } from '../../app/router/routePaths';
import { confirmImport, importHeaders, previewErrorsCsv, previewImport, type ImportDraft, type ImportResult } from './api';
import { ImportPreviewTable, type EditableImportRow } from './ImportPreviewTable';
import { isImportTerminal, useImportBatch } from './queries';

export function ShipmentImportPanel() {
  const { session } = useSession(); const { context } = useShopContext(); const copy = useImportCopy();
  const shop = context?.shops.find(item => item.shopId === context.selectedShopId); const [open, setOpen] = useState(false); const [initialized, setInitialized] = useState(false);
  if (!session || !shop?.permissions.includes('ManageShipments')) return null;
  return <section className="import-launcher"><Button icon={<UploadOutlined aria-hidden />} onClick={() => { setInitialized(true); setOpen(value => !value); }}>{open ? copy.close : copy.open}</Button>
    <div hidden={!open}>{initialized && <ImportWorkspace key={`${session.userId}/${shop.shopId}/${shop.isActive}/${shop.permissions.join(',')}`} shopId={shop.shopId} active={shop.isActive} />}</div></section>;
}
function ImportWorkspace({ shopId, active }: { shopId: string; active: boolean }) {
  const copy = useImportCopy(); const command = useShipmentCommand(); const { session } = useSession(); const cache = useQueryClient();
  const [file, setFile] = useState<File | null>(null); const [rows, setRows] = useState<EditableImportRow[] | null>(null);
  const [previewing, setPreviewing] = useState(false); const [error, setError] = useState(''); const [submitted, setSubmitted] = useState<ImportResult | null>(null);
  const [batchId, setBatchId] = useState<string | null>(null); const [lookup, setLookup] = useState(''); const controller = useRef<AbortController | null>(null);
  const batch = useImportBatch(batchId); const result = batch.data ?? submitted;
  const createdRows = batch.data?.createdRows, batchStatus = batch.data?.status;
  useEffect(() => () => controller.current?.abort(), []);
  useEffect(() => { if (createdRows !== undefined) void cache.invalidateQueries({ queryKey: ['user', session?.userId, 'shop', shopId] }); }, [createdRows, batchStatus, cache, session?.userId, shopId]);
  async function preview() {
    if (!file || previewing || command.pending) return;
    const request = new AbortController(); controller.current = request; setPreviewing(true); setError(''); setRows(null);
    try { const value = await previewImport(shopId, file, request.signal); if (!request.signal.aborted) setRows(value.rows.map(row => {
      const draft = { ...row.draft }; for (const field of row.invalidNumericFields ?? []) if (numericFields.includes(field as typeof numericFields[number])) draft[field as typeof numericFields[number]] = '';
      return { ...row, draft };
    })); }
    catch { if (!request.signal.aborted) setError(copy.failure); }
    finally { if (!request.signal.aborted) setPreviewing(false); }
  }
  async function confirm() {
    if (!rows || submitted || batchId || !active || invalidNumbers) return;
    const outcome = await command.run(() => confirmImport(shopId, rows.map(row => row.draft)));
    if (outcome) { setSubmitted(outcome.value); setBatchId(outcome.value.batchId ?? null); setRows(null); }
  }
  function update(index: number, key: keyof ImportDraft, value: string) {
    setRows(previous => previous?.map((row, i) => i === index ? { ...row, edited: true, draft: { ...row.draft, [key]: value } } : row) ?? null);
  }
  const locked = command.pending || command.blocked || previewing || Boolean(result) || Boolean(batchId);
  const numericFields = ['weightKg', 'lengthCm', 'widthCm', 'heightCm', 'goodsValueAmount', 'codAmount'] as const;
  const invalidNumbers = rows?.some(row => numericFields.some(field => !/^-?\d+(?:\.\d+)?$/.test(row.draft[field]))) ?? false;
  return <Card title={copy.title} className="shipment-import-panel" extra={<Button icon={<DownloadOutlined aria-hidden />} onClick={() => saveBlob(new Blob(['\uFEFF' + importHeaders + '\r\n'], { type: 'text/csv;charset=utf-8' }), 'shipment-import-template.csv')}>{copy.template}</Button>}>
    <p>{copy.limit}</p><p>{copy.selectFile}</p>{!active && <Alert type="warning" title={copy.forbidden} />}
    {(error || command.failure) && <Alert type="error" role="alert" title={error || (command.unknown || command.conflict ? copy.unknown : copy.failure)} />}
    <Space wrap><Upload accept=".csv,text/csv" maxCount={1} showUploadList={false} disabled={locked || !active} beforeUpload={value => {
      if (!value.name.toLowerCase().endsWith('.csv') || value.size > 2_000_000 || value.size === 0) { setError(copy.oversized); return Upload.LIST_IGNORE; }
      setFile(value); setRows(null); setError(''); return false;
    }}><Button icon={<UploadOutlined aria-hidden />} disabled={locked || !active}>{copy.choose}</Button></Upload><span>{file?.name}</span>
      <Button onClick={() => { void preview(); }} loading={previewing} disabled={!file || locked || !active}>{copy.preview}</Button>
      <Button disabled={command.pending || command.blocked || previewing} onClick={() => { setFile(null); setRows(null); setSubmitted(null); setBatchId(null); setError(''); command.reset(); }}>{copy.reset}</Button>
    </Space>
    {rows && <><p role="status">{copy.total}: {rows.length} · {copy.valid}: {rows.filter(row => row.isValid && !row.edited).length} · {copy.invalid}: {rows.filter(row => !row.isValid && !row.edited).length}</p>
      <p>{copy.hint}</p><ImportPreviewTable rows={rows} onChange={update} disabled={command.pending || command.blocked || !active} />
      {rows.some(row => row.errors.length > 0) && <Button icon={<DownloadOutlined aria-hidden />} onClick={() => saveBlob(new Blob([previewErrorsCsv(rows)], { type: 'text/csv;charset=utf-8' }), 'shipment-import-errors.csv')}>{copy.previewErrors}</Button>}
      {invalidNumbers && <Alert type="warning" title={copy.numericRequired} />}
      <Button type="primary" icon={<CheckOutlined aria-hidden />} loading={command.pending ? { icon: <LoadingOutlined aria-hidden /> } : false} aria-busy={command.pending} disabled={command.pending || command.blocked || !active || rows.length === 0 || invalidNumbers} onClick={() => { void confirm(); }}>{copy.confirm}</Button></>}
    <Space wrap className="batch-lookup"><Input aria-label={copy.batchId} placeholder={copy.batchId} value={lookup} disabled={command.pending || previewing} onChange={event => setLookup(event.target.value)} />
      <Button disabled={command.pending || previewing || !isShipmentId(lookup)} onClick={() => { setSubmitted(null); setRows(null); setBatchId(lookup); }}>{copy.lookup}</Button></Space><p>{copy.lookupHint}</p>
    {batchId && <p>{copy.batchId}: <code>{batchId}</code></p>}
    {batch.error && <Alert type="error" role="alert" title={copy.pollError} action={<Button onClick={batch.retry}>{copy.retry}</Button>} />}
    {batchId && !result && !batch.error && <Spin />}
    {result && <section aria-label={copy.results}><p role="status">{isImportTerminal(result.status) || !result.batchId ? copy.done : copy.pending} · {copy.total}: {result.totalRows} · {copy.created}: {result.createdRows} · {copy.failed}: {result.failedRows}</p>
      {result.batchId && Number(result.failedRows) > 0 && <FileDownloadButton path={importErrorsUrl(shopId, result.batchId)} mime="text/csv" label={copy.downloadErrors} />}
      <div className="data-table-region" role="region" aria-label={copy.results} tabIndex={0}><Table rowKey={row => String(row.rowNumber)} pagination={{ pageSize: 10, showSizeChanger: false }} dataSource={result.rows} scroll={{ x: 700 }} columns={[
        { title: copy.row, dataIndex: 'rowNumber', key: 'row' }, { title: copy.code, dataIndex: 'clientOrderCode', key: 'code' },
        { title: copy.tracking, key: 'tracking', render: (_, row) => row.isCreated && row.shipmentId ? <Link to={`${routePaths.shipmentDetail(row.shipmentId)}?shopId=${shopId}`}>{row.trackingCode}</Link> : '—' },
        { title: copy.errors, key: 'errors', render: (_, row) => row.errors.join('; ') || (row.isCreated ? copy.created : row.status) },
      ]} /></div></section>}
  </Card>;
}
