import { afterEach, expect, it, vi } from 'vitest';
import { exportShipmentsUrl, downloadFile } from './downloads';
afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});
it('exports current server filters and exact UTC ticks without page limits', () => {
  const url = new URL(
    exportShipmentsUrl('shop', {
      status: 'Delivered',
      fromUtc: '2026-10-01T00:00:00Z',
      toUtc: '2026-10-01T23:59:59.9999999Z',
      pageNumber: 2,
      pageSize: 25,
      sortBy: 'CreatedAt',
      sortDirection: 'Descending',
    }),
    'https://localhost',
  );
  expect(url.searchParams.get('toUtc')).toBe('2026-10-01T23:59:59.9999999Z');
  expect(url.searchParams.has('pageNumber')).toBe(false);
  expect(url.searchParams.get('shopId')).toBe('shop');
});
it('preserves filename/MIME and revokes the object URL after a download', async () => {
  vi.useFakeTimers();
  const create = vi.fn().mockReturnValue('blob:test');
  const revoke = vi.fn();
  vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: create, revokeObjectURL: revoke }));
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue(
      new Response('%PDF-test', {
        headers: {
          'Content-Type': 'application/pdf',
          'Content-Disposition': 'attachment; filename="label.pdf"',
        },
      }),
    ),
  );
  await downloadFile('/shop/files/test', 'application/pdf', new AbortController().signal);
  expect(click).toHaveBeenCalledOnce();
  expect(create).toHaveBeenCalledOnce();
  await vi.runAllTimersAsync();
  expect(revoke).toHaveBeenCalledWith('blob:test');
  click.mockRestore();
});
it.each([400, 401, 403, 429])('rejects legacy/error HTTP %s without downloading it', async (status) => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json('Safe legacy error', { status })));
  await expect(
    downloadFile('/shop/files/test', 'text/csv', new AbortController().signal),
  ).rejects.toMatchObject({ status });
});
it('rejects HTML login and incorrect MIME responses', async () => {
  vi.stubGlobal(
    'fetch',
    vi
      .fn()
      .mockResolvedValueOnce(new Response('<html>Login</html>', { headers: { 'Content-Type': 'text/html' } }))
      .mockResolvedValueOnce(Response.json({ error: 'not a file' })),
  );
  await expect(
    downloadFile('/shop/files/test', 'text/csv', new AbortController().signal),
  ).rejects.toMatchObject({ code: 'Browser.ProtocolError' });
  await expect(
    downloadFile('/shop/files/test', 'text/csv', new AbortController().signal),
  ).rejects.toMatchObject({ code: 'Browser.ProtocolError' });
});
