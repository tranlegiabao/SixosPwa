# Đăng nhập được thì phải có tài khoản — chặn OTP khi chưa biết cơ sở

`XacNhanOtp` cấp cookie **vô điều kiện** sau khi OTP đúng, nhưng dòng `HT_TaiKhoan` chỉ được tạo bên
trong `BaoDamHoSoNoiBoAsync`, mà hàm đó **thoát ngay khi không có cơ sở** (`idCoSo is null`). Hệ quả là
một trạng thái hệ thống không tự thoát ra được: **đã đăng nhập, mà không có tài khoản** — mọi màn phía
sau chỉ biết nói *"Không tìm thấy tài khoản."*, không đường ra, không lời chỉ dẫn.

Đây không phải URL exotic. Hai lối vào sống thật dẫn tới đúng nhánh đó:

- `Program.cs:60` đặt `LoginPath = "/DangNhap/Login"` — **trần, không `coSo`**. Đo thật 10/09: mở
  `/benh-nhan/ho-so` khi chưa đăng nhập trả `302 → /DangNhap/Login?ReturnUrl=%2Fbenh-nhan%2Fho-so`.
  Ai lưu dấu trang màn *Hồ sơ của tôi* rồi quay lại sau khi hết phiên là rơi vào đây.
- `ChiTietCoSo.cshtml:1086` — cơ sở nào thiếu `Slug` thì chính hai nút *Đăng ký khám* trên trang cơ sở
  đó cũng trỏ tới Login trần. Hôm nay 12/12 cơ sở đều có slug nên chưa nổ; chốt duy nhất giữ nó là **dữ
  liệu**, không phải mã.

## Quyết định

**Bất biến: đăng nhập thành công ⇒ tồn tại `HT_TaiKhoan`.** Thi hành bằng cách **chặn ở `XacNhanOtp`**
(và `XacNhanFirebaseToken` — bỏ sót cửa này là mở cửa lách): không có `MaCoSo` **và** định danh chưa có
`HT_TaiKhoan` ⇒ **không cấp cookie**, trả thông báo rồi tự đưa về danh sách cơ sở.

Ranh giới đặt ở **"và"**, không phải "hoặc". Người **đã có** tài khoản vẫn đăng nhập được từ Login trần,
không bị chặn. Chỉ chặn đúng tập mà hệ thống **không thể** cấp tài khoản cho — người mới, đến mà không
mang theo cơ sở nào.

⚠️ Đo thật 10/09 sau khi vá: người đã có tài khoản vào được, nhưng **hạ cánh ở `/benh-nhan` chứ không
phải đúng dấu trang họ bấm** — `ChonDichDenAsync` trả thẳng `"/benh-nhan"` và **bỏ `returnUrl`** khi
không có mã cơ sở (`DangNhapController.cs`, nhánh `maCoSo` rỗng). Đây là hành vi **có sẵn từ trước**,
không phải do quyết định này sinh ra, và **chưa được sửa** — ghi lại để không ai tưởng dấu trang đã
chạy đúng.

## Considered Options

- **Tự cấp tài khoản khi chưa biết cơ sở** — sửa `BaoDamHoSoNoiBoAsync` để vẫn tạo `HT_TaiKhoan` +
  `DM_BenhNhan` (hai bảng này vốn không gắn cơ sở), chỉ bỏ qua dòng `DM_BenhNhanCoSo`. Kiến trúc sạch
  hơn và không chặn ai. **Bị loại**: một tài khoản sinh ra ngoài mọi cơ sở là một danh tính không ai
  chịu trách nhiệm, và cổng này tồn tại để phục vụ *một cơ sở cụ thể*.
- **Chặn mọi lần đăng nhập không kèm cơ sở**, kể cả người đã có tài khoản. Luật một câu, dễ kiểm. **Bị
  loại**: biến mọi dấu trang màn nội bộ thành vô dụng cho cả người đã dùng lâu.
- **Chỉ sửa lời ở ngõ cụt** — thay *"Không tìm thấy tài khoản."* bằng lời chỉ đường. **Bị loại**: đổi
  một ngõ cụt câm lấy một ngõ cụt có lời, trạng thái quái đản vẫn còn đó.
- **Đổi `LoginPath` sang trang chọn cơ sở**. **Bị loại**: bịt đúng lối vào nhưng làm mất `returnUrl`.

## Consequences

- Nhánh **re-auth Admin/Đối tác đi chung action này** nên phải được **miễn trừ** (`adminReauth`) — quên
  là khoá luôn đường đăng nhập quản trị. Đã có tiền lệ y hệt ở ADR 0013.
- `/DangNhap/Login?coSo=<giá trị lạ>` từ nay **đá về `/`** thay vì lặng lẽ tụt xuống chế độ tối giản —
  bám đúng khuôn nhánh `Active = 0` đã có sẵn ngay cạnh (`DangNhapController.cs:56`). `?coSo=` vẫn chỉ
  nhận **slug**; không nhận mã cơ sở, để một cơ sở không có hai danh tính trên URL.
- Chế độ Login trần **vẫn sống** cho người đã có tài khoản và cho re-auth Admin. Không xoá.
- Nhánh Firebase trong `Login.cshtml` đang **nuốt** `data.message` (hiện cứng *"Lỗi tạo phiên đăng
  nhập!"*). Phải sửa cùng đợt, không thì thông báo mới không bao giờ tới được người dùng.
