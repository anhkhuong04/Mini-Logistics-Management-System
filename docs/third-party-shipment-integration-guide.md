# Huong Dan Tich Hop Trang Thai Van Don Cho Website Shop

Tai lieu nay danh cho backend/service cua shop. Contract day du nam tai
[`partner-api.md`](partner-api.md) va
[`partner-api.openapi.json`](partner-api.openapi.json).

## 1. Kien truc bat buoc

```text
Trinh duyet khach hang
  -> backend shop (xac thuc va kiem tra quyen don hang)
      -> database/cache shop
      -> MiniLogistics Partner API

MiniLogistics webhook
  -> HTTPS receiver cua shop
      -> unique event store + queue
      -> database/cache shop
```

Khong goi Partner API tu browser. API key chi nam trong secret manager/environment
cua backend shop. Frontend nen goi endpoint noi bo nhu
`GET /api/orders/{orderId}/shipping-status`.

## 2. Chuan bi sandbox

1. Dang nhap bang shop owner/staff co quyen `ManageIntegrations`.
2. Tao API client sandbox (`ml_test_`) voi scope toi thieu can dung.
3. Dat expiry va optional exact-IP allowlist theo static egress IP cua backend shop.
4. Luu key khi hien thi mot lan; khong dua vao repository/log.
5. Neu dung webhook, them `WebhookManage`, URL HTTPS public va secret ngau nhien it
   nhat 32 bytes.
6. Gui `webhook.test`, verify signature/timestamp/version va response `2xx`.
7. Test create -> track -> status webhook -> cancel trong sandbox.

Production cap key `ml_live_` rieng. Key hai moi truong khong dung cheo duoc.

## 3. Quyen tracking va mapping don

API client co `TrackShipment` track duoc moi van don non-draft thuoc cung shop, ke ca
van don tao tu Partner API, portal UI, CSV import hoac API client khac. Cac quy tac
quan trong:

- Cross-shop luon `404`.
- `externalOrderId` chi co gia tri neu reference thuoc current API client; nguoc lai
  la `null`.
- API client khac trong cung shop khong cancel duoc van don cua client tao ban dau.
- Webhook chi duoc gui cho API client/external reference da tao shipment. Shipment tu
  UI/CSV hoac client khac trong shop phai duoc dong bo bang polling tracking.

Shop nen luu:

| Cot shop | Nguon |
| --- | --- |
| `order_id` | ID noi bo cua shop |
| `external_order_id` | Gia tri gui khi create, neu co |
| `tracking_code` | Response create/portal export |
| `shipment_status` | Tracking/webhook |
| `cod_status` | Tracking response |
| `last_changed_at_utc` | Timeline/webhook |
| `last_synced_at_utc` | Thoi diem shop dong bo |

Khong proxy tracking code bat ky do customer gui. Backend phai lookup tracking code
tu order ma customer dang duoc phep xem.

## 4. Polling an toan

Vi du Node.js day du nam tai
[`examples/partner-api-node.mjs`](examples/partner-api-node.mjs).

```js
const response = await fetch(
  `${baseUrl}/api/v1/partner/shipments/${encodeURIComponent(order.trackingCode)}`,
  {
    headers: { authorization: `Bearer ${apiKey}`, accept: "application/json" },
    signal: AbortSignal.timeout(5000)
  }
);
```

Khi tra cho frontend, chi can `trackingCode`, `status`, `codStatus` neu phu hop, va
timeline public:

```js
res.json({
  trackingCode: status.trackingCode,
  status: status.status,
  timeline: status.timeline.map(item => ({
    status: item.status,
    messageCode: item.messageCode,
    message: item.message,
    locale: item.locale,
    changedAtUtc: item.changedAtUtc
  }))
});
```

Timeline da duoc tach khoi internal note. Dung `messageCode` cho logic; khong parse
`message`. Tan suat goi de xuat:

- Don dang chay: cache 30-60 giay.
- Co webhook: polling doi chieu 10-15 phut cho don non-terminal.
- Don terminal: luu ket qua va dung polling thuong xuyen.
- `429`: ton trong `Retry-After`, them jitter.
- `503`, `5xx`, timeout: exponential backoff co gioi han.

## 5. Webhook receiver

Phai verify tren raw body truoc khi parse JSON. Chon secret theo
`X-MiniLogistics-Secret-Version`; cho phep old/new version trong cua so rotate da
thoa thuan.

Vi du Express:

```js
import crypto from "node:crypto";
import express from "express";

app.post(
  "/webhooks/minilogistics",
  express.raw({ type: "application/json", limit: "64kb" }),
  async (req, res) => {
    const timestamp = req.get("x-minilogistics-timestamp") ?? "";
    const signature = req.get("x-minilogistics-signature") ?? "";
    const secretVersion = req.get("x-minilogistics-secret-version") ?? "1";
    const secret = await secrets.findActiveVersion(secretVersion);
    const signedAt = Date.parse(timestamp);
    if (!secret || !Number.isFinite(signedAt) || Math.abs(Date.now() - signedAt) > 300000) {
      return res.sendStatus(401);
    }

    const rawBody = req.body.toString("utf8");
    const expected = `sha256=${crypto
      .createHmac("sha256", secret)
      .update(`${timestamp}.${rawBody}`, "utf8")
      .digest("hex")}`;
    const expectedBytes = Buffer.from(expected, "utf8");
    const suppliedBytes = Buffer.from(signature, "utf8");
    if (expectedBytes.length !== suppliedBytes.length
        || !crypto.timingSafeEqual(expectedBytes, suppliedBytes)) {
      return res.sendStatus(401);
    }

    const event = JSON.parse(rawBody);
    const inserted = await webhookEvents.insertIfAbsent(event.eventId, event);
    if (!inserted) return res.sendStatus(204);
    await jobs.enqueue("apply-minilogistics-event", event.eventId);
    return res.sendStatus(204);
  }
);
```

Dat route `express.raw` truoc `express.json()`. Worker shop:

1. Deduplicate `eventId` bang unique constraint.
2. Lookup order bang `externalOrderId` va doi chieu `trackingCode` da luu noi bo.
3. Xac nhan tracking code trung mapping.
4. Chi cap nhat neu `changedAtUtc` moi hon version hien tai.
5. Reconcile qua GET tracking neu event trung, lech thu tu, khong ro hoac bi mat.
6. Khong log raw payload neu he thong log co the truy cap boi customer/support rong.

Webhook la at-least-once, khong dam bao thu tu. Response nhanh duoi 2 giay va xu ly
nang qua queue.

## 6. Rotate va xu ly su co credential

- API key rotate lam key cu vo hieu ngay. Deploy replacement secret cho backend shop
  truoc khi xoa reference cu khoi secret manager.
- Webhook secret rotation tang version; pending delivery giu secret/version tai thoi
  diem queue. Receiver can tam thoi chap nhan cac version dang con delivery.
- Neu nghi lo key: deactivate client, rotate, thu hep scope/IP, xem audit bang client
  ID/trace ID, va lien he support. Khong gui key qua ticket/chat.

## 7. Checklist go-live shop

- Da dung backend-to-backend va secret manager.
- Key dung environment, least privilege, expiry va IP allowlist da test qua ingress.
- Create dung `Idempotency-Key` on dinh theo order/action.
- Tracking UI/API/CSV cua cung shop deu dung; cross-shop `404`.
- Frontend chi hien public message va xu ly `externalOrderId=null`.
- Receiver verify timestamp, constant-time HMAC, schema/secret version va raw body.
- `eventId` co unique constraint; event cu/duplicate/out-of-order da test.
- Co reconciliation polling va bounded retry cho `429`/`503`/`5xx`/timeout.
- API/webhook logs khong co key, secret, PII hoac raw internal note.
- Da test rotate/revoke va co support/escalation owner.

MiniLogistics chi cap production sau khi sandbox flow va release gate noi bo dat.
