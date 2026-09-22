# 0013 — `Active` là cổng hiển thị duy nhất, khai tử `XacMinh`

- **Tác giả:** Nam · **Ngày:** 2026-08-26 · **Trạng thái:** Đã chấp nhận
- **Bối cảnh liên quan:** [0006](0006-chan-dang-nhap-cheo-co-so.md) ·
  [0008](0008-moi-duong-ghi-qua-stored-procedure.md) · [0011](0011-co-so-thuoc-doi-tac-mot-nhieu.md)

## Bối cảnh

Form chỉnh sửa cơ sở y tế có một toggle nhãn "Đã xác minh và cho phép hiển thị". Bật tắt nó **không
đổi gì cả**.

Chẩn đoán không giống điều người ta tưởng: nó **không chết ở đường ghi**. Giá trị đi trọn vẹn từ
`Edit.cshtml` qua `CoSoYTeController` xuống `AdminStoredProcedureService` vào thủ tục `DM_CSKCB_Save`,
và cột `DM_CSKCB.XacMinh` được cập nhật đúng. Nó **chết ở đường đọc**: không một truy vấn hiển thị nào
đọc cột đó. Danh sách công khai chỉ lọc theo nhóm cơ sở; trang chi tiết không kiểm gì. Hai chỗ duy
nhất đọc `XacMinh` là huy hiệu trong bảng admin và một biến đếm ở Dashboard **không được render ra
view nào**. Dòng chữ "Hiển thị công khai theo trạng thái xác minh" trên bảng admin là lời hứa suông.

Cùng lúc, bảng mang **cột cờ thứ hai** là `Active`, ở tình trạng ngược lại: không có ô nào trên form,
bị ép về `1` mỗi lần bấm Lưu (ViewModel để mặc định `true`, thủ tục `SET Active = @Active` vô điều
kiện) — nhưng lại là cột **duy nhất đang được lọc thật**, ở `DM_CSKCB_TopQuangCao`.

Nói cách khác: cột có ý nghĩa thì không ai đọc, cột được đọc thì không ai điều khiển được.

Số liệu lúc quyết định: **11/11 cơ sở đang mang `XacMinh = 1` và `Active = 1`**. Không dòng nào tắt,
nên bật bộ lọc không làm biến mất cơ sở nào, và cả hai cột đều xoá đi được mà không mất thông tin.

## Quyết định

**`DM_CSKCB.Active` là cổng hiển thị duy nhất. Cột `XacMinh` bị xoá khỏi bảng.**

Cờ này quyết định hai thứ, không phải một:

1. Cơ sở có xuất hiện ở cổng công khai không — danh sách theo nhóm, trang chi tiết, URL cũ, khối quảng
   cáo trang chủ.
2. Cơ sở có **nhận đăng nhập / đăng ký mới** không.

Nó **không** đụng tới phiên đã đăng nhập. Ẩn một cơ sở nghĩa là thôi quảng bá và thôi nhận người mới,
**không phải** khoá tài khoản người cũ.

Kèm theo, **default của `Active` đổi từ `((1))` sang `((0))`**, và form Tạo mới cũng mặc định tắt.

## Vì sao không chọn cách khác

**Giữ `XacMinh`, xoá `Active`** là hướng được cân nhắc đầu tiên, và nó có một lợi thế thật: default sẵn
là `((0))` nên hỏng theo hướng an toàn, lại không phải đụng tới view, huy hiệu hay Dashboard. Bỏ qua vì
`Active` là **quy ước của cả schema** — nó có mặt ở `DM_ChuDe`, `DM_NhomCS`, `DM_DoiTacApi`,
`DM_CSKCB_CapQuangCao` và `DM_CSKCB`, trong khi `XacMinh` chỉ tồn tại đúng ở một bảng. Xoá `Active`
khỏi `DM_CSKCB` sẽ biến nó thành bảng danh mục duy nhất thiếu cột đó, và người đọc schema về sau sẽ
vấp. Điểm yếu default được bù bằng cách đổi thẳng ràng buộc, nên nó không còn là lý do để chọn.

**Giữ cả hai cột, phân vai rõ** (`XacMinh` = đã duyệt nội dung, `Active` = còn hợp tác) trung thực nhất
với dữ liệu. Bỏ qua vì hai cờ đều đang là `1` ở toàn bộ 11 dòng — chưa có nhu cầu thật nào tách hai
trạng thái đó, và hai toggle cạnh nhau đẻ ra câu hỏi "hai nút này khác gì nhau" cho người dùng.

**Chỉ vá bug ép `Active = 1` mà không xoá cột nào** là ít việc nhất. Bỏ qua vì để nguyên hai cột cờ mà
chỉ một cột có tác dụng chính là cái bẫy đã sinh ra vấn đề này ngay từ đầu.

**Giữ `Active` với default `((1))`** đỡ được một bước DDL. Bỏ qua vì hỏng theo hướng mở: bất kỳ cơ sở
nào chèn thẳng vào DB đều lên công khai ngay trước khi có người xác minh.

## Hệ quả

- **Cột `XacMinh` biến mất vĩnh viễn.** Việc này chia làm hai file `.sql` theo mốc deploy: file `01`
  gỡ cột khỏi thủ tục và đổi default (chạy lúc nào cũng được, có rollback đối xứng); file `02` mới
  `DROP COLUMN`, và **chỉ được chạy sau khi bản mới của app đã lên**. Chạy `02` khi bản cũ còn sống là
  vỡ toàn bộ app, vì EF vẫn sinh `SELECT … [XacMinh]`. File `02` có câu tự chặn nếu `01` chưa chạy,
  nhưng "app bản mới đã lên chưa" thì máy chủ không biết được — người chạy phải tự bảo đảm.
- **`DM_CSKCB_TopQuangCao` không phải sửa một chữ nào** — nó vốn đã lọc `WHERE c.Active = 1`. Đây là
  món lợi bất ngờ của việc chọn `Active`. 🔴 Vế `q.Active = 1` ngay dòng trên là cờ của
  `DM_CSKCB_CapQuangCao`, cấm đụng.
- 🔴 **Ba cột cùng tên `Active`, ba nghĩa khác nhau.** `PartnerGatewayFactory` đọc
  `DM_DoiTacApi.Active` (đăng ký API của cơ sở còn hiệu lực không) và `DM_CSKCB_TopQuangCao` đọc
  `DM_CSKCB_CapQuangCao.Active` (cấp quảng cáo còn hiệu lực không). Cả hai **không liên quan** tới cổng
  hiển thị. Đây là cái giá đã biết trước của việc chọn một cái tên đang được dùng rộng.
- **Chặn đăng nhập cần bốn điểm, không phải một.** Không có chỗ nghẽn nào cắm được một guard duy nhất:
  hai luồng "phiên mới" và "phiên cũ" hội tụ đúng tại `ChonDichDenAsync`, chặn ở đó là đá luôn người
  đang đăng nhập. Bốn điểm: hai cửa sinh phiên (`XacNhanOtp`, `XacNhanFirebaseToken` — đặt **trước**
  `SignInAsync`), đường mở tài khoản (`MoTaiKhoanAsync`), và màn Login GET để khỏi bắt bệnh nhân gõ hết
  OTP rồi mới bị từ chối.
- 🔴 **Guard ở `XacNhanOtp` bắt buộc có vế `!adminReauth`.** Action đó phục vụ **cả** re-auth của
  Admin/Đối tác; thiếu vế này là khoá luôn đường đăng nhập quản trị.
- **Hai đường vẫn sinh hồ sơ mới tại cơ sở đã ẩn**, có chủ ý: `XacNhanLienKetAsync` và
  `BaoDamHoSoNoiBoAsync`. Cả hai chỉ chạy từ phiên **đã** đăng nhập, nên chặn chúng sẽ đi ngược nguyên
  tắc "không đá người cũ" — và `BaoDamHoSoNoiBoAsync` mà chặn thì phiên cũ chưa có hồ sơ nội bộ sẽ gãy
  khi vào trang bệnh nhân.
- **Khách vãng lai mở URL của cơ sở đã ẩn bị đẩy đi im lặng** — 302 về danh sách công khai của nhóm,
  không băng thông báo. Cơ sở không suy được nhóm (`IdNhomCS` rỗng) thì về trang chủ.
- **Quản trị viên không được đặc cách.** Muốn xem cơ sở đang ẩn trông thế nào thì bấm "Xem trước" trong
  form — đường đó chạy riêng, không đi qua route công khai nên không bị chặn.
