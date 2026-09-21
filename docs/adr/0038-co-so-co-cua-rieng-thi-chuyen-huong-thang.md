# 0038 — Cơ sở có cửa riêng thì chuyển hướng thẳng, không dựng lại màn của họ

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-09-19
- **Thay thế:** [0014](0014-co-so-ub-dung-man-cua-khach.md)
- **Bối cảnh liên quan:** [0006](0006-chan-dang-nhap-cheo-co-so.md) ·
  [0013](0013-active-la-cong-hien-thi-duy-nhat.md) ·
  [0016](0016-phien-con-song-di-thang-va-dau-an-doi-tac.md)

## Bối cảnh

ADR 0014 chốt: với cơ sở Ung Bướu, SixosPwa **dựng lại bộ ba màn của khách** (Đăng nhập, Đăng ký,
Quên mật khẩu) ngay trong cổng, gọi thẳng các cửa dưới `TrangChu` của họ. Điểm rẽ đặt ở
`DangNhapController.Login`: `if (cuaDoiTac?.DungManDoiTac == true) return View("UbLogin")`.

Đợt A (18-09) **xoá hẳn** hướng đó — có chủ ý, ghi ở `PLAN-DOT-A.md §5.1`: bỏ 7 action `Ub*`,
bốn view `UbLogin` / `UbDangKy` / `UbQuenMatKhau` / `BanGiao`, cả tầng `IPartnerGateway` +
`PartnerGatewayFactory` (695 dòng), thay bằng **một** `CuaCoSoService` đọc đúng một cột
`DM_CSKCB.KetNoi_UrlChuyenHuong`.

🔴 **Nhưng nhánh thay thế không bao giờ được viết.** Điểm rẽ cũ bị xoá, điểm rẽ mới không có. Hệ quả:
bệnh nhân **chưa đăng nhập** bấm *Đăng nhập* ở cơ sở Ung Bướu lại thấy **màn đăng nhập của SixosPwa** —
một màn hỏi OTP mà nhóm cơ sở đó không dùng. Phiên **đã** đăng nhập thì không dính, vì
`ChonDichDenAsync` vẫn trả URL đối tác; nên lỗi chỉ lộ ở người chưa đăng nhập, và không test nào của
đợt A chạm tới.

## Quyết định

Cơ sở có `KetNoi_UrlChuyenHuong` (và `KetNoi_Active = 1`) thì **việc đăng nhập / đăng ký là của chính
họ**. SixosPwa **chuyển hướng thẳng** sang trang đó, không hỏi OTP, không dựng tài khoản, không dựng
lại màn nào.

Điểm rẽ nằm đúng một chỗ — `DangNhapController.Login` (GET) — và đặt **sau mọi guard**, giữ nguyên
ràng buộc ADR 0014 đã nêu:

1. chặn cơ sở đang ẩn (ADR 0013) — đi trước;
2. chặn đăng nhập chéo cơ sở (ADR 0006) — đi trước;
3. `?coSo=` phải là **slug** hợp lệ, sai thì về `/` — đi trước;
4. `adminReauth` đi đường riêng: quản trị viên xác thực lại thì **ở lại cổng**, không bị đẩy sang
   trang đối tác.

## Vì sao đổi hướng so với ADR 0014

ADR 0014 chấp nhận dựng lại màn của khách vì lúc đó `BaseUrl` của Ung Bướu là IP nội bộ bệnh viện,
gọi từ internet luôn *connection refused*. Cái giá là SixosPwa phải **giữ một bản sao mật khẩu bệnh
nhân** và bám vào bốn cửa của đối tác — mỗi lần họ đổi là ta vỡ, mà ta không có cách nào biết trước.

Chuyển hướng thẳng bỏ được toàn bộ cái giá đó: không bản sao mật khẩu, không phụ thuộc chữ ký API
của ai, và 695 dòng tầng cửa biến mất. Đổi lại, cổng **không còn giữ phiên** cho nhóm cơ sở này —
đúng bản chất: trang của họ, tài khoản của họ.

## Hệ quả

**Dữ liệu là nguồn sự thật, không phải kiểu bản cài.** Bật/tắt cửa riêng của một cơ sở nay là một câu
`UPDATE` trên `KetNoi_UrlChuyenHuong` / `KetNoi_Active`, không cần biên dịch lại.

🔴 **URL sai là bệnh nhân bay đi chỗ chết mà cổng không biết.** Trước đây điểm rẽ dẫn tới một màn
trong cổng nên URL hỏng chỉ ảnh hưởng lời gọi API; nay nó là **đích cuối** của người dùng. Hai chỗ đã
thấy ngay lúc viết ADR này:

- `79423` (Ung Bướu CS1) đang trỏ một **đường hầm `trycloudflare` cũ** — địa chỉ đổi mỗi lần dựng lại,
  nên gần như chắc chắn đã chết. `CONTEXT.md` mục *Đường hầm tạm* đã cảnh báo đúng chuyện này.
- `DM_CSKCB_Save` gán `KetNoi_BaseUrlHIS = @KetNoi_BaseUrlHIS` **không có `ISNULL`**, nên mọi lời gọi
  không truyền tham số đó sẽ **xoá trắng** cột. `KetNoi_UrlChuyenHuong` cần được soi lại cùng kiểu.

**Kiểm được bằng một phép rẻ:** `GET /DangNhap/Login?coSo={slug}` khi **chưa đăng nhập** — cơ sở có
cửa riêng phải trả `302` tới URL của họ, cơ sở thường phải trả `200` và ở lại cổng. Đo ngày 19-09:
Ung Bướu CS1/CS2 → `302`; Thiên Nam, Hoàng Dũng → `200`.
