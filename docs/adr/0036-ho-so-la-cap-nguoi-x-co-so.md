# 0036 — Hồ sơ là cặp (người × cơ sở), và điều đó đảo ba ADR cũ

- **Tác giả:** Nam · **Ngày:** 2026-09-19 · **Trạng thái:** Đề xuất
- **Thay thế:** [0011](0011-co-so-thuoc-doi-tac-mot-nhieu.md) ·
  [0019](0019-mot-tai-khoan-nhieu-ho-so.md) ·
  [0027](0027-dang-nhap-duoc-thi-phai-co-tai-khoan.md)
- **Bối cảnh liên quan:** [0018](0018-luat-gop-ho-so-cccd-ten-ngaysinh.md) ·
  [0020](0020-tin-cccd-o-loi-vao-chan-o-tang-tai-lieu.md) ·
  [0032](0032-mot-ho-so-giu-dung-mot-ma.md) ·
  [0040](0040-idtaikhoantheosdt-soi-guong-idbenhnhan.md) ·
  [0035](0035-hop-dong-linked-server-spwa-cong.md)

## Bối cảnh

Đợt 1B gộp `DM_BenhNhanCoSo` vào `DM_BenhNhan`. Một dòng nay **là** một hồ sơ tại một cơ sở: một
người khám ở N cơ sở thì có N dòng. Đây là *"chấp nhận lặp dòng, không tối ưu SQL"* mà sếp yêu cầu.

Thay đổi đó kéo theo ba ADR đang ở trạng thái *Đã chấp nhận* nói ngược lại. Ghi một ADR chung thay vì
ba, vì cả ba đảo **cùng một lý do**: khái niệm neo danh tính đổi từ *tài khoản* sang *cặp (SĐT × cơ sở)*.

## Quyết định

### Đảo ADR 0027 — *"đăng nhập được thì phải có `HT_TaiKhoan`"*

Bất biến **giữ nguyên hình dạng**, chỉ đổi bảng neo:

> Một người đăng nhập được vào một cơ sở **khi và chỉ khi** tồn tại dòng `DM_BenhNhan` có `SDT` của họ
> **và** `IdCoSo` của cơ sở đó.

Không còn bảng trung gian. `HT_TaiKhoan` chỉ còn Admin (`CK_HT_TaiKhoan_Role CHECK (Role = 'Admin')`);
13.152 dòng vai trò `BenhNhan` đã bị xoá, trong đó 13.000 vốn là seed giả — bỏ lớp này gần như chỉ là
dọn rác. Câu báo lỗi đang hiển thị, *"Số điện thoại chưa có hồ sơ tại cơ sở y tế"*, trước 1B **nói chặt
hơn code**; sau 1B mới đúng nghĩa đen.

🔴 Bất biến này phải được canh ở **ba** cửa, không phải hai: `GuiOtp`, nhánh OTP, nhánh Firebase. Kế
hoạch 1B chỉ liệt kê hai cửa sau; cửa `GuiOtp` bị bỏ sót và **chặn sạch mọi bệnh nhân** cho tới khi
chạy thật mới lộ ra. Thêm một cửa nữa thì thêm một chỗ phải sửa — đừng tin danh sách, hãy grep.

🔴 Và khối điền `MaCoSo` mặc định phải chạy **trước** cửa chắn. Điều kiện cũ không dùng tới `MaCoSo`
nên đặt đâu cũng được; điều kiện mới thì có. Giữ nguyên thứ tự cũ là phiên không mang `MaCoSo` bị chặn
sạch — kể cả người **có** hồ sơ.

### Đảo ADR 0019 — *"một tài khoản quản nhiều hồ sơ, ai khai trước giữ CCCD"*

- Vế 1 (*một tài khoản nhiều hồ sơ*) → **một cặp (SĐT × cơ sở) giữ nhiều hồ sơ**, và đó là hành vi
  mặc định. Bật `HT_Config.MOT_HO_SO` thì siết về đúng một hồ sơ, chọn theo **CCCD của phiên**.
- Vế 2 (*ai khai trước giữ CCCD*) **bị bỏ**. Nó dựa vào `UK_DM_BenhNhan_CCCD` — UNIQUE CCCD toàn hệ —
  mà khoá đó nay là `UNIQUE(IDCoSo, CCCD)`: cùng một người **được phép** có hồ sơ ở nhiều cơ sở, nên
  không còn ai "giữ" CCCD của ai. `DM_BenhNhan_NhanChuSoHuu` theo đó thành no-op.

### Đảo ADR 0011 — *"cơ sở thuộc đối tác theo quan hệ một–nhiều"*

Bảng `DM_DoiTac` bị bỏ; khái niệm còn lại là một cột phẳng `DM_CSKCB.TenCongTy`. Số đo quyết định:
**2 dòng** đối tác, và **1/12 cơ sở** từng có giá trị — không đáng một bảng, càng không đáng một khoá
ngoại. Bỏ bảng cũng đóng luôn lỗ lộ mật khẩu: `MatKhauDoiTac` được in thô vào HTML qua `data-password`,
rồi "phép xác thực" so lại đúng chuỗi mà trang vừa tự điền vào ô.

## Hệ quả

**Được:** bớt một bảng trung gian trên đường nóng nhất; câu hỏi *"người này vào được cơ sở này chưa"*
trả lời bằng **một** phép `EXISTS` trên **một** bảng; và nó là **cùng một câu hỏi** mà hợp đồng linked
server hỏi (ADR 0040) — trước đây hai tầng trả lời hai kiểu.

**Mất:** một người ở N cơ sở thì N dòng, và các dòng đó **trôi độc lập** — sửa tên ở cơ sở A không
sang cơ sở B. Đây là cố ý (C12), không phải bỏ sót.

**Cái giá đã trả, đo được:** luật *một SĐT một hồ sơ tại một cơ sở* không đặt được ở tầng DB — 807 nhóm
vi phạm sẵn nên `CREATE UNIQUE INDEX (SDT, IDCoSo)` sẽ nổ. Luật sống ở tầng ứng dụng/stored, **không có
lưới an toàn ở DB**. Dọn một lần đã gỡ SĐT của **1.336** dòng (729 là bệnh nhân thật, giữ 30.175 tài
liệu + 3.837 đợt khám); cộng 280 dòng vốn rỗng ⇒ **1.616 hồ sơ không có lối vào** ngay sau khi chạy.
Và việc dọn **tự rữa**: `DM_BenhNhan_Save` có `SDT = ISNULL(@SDT, SDT)` nên HIS đẩy sang là SĐT quay
lại ⇒ đây phải là **việc chạy định kỳ**, không phải một `.sql` chạy một lần. Mặt tốt: 729 người chỉ mất
đường vào **tới lần khám kế tiếp**, không mất hẳn.
