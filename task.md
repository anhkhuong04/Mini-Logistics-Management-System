# Production Roadmap - Shop & Partner API

Muc tieu: dua Mini Logistics tien gan hon chuan production cho cac flow Shop,
shipment va Partner API. Ban nay la checklist tinh gon sau cac dot trien khai
Task 1-7; cac phan phan tich dai da duoc chot thanh quyet dinh va test tuong ung.

## Trang thai tong quan

| Task | Trang thai | Ghi chu |
| --- | --- | --- |
| 1. Shop Profile Management | Done | Shop tu quan ly profile; Admin quan ly active/inactive. |
| 2. Multi-Shop Per User | Done | Shop context theo `shopId`, validate ownership o Application layer. |
| 3. Draft Shipment & Edit Before Pickup | Done | Draft, submit draft, edit truoc pickup, COD dung thoi diem. |
| 4. Order Import / Bulk Create | Done | CSV preview/confirm, validate row, duplicate detection, template UI. |
| 5. Partner API Production Hardening | Done | Credential audit, webhook secret encryption, distributed rate limiter. |
| 6. Transaction Boundary & Outbox Pattern | Done | Webhook events di qua outbox; network retry nam ngoai request flow. |
| 7. Public Tracking Privacy | Done | Summary/verified access, phone last-4 verification, rate limit. |

## Quyet dinh con hieu luc

### 1. Shop Profile Management

- Shop chi duoc sua profile cua shop minh so huu.
- Shop inactive khong duoc update profile, tao shipment hoac thao tac Partner API
  integration; du lieu lich su van co the xem o che do read-only.
- Admin chi bat/tat `Shop.IsActive`, khong sua noi dung profile thay Shop trong
  scope task nay.
- Dia chi pickup mac dinh moi chi ap dung cho shipment tao sau thoi diem update.

### 2. Multi-Shop Per User

- Mot user role `Shop` co the so huu nhieu `Shop` record.
- Neu user co nhieu shop, cac page Shop-facing phai co selected `shopId`.
- `shopId` tu UI/query string khong duoc tin cay; service phai validate ownership.
- Partner API key luon thuoc mot shop cu the; request Partner API khong duoc truyen
  `shopId` tuy y.

### 3. Draft Shipment & Edit Before Pickup

- Draft khong tao COD transaction, khong auto assign, khong publish webhook.
- Submit draft validate lai, tinh lai route/fee, tao COD transaction, chuyen sang
  `PendingPickup`, roi chay auto assignment theo flow hien tai.
- Chi cho edit shipment khi status la `Draft` hoac `PendingPickup` va chua co
  active assignment.
- Khong cho edit tu `Assigned` tro di.
- Public tracking, operations board va shipper workspace khong hien Draft.

### 4. Order Import / Bulk Create

- MVP uu tien CSV UTF-8, batch nho, co buoc preview/staging truoc khi confirm.
- Moi row phai du thong tin receiver, phone, delivery address, parcel, goods value
  va COD amount.
- Fee luon tinh server-side, khong nhan fee tu file import.
- Row loi khong chan cac row hop le neu nguoi dung confirm tao cac row hop le.
- Duplicate theo `clientOrderCode` can bi chan trong batch va theo shop neu da ton tai.

### 5. Partner API Production Hardening

- Khong log raw API key, Authorization header, webhook signing secret hoac payload PII.
- API key raw chi hien thi mot lan sau create/rotate; database chi luu hash/prefix.
- Rotate key lam key cu invalid ngay.
- Webhook signing secret phai duoc decrypt de ky HMAC nhung khong luu plaintext trong DB.
- Rate limiter duoc chon qua configuration, giu nguyen endpoint contract.

### 6. Transaction Boundary & Outbox Pattern

- Shipment/COD/reference va webhook event can publish phai duoc ghi trong cung
  transaction.
- Webhook event duoc ghi thanh `OutboxMessage`; worker tao `WebhookDelivery` va
  delivery worker hien co tiep tuc retry network.
- Auto assignment van synchronous trong phase hien tai de giu Partner API response
  contract on dinh.
- Neu sau nay chuyen auto assignment sang async, phai cap nhat contract vi response
  create co the chi tra `PendingPickup` ban dau.

### 7. Public Tracking Privacy

- Tracking code sai tra not found chung, khong tiet lo shipment co ton tai hay khong.
- Tracking code dung nhung chua verify chi tra summary da mask PII.
- Full detail chi mo khi 4 so cuoi phone match receiver hoac sender.
- Draft khong bao gio public tracking.
- Note noi bo/van hanh khong dua ra public response.

## Verification gan nhat

- `dotnet build Mini-logistics-manegemant-system.slnx --no-restore`: pass.
- `dotnet test Mini-logistics-manegemant-system.slnx --no-build`: pass, tong 123 tests.
- Local dev server chua chay duoc trong sandbox do ASP.NET Data Protection khong ghi
  duoc key vao `C:\Users\Admin\AppData\Local\ASP.NET\DataProtection-Keys`.

## Follow-up khuyen nghi

- Chay manual browser QA ngoai sandbox cho cac flow: shop selector, draft/edit/submit,
  import CSV, Partner integration, public tracking.
- Cau hinh Data Protection key path rieng cho local/dev environment de dev server
  chay on dinh.
- Can nhac bo sung Partner API request signing va API scopes sau khi hardening hien
  tai on dinh.
- Can nhac chuyen auto assignment sang outbox command/event neu can giam latency
  request create shipment.
- Neu import production can file lon, bo sung import batch persistence/background
  worker thay vi xu ly dong bo.
