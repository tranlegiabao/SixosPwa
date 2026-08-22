# SixosPwa lưu mật khẩu ở dạng đọc lại được, không băm

`TaiKhoan.MatKhau` bên `HIS_CSKH` lưu mật khẩu **đọc lại được** thay vì băm. Đây là deviation cố ý và
trái trực giác của mọi người đọc code, nên ghi lại để không ai "sửa" nó.

Lý do bắt buộc: để bàn giao phiên sang UB ở những lần sau, SixosPwa phải POST **nguyên văn** mật khẩu
tới `POST /HeThong/HT_DangNhap/login` của UB. Hàm `LoginAsync` (`HtDangNhapServices.cs:327-341`) so
sánh `x.MatKhau == password` bằng **chuỗi thuần** — bản thân UB cũng không băm. Băm ở phía ta thì
không còn gì để gửi đi, và bệnh nhân sẽ phải gõ lại mật khẩu mỗi lần bàn giao.

## Consequences

- Mật khẩu bệnh nhân tồn tại ở dạng đọc được ở **hai** hệ (`HIS_CSKH.TaiKhoan` và
  `UB_DangKyOnline.HtTaiKhoan`). Rò một chỗ là mất cả hai.
- Mật khẩu này **chỉ dùng cho trang đối tác**, không phải để đăng nhập SixosPwa — SixosPwa dùng OTP.
  Đừng tái sử dụng cột này làm credential đăng nhập nội bộ.
- Khi nào UB băm mật khẩu (hoặc mở SSO), ADR này nên được thay thế và cột chuyển sang băm hoặc bỏ hẳn.
