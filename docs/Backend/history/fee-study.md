# Nghiên cứu phí — tham khảo lịch sử

> Phần đối chiếu bên ngoài và đề xuất từ `fee.md` cũ, ghi nhận ngày 2026-06-29; chưa xác minh lại tài liệu hãng vận chuyển. Đây không phải mô tả implementation hay backlog được phê duyệt. Phần mô tả phí trùng lặp đã được gộp và cập nhật tại [Business rules](../business-rules.md).

## 4. So sanh voi luong thuc te o du an logistics va doi chieu SPX Express

## 4.1. Tong quan

Trong du an logistics production, pricing flow thuong la:

1. Validate serviceability.
2. Xac dinh service/product duoc phep ban.
3. Xac dinh zone chi tiet theo origin-destination.
4. Tinh chargeable weight.
5. Lay rate card theo:
   - service
   - merchant contract
   - channel
   - thoi diem hieu luc
   - pickup/drop-off
6. Cong them phu phi:
   - COD
   - bao hiem / high-value
   - remote area
   - return
   - fuel / peak season
   - handling fee dac biet
7. Chot quote.
8. Khi shipment chay van hanh, phat sinh cac phi event-based:
   - giao lai
   - buyer reject
   - return
   - dieu chinh COD
9. Den ky doi soat, fee moi duoc tru/doi chieu/thanh toan.

He thong hien tai moi cover tot buoc 3, 4, 5 o muc do don gian, va cover mot phan buoc 8 qua `ReturnFee`.

## 4.2. Doi chieu chi tiet

| Chu de | Repo hien tai | SPX / production thuc te |
| --- | --- | --- |
| Chargeable weight | `max(actual, volumetric)` voi divisor `5000` | SPX cong khai tinh can tinh phi theo `max(actual, volumetric)` nhung divisor la `6000`. Day la khac biet nghiep vu ro nhat. |
| Zone / route | 3 nhom: noi tinh, noi vung, lien vung | SPX cong khai co service coverage va vung chi tiet hon, co ca nhom `Special` va danh sach tinh/thanh theo tung service. He thong production thuong dung zone matrix, khong chi 3 bucket. |
| Bang gia | 3 seed rules, hard-coded qua migration/config | Hop dong SPX neu ro service fee tinh theo pricing schedule dinh kem hop dong va co the duoc cap nhat cong khai tren app. Nghia la bang gia production la du lieu van hanh, khong phai fixed seed. |
| COD fee | COD transaction duoc tao cung shipment, khong co extra fee rieng | SPX cong khai noi COD service fee va COD transfer fee da duoc gom trong postal service fee. Nghia la huong "khong tach COD fee rieng tren UI" cua repo khong sai, nhung repo chua model payout/reconciliation. |
| COD payout | Chi co trang thai `PendingCollection -> Collected -> Settled` | SPX cong khai co payout schedule Thu 2 / Thu 4 / Thu 6, bank account verification, va auto payment order. Production can co settlement scheduler va ledger. |
| Gia tri cao / bao hiem | Bao hiem 0.5% tren declared value >= 1M, cap 20M | SPX cong khai co high-value handling service cho don >= 3M, phi co dinh 25,000 VND/don, va cach xac dinh dua tren max(COD, declared value) tai thoi diem pickup thanh cong. Day la mo hinh khac voi insurance rate 0.5% cua repo. |
| Return fee | Co dinh 50% service fee khi `Returned` | SPX cong khai noi return fee nam trong delivery rate table; homepage con noi co package "return fee support", va co "Buyer Reject Fee Service". Nghia la return fee thuc te co the contract-driven / product-driven, khong phai 50% co dinh. |
| Chinh sua COD sau tao don | Repo khong thay model explicit cho re-price/re-evaluate | SPX cong khai cho phep sua COD amount o mot so trang thai edit duoc. Voi production system, thay doi COD co the anh huong high-value fee, reconciliation, risk rules. |
| Gioi han package | Validator chi check > 0, UI cho nhap toi da rat lon | SPX cong khai nhan hang toi da 17kg, moi chieu khong qua 60cm cho quy cach chuan. Repo hien tai chua enforce rang buoc nay. |
| Loai phi dac biet | Chua co | SPX cong khai co them `Buyer Reject Fee Service`, `High-Value Parcel Handling`, `Partial Delivery`, `Try-on`, pickup/drop-off options. He thong production thuong phai co service catalog thay vi mot fee model duy nhat. |

## 4.3. Nhan xet quan trong nhat

### A. Repo hien tai giong "core quote engine" hon la "billing engine"

No lam tot viec:

- phan loai tuyen
- tinh can tinh phi
- tinh phi co ban
- snapshot phi vao shipment

Nhung chua cover day du:

- service catalog
- contract pricing
- surcharge engine
- settlement engine
- dispute/reconciliation engine

### B. Khac biet lon nhat voi SPX la mo hinh phi gia tri cao va can quy doi

Hai diem nay anh huong truc tiep den tong phi:

- Repo: divisor `5000`
- SPX cong khai: divisor `6000`

Va:

- Repo: bao hiem theo ty le 0.5%
- SPX cong khai: high-value service co phi handling co dinh `25,000 VND` cho don >= `3,000,000 VND`

Day khong phai sai ve ky thuat, nhung la khac biet ve **business model**.

### C. Return fee trong repo dang la gia dinh nghiep vu de demo

Cong thuc `50% * service fee` de hieu va de demo, nhung trong production:

- return fee thuong phu thuoc contract
- co the khac nhau theo service
- co the duoc ho tro boi package uu dai
- co the co buyer reject fee tach rieng

Do do, neu muc tieu la "gan SPX", day la diem can doi lai som.

## 5. Ket luan

He thong hien tai phu hop muc tieu MVP va demo nghiep vu logistics:

- quote tren UI
- tinh lai o backend
- luu breakdown vao shipment
- bo sung return fee khi don bi hoan

Neu so voi luong thuc te tai mot he thong production nhu SPX Express, hien tai repo moi o muc:

- **tot cho demo va hoc architecture**
- **chua dat muc pricing/billing engine thuc te**

Khoang cach lon nhat nam o:

1. zone va service granularity
2. tariff configuration theo thoi gian va theo merchant
3. surcharge catalog
4. COD settlement / reconciliation
5. high-value / return-fee policy giong van hanh thuc

## 6. De xuat nang cap neu muon tien sat SPX hon

## 6.1. Uu tien 1: sua mo hinh tinh can

- doi volumetric divisor tu `5000` thanh cau hinh duoc
- cho phep divisor theo service
- them gioi han package standard: 17kg, 60cm

## 6.2. Uu tien 2: tach pricing rule thanh tariff engine

Nen co cac bang/aggregate nhu:

- `ServiceType`
- `PricingVersion`
- `Zone`
- `TariffRule`
- `SurchargeRule`
- `MerchantPricingProfile`

## 6.3. Uu tien 3: tach high-value ra khoi insurance policy hien tai

Neu muon gan SPX:

- them `HighValueHandlingFee`
- rule: `max(COD, DeclaredValue)` tai moc pickup thanh cong
- configurable threshold va fee amount

## 6.4. Uu tien 4: model hoa return/reject fee theo policy

Khong nen hard-code `50%`.
Nen doi thanh:

- `ReturnFeePolicy`
- `BuyerRejectFeePolicy`
- `ServicePackageBenefit`

## 6.5. Uu tien 5: bo sung settlement layer

Can them:

- COD payout schedule
- reconciliation batch
- fee deduction from COD funds
- outstanding balance
- invoice/payment request state

## 7. Tai lieu doi chieu

Tai lieu cong khai SPX Express Vietnam, truy cap ngay **2026-06-29**:

- Terms of Service: https://spx.vn/en/dieu-khoan-su-dung.html
- COD Service: https://spx.vn/en/service/dich-vu-thu-tien-ho.html
- Service and Lead Time: https://spx.vn/en/shipping/chat-luong-dich-vu-buu-chinh.html
- High-Value Parcel Handling: https://spx.vn/en/service/xu-ly-buu-gui-gia-tri-cao.html
- Contract Template: https://spx.vn/en/mau-hop-dong.html
- SPX homepage / service overview: https://spx.vn/en

Tom tat cac diem doi chieu chinh tu tai lieu cong khai:

- SPX tinh chargeable weight theo `max(actual, volumetric)` voi divisor `6000`.
- SPX cong khai COD khong thu phi rieng, phi COD da nam trong postal fee.
- SPX cong khai payout COD theo lich Thu 2 / Thu 4 / Thu 6.
- SPX cong khai high-value service co phi `25,000 VND` cho don >= `3,000,000 VND`.
- Hop dong mau cho thay service fee va return delivery fee la doi tuong doi soat/thanh toan, va co co che tru phi tu COD funds.
