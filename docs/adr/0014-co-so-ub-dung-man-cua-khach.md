# 0014 — Cơ sở Ung Bướu dùng bộ màn của khách, không dùng OTP của SixosPwa

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-08-26
- **Thay:** [0003](0003-vao-ub-qua-cua-an-danh.md) — bản đó chuyển sang *Bị thay thế*
- **Bối cảnh liên quan:** [0005](0005-luu-mat-khau-khong-bam.md) ·
  [0006](0006-chan-dang-nhap-cheo-co-so.md) · [0013](0013-active-la-cong-hien-thi-duy-nhat.md)

## Bối cảnh

`DM_DoiTacApi.BaseUrl` của cơ sở Ung Bướu là `http://10.85.9.34:5000` — IP nội bộ bệnh viện
(RFC1918). SixosPwa chạy **công khai trên internet**, nên gọi vào đó luôn là *connection refused*
(đo thật từ máy công ty). Hệ quả: `LuongCongBenhNhan.ChonDichDenAsync` chết ngay ở bước hỏi tình
trạng tài khoản, và **mọi bệnh nhân đã có sẵn tài khoản UB đều tắc hoàn toàn** — họ chỉ thấy
*"chưa kết nối được với cơ sở"*, không đăng nhập được, không liên kết được.

Ba cửa mà SixosPwa cần đều nằm sau `BaseUrl` (`/api/TaiKhoan/cccd/{cccd}`,
`/api/Auth/forgot-password/send-otp`, `/api/Auth/forgot-password/reset`) — đó là ứng dụng
**SixOSDatKhamAPI**, deploy riêng, chỉ sống trong mạng bệnh viện. Còn `TrangChu`
(`https://kcg.bvungbuou.vn`) là ứng dụng **DangKyOnlineUB**, công khai và gọi được.

Hai sự thật đo được lật cả bài toán:

1. `HtDangNhapServices.LoginAsync:359` so khớp **mật khẩu trần** (`x.MatKhau == password`) và đòi
   `DaXacThuc == true`. Nghĩa là **cầm được mật khẩu bệnh nhân gõ là đăng nhập được ngay**.
2. Toàn bộ luồng Quên mật khẩu của UB **nằm trên `TrangChu`** và đều `[AllowAnonymous]`
   (`HT_QuenMatKhauController.cs:34`).

⇒ Nếu để bệnh nhân **tự gõ mật khẩu thật của họ**, `BaseUrl` không còn cần thiết nữa, và
**đối tác không phải viết thêm dòng code nào**.

## Quyết định

Với cơ sở có `KieuApi = UB`, SixosPwa **dựng lại bộ ba màn của khách** — Đăng nhập, Đăng ký,
Quên mật khẩu — cả giao diện lẫn logic, và **thôi dùng luồng OTP của chính mình** cho nhóm cơ sở đó.
Các cơ sở khác không đổi một dòng.

| Việc | Cửa gọi (đều dưới `TrangChu`) | Neo bên đối tác |
|---|---|---|
| Kiểm mật khẩu | `POST /HeThong/HT_DangNhap/login` | `Controllers/HT_DangNhapController.cs:44` |
| Mở tài khoản + gửi SMS | `POST /HeThong/HT_DangNhap/register`, `xacthuc = 3` | `:96` → `RegisterAsync` |
| Đổi mã lấy tài khoản | `POST /HeThong/HT_DangNhap/XacThucMaXacNhan` | `:138` — bản **MVC** |
| Gửi đường dẫn đặt lại mật khẩu | `POST /HeThong/HT_QuenMatKhau/QuenMatKhau` | `HT_QuenMatKhauController.cs:34` |

**Đối tác là nguồn sự thật của mật khẩu.** SixosPwa không bao giờ tự phán xử đúng/sai, và không
bao giờ ghi đè mật khẩu của bệnh nhân. Ta chỉ giữ một **bản sao** để bàn giao phiên.

**Điểm rẽ nằm đúng một chỗ:** `DangNhapController.Login` (`:40`). Cơ sở đã biết trước khi vào màn
đăng nhập (`DanhSachCoSo → ChiTietCoSo → /DangNhap/Login?coSo={slug}`), nên chỉ cần tra
`CuaCoSo.DungManDoiTac`. Điểm rẽ đặt **sau** mọi guard sẵn có — không được phép bỏ qua chặn cơ sở
ẩn (ADR 0013) hay chặn đăng nhập chéo cơ sở (ADR 0006).

## Vì sao không chọn cách khác

- **Mở một cửa đối tác mới có gác `X-API-KEY`** (`CapMaBanGiao`, cấp mã bàn giao theo CCCD) — đây là
  hướng đã thiết kế xong rồi bỏ. Nó bắt đối tác viết code mới và chờ quản lý UB duyệt, trong khi
  cách trên dùng được ngay bằng các cửa đã có. Nó cũng đẻ ra một cửa **cấp phiên chỉ bằng CCCD** —
  CCCD không phải bí mật, nên toàn bộ an ninh dồn hết vào một khoá dùng chung.
- **SixosPwa là nguồn sự thật của mật khẩu** (đặt mật khẩu bên mình rồi đẩy sang) — bất khả thi:
  `RegisterAsync:81` chặn thẳng CCCD đã `DaXacThuc`, nên với người **đã có tài khoản UB** ta không
  bao giờ đẩy được mật khẩu sang. Đúng nhóm bệnh nhân đang tắc.
- **Hai bên giữ mật khẩu độc lập** — bệnh nhân đổi mật khẩu ở `kcg` là bàn giao hỏng ngay, mà không
  có tín hiệu nào cho ta biết.
- **Nối thẳng vào DB `UB_DangKyOnline`** — SixosPwa sẽ ghi thẳng vào DB của khách, và mỗi lần họ đổi
  logic là ta lệch âm thầm.
- **Kênh 4 (chỉ sinh mã, không gửi tin)** — rẻ hơn kênh 3 vì không tốn tin nhắn, nhưng làm màn *Xác
  thực tài khoản* 4 ô của khách mất lý do tồn tại. Đã chọn **kênh 3**: bệnh nhân nhận SMS và tự gõ
  mã, đúng trải nghiệm trên trang của họ. Cái giá đã biết: mỗi lần đăng ký là một tin nhắn thật
  bệnh viện phải trả tiền.

## Ba bẫy — mỗi cái đều làm build xanh nhưng chạy sai

1. **`X-Requested-With` mang hai nghĩa ngược nhau.** Bản vá `f653f96` bên đối tác làm `login`
   `Redirect` khi request **thiếu** header này.
   - Gọi **máy chủ tới máy chủ** thì **phải gắn** — không thì nhận `302` thay vì JSON và ta đọc nhầm
     thành "sai mật khẩu". Đã gắn cứng trong `UbGateway.TaoClient()`.
   - Bước **bàn giao** thì **phải KHÔNG gắn** — chính nhờ thiếu nó mà đối tác `Redirect` thay vì
     hiện JSON trần cho bệnh nhân. Form POST top-level không tự gắn, nên chỉ cần đừng thêm.
2. **Có HAI action trùng tên `XacThucMaXacNhan`.** Bản ở `Area/API/Controllers/` vừa đặt cookie vừa
   `Redirect`; bản ở `Controllers/` chỉ trả JSON. Gọi ở tầng máy chủ **phải dùng bản `Controllers/`**
   — bản kia đặt cookie vào `HttpClient` của ta chứ không phải trình duyệt bệnh nhân, mà vẫn **tiêu
   thụ mất mã**.
3. **Quên mật khẩu chỉ clone được bước 1.** `HT_QuenMatKhauServices:90` dựng đường dẫn bằng
   `{request.Scheme}://{request.Host}`, nên gọi tới `kcg` thì link luôn trỏ về `kcg`. Bệnh nhân đặt
   mật khẩu mới **trên trang của đối tác** rồi quay lại app đăng nhập. Màn của ta nói rõ điều đó.

## Hệ quả

**Bỏ đi:** cột `BaseUrl` không còn lượt gọi nào (chỉ còn khai báo model/DDL); màn **Liên kết**
(`Views/DangNhap/LienKet.cshtml` + 3 action) bị xoá — nó vốn **ghi đè mật khẩu UB của bệnh nhân**
bằng chuỗi máy sinh, đúng nhóm người đang tắc; nhánh đối tác của `DoiMatKhauAsync` bị bỏ, và mục
*Đổi mật khẩu* bị ẩn khỏi trang bệnh nhân của cơ sở dùng màn đối tác.

**Vá kèm:** `ChonDichDenAsync` nay từ chối cơ sở `DungManDoiTac`. Đó cũng là chốt chặn cho
`XacNhanOtp` — action đó gọi trần được, và qua được guard là chạy tiếp tới `BaoDamHoSoNoiBoAsync`,
**tạo thật** `DM_BenhNhan` + `DM_BenhNhanCoSo` + `HT_TaiKhoan`.

**Rủi ro đã chấp nhận:** SixosPwa lưu **mật khẩu thật** của bệnh nhân dạng trần trong
`TaiKhoanDoiTac.MatKhau`. Nặng hơn trước, vì trước đó chỉ là chuỗi máy sinh mà bệnh nhân không bao
giờ dùng ở đâu khác. **Không băm được** — bàn giao phải phát lại nguyên văn cho `login` của đối tác
(ADR 0005). Cách vá rẻ nhất là **mã hoá khi lưu** bằng `IDataProtector` của ASP.NET; chưa thi hành,
cần người dùng quyết.

**Rủi ro có sẵn của đối tác, ta không làm tệ hơn:** mã xác thực chỉ 4 chữ số, **không có hạn dùng**
(bảng `HT_TaiKhoan` không có cột thời điểm cấp, dù thông báo của họ nói *"đã hết hạn"*) và không có
giới hạn tần suất. Màn Đăng ký công khai của chính họ cũng vậy.

**Ba điểm tựa còn lại của ADR 0003 vẫn nguyên hiệu lực** — nhất là `SameSite=Lax`: bàn giao vẫn chỉ
POST được **một** bước, chuỗi nhiều bước để đi thẳng tới màn đích vẫn không khả thi.

**Thứ tự deploy: UB trước, SixosPwa sau.** `f653f96` và `d57d081` đang nằm trên nhánh
`namnhat_2408_BanGiaoTuSixosPwa`, **chưa merge `master`, chưa lên production**. `a172c0b` (kênh 4)
không còn dùng cho luồng này.
