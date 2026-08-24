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

## Đính chính 2026-08-24 — bỏ popup, chấp nhận đích mờ

`BanGiao.cshtml` từng mở một cửa sổ popup (`window.open('', 'cuaSoBanGiao', 'width=480,height=560')`)
chỉ để có một script sống sót qua bước POST: script đó nằm ở tab gốc (SixosPwa), POST vào popup, đợi
cookie đặt xong rồi tự tay ép popup nhảy tiếp tới đích chính xác (`cuaSo.location.href = trangChu`).
Popup không phải là phần thiết kế cốt lõi — nó chỉ là **cái giá phải trả để giữ được khả năng ép điều
hướng chính xác** sau một POST liên site.

Bỏ popup, cho form POST chạy **top-level ngay trong tab đang đứng**, thì cái giá đó không còn trả
được nữa: tab rời SixosPwa ngay lúc `form.submit()`, script chết theo, không còn ai đứng đó ép điều
hướng tiếp. Đây không phải lỗi cần vá — là hệ quả tất yếu của việc bỏ cửa sổ thứ hai.

**Quyết định:** chấp nhận **đích mờ**. Ban đầu đoán bệnh nhân sẽ hạ cánh ở "trang chủ của họ" — **đo
thật 24/08 thì sai**: `/HeThong/HT_DangNhap/login` là API trả **JSON trần**
(`{"statusCode":200,"message":"Đăng nhập thành công",...}`), không phải một trang. Vì POST đi thẳng
top-level (không phải AJAX — fetch không đặt được cookie liên site, xem phần trên), trình duyệt hiển
thị nguyên văn JSON đó, và bệnh nhân **kẹt ở đây** — không có gì để bấm tiếp, không tự chuyển đi đâu.
**User đã xem trực tiếp và chốt CHẤP NHẬN tạm thời** (2026-08-24) thay vì quay lại cơ chế 2 cửa sổ
(script sống sót qua POST cần một tab/popup thứ hai — đã cân nhắc, từ chối vì lo ngại hành vi
`window.open` trong PWA cài đặt standalone, bung ra ngoài app). Đây là **giới hạn đã biết, chưa vá**,
không phải sơ suất bỏ sót — vá tận gốc chỉ có được khi UB mở cửa GET (mục dưới) hoặc UB đổi endpoint
login trả về một trang thay vì JSON (ngoài tầm, không sửa được repo UB). Bốn điểm tựa và giới hạn
SameSite=Lax ở trên **không đổi** — vẫn không sửa dòng nào trong repo UB, vẫn chỉ một bước POST.

**Chỗ cắm cho cửa GET tương lai:** không đổi gì ở tầng dữ liệu — `UbGateway.DungThongTinBanGiao` vẫn
tính `DichCuoi` như cũ (`ManTheoYDinh` + `TrangChu`), chỉ là view không dùng nó để ép điều hướng nữa.
Khi UB mở cửa GET, đổi `BanGiao.cshtml` sang `Redirect(dichCuoi)` ngay sau khi có cookie là đủ — không
cần đụng `UbGateway.cs` hay thêm cột DB nào.
