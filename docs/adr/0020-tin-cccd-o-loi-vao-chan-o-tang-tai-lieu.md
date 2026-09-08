# 0020 — Tin CCCD ở lối vào, đặt cửa chắn ở tầng tài liệu

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-09-08
- **Bối cảnh liên quan:** [0018](0018-luat-gop-ho-so-cccd-ten-ngaysinh.md) — gộp sai là lộ hồ sơ y tế.
  [0019](0019-mot-tai-khoan-nhieu-ho-so.md) — một tài khoản nhiều hồ sơ.
  [0017](0017-hai-chieu-theo-loai-du-lieu.md) — chia theo loại dữ liệu.

## Bối cảnh

`DangNhapController.cs:348-350` nhận CCCD **thẳng từ thân request** rồi đóng vào claim của phiên;
OTP chỉ xác thực **số điện thoại**. Không chỗ nào kiểm CCCD đó có phải của người cầm máy không. Chính
code đã ghi nhận nguy cơ ở `:775-776`.

Tới trước giai đoạn 2, hậu quả còn nhẹ: cùng lắm là mở một tài khoản. Từ giai đoạn 2, CCCD trở thành
**chìa để kéo hồ sơ khám và kết quả cận lâm sàng** từ HIS về.

Xác minh CCCD cho đúng thì phải đối chiếu với một nguồn ngoài tầm — cổng không có, và đòi bệnh nhân
chứng minh danh tính ngay ở cửa thì rớt người dùng tại chỗ. Số đo cho biết cái gì có sẵn để dùng làm
bằng chứng thay thế: trong 59.757 bệnh nhân Thiên Nam có CCCD hợp lệ, **54.399 (91,0%)** có luôn số
điện thoại trên hồ sơ HIS.

## Quyết định

Chia làm hai tầng thay vì đặt một cửa duy nhất ở lối vào:

1. **Lối vào — tin CCCD**, giữ nguyên hiện trạng. Qua OTP rồi gõ CCCD nào thì đứng tên CCCD đó. Tầng
   này cho xem **tóm tắt đợt khám**: ngày giờ, khoa, bác sĩ, chẩn đoán chính, mã vào viện.
2. **Tài liệu — có cửa riêng.** Kết quả cận lâm sàng và đơn thuốc chỉ mở khi **số điện thoại trên hồ
   sơ HIS trùng số điện thoại của tài khoản**; lệch, hoặc HIS không có số, thì bệnh nhân gõ **Mã BN**
   in trên phiếu đúng một lần rồi ghi cờ vào hồ sơ.

Chống dò: Mã BN ở Thiên Nam chỉ 6 chữ số nên dò được — khoá 15 phút sau 5 lần sai, đếm theo cả tài
khoản lẫn Mã BN bị thử. Và phải chặn các số điện thoại gắn với quá nhiều người: có số đang gắn 876
người, gần như chắc chắn là số của phòng khám bị nhân viên nhập nhầm hàng loạt.

## Hệ quả

- Điều kiện SĐT ở tầng 2 **đổi nghĩa** trong mô hình nhiều hồ sơ (ADR 0019): nó không còn nói *"bạn
  chính là người này"* mà nói *"cơ sở đã ghi bạn là đầu mối liên lạc của người này"*. Đó chính là
  4.140 số dùng chung — vốn bị coi là bẫy, nay thành đúng cái ta cần: người nhà lo cho nhau.
- Đổi lại, **người nhà tự mở được tài liệu của nhau** mà không ai bấm duyệt. Đã biết và chấp nhận.
- Trang bệnh nhân có **hai mức quyền trên cùng một màn** — cần một cột cờ trên `DM_BenhNhanCoSo` ghi
  hồ sơ nào đã qua cửa, và một trạng thái hiển thị "còn khoá" phải giải thích được cho người dùng.
- Rủi ro còn lại được ghi nhận rõ: ai biết CCCD người khác vẫn đọc được **tóm tắt đợt khám** của họ.
  Đây là quyết định có ý thức, không phải lỗ hổng bị bỏ sót.

## Các lựa chọn đã cân nhắc

**Đặt cổng ngay ở lối vào (SĐT phải khớp mới cho vào).** Chặn được kẻ gõ CCCD người khác ngay từ đầu.
Bỏ vì nó chặn luôn 9,0% người có CCCD mà HIS không lưu số điện thoại, cộng 20% không có CCCD — họ bị
tắc ngay ở cửa mà không hiểu vì sao, trong khi phần lớn chỉ muốn xem lịch sử khám của chính mình.

**Bỏ hẳn cửa tài liệu — nối được hồ sơ là xem được hết.** Một cơ chế, một nhánh nghiệm thu, màn hình
không có trạng thái "nửa khoá" khó giải thích. Bỏ vì hồ sơ nối được chỉ cần khớp CCCD + họ tên + ngày
sinh, mà ba thứ đó không phải bí mật — gõ đúng là đọc trọn kết quả xét nghiệm của người lạ.

**Luôn bắt gõ Mã BN mới xem được tài liệu.** Bằng chứng thật sự chỉ người cầm phiếu mới có, và không
dính bẫy số điện thoại phòng khám. Bỏ vì bắt 100% người dùng làm thêm một bước — kể cả người đã khớp
đủ ba ô và đang xem hồ sơ của chính mình — là đánh đổi sai chỗ, cùng lý do ADR 0018 đã bỏ phương án
"không tự gộp gì, bệnh nhân tự nhận hết".
