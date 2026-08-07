import crypto from "node:crypto";

const baseUrl = process.env.MINILOGISTICS_BASE_URL;
const apiKey = process.env.MINILOGISTICS_API_KEY;
const webhookSecrets = new Map([
  ["1", process.env.MINILOGISTICS_WEBHOOK_SECRET_V1],
  ["2", process.env.MINILOGISTICS_WEBHOOK_SECRET_V2]
]);

export async function getShipment(trackingCode) {
  const response = await fetch(
    `${baseUrl}/api/v1/partner/shipments/${encodeURIComponent(trackingCode)}`,
    {
      headers: { authorization: `Bearer ${apiKey}`, accept: "application/json" },
      signal: AbortSignal.timeout(5000)
    }
  );

  if (response.status === 429 || response.status === 503) {
    const retryAfter = Number(response.headers.get("retry-after") ?? "1");
    throw new Error(`RETRYABLE:${response.status}:${retryAfter}`);
  }

  const body = await response.json();
  if (!response.ok) {
    throw new Error(`${body.error?.code ?? "HTTP_ERROR"}:${body.error?.traceId ?? ""}`);
  }

  return body;
}

export async function createShipment(order) {
  const response = await fetch(`${baseUrl}/api/v1/partner/shipments`, {
    method: "POST",
    headers: {
      authorization: `Bearer ${apiKey}`,
      "content-type": "application/json",
      "idempotency-key": `order:${order.id}:shipment:v1`
    },
    body: JSON.stringify({
      externalOrderId: String(order.id),
      receiver: { name: order.name, phone: order.phone },
      deliveryAddress: order.address,
      parcel: order.parcel,
      goodsValueAmount: order.goodsValue,
      codAmount: order.codAmount,
      currency: "VND"
    }),
    signal: AbortSignal.timeout(8000)
  });
  const body = await response.json();
  if (response.status !== 200 && response.status !== 201) {
    throw new Error(`${body.error?.code ?? "HTTP_ERROR"}:${body.error?.traceId ?? ""}`);
  }
  return body;
}

export function verifyWebhook(headers, rawBody, now = Date.now()) {
  const timestamp = headers["x-minilogistics-timestamp"] ?? "";
  const signature = headers["x-minilogistics-signature"] ?? "";
  const secretVersion = headers["x-minilogistics-secret-version"] ?? "1";
  const secret = webhookSecrets.get(secretVersion);
  const timestampMs = Date.parse(timestamp);
  if (!secret || !Number.isFinite(timestampMs) || Math.abs(now - timestampMs) > 300_000) {
    return false;
  }

  const expected = `sha256=${crypto
    .createHmac("sha256", secret)
    .update(`${timestamp}.${rawBody}`, "utf8")
    .digest("hex")}`;
  const expectedBytes = Buffer.from(expected, "utf8");
  const suppliedBytes = Buffer.from(signature, "utf8");
  return expectedBytes.length === suppliedBytes.length
    && crypto.timingSafeEqual(expectedBytes, suppliedBytes);
}

// Receiver transaction:
// 1. Verify the raw body before JSON parsing.
// 2. INSERT eventId under a unique constraint; duplicates return 204.
// 3. Update only when changedAtUtc is newer than the stored shipment version.
// 4. Enqueue work and return 204 quickly. Reconcile ambiguous order through GET tracking.
