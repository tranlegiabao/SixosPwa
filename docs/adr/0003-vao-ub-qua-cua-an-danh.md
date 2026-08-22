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

## Đính chính 2026-08-22 — chỉ bàn giao được MỘT bước

Bản đầu của ADR này nói cookie là Lax nên "điều hướng liên site vẫn gửi cookie".
Đúng một nửa, và nửa thiếu làm hỏng cả một hướng thiết kế.

**Lax gửi cookie với `GET` top-level, KHÔNG gửi với `POST` liên site.**

Hệ quả đo được bằng Playwright (theo dõi popup từng 400ms):

```
 800ms  → /HT_DangNhap/login          nhận cookie ✅
1600ms  → /HT_DangNhap/select-branch  POST không mang cookie
3200ms  → /HT_DangNhap/login?ReturnUrl=…ThemIdXemThongTinBenhNhan   ❌ bị đá về
```

Nên chuỗi POST nhiều bước để cấp đủ 3 claim là **không khả thi**, và bước 2 còn
gây hại: `AddClaimsAsync` đọc `User` để *cộng dồn* claim, `User` rỗng thì nó
**ghi đè và làm mất `IdTK`** vừa cấp — kéo theo màn chọn hồ sơ của UB hiện danh
sách rỗng.

**Quyết định:** chỉ POST `login`, rồi để UB tự dẫn bệnh nhân qua hai màn của họ
(chọn chi nhánh → chọn hồ sơ). Bệnh nhân thấy thêm hai màn mang thương hiệu UB
trước khi tới đích — đây là cái giá đã biết và chấp nhận.

**Điều kiện để đi thẳng tới đích:** phải có một cửa **GET** bên UB (GET top-level
*có* mang cookie). Đó chính là phương án SSO mà vòng grill số 4 đã chọn rồi bị
đảo ở vòng 9 — nếu sau này mở lại thì đây là lý do kỹ thuật, không phải sở thích.

Cột `DM_DoiTacApi.MaChiNhanh` giữ lại dù chưa dùng: có cửa GET thì nó chính là
tham số cần gửi.
