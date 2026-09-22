# 0022 — Khoá API ở bảng riêng, băm, và có công tắc riêng

- **Tác giả:** Nam · **Ngày:** 2026-09-08 · **Trạng thái:** Đã chấp nhận
- **Bối cảnh liên quan:** [0008](0008-moi-duong-ghi-qua-stored-procedure.md) ·
  [0013](0013-active-la-cong-hien-thi-duy-nhat.md) · [0017](0017-hai-chieu-theo-loai-du-lieu.md) ·
  [0021](0021-tu-choi-tai-lieu-mo-coi.md)

## Bối cảnh

Khu API nhận cần biết cuộc gọi đến từ cơ sở nào. Bản dựng ngày 08/09 thêm cột `DM_CSKCB.ApiKey`, lưu khoá
**thô**, so sánh chuỗi trực tiếp, và chặn cơ sở bằng chính `DM_CSKCB.Active`.

Ba điểm gãy, theo thứ tự nặng dần:

1. **Khoá nằm thô trong cơ sở dữ liệu.** Ai đọc được bảng cơ sở là có khoá của mọi cơ sở.
2. **Một cơ sở một khoá.** Muốn thay khoá thì phải đổi cột và đổi cấu hình HIS đúng cùng lúc — giữa hai
   thời điểm đó đường đẩy chết.
3. **`Active` bị gán nghĩa thứ ba.** ADR 0013 chốt cờ này quyết định đúng hai thứ: cơ sở có hiện ở cổng
   công khai, và có nhận đăng nhập/đăng ký mới. Nay nó gánh thêm "cơ sở có được đẩy dữ liệu vào không".
   Cảnh huống làm nó cắn: người quản trị tạm ẩn một cơ sở khỏi trang quảng bá để sửa nội dung — HIS của
   cơ sở đó **im lặng ngừng đẩy được**, kết quả mới của bệnh nhân cũ biến mất, và không ai được báo gì.
   Đúng loại lỗi im lặng mà ADR 0013 sinh ra để dẹp.

## Quyết định

**Khoá API sang bảng riêng `HT_KhoaApiCoSo`**, với ba tính chất:

- Lưu **bản băm** (SHA-256) chứ không lưu khoá thô. Cổng bằm khoá người gọi gửi lên rồi tra theo bản băm.
  Bằm theo **byte UTF-8** ở cả hai phía — chỗ này dễ sai: bằm thẳng `nvarchar` trong SQL Server ra byte
  UTF-16 và hai đầu không bao giờ gặp nhau.
- Một cơ sở mang **nhiều khoá** cùng lúc, nên xoay khoá là: cấp khoá mới → cơ sở đổi cấu hình HIS → tắt
  khoá cũ. Không có khoảng chết, không phải canh giờ.
- Có **`Active` riêng của khoá**. Cắt đường API không đụng gì tới cờ hiển thị của cơ sở, nên ADR 0013 giữ
  nguyên. Đây là cách khuôn LIS bên HisSoft vẫn đang cắt đối tác (`LIS_TaiKhoan.Active = 0`).

Kèm theo: khoá đúng nhưng gõ nhầm mã cơ sở ở header thì **chặn**, không lặng lẽ lấy cơ sở theo khoá — nếu
không, một cơ sở gõ nhầm sẽ đẩy dữ liệu của mình vào hồ sơ của cơ sở khác mà không ai biết.

Việc bỏ cột `DM_CSKCB.ApiKey` **tách ra một bước riêng chạy sau**, vì khu tài liệu còn đang đọc cột đó và
bỏ ngay là làm vỡ nhánh đang chạy chung.

## Vì sao không chọn cách khác

**Giữ khoá trên `DM_CSKCB`, chỉ thêm `ApiKeyHash` và `ApiActive`** vá được hai điểm gãy nặng nhất mà gần
như không phải đổi hình dạng code đã viết. Bỏ qua vì nó không giải quyết được điểm thứ hai: một cơ sở vẫn
chỉ một khoá, nên mọi lần xoay khoá — kể cả lần xoay vì nghi khoá bị lộ, đúng lúc cần nhanh nhất — vẫn có
một khoảng chết phải canh. Thêm một bảng cho 11 cơ sở nghe như thừa, nhưng thứ đang mua là *xoay được
khoá*, không phải chỗ chứa.

**Bê nguyên khuôn LIS: cổng cấp đường đăng nhập để HIS đổi tài khoản/mật khẩu lấy token có hạn.** Đồng
nhất tuyệt đối với thứ đội đã quen, và là khuôn đang chạy thật. Bỏ qua vì chiều này chỉ có **máy gọi máy
qua HTTPS**, không có người ngồi gõ mật khẩu; đổi lại phải nuôi thêm cả cơ chế phát, thu hồi và gia hạn
token. Một khoá tĩnh đã băm, xoay được, có công tắc riêng và có nhật ký thì đủ cho chiều này. Nếu sau này
cổng phải phục vụ đối tác không tin cậy bằng cơ sở của chính mình, quyết định này nên xem lại.

**Chỉ vá cột `ApiKey` cho khỏi lưu thô mà không đụng tới `Active`** là ít việc nhất. Bỏ qua vì điểm gãy
thứ ba mới là cái gây mất dữ liệu lâm sàng trong im lặng, còn hai điểm kia mới chỉ là rủi ro.
