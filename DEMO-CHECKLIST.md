# Manual Demo Checklist

Mục tiêu demo: Shop tạo đơn -> Operator assign -> Shipper giao hàng -> COD collected -> Tracking timeline rõ ràng -> Operator theo dõi được vận hành.

## 1. Chuẩn bị dữ liệu

1. Chạy seed database:

```powershell
dotnet run --project src/MiniLogistics.Web -- --seed
```

2. Chạy web app:

```powershell
dotnet run --project src/MiniLogistics.Web
```

3. Tài khoản demo:

| Role | Email | Password |
| --- | --- | --- |
| Shop | shop@minilogistics.local | Shop@123456 |
| Admin | admin@minilogistics.local | Admin@123456 |
| Operator | operator@minilogistics.local | Operator@123456 |
| Shipper | shipper@minilogistics.local | Shipper@123456 |

## 2. Shop tạo đơn

1. Login bằng `shop@minilogistics.local`.
2. Vào `Tạo đơn mới`.
3. Nhập thông tin người gửi, người nhận, hàng hóa, COD lớn hơn 0.
4. Submit tạo đơn.
5. Ghi lại tracking code.
6. Kỳ vọng: đơn nằm ở trạng thái `Chờ lấy hàng` hoặc `PendingPickup` trong danh sách đơn shop.

## 3. Operator assign shipper

1. Logout hoặc mở trình duyệt khác, login bằng `operator@minilogistics.local` hoặc `admin@minilogistics.local`.
2. Vào `Điều phối`.
3. Ở phần `Đơn chờ phân công`, chọn `Demo Shipper` cho đơn vừa tạo.
4. Bấm `Assign`.
5. Kỳ vọng: đơn biến mất khỏi danh sách chờ assign và xuất hiện ở `Đơn đang được xử lý` với trạng thái `Đã phân công`.

## 4. Shipper xử lý giao hàng

1. Login bằng `shipper@minilogistics.local`.
2. Vào `Đơn được giao`.
3. Cập nhật lần lượt:
   - `Đang lấy hàng`
   - `Đã lấy hàng`
   - `Đang vận chuyển`
   - `Đang giao`
   - `Đã giao`
4. Nếu chọn `Giao thất bại`, nhập ghi chú lý do trước khi cập nhật.
5. Kỳ vọng: sau khi đơn `Đã giao`, nếu COD còn `Chờ thu` thì đơn vẫn hiển thị để shipper xác nhận COD. Nếu COD không yêu cầu hoặc đã thu, đơn không còn hiển thị trong danh sách xử lý chính.

## 5. Xác nhận COD

1. Flow chính: shipper bấm `Xác nhận đã thu COD` ngay trong workspace shipper.
2. Flow hỗ trợ: nếu shipper quên xác nhận, login Operator/Admin và vào `Điều phối`.
3. Tìm đơn `Đã giao` còn COD `Chờ thu` trong `Đơn đang được xử lý`.
4. Bấm `Xác nhận COD đã thu`.
5. Kỳ vọng: COD chuyển sang `Đã thu`; assignment active được đóng; đơn không còn nằm trong workspace shipper và không còn nằm trong danh sách vận hành nếu không còn việc cần xử lý.

## 6. Tracking timeline

1. Mở trang `Tra cứu vận đơn`.
2. Nhập tracking code đã ghi lại.
3. Kỳ vọng timeline có đủ các mốc tạo đơn, assign, pickup, transit, delivering, delivered và ghi chú thất bại nếu có.

## 7. Kiểm tra hồi quy nhanh

1. Login Shipper sau khi Delivered/COD collected.
2. Kỳ vọng: workspace shipper không còn đơn hoàn tất.
3. Login Operator/Admin.
4. Kỳ vọng: vẫn xem được đơn đang vận hành, cập nhật trạng thái hợp lệ, và bị chặn nếu transition sai hoặc thiếu note khi `Giao thất bại`.
5. Chạy test:

```powershell
dotnet test Mini-logistics-manegemant-system.slnx
```
