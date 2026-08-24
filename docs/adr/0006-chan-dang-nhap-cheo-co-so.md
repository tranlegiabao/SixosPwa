# Chặn đăng nhập chéo cơ sở bằng modal, không đổi claim âm thầm

Trước đây, bệnh nhân đang có phiên ở cơ sở A mà bấm "Đăng nhập"/"Đăng ký khám" trên trang cơ sở B thì
`DangNhapController.DiTiep` **âm thầm đổi claim `MaCoSo`** sang B (`DoiCoSoTrongPhienAsync`) — bệnh
nhân không được hỏi, không biết mình vừa "rời" cơ sở A. Với luồng bàn giao sang hệ đối tác (Ung Bướu…)
đây là rủi ro thật: đổi cơ sở âm thầm giữa chừng có thể kéo theo bàn giao sai cơ sở.

**Quyết định:** chặn bằng **modal**, không đổi claim tự động. Điều kiện chặn là **khác `MaCoSo`**
(không phải khác hệ đối tác `TrangChu`) — kể cả hai cơ sở cùng một hệ đối tác (vd hai chi nhánh Ung
Bướu) vẫn bị chặn, vì mỗi cơ sở là một phiên riêng theo thiết kế của `LuongCongBenhNhan`.

## Cơ chế

- **Điểm chặn duy nhất là màn `/DangKyOnline/{slug}` (`ChiTietCoSo.cshtml`)** — cả hai lối vào (bấm
  nút trên trang, hoặc gõ tay/bookmark `/DangNhap/Login?coSo=B`) đều quy về đây, để không lệch thông
  điệp giữa hai nơi. `Login` GET action phát hiện lệch cơ sở (dùng biến `maCoSoDich` vốn đã tính sẵn
  trong code cũ nhưng chưa từng dùng tới) thì `Redirect` ngược về
  `/DangKyOnline/{B}?canhBao=1&returnUrl=...` thay vì tự vẽ UI ở màn đăng nhập.
- Modal có 2 nút: **"Đăng xuất và đăng nhập lại"** (đỏ, chính) và **"Quay lại"** (đóng modal, ở lại đọc
  trang, phiên A không đổi gì). Bấm ra ngoài hộp cũng đóng như "Quay lại" — mở nhầm trang cơ sở khác
  chưa chắc đã muốn bỏ phiên đang có.
- "Đăng xuất và đăng nhập lại" đi qua `DangXuat(denCoSo, returnUrl)` — tham số mới, chỉ dùng cho
  nhánh này; đăng xuất bình thường (từ menu) không truyền `denCoSo` và giữ nguyên hành vi cũ (về lại
  cơ sở vừa đăng xuất).
- `DiTiep` không còn nhận tham số đổi cơ sở nữa — chỉ còn chạy cây quyết định
  (`ChonDichDenAsync`) cho `MaCoSo` của phiên hiện tại. Việc "khác cơ sở" đã bị chặn từ lối vào, nên
  nhánh đổi claim âm thầm trở thành code chết và đã gỡ.

## Consequences

- Bệnh nhân phải đăng xuất tường minh mới đổi được cơ sở — chậm hơn một bước so với đổi âm thầm, đổi
  lại là không còn tình huống "đang ở cơ sở nào cũng không rõ".
- Modal là màn hoàn toàn mới (`ChiTietCoSo.cshtml`), không dùng Bootstrap modal component — dựng nhẹ
  bằng lớp phủ `position:fixed` để không kéo thêm phụ thuộc trên một trang vốn đã ẩn `_Layout`.
- `returnUrl` đi qua `Url.IsLocalUrl` trước khi ghép vào link đăng xuất, chặn lỗ open-redirect nếu ai
  đó chèn `returnUrl` trỏ ra ngoài domain.
