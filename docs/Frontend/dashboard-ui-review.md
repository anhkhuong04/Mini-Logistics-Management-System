# Đánh giá và refactor UI/UX dashboard

**Runtime sau Phase11:** React/Ant Design tại root đã thay renderer theo checkpoint trong [validation log](react-migration-validation.md). Tài liệu này giữ lịch sử review Blazor; không dùng các source links/primitive bên dưới làm hướng dẫn runtime mới. [Frontend README](README.md) và [retirement map](react-migration-inventory.md#retirement-map-cho-test-renderer-blazor) sở hữu implementation/assertion mapping hiện tại.

Ngày đối chiếu source: 15/10/2026. Phạm vi: dashboard Shop/Admin, navigation, shared UI và các khu vực vận hành liên quan.

**Phạm vi lịch sử và migration (25/10/2026):** đây là đánh giá đợt UI Blazor. Quyết định frontend mới là React + TypeScript + Ant Design theo [ADR-004](decisions/adr-004-react-typescript-ant-design.md) và [kế hoạch migration](../../refactorfe.md). Bootstrap/Blazicons/Razor primitives bên dưới chỉ mô tả lựa chọn của UI cũ. Các yêu cầu về số liệu thật, error/empty, drill-down, quyền, responsive và accessibility tiếp tục là tiêu chí parity; kết quả QA React phải được ghi riêng trong [validation log](react-migration-validation.md).

**Kết luận:** source có các luồng chức năng nền tảng, nhưng giao diện trước refactor chưa đồng nhất và chưa đủ căn cứ để coi toàn website đạt UX tốt. Các điểm nghiêm trọng nhất là biểu đồ không phản ánh dữ liệu, dữ liệu lỗi hiển thị như rỗng, bảng tổng quan quá dày và action lặp. Đợt này refactor dashboard và UI dùng chung; kiểm tra hình ảnh/browser vẫn cần hoàn tất khi có browser kết nối.

## Vấn đề đã xử lý

| Trước | Sau | Tác dụng |
| --- | --- | --- |
| Donut Shop luôn có phần màu 72%, kể cả không có dữ liệu | Thanh phân bố theo count thật; nhóm trạng thái và bỏ nhóm count bằng 0; empty không có chart | Người dùng đọc được tình hình đúng |
| KPI, COD, phí cùng chiếm sáu card lớn | Bốn KPI đơn hàng; tài chính ở panel riêng | Phân cấp thông tin và giảm chiều cao |
| Bảng dashboard 8–9 cột và action xem đơn lặp | 4–5 cột; ngày dưới mã đơn, tuyến dưới người nhận; mã đơn là link | Giảm cuộn ngang và thao tác dư |
| Kpi thất bại có thể hiện các card bằng 0 | Error riêng, chỉ render KPI khi có dữ liệu; lỗi latest không giả empty | Phân biệt dữ liệu thiếu với giá trị bằng 0 |
| Chỉ số thiếu lối đi đến công việc | KPI Shop mở danh sách đúng Shop/status/date | Từ nhận biết sang xử lý |
| Shop selector xuất hiện dù chỉ có một Shop | Selector chỉ khi có nhiều Shop; Shop tạm ngưng có thông báo | Loại control không cần thiết |
| Bộ lọc nhiều trường xuất hiện ngay | Tìm mã/trạng thái trước; phần nâng cao mở khi cần, báo khi đang áp dụng | Giảm tải nhận thức |
| Nút tải mẫu CSV ở header thường trực | Mẫu đặt trong phần nhập CSV | Action ở đúng ngữ cảnh |
| Font Montserrat/Be Vietnam Pro, màu navy/royal blue/sea-blue lẫn nhau | Dashboard, shell và các workspace chính dùng Be Vietnam Pro, token sea-blue, weight nhẹ hơn | Giao diện thống nhất |
| KPI/header/nav chữ hoa, weight 750–900, nhiều viền và shadow | Label sentence case, value tabular-number, card phẳng, viền nhẹ | Quét thông tin dễ hơn |
| Account có chevron nhưng không mở menu | Bỏ chevron; icon theo loại tài khoản | Không tạo kỳ vọng thao tác giả |
| NavLink trong child component có selector scoped không bao phủ rõ | Dùng `::deep` trên navigation link; active/hover/focus phân biệt | Định vị và tương tác nhất quán |
| Input focus/nút có rule bỏ outline | Focus rõ, skip-to-content, Escape/close/focus-return cho menu mobile | Hỗ trợ thao tác bàn phím |
| Tên icon user/user-plus không được map | Bổ sung Lucide mapping | Tránh icon fallback sai nghĩa |

## Đánh giá các danh mục

| Khu vực | Đánh giá theo source | Công việc trong đợt này / phần còn lại |
| --- | --- | --- |
| Dashboard Shop | Có KPI, date/Shop filter, latest và permission; trước đây chart và error state chưa đúng | Refactor bố cục, chart, tài chính, dữ liệu rỗng/lỗi và drill-down |
| Dashboard Admin | Có shipment/capacity/COD/webhook; metrics có phạm vi khác nhau | Bốn KPI, COD flow, capacity riêng, integration summary; ghi rõ phạm vi dữ liệu |
| Đơn hàng | Có search/filter/page/import/export; trước đây quá nhiều trường và label pha tiếng Anh | Header/primary action thống nhất, advanced filter, CSV theo ngữ cảnh, nhận filter từ dashboard; phần import chi tiết vẫn cần chuẩn hóa copy tiếp |
| Tạo/sửa/chi tiết đơn | Có draft, fee estimate, validation và action theo trạng thái | Hưởng theme/nút dùng chung; còn cần kiểm tra form dài, mobile, vị trí lỗi và draft/submit trên browser |
| Điều phối | Có summary, filter, capacity/area warning và action nghiệp vụ | Chuẩn hóa font/màu/header copy; cần kiểm tra mật độ bảng và thao tác bulk/manual theo từng trạng thái |
| Shipper | Có workspace, COD pending, proof/status form và confirmation | Chuẩn hóa font/màu; cần kiểm tra trên điện thoại, thứ tự pickup/delivery/COD và bàn phím nhập liệu |
| COD/report | Có filter, summary, export và settlement theo quyền | Dashboard làm rõ tiền chờ thu/chờ đối soát; các page report riêng còn label không dấu/tiếng Anh và chưa dùng hết shared UI |
| Quản trị user/Shop/hub/config/banner | Có management flow, alert/loading và xác nhận tại các use case | Hưởng shell/nút; còn header/form riêng và copy không đồng nhất, cần review theo action nguy hiểm |
| Staff/profile/notification/audit | Có các page theo nhiệm vụ | Navigation nhóm rõ hơn; còn cần thống nhất header/empty/table copy và kiểm tra trải nghiệm staff theo permission |
| Tích hợp | Có key/scope/webhook/test/retry/audit | Bỏ câu giải thích implementation không cần thiết; technical label phù hợp actor tích hợp, vẫn cần review form và secret lifecycle trên browser |
| Public/auth/tracking | Có landing/login/register/tracking; Application hỗ trợ xác minh tracking | Không refactor marketing trong đợt này. Tracking phone-last-four chưa được truyền từ UI là khoảng trống chức năng còn tồn tại |

Các nhận định về layout được suy ra từ Razor/CSS và output render, không phải kết quả usability study với người dùng thật. Những luồng còn lại không được ghi nhận là đã hoàn thiện chỉ vì dashboard đã đổi diện mạo.

## Quy ước áp dụng

- Font Be Vietnam Pro; body/control khoảng 14–16px; heading 22–28px; dùng weight 400/500/600 thay vì mọi phần đều đậm.
- Khoảng cách theo bước 4/8px; card padding 16–20px, section gap 24px; dashboard desktop 4 KPI, tablet 2, màn hình rất hẹp 1.
- Một primary action theo nhiệm vụ; secondary action trung tính; danger dành cho thao tác phá hủy. Text/icon phải chỉ rõ tác dụng.
- Border/card/shadow cùng token. Không dùng màu chỉ để trang trí số liệu hoặc biểu đồ có tỷ lệ cố định.
- Button chính 44px; focus/label và status text phải đọc được bằng bàn phím/screen reader. Bảng cuộn ngang nằm trong region có tên và focus.
- Chart chỉ dùng dữ liệu hiện có. Không tạo tăng trưởng tháng trước, time series hay so sánh kỳ khi API chưa trả dữ liệu đó.
- Trong đợt Blazor này, dùng [Blazicons.Lucide](https://github.com/kyleherzog/Blazicons.Lucide), Bootstrap hiện có và shared Blazor primitives. Khi thực hiện migration đã chọn, React dùng Ant Design và token tương ứng; không trộn Bootstrap/Blazicons vào UI React hoặc giữ hai bộ primitive trong cùng feature mới.

Theo [WCAG 2.2](https://www.w3.org/TR/WCAG22/), kiểm tra contrast, focus và target size là một phần của accessibility. Một số cặp token đã tính: primary/white ~4,70:1; muted/white ~5,58:1; input border/white ~3,37:1; primary-hover/primary-soft ~6,34:1. Các phép tính này không thay thế audit toàn trang và không chứng nhận WCAG conformance.

## Kiểm chứng và giới hạn

Test mới tại `test/MiniLogistics.Web.Tests/DashboardPresentationTests.cs`, `DashboardRenderingTests.cs` và `DashboardDrillDownTests.cs`: mọi trạng thái được tính đúng một lần, tỷ lệ thật/empty, ngày không hợp lệ, URL Shop/status/date, render lỗi KPI/latest, staff không quyền COD và Shop inactive. HTTP test xác nhận query được bind thành filter thật; có regression cho UTC cuối ngày bị chuyển sang ngày kế tiếp khi framework chuyển DateTime sang local time. Các check authorization nghiệp vụ tiếp tục thuộc Application; test UI không thay thế chúng.

Phiên thực hiện chưa có browser kết nối, nên chưa có screenshot, kiểm tra responsive bằng ảnh hoặc xác nhận hành vi focus/circuit thực tế. Trước khi xác nhận UX hoàn tất, kiểm tra browser ở 1440px, 1024px, 768px, 390px và zoom 200%: không mất action, không tràn trang, bảng chỉ cuộn trong region, không có focus ẩn sau sidebar, feedback đúng khi loading/error/empty, role/Shop filter được giữ.

Ưu tiên tiếp theo: visual QA dashboard và sidebar → kiểm tra Shipper mobile → chuẩn hóa copy/header của COD/admin/settings → kiểm tra staff theo permission → usability test với Shop/Operator/Shipper. Chỉ sửa business flow như verified public tracking trong một task riêng có acceptance và test tương ứng.
