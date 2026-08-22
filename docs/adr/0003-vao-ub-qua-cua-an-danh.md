# Vào trang Ung Bướu bằng cửa ẩn danh sẵn có, không mở SSO

SixosPwa cần bàn giao phiên bệnh nhân sang `kcg.bvungbuou.vn` (app MVC gác bằng cookie
`DKOnline_auth`). Phương án sạch nhất là xin repo `DangKyOnlineUB` mở một endpoint SSO — nhưng luật
của task là **không sửa codebase UB**, và bên đó do nhóm khác giữ. Vì vậy chúng ta dùng đúng các cửa
**đã có sẵn và đã là `[AllowAnonymous]`** của UB: gọi `register` ở tầng máy chủ để lấy mã, rồi cho
trình duyệt POST `XacThucMaXacNhan` (hoặc `login` với tài khoản đã biết mật khẩu) để **chính UB đặt
cookie**. Kết quả: luồng cookie giống 100% luồng đăng nhập gốc của UB, và repo UB không đổi một dòng.

## Consequences

Thiết kế này treo lên **bốn điểm tựa trong code của UB**. Bên đó đụng vào bất kỳ điểm nào là gãy toàn
bộ, mà lỗi sẽ hiện ra ở phía ta chứ không phải phía họ:

1. `Area/API/Controllers/HT_DangNhapController.cs:18` — `[AllowAnonymous]` ở cấp lớp, đè
   `[Authorize(JwtBearer)]` của `ApiBaseController`. Bỏ dòng này ⇒ `XacThucMaXacNhan` đòi JWT.
2. `Services/HeThongServices/HtDangNhapServices.cs:146-243` — `SendCode` trả **`code` trong thân phản
   hồi**, và `RegisterAsync` trả thẳng kết quả đó ra. Đây là lý do bệnh nhân không phải gõ mã của UB.
   Bỏ trường `code` ⇒ ta mất đường lấy mã, bệnh nhân sẽ phải tự nhập.
3. `HtDangNhapServices.cs:315` — chỗ **duy nhất** trong cả codebase ghi `DaXacThuc = true`, nằm trong
   `XacThucMaXacNhan`. Không có đường nào khác bật cờ này, mà `LoginAsync:327-341` lại bắt buộc nó.
4. `Program.cs:128-132` cấu hình `CookiePolicyOptions{MinimumSameSitePolicy = Strict}` nhưng
   **`app.UseCookiePolicy()` không có trong pipeline** ⇒ cấu hình đó vô hiệu và cookie thực tế là
   **Lax**. Nếu ai đó thêm `UseCookiePolicy()` vào, cookie thành Strict và điều hướng liên site từ
   SixosPwa sẽ không gửi cookie nữa — bàn giao chết ngay, không có cách vá ở phía ta.

Hệ quả kỹ thuật kèm theo: `app.UseCors` của UB dùng `AllowAnyOrigin()` (`Program.cs:224-228`), mà
`AllowAnyOrigin` không đi cùng `AllowCredentials` ⇒ **không thể** đặt cookie bằng `fetch`. Bước đặt
cookie bắt buộc phải là **form POST top-level trong cửa sổ popup** do cú bấm của bệnh nhân mở ra.
Iframe ẩn cũng không dùng được vì SameSite chặn.
