# Mini Logistics Management System - Project Overview

## 1. Định vị đề tài

### 1.1. Giới thiệu

Mini Logistics Management System là một hệ thống quản lý giao hàng mini được xây dựng nhằm mô phỏng quy trình vận hành cơ bản của các nền tảng logistics như GHN, GHTK hoặc J&T Express.

Mục tiêu của dự án:

- Xây dựng MVP.
- Thể hiện khả năng phát triển Fullstack .NET.
- Áp dụng Clean Architecture và tư duy thiết kế hệ thống.
- Thực hành workflow nghiệp vụ thực tế.

Hệ thống tập trung vào các nghiệp vụ cốt lõi:

- Tạo đơn giao hàng.
- Phân công shipper.
- Theo dõi trạng thái đơn.
- Quản lý COD.
- Tracking vận đơn.
- Dashboard quản trị.

---

### 1.2. Mục tiêu kỹ thuật

| Thành phần     | Công nghệ                             |
| -------------- | ------------------------------------- |
| Backend        | .NET 10                               |
| Frontend       | Blazor Web App                        |
| Architecture   | Clean Architecture + Modular Monolith |
| Database       | SQL Server                            |
| ORM            | Entity Framework Core                 |
| Authentication | ASP.NET Core Identity                 |
| Validation     | FluentValidation                      |
| Logging        | Serilog                               |
| Testing        | xUnit                                 |

---

### 1.3. Phạm vi MVP

### Bao gồm

- Authentication & Authorization
- Shipment Management
- Shipment Tracking
- Shipper Assignment
- COD Basic
- Dashboard cơ bản
- Public Tracking Page

### Không bao gồm

- Real-time GPS tracking
- Payment gateway
- Route optimization
- Mobile app
- Microservices
- Warehouse management phức tạp

---

## 2. Ai là người dùng?

Hệ thống có nhiều nhóm người dùng với vai trò và quyền hạn khác nhau.

---

### 2.1. Admin

Admin là người quản lý toàn bộ hệ thống.

#### Chức năng chính

- Quản lý user.
- Quản lý shipper.
- Quản lý đơn hàng.
- Theo dõi dashboard.
- Quản lý phí giao hàng.
- Quản lý COD.
- Can thiệp trạng thái đơn.

---

### 2.2. Operator (Điều phối viên)

Operator chịu trách nhiệm vận hành và điều phối giao hàng.

#### Chức năng chính

- Xem đơn hàng mới.
- Phân công shipper.
- Theo dõi trạng thái giao hàng.
- Xử lý đơn giao thất bại.
- Hỗ trợ cập nhật trạng thái.

---

### 2.3. Shop / Sender

Shop là người tạo đơn giao hàng.

#### Chức năng chính

- Đăng ký tài khoản.
- Đăng nhập hệ thống.
- Tạo đơn giao hàng.
- Theo dõi trạng thái đơn.
- Xem COD.
- Hủy đơn nếu chưa lấy hàng.
- Tra cứu vận đơn.

---

### 2.4. Shipper

Shipper là người lấy hàng và giao hàng.

#### Chức năng chính

- Xem đơn được phân công.
- Cập nhật trạng thái đơn.
- Xác nhận giao hàng thành công.
- Ghi nhận giao thất bại.
- Xác nhận thu COD.

---

### 2.5. Receiver (Người nhận)

Receiver là người nhận hàng.

#### Chức năng chính

- Tra cứu vận đơn.
- Xem trạng thái đơn hàng.
- Xem lịch sử tracking.

Receiver không cần đăng nhập tài khoản.

---

## 3. Các chức năng chính của MVP

---

### 3.1. Authentication & Authorization

#### Chức năng

- Đăng ký.
- Đăng nhập.
- Đăng xuất.
- Phân quyền theo role.
- Bảo vệ route theo quyền truy cập.

#### Role hệ thống

- Admin
- Operator
- Shop
- Shipper

---

### 3.2. Shipment Management

Shop có thể tạo và quản lý đơn giao hàng.

#### Thông tin đơn hàng

- Người gửi.
- Người nhận.
- Địa chỉ lấy hàng.
- Địa chỉ giao hàng.
- Khối lượng hàng.
- Giá trị hàng.
- Tiền COD.
- Ghi chú.
- Phí vận chuyển.

#### Chức năng chính

- Tạo đơn.
- Xem danh sách đơn.
- Xem chi tiết đơn.
- Hủy đơn.
- Sinh mã vận đơn.

---

### 3.3. Shipment Tracking

Hệ thống cho phép tracking trạng thái vận đơn.

#### Chức năng chính

- Tra cứu bằng mã vận đơn.
- Xem trạng thái hiện tại.
- Xem lịch sử tracking.
- Hiển thị timeline giao hàng.

#### Trạng thái đơn hàng

- Draft
- PendingPickup
- Assigned
- PickingUp
- PickedUp
- InTransit
- Delivering
- Delivered
- DeliveryFailed
- Returned
- Cancelled

---

### 3.4. Shipper Assignment

Operator/Admin có thể phân công shipper cho đơn hàng.

#### Chức năng chính

- Xem danh sách shipper.
- Gán shipper cho đơn.
- Theo dõi assignment.
- Đổi shipper khi cần.

#### Business Rule

- Một đơn chỉ có một shipper active.
- Chỉ đơn PendingPickup mới được assign.

---

### 3.5. COD Management

Quản lý tiền thu hộ COD.

#### Chức năng chính

- Nhập COD khi tạo đơn.
- Xác nhận COD đã thu.
- Theo dõi COD pending.
- Đối soát COD.

#### Trạng thái COD

- NotRequired
- PendingCollection
- Collected
- Settled

---

### 3.6. Shipping Fee Calculation

Tính phí giao hàng theo hướng GHN-like core fee.

#### Công thức hiện tại

```text
TotalFee = BaseFee + ExtraWeightFee + InsuranceFee + ReturnFee
```

#### Quy tắc tính phí

- Khối lượng quy đổi = dài x rộng x cao / 5000.
- Cân tính phí = max(khối lượng thực tế, khối lượng quy đổi).
- Phí dịch vụ chính gồm `BaseFee + ExtraWeightFee`.
- Phí vượt cân tính theo block 0.5 kg sau ngưỡng cân chuẩn của từng tuyến.
- Bảo hiểm tự động: miễn phí nếu giá trị hàng < 1.000.000đ; từ 1.000.000đ trở lên tính 0.5% giá trị khai báo, cap giá trị tính bảo hiểm ở 20.000.000đ.
- Phí hoàn hàng = 50% phí dịch vụ chính = 50% x (`BaseFee + ExtraWeightFee`).

#### Phân loại tuyến

- Nội tỉnh: cùng tỉnh/thành phố.
- Nội vùng: khác tỉnh nhưng cùng vùng.
- Liên vùng: khác vùng.
- Tuyến được phân loại tự động từ tỉnh lấy hàng và tỉnh giao hàng; shop không chọn tuyến thủ công.

---

### 3.7. Dashboard

#### Dashboard cho Admin

- Tổng số đơn.
- Đơn đang giao.
- Đơn thành công.
- Đơn thất bại.
- Tổng COD.
- Tổng doanh thu phí.

#### Dashboard cho Shop

- Tổng đơn đã tạo.
- Đơn đang xử lý.
- COD chờ nhận.

#### Dashboard cho Shipper

- Đơn được assign.
- Đơn giao thành công.
- Đơn giao thất bại.

---

## 4. Các role và permission

| Chức năng               | Admin | Operator | Shop | Shipper | Receiver |
| ----------------------- | ----- | -------- | ---- | ------- | -------- |
| Đăng nhập               | ✅    | ✅       | ✅   | ✅      | ❌       |
| Tạo đơn hàng            | ✅    | ❌       | ✅   | ❌      | ❌       |
| Xem tất cả đơn          | ✅    | ✅       | ❌   | ❌      | ❌       |
| Xem đơn của mình        | ✅    | ✅       | ✅   | ✅      | ❌       |
| Phân công shipper       | ✅    | ✅       | ❌   | ❌      | ❌       |
| Cập nhật trạng thái đơn | ✅    | ✅       | ❌   | ✅      | ❌       |
| Quản lý user            | ✅    | ❌       | ❌   | ❌      | ❌       |
| Quản lý phí vận chuyển  | ✅    | ❌       | ❌   | ❌      | ❌       |
| Xem dashboard           | ✅    | ✅       | ✅   | ✅      | ❌       |
| Quản lý COD             | ✅    | ✅       | ❌   | ✅      | ❌       |
| Tra cứu vận đơn         | ✅    | ✅       | ✅   | ✅      | ✅       |
| Hủy đơn                 | ✅    | ✅       | ✅   | ❌      | ❌       |

---

## 5. Các use case quan trọng

---

### 5.1. Use Case: Shop tạo đơn giao hàng

#### Actor

- Shop

#### Mục tiêu

Tạo yêu cầu giao hàng mới.

#### Main Flow

1. Shop đăng nhập.
2. Chọn chức năng tạo đơn.
3. Nhập thông tin đơn hàng.
4. Hệ thống validate dữ liệu.
5. Hệ thống tính phí giao hàng.
6. Hệ thống sinh mã vận đơn.
7. Lưu đơn hàng.
8. Trả kết quả thành công.

#### Business Rules

- Số điện thoại là bắt buộc.
- Khối lượng > 0.
- COD >= 0.
- Tracking code phải unique.

---

### 5.2. Use Case: Operator phân công shipper

#### Actor

- Operator
- Admin

#### Mục tiêu

Phân công shipper xử lý đơn hàng.

#### Main Flow

1. Operator xem danh sách đơn PendingPickup.
2. Chọn đơn hàng.
3. Chọn shipper.
4. Xác nhận phân công.
5. Hệ thống cập nhật assignment.
6. Đơn chuyển sang trạng thái Assigned.

#### Business Rules

- Chỉ assign đơn PendingPickup.
- Shipper phải active.
- Một đơn chỉ có một assignment active.

---

### 5.3. Use Case: Shipper cập nhật trạng thái đơn

#### Actor

- Shipper

#### Mục tiêu

Cập nhật tiến độ giao hàng.

#### Main Flow

1. Shipper đăng nhập.
2. Xem đơn được phân công.
3. Chọn đơn hàng.
4. Cập nhật trạng thái.
5. Nhập ghi chú nếu cần.
6. Hệ thống lưu tracking history.

#### Business Rules

- Shipper chỉ cập nhật đơn của mình.
- Không được chuyển trạng thái không hợp lệ.
- Không được cập nhật đơn đã hoàn tất.

---

### 5.4. Use Case: Người nhận tra cứu vận đơn

#### Actor

- Receiver

#### Mục tiêu

Theo dõi trạng thái đơn hàng.

#### Main Flow

1. Người nhận nhập mã vận đơn.
2. Hệ thống tìm kiếm đơn hàng.
3. Hiển thị trạng thái hiện tại.
4. Hiển thị lịch sử tracking.

#### Business Rules

- Không yêu cầu đăng nhập.
- Chỉ hiển thị thông tin cần thiết.

---

### 5.5. Use Case: Shop hủy đơn hàng

#### Actor

- Shop

#### Mục tiêu

Hủy đơn chưa được lấy hàng.

#### Main Flow

1. Shop mở chi tiết đơn.
2. Chọn hủy đơn.
3. Nhập lý do hủy.
4. Hệ thống kiểm tra trạng thái.
5. Cập nhật trạng thái Cancelled.

#### Business Rules

- Không được hủy đơn đã PickedUp.
- Chỉ owner của đơn mới được hủy.

---

### 5.6. Use Case: Quản lý COD

#### Actor

- Shipper
- Operator
- Admin

#### Mục tiêu

Theo dõi và đối soát COD.

#### Main Flow

1. Shop tạo đơn có COD.
2. Shipper giao hàng thành công.
3. Shipper xác nhận đã thu COD.
4. Hệ thống cập nhật trạng thái COD.
5. Admin thực hiện đối soát.
6. COD chuyển sang Settled.

#### Business Rules

- Chỉ đơn Delivered mới được collected COD.
- COD amount phải chính xác.
- COD không áp dụng với đơn Returned.

---

## 6. Cập nhật tiến độ gần nhất

### 6.1. Nội dung đã hoàn thành hôm nay

#### Dashboard và dữ liệu đồng nhất

- Dashboard shop đã kết nối dữ liệu thực từ shipment list service.
- Thống kê tổng đơn, trạng thái, COD và phí vận chuyển không còn dùng dữ liệu tĩnh.

#### Shop cancel shipment end-to-end

- Shop có thể hủy đơn thuộc shop của mình trước khi đơn được lấy hàng.
- Domain rule chặn hủy các đơn đã PickedUp/InTransit/Delivering/Delivered/DeliveryFailed/Returned/Cancelled.
- UI chi tiết đơn có form nhập lý do hủy.

#### Navigation và assign shipper skeleton

- NavMenu đã bỏ các entry chưa có route thật.
- Đã tạo skeleton application service cho assign shipper.
- Domain rule assign hiện tại: chỉ đơn PendingPickup mới được assign và mỗi đơn chỉ có một assignment active.

#### GHN-like Core Fee

- Đã thay cách tính phí đơn giản bằng core fee gần GHN:
  - Tính khối lượng quy đổi theo công thức `length x width x height / 5000`.
  - Cân tính phí là max(cân thực tế, cân quy đổi).
  - Tính base fee và phí vượt cân theo tuyến.
- Đã thêm kích thước kiện hàng và cân tính phí vào shipment.
- UI tạo đơn, danh sách đơn, dashboard và chi tiết đơn đã hiển thị cân tính phí/kích thước phù hợp.

#### Shipment fee breakdown

- Đã thêm breakdown phí:
  - `BaseFee`
  - `ExtraWeightFee`
  - `InsuranceFee`
  - `ReturnFee`
  - `TotalFee`
- Shipment vẫn giữ `ShippingFee` là tổng phí để các màn hình cũ không bị vỡ.
- UI tạo đơn và chi tiết đơn đã hiển thị breakdown.

#### Bỏ InterProvince

- Đã xóa `RouteType.InterProvince`.
- FeeRule seed chỉ còn:
  - `IntraProvince`
  - `IntraRegion`
  - `InterRegion`
- Migration đã chuyển dữ liệu cũ `InterProvince` sang `InterRegion`.

#### Auto insurance fee

- Bảo hiểm được tính tự động khi estimate và tạo đơn.
- Rule hiện tại:
  - Giá trị hàng < 1.000.000đ: miễn phí bảo hiểm.
  - Giá trị hàng >= 1.000.000đ: 0.5% giá trị khai báo.
  - Cap giá trị tính bảo hiểm ở 20.000.000đ.
- Đã backfill dữ liệu cũ để cập nhật `InsuranceFee`, `TotalShippingFee` và `ShippingFee`.

#### Return fee

- Đã xác nhận phí dịch vụ chính = `BaseFee + ExtraWeightFee`.
- Return fee = 50% x (`BaseFee + ExtraWeightFee`).
- Domain tự apply return fee khi shipment chuyển sang `Returned`.
- Đã backfill các đơn đang `Returned`.

#### Auto classify route

- Đã tạo `RouteClassificationService`.
- Đã map 34 tỉnh/thành hiện có trong data file sang vùng vận chuyển.
- Rule phân tuyến:
  - Cùng tỉnh/thành: `IntraProvince`.
  - Khác tỉnh nhưng cùng vùng: `IntraRegion`.
  - Khác vùng: `InterRegion`.
- `CreateShipmentService` tự phân loại tuyến từ pickup/delivery province; không còn tin route client gửi lên.
- UI tạo đơn đã chuyển route dropdown thành readonly text.
- Khi đổi tỉnh giao hàng, hệ thống tự cập nhật tuyến và tính lại phí.

### 6.2. Trạng thái kỹ thuật sau cập nhật

- Build hiện tại pass với `dotnet build Mini-logistics-manegemant-system.slnx -p:UseAppHost=false`.
- Các migration mới đã apply thành công:
  - `AddShipmentFeeBreakdown`
  - `ApplyAutomaticInsuranceFees`
  - `ApplyReturnFeesToReturnedShipments`
- Web app chạy được tại `http://localhost:5221`.

---

## 7. Kế hoạch task ngày mai

### 7.1. Task ưu tiên cao

#### Task 1: Hoàn thiện status update flow cho shipper/operator

- Tạo application service cập nhật trạng thái shipment.
- Enforce rule:
  - Shipper chỉ update đơn được assign active.
  - Operator/Admin có thể hỗ trợ cập nhật theo quyền.
  - Không cho chuyển trạng thái sai lifecycle.
- Khi chuyển sang `Returned`, xác nhận return fee đã tự cập nhật đúng.
- UI cần có action cập nhật trạng thái theo role.

#### Task 2: Hoàn thiện assign shipper end-to-end

- Bổ sung validate shipper tồn tại, active và có role `Shipper`.
- Bổ sung authorization cho Operator/Admin.
- Tạo UI danh sách đơn PendingPickup cho operator/admin.
- Tạo action assign shipper và hiển thị assignment hiện tại.

#### Task 3: Shipper workspace

- Tạo màn hình đơn được phân công cho shipper.
- Hiển thị thông tin lấy hàng/giao hàng và timeline.
- Cho shipper cập nhật các trạng thái hợp lệ:
  - Assigned -> PickingUp
  - PickingUp -> PickedUp
  - PickedUp -> InTransit
  - InTransit -> Delivering
  - Delivering -> Delivered hoặc DeliveryFailed
  - DeliveryFailed -> Returned hoặc Delivering

### 7.2. Task ưu tiên trung bình

#### Task 4: COD collection flow

- Tạo service mark COD collected.
- Chỉ cho collected khi shipment `Delivered`.
- Chỉ assigned shipper hoặc role hợp lệ được xác nhận thu COD.
- Update dashboard COD theo trạng thái thực.

#### Task 5: Chuẩn hóa route classification mapping

- Review lại mapping 34 tỉnh/thành theo vùng vận chuyển mong muốn.
- Nếu cần, đổi tên vùng sang tiếng Việt để dễ hiển thị.
- Thêm test/table test cho các case:
  - Hà Nội -> Hà Nội = nội tỉnh.
  - TP.HCM -> Đồng Nai = nội vùng.
  - TP.HCM -> Hà Nội = liên vùng.

#### Task 6: Test coverage cho fee calculator

- Test cân quy đổi.
- Test base/extra fee từng route.
- Test insurance fee.
- Test return fee.
- Test route auto classification.

### 7.3. Cần xác nhận trước khi làm

- Role Operator/Admin hiện đã có account seed hoặc màn login tương ứng chưa?
- Assign shipper UI nên đặt ở trang riêng `/operations/assignments` hay tích hợp vào shipment detail?
- Shipper update status có cần ảnh chụp/ghi chú bắt buộc khi DeliveryFailed không?
- COD collected sẽ do shipper xác nhận ngay khi Delivered hay tách thành thao tác riêng?
