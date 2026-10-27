import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, requestJson, requestForm, requestDownload, resetCsrf } from './httpClient';
afterEach(() => {
  vi.unstubAllGlobals();
  resetCsrf();
});
describe('HTTP boundary', () => {
  it('sends cookies and handles 204 without parsing JSON', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetch);
    expect(await requestJson('/api/v1/app/auth/me')).toBeUndefined();
    expect(fetch.mock.calls[0][1].credentials).toBe('same-origin');
  });
  it('gets CSRF before mutation, leaves FormData content type to the browser, and forwards AbortSignal', async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(Response.json({ requestToken: 'csrf-test' }))
      .mockResolvedValueOnce(Response.json({ ok: true }));
    vi.stubGlobal('fetch', fetch);
    const controller = new AbortController();
    const body = new FormData();
    body.set('file', new Blob(['test']), 'test.txt');
    expect(await requestForm('/api/v1/app/upload', body, { signal: controller.signal })).toEqual({
      ok: true,
    });
    const options = fetch.mock.calls[1][1];
    expect(new Headers(options.headers).get('X-CSRF-TOKEN')).toBe('csrf-test');
    expect(new Headers(options.headers).has('Content-Type')).toBe(false);
    expect(options.signal).toBe(controller.signal);
  });
  it('preserves ProblemDetails and rejects HTML as a protocol failure', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(
          Response.json(
            { status: 403, code: 'Application.Forbidden', traceId: 'trace-test', title: 'Denied' },
            { status: 403 },
          ),
        )
        .mockResolvedValueOnce(
          new Response('<html>login</html>', { headers: { 'Content-Type': 'text/html' } }),
        ),
    );
    await expect(requestJson('/api/v1/app/test')).rejects.toMatchObject({
      status: 403,
      code: 'Application.Forbidden',
      traceId: 'trace-test',
    });
    await expect(requestJson('/api/v1/app/test')).rejects.toMatchObject({ code: 'Browser.ProtocolError' });
  });
  it('never retries a mutation after transport timeout or abort', async () => {
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(Response.json({ requestToken: 'csrf-test' }))
      .mockRejectedValueOnce(new DOMException('Aborted', 'AbortError'));
    vi.stubGlobal('fetch', fetch);
    await expect(requestJson('/api/v1/app/test', { method: 'POST', body: {} })).rejects.toMatchObject({
      name: 'AbortError',
    });
    expect(fetch).toHaveBeenCalledTimes(2); // CSRF GET + exactly one POST.
  });
  it('downloads binary and sanitizes the filename', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response('pdf', {
          headers: {
            'Content-Type': 'application/pdf',
            'Content-Disposition': 'attachment; filename="../label.pdf"',
          },
        }),
      ),
    );
    const download = await requestDownload('/shop/files/test');
    expect(download.fileName).toBe('label.pdf');
    expect(download.blob.size).toBe(3);
  });
  it('only permits same-origin relative API URLs', async () => {
    await expect(requestJson('https://outside.test')).rejects.toBeInstanceOf(ApiError);
  });
});
