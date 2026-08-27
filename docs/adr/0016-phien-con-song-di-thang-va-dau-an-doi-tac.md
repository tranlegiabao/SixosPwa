# 0016 — Phiên còn sống thì đi thẳng, và bàn giao đòi dấu ấn của đối tác

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-08-27
- **Bối cảnh liên quan:** [0014](0014-co-so-ub-dung-man-cua-khach.md) — quyết định này gỡ hai ngõ cụt mà
  0014 để lại. [0015](0015-phan-hoi-doi-tac-phai-co-statuscode-200.md) — đường tự động dưới đây **chỉ an
  toàn khi có 0015**; thiếu nó thì một trang HTML kèm HTTP 200 sẽ bị đọc thành "mật khẩu đúng".
  **Thay một phần** lý lẽ đã ghi trong mã tại `DangNhapController.Login` và `Views/DangNhap/Login.cshtml`.

## Bối cảnh

Từ 0014, cơ sở Ung Bướu dùng bộ màn dựng lại của khách: bệnh nhân gõ **mật khẩu thật** của họ, và
SixosPwa cấp một phiên riêng qua `CapPhienBenhNhanAsync`. Phiên đó `IsPersistent`, hạn 365 ngày.

Nhưng **không đường nào dùng tới nó**. Bệnh nhân đăng nhập trên điện thoại, được bàn giao ra TrangChủ
UB, tắt app; bật lại thì bị bắt gõ CCCD + mật khẩu **lần nữa** — dù chưa hề đăng xuất. Hai ngõ cụt:

1. `Login` GET nạp `ViewBag.DangDangNhapLa` / `LinkDiTiep` rồi `return View("UbLogin")` — mà `UbLogin.cshtml`
   **không đọc** hai ViewBag đó (chỉ `Login.cshtml` đọc). Khối "Tiếp tục với tài khoản này" không bao giờ hiện.
2. `DiTiep` → `ChonDichDenAsync`, gặp `DungManDoiTac` là **trả về đúng màn đăng nhập** — nên nút
   "Hồ sơ bệnh nhân" ở thanh dưới cũng vòng lại form.

Nguyên liệu để đi tiếp thì đã có sẵn từ 0014: mật khẩu thật nằm trong `HT_TaiKhoanDoiTac.MatKhau`, và
màn Bàn giao chỉ cần đúng cặp CCCD + mật khẩu đó.

## Quyết định

### 1. Phiên còn sống thì **đi thẳng**, không hỏi

Bệnh nhân bấm nút ở trang cơ sở mà phiên còn sống và đã mang dấu ấn: SixosPwa lấy mật khẩu đã cất, **hỏi
lại đối tác** rồi bàn giao — không hiện màn đăng nhập nào.

Điều này **đảo ngược** lý lẽ đang ghi trong mã: *"Còn phiên thì KHÔNG tự đây đi đâu cả… vì SixosPwa không
biết bệnh nhân vừa đăng xuất bên hệ đối tác hay chưa, nên tự động bàn giao lại sẽ khiến nút Đăng nhập
thành nút không cho đăng nhập."* Lý lẽ đó đúng về mặt logic nhưng trả giá sai: nó bắt **mọi** bệnh nhân
gõ lại mật khẩu để phòng một trường hợp hiếm. Lối thoát cho trường hợp hiếm ấy vẫn còn nguyên và ở chỗ
dễ tìm hơn — **menu 3 gạch → Đăng xuất** (có ở `_Layout` và ở chính trang bệnh nhân).

### 2. Vẫn **hỏi đối tác trước**, không POST mù

Đường tự động gọi lại `DangNhapDoiTacAsync` với mật khẩu đã cất chứ không bàn giao thẳng. Lý do: mật khẩu
cất ở ta **có thể đã cũ** — bệnh nhân đổi mật khẩu trên trang của đối tác, kể cả qua Quên mật khẩu (đường
dẫn đặt lại của họ dựng bằng `{request.Host}` nên luôn trỏ về bên họ), mà SixosPwa không hề hay biết.
POST mù thì bệnh nhân rơi xuống trang đăng nhập của đối tác, ngoài tầm SixosPwa, không có lối về.

Gọi thử là an toàn: `LoginAsync` bên họ là truy vấn **đọc thuần**, không đếm lần sai, không khoá tài khoản.

Kèm theo, `KetQuaThaoTac` mọc thêm cờ `DoiTacHong` để phân biệt **"đối tác từ chối mật khẩu"** với
**"đối tác đang hỏng"**. Cần phân biệt thật, vì báo nhầm theo hướng *"hệ thống gặp sự cố"* trong khi mật
khẩu đã đổi sẽ khiến bệnh nhân chờ mãi mà không bao giờ vào được — đúng loại lỗi mà 0015 sinh ra để chặn.

### 3. Bàn giao đòi **dấu ấn** `DoiTacXacThuc`, không chỉ `[Authorize]`

Claim `DoiTacXacThuc` chỉ được đóng ở **một chỗ duy nhất**: `CapPhienBenhNhanAsync` — tức chỉ khi **chính
đối tác** vừa phán mật khẩu thật (`UbDangNhap`) hoặc mã xác thực của họ (`UbXacThucMa`) là đúng. Màn
`BanGiao` và cả hai lối tự động ở trên đều đòi nó khi cơ sở `DungManDoiTac`.

Đây **không phải** thắt chặt cho vui — nó bịt một lỗ đang mở sẵn:

1. `XacNhanOtp` là `[HttpPost]` **không `[Authorize]`**, gọi trần được.
2. Mã OTP còn kê tạm một giá trị cố định lúc phát triển, và tài khoản không tồn tại cũng lọt.
3. `Cccd` và `MaCoSo` lấy **thẳng từ thân request**.
4. `BanGiao` chỉ `[Authorize]` và **không kiểm** `DungManDoiTac`; `DungThongTinBanGiaoAsync` chỉ cần
   claim CCCD + một liên kết có mật khẩu là trả về form đã điền sẵn **mật khẩu thật**.

⇒ Ai biết CCCD của một người đã từng đăng nhập qua SixosPwa là **vào thẳng tài khoản của họ bên đối tác**.

🔴 **Chặn ngay tại cửa `XacNhanOtp` thì KHÔNG đủ.** Nếu chỉ chặn theo `model.MaCoSo`, kẻ tấn công bỏ trống
trường đó: phiên không có claim `MaCoSo`, và `LayMaCoSoPhien` **rơi xuống lấy `?coSo=` trên URL**. Phải
chặn bằng một dấu ấn nằm trên **chính phiên**, không phải bằng tham số nào gửi lên.

### 4. Đường mới đi **bên cạnh** `ChonDichDenAsync`, không xuyên qua nó

`ChonDichDenAsync` cố ý trả về màn đăng nhập cho cơ sở `DungManDoiTac`. Đó là chốt chặn cho **đường OTP**:
`XacNhanOtp` gọi trần được, và qua được guard là chạy tới tận `BaoDamHoSoNoiBoAsync`, tạo thật
`DM_BenhNhan` / `DM_BenhNhanCoSo` / `HT_TaiKhoan`. Vì vậy đường tự động đặt ở **controller** (`DiTiep`,
`Login`) — nơi đọc được `User` và kiểm được dấu ấn — chứ **không** nới lỏng `ChonDichDenAsync`.

### 5. Mở nguội app mà đã đăng nhập thì về **đúng nhà của bệnh nhân đó**

> **Sửa 2026-08-27 (bản đầu ghi "thì về `/benh-nhan`" cho mọi phiên).** Bản đầu bỏ sót đúng đối tượng
> mà ADR này sinh ra để phục vụ: bệnh nhân của cơ sở dùng bộ màn đối tác. Với họ, "nhà" là TrangChủ
> của đối tác chứ không phải trang bệnh nhân nội bộ — mục 1 đã chốt "phiên còn sống thì đi thẳng", mà
> mục 5 lại chặn đúng lối vào hay dùng nhất (biểu tượng PWA). Hai mục tự mâu thuẫn; mục 5 nhường.

`/` là `start_url` của PWA. **Mở nguội** (khởi động lại app từ biểu tượng, vào bằng bookmark, gõ thẳng
URL) mà đã đăng nhập **và có claim `Cccd`** thì rẽ theo cơ sở của phiên:

- Cơ sở **dùng bộ màn đối tác** *và* phiên **có dấu ấn** `DoiTacXacThuc` → `/DangNhap/DiTiep`, tức là
  đi thẳng sang TrangChủ đối tác. Uỷ thác chứ **không** chép lại cây quyết định: `DiTiep` đã giữ đủ ba
  vế và đã hỏi đối tác trước khi bàn giao (mục 2). Mọi nhánh thoát của nó đều là trang cuối
  (`/benh-nhan`, `/DangNhap/Login?coSo=`, `/DangNhap/BanGiao?coSo=`) nên không thể vòng lại `/`.
- Còn lại (cơ sở nội bộ, hoặc phiên **chưa** có dấu ấn) → `/benh-nhan` như cũ.

Vế claim `Cccd` là bắt buộc: khu Admin ký **cả hai** cookie nên admin cũng tính là đã xác thực, nhưng
họ không có `Cccd`/`MaCoSo` — đẩy họ sang trang bệnh nhân là ra trang rỗng không biết chào ai.

🔴 Chỉ đẩy khi **mở nguội**, đo bằng Referer không cùng host — đúng phép thử mà "PWA Last Page Restore"
trong `_Layout` dùng. Bấm **trong app** tới `/` (ví dụ nút logo ở header trang danh sách cơ sở) thì có
Referer cùng host, phải render `/` bình thường — nếu không, trang công khai thành không bao giờ xem lại
được khi đã đăng nhập.

Kèm theo, trang bệnh nhân mọc một **logo bấm được** trỏ tới danh sách cơ sở cùng nhóm. Không trỏ `/`:
lối ra khỏi `/benh-nhan` phải là danh sách cơ sở, còn từ danh sách cơ sở bấm logo mới về `/`.

## Vì sao không chọn cách khác

**Dựng khối "Tiếp tục với tài khoản này" trên màn UB** (đúng khuôn `Login.cshtml` đang có) — một chạm thay
vì không chạm, và giữ nguyên lý lẽ cũ. Bỏ vì bệnh nhân bấm nút "Đăng ký khám" là đã nói rõ ý định rồi;
thêm một màn hỏi lại "có thật muốn không" chỉ là thuế. Lối đổi tài khoản vẫn còn ở menu.

**Cứ POST thẳng, không hỏi đối tác** — nhanh hơn một lượt HTTP. Bỏ vì ai đổi mật khẩu bên đối tác sẽ bị
văng ra ngoài SixosPwa **mọi lần**, và ta không bao giờ biết để dọn mật khẩu rác.

**Bịt lỗ bằng cách gỡ mã OTP cố định** — triệt để hơn nhưng đụng luồng đăng nhập của **mọi** cơ sở nội bộ,
không chỉ cơ sở đối tác. Để riêng một đợt.

## Đánh đổi đã biết

- **Phiên cũ phải gõ lại đúng một lần.** Cookie đang sống trên máy bệnh nhân không có dấu ấn, nên lần đầu
  sau khi triển khai họ vẫn thấy màn đăng nhập. Từ lần sau mới đi thẳng.
- **Nhánh `CoBanGiao && !DungManDoiTac` chưa siết.** Đó là chỗ dành cho đối tác tương lai, mật khẩu do máy
  sinh, và **hiện không cơ sở nào chạy**. Siết luôn ở đó là gãy một đường không ai đi mà chẳng được lợi gì.
  Khi có đối tác thứ hai, phải xét lại mục này trước.
- **"PWA Last Page Restore" chỉ còn chạy khi bấm trong app.** Mở nguội `/` mà đã đăng nhập thì chuyển
  hướng phía máy chủ đi trước khi script kịp chạy — đây là hành vi được yêu cầu. Bấm trong
  app tới `/` thì `/` render và script đó chạy bình thường. Đừng "sửa" chỗ chuyển hướng mà không đọc
  dòng này.
- **Mỗi lần vào là một lượt gọi sang đối tác.** Đối tác chết thì bệnh nhân không đi thẳng được, nhưng sẽ
  đọc được câu báo sự cố đúng nghĩa thay vì rơi xuống một trang trắng bên kia.
- **Từ 2026-08-27, lượt gọi đó xảy ra ngay khi MỞ APP,** chứ không còn đợi bệnh nhân bấm nút. Ba hệ quả
  nhận trọn: ① mỗi lần bật app là một lượt HTTP sang đối tác; ② đối tác chết thì mở app ra là **màn đăng
  nhập của cơ sở kèm câu báo sự cố**, chứ không phải `/benh-nhan` — đúng theo mục 2, nhưng khác hẳn cảm
  giác trước đó; ③ bệnh nhân của cơ sở đối tác **không còn thấy `/benh-nhan` khi mở nguội**, muốn vào thì
  qua menu 3 gạch. Ai muốn đảo lại phải đọc mục 5 trước, đừng sửa mò ở `HomeController`.
