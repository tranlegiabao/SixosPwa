# Băm mật khẩu đăng nhập nội bộ, tách hẳn khỏi mật khẩu đối tác

[ADR 0005](0005-luu-mat-khau-khong-bam.md) cho phép lưu mật khẩu ở dạng đọc lại được — nhưng chỉ cho
**mật khẩu đối tác**, và đã ghi rõ ở phần Consequences: *"Đừng tái sử dụng cột này làm credential đăng
nhập nội bộ."*

Audit 2026-08-24 phát hiện code đang làm **đúng cái điều ADR 0005 cấm**. Tại
`Controllers/DangNhapController.cs:184-192`, cùng một endpoint `XacNhanOtp` và cùng một ô nhập: nếu
`Role` là `Admin` hoặc `DoiTac` thì nội dung ô đó được so **`string.Equals` thuần** với
`TaiKhoan.MatKhau`; chỉ khi là bệnh nhân nó mới là OTP thật. Cột lại là `nvarchar(20)`, tức mật khẩu
quản trị bị chặn ở 20 ký tự.

Trong khi đó mật khẩu mà ADR 0005 thực sự bảo vệ nằm ở một cột khác hẳn: `TaiKhoan_DoiTac.MatKhau`, do
`Services/Partner/LuongCongBenhNhan.cs:67 SinhMatKhauChoDoiTac()` sinh ra.

Từ đợt này hai khái niệm được tách bằng hai cột mang hai luật khác nhau:

| Cột | Dùng để | Lưu thế nào |
|---|---|---|
| `HT_TaiKhoan.MatKhauNoiBo` | đăng nhập Admin / Đối tác vào SixosPwa | **băm** |
| `HT_TaiKhoanDoiTac.MatKhau` | POST nguyên văn sang hệ đối tác khi bàn giao | **không băm** — ADR 0005 vẫn nguyên hiệu lực |

## Consequences

- **T-SQL không băm được mật khẩu** (hàm băm nằm ở tầng C#), nên migration `V006` để `MatKhauNoiBo` là
  `NULL` chứ không chuyển chuỗi cũ sang. Sau bước đó **2 tài khoản** (1 `Admin` + 1 `DoiTac`) không đăng
  nhập được cho tới khi đặt lại mật khẩu qua ứng dụng. Đường đặt lại phải có sẵn **trước** khi chạy
  `V006`, không phải sau.
- Vì lý do trên, schema **cố ý không** đặt `CHECK` kiểu "Admin/DoiTac bắt buộc có `MatKhauNoiBo`" — ngay
  sau migration cột đó buộc phải rỗng. Luật "không có mật khẩu thì không đăng nhập được" thuộc về tầng
  ứng dụng.
- Mật khẩu bệnh nhân vẫn tồn tại ở dạng đọc được tại **hai** hệ (`HT_TaiKhoanDoiTac.MatKhau` và
  `UB_DangKyOnline.HtTaiKhoan`) — hệ quả này của ADR 0005 **không** được đợt này giải quyết.
- Khi nào hệ đối tác băm mật khẩu (hoặc mở SSO), ADR 0005 nên bị thay thế và cột kia chuyển sang băm
  hoặc bỏ hẳn. ADR này không đụng tới điều đó.

## Đính chính 2026-08-25 — phần BĂM chưa được thi hành

Đợt 2 đã chạy (`V000`…`V008` + 17 thủ tục + sửa code). Quyết định **tách hai khái niệm mật khẩu vẫn
nguyên hiệu lực**: `HT_TaiKhoan.MatKhauNoiBo` và `HT_TaiKhoanDoiTac.MatKhau` nay là hai cột riêng mang
hai luật riêng, và mọi đường ghi đã đi qua stored procedure.

**Nhưng phần *băm* thì chưa làm.** Người đọc bản gốc ở trên rất dễ tưởng cột `MatKhauNoiBo` đang chứa
chuỗi đã băm — không phải. Sự thật sau Đợt 2:

- Trong toàn bộ codebase **không có một hàm băm nào**. Chỗ so mật khẩu vẫn là `string.Equals` thuần
  (`Controllers/DangNhapController.cs`, `Areas/Admin/Controllers/DangNhapController.cs`).
- Thủ tục `HT_TaiKhoan_Save` có sẵn tham số `@MatKhauNoiBoDaBam`, nhưng **mọi nơi gọi đều truyền
  `null`** — cố ý, để chỗ nối sẵn cho đợt sau.
- `V006` đổ `MatKhauNoiBo` sang là `NULL` như thiết kế, nên **hai tài khoản mất đăng nhập**:
  `ID = 1` (`DoiTac`, SĐT `0833405847`) và `ID = 2` (`Admin`, SĐT `0389926996`).

**Vì sao chấp nhận:** người dùng chốt ngày 25/08 sau khi được nêu rõ hệ quả. Hai mật khẩu cũ chỉ dài
**một ký tự** — mật khẩu dev, mất không tiếc — và không có ai đang phụ thuộc vào việc đăng nhập khu
Admin trong lúc chờ.

**Mật khẩu cũ chưa mất hẳn:** chúng còn nguyên trong `ZZ_TaiKhoan_cu` cho tới khi chạy `V009`. `V009`
không nằm trong Đợt 2.

**Điều kiện để gỡ đính chính này** (làm ở đợt sau, ba việc nhỏ):

1. Dùng `PasswordHasher<T>` của ASP.NET Core — nằm sẵn trong shared framework `Microsoft.AspNetCore.App`,
   **không phải thêm gói NuGet**.
2. Đổi hai chỗ so chuỗi thuần sang `VerifyHashedPassword`.
3. Sinh băm cho hai tài khoản trên rồi truyền vào `@MatKhauNoiBoDaBam` — tham số đã có sẵn, không phải
   sửa thủ tục.

**Hệ quả còn treo:** khu Admin không đăng nhập được cho tới khi làm xong ba việc trên, nên nghiệm thu
Đợt 3 (Playwright ba vai admin / bệnh nhân / khách) sẽ vướng vai admin.
