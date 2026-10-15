# Tích hợp trạng thái vận đơn vào website Shop

Dành cho developer backend của website Shop. Contract chi tiết, scope, HTTP error và webhook signature chỉ duy trì tại [Partner API reference](partner-api.md); schema nằm trong [OpenAPI](partner-api.openapi.json).

## Kiến trúc

```text
Browser → Backend Shop (session + quyền order) → database/cache Shop → Partner API
Mini Logistics → HTTPS webhook receiver Shop → durable event store/queue → order state
```

API key nằm trong secret manager/environment của backend Shop. Browser gọi endpoint nội bộ như `GET /api/orders/{orderId}/shipping-status`. Backend lookup tracking code từ order đã authorize, không proxy tracking code tùy ý của customer.

## Kết nối sandbox

1. Owner/staff có `ManageIntegrations` tạo sandbox client tại `/partner/integrations`, cấp scope tối thiểu, expiry và IP allowlist nếu cần.
2. Lưu key hiển thị một lần vào secret store. Production dùng key riêng đúng môi trường.
3. Nếu dùng webhook, cấp quyền cần thiết, đặt URL HTTPS public và secret theo API reference; gửi `webhook.test` và xác nhận verification/response.
4. Kiểm tra quote → create với idempotency key ổn định → track → event trạng thái → cancel khi hợp lệ. Test cả denied scope, cross-shop, replay và conflict.

Ví dụ gọi API và verify webhook: [Node.js](examples/partner-api-node.mjs), [C#](examples/PartnerApiExample.cs); gọi thử bằng [Postman](../../postman/partner-api.postman_collection.json).

## Mapping và đồng bộ

| Dữ liệu Shop nên lưu | Nguồn |
| --- | --- |
| `order_id` | Đơn nội bộ đã authorize |
| `external_order_id`, `tracking_code` | Create response hoặc mapping portal/export |
| `shipment_status`, `cod_status` | Tracking; webhook cho thay đổi shipment |
| `last_changed_at_utc`, `last_synced_at_utc` | Thời điểm event và lần reconcile |

Tracking được phép đọc đơn non-draft của cùng Shop, nhưng external ID và quyền cancel/webhook vẫn theo client tạo đơn. `externalOrderId` có thể null. Đơn UI/import/client khác cần polling; không giả định mọi đơn Shop đều phát webhook cho client của bạn.

Cache ban đầu có thể 30–60 giây với đơn đang chạy; khi có webhook, reconcile 10–15 phút theo nhu cầu. Đây là gợi ý consumer, không phải guarantee của API. Dừng polling thường xuyên với đơn terminal. Tôn trọng `Retry-After`; timeout/5xx dùng bounded backoff và jitter.

Frontend chỉ nhận projection cần hiển thị. Dùng `messageCode` cho logic, `message` theo locale cho text; không parse câu thông báo hoặc bổ sung internal note.

## Webhook receiver

1. Đọc raw body trước JSON middleware; chọn secret theo secret version.
2. Verify timestamp và HMAC constant-time đúng công thức/headers trong API reference; reject trước khi xử lý nếu sai.
3. Persist `eventId` bằng unique constraint và trạng thái chờ xử lý, rồi trả `2xx` nhanh. Event store phải đủ để worker tìm lại sự kiện nếu enqueue lỗi; không acknowledge rồi bỏ mất công việc.
4. Worker lookup order, đối chiếu mapping tracking code, bỏ duplicate và chỉ apply `changedAtUtc` mới hơn.
5. Reconcile qua GET tracking khi event trễ, sai thứ tự, thiếu hoặc không rõ. Không dựa vào thứ tự nhận webhook.

Đặt giới hạn body/timeout và không log raw payload chứa dữ liệu nhạy cảm. API/webhook examples minh họa transport; Shop phải bổ sung transaction, persistence, authorization và recovery cho môi trường của mình.

## Rotation và go-live

API key rotate làm key cũ vô hiệu ngay: chuẩn bị consumer nhận key mới và xử lý khoảng chuyển đổi. Webhook deliveries đang queue giữ secret/version cũ; receiver cần overlap các version còn được sử dụng.

Trước go-live, kiểm tra đúng môi trường/scope/IP, stable idempotency, ownership, null external ID, verification/deduplication/out-of-order, bounded retry/reconcile và rotate/revoke. Ghi bằng chứng theo release/environment tại [Operations](operations.md), không coi local demo là production approval.
