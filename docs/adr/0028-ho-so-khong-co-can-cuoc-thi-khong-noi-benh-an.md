# Hồ sơ không có căn cước thì không nối bệnh án

HIS đánh dấu bệnh nhân không có căn cước bằng mã giả `11111111111` / `111111111111`, và cổng bắt buộc
phải điền ô CCCD nên bệnh nhân gõ đúng mã đó vào. Ngày 10/09 nhánh `HIeu_10/09` mở đường cho nhóm này
nối bệnh án: bỏ khớp theo căn cước, chuyển sang khớp **họ tên không dấu + ngày sinh + giới tính**, và
nếu HIS trả về đúng một ứng viên thì **gán mã im lặng** ở *Tầng 1*.

Ý đồ đúng — mục tiêu là **không bày danh sách radio Mã BN ra màn hình** cho nhóm đông này, vì bày ra là
lộ. Nhưng cách làm đi ngược mục tiêu.

## Vấn đề

Ba ô *họ tên + ngày sinh + giới tính* **không phải bí mật** — chúng in trên mọi toa thuốc, hoá đơn,
phiếu hẹn. Nối theo ba ô đó nghĩa là ai biết tên và ngày sinh của người khác cũng kéo được bệnh án của
họ về, chỉ cần một SIM để qua OTP.

Ẩn cái radio đi không bịt được gì, vì nó không phải thứ cấp quyền — nó chỉ là màn hình. Đặt hai kịch
bản cạnh nhau, kẻ tấn công cùng gõ danh tính công khai của nạn nhân:

| | Tầng 2 (có radio) | Tầng 1 (ẩn radio) |
|---|---|---|
| Hắn thấy | một dòng: Mã BN + CCCD che + ngày khám gần nhất | không thấy gì |
| Hắn được | phải bấm chọn ⇒ nối | **nối luôn, im lặng** |
| Kết cục | chiếm hồ sơ | chiếm hồ sơ |

Kết cục giống nhau; bản mới chỉ nhanh hơn và không để lại dấu *"người dùng đã chọn"*. Và với ca từ hai
ứng viên trở lên thì radio **vẫn hiện như cũ**, nên rò rỉ cũng chưa bịt hết.

Đo thật trên `PKDK_ThienNam` (production, 75.312 hồ sơ) cho biết quy mô:

- **11.533** hồ sơ không có căn cước (bỏ trống hoặc mã giả) và có đủ ngày sinh + giới tính.
- Gom theo họ tên + ngày sinh + giới tính ra **10.019 nhóm**, trong đó **1.203 nhóm nhập nhằng** — tức
  **12% số nhóm** có từ hai người trùng cả ba ô, phủ **2.717 hồ sơ**.
- Trong nhóm không căn cước, chỉ **1.331 người (11,5%)** có số điện thoại trên hồ sơ HIS, nên phương án
  *"lấy SĐT đã qua OTP làm bằng chứng thay thế"* của ADR 0020 không cứu được đa số.

Tệ hơn: **nối xong là tài liệu mở ngay**. `SaveBenhNhanCoSoAsync` không truyền `@DaMoTaiLieu` nên thủ
tục dùng mặc định `= 1`; đo ngày 10/09 thấy **55/55** hồ sơ đang mở. *Cửa tài liệu* mà ADR 0020 dựng lên
(SĐT trùng, hoặc gõ đúng Mã BN một lần) trên thực tế **chưa từng chặn ai** kể từ `08_MO_CUA_TAI_LIEU.sql`.

## Quyết định

**Hồ sơ mang mã giả không nối bệnh án. Chấm hết.** Người dùng chốt ngày 10/09, chấp nhận mất nhóm khách
đó ở tính năng xem bệnh án.

Chặn đặt ở **hai cổng**, và cổng thứ nhất nằm **trước cả lúc hỏi HIS**:

1. `NoiKhiLuuAsync` thoát sớm khi CCCD là mã giả — không gọi `SPWA_TraCuuHoSo`, nên **không có danh sách
   ứng viên nào tồn tại để mà lộ**. Đây mới là cách đạt được mục tiêu ban đầu: kẻ gõ danh tính người
   khác không thấy gì hết.
2. `XacNhanNoiAsync` kiểm lại CCCD của hồ sơ trong CSDL rồi mới nối. Action này POST trần được — giữ một
   form cũ, hoặc đổi CCCD hồ sơ thành mã giả *sau khi* danh sách ứng viên đã nằm trong tay, là đi vòng
   được cổng thứ nhất.

Kèm theo, hai thứ chống lạm dụng ở cùng đợt:

- **Chặn tốc độ dò danh tính** trên `/benh-nhan/ho-so/them` và `/benh-nhan/ho-so/xac-nhan-noi`: 10 lần /
  tài khoản và 30 lần / IP trong cửa sổ trượt 5 phút. Đếm theo **cả hai** vì khoá riêng tài khoản thì
  kẻ tấn công mở tài khoản mới, khoá riêng IP thì cả phòng khám dùng chung một IP bị vạ lây. HIS đã
  chặn dò Mã BN từ Đợt 1 (`SPWA_TraCuuTheoMaBN`, 20 lần / 15 phút, fail-closed) nhưng **không ai chặn dò
  danh tính** cho tới nay.
- **`@DaMoTaiLieu` phải truyền tường minh**, không để thủ tục tự mặc định `= 1` nữa.

Hồ sơ mang mã giả **vẫn tạo được** — nó chỉ là hồ sơ tự khai, không kéo được gì từ HIS. Chặn *nối*,
không chặn *tồn tại*.

## Consequences

- Nhóm không có căn cước mất khả năng xem bệnh án qua cổng. **Đây là cái giá đã biết và đã chấp nhận**,
  không phải sơ suất — ghi ở đây để lần sau không ai "sửa lại cho tiện".
- `UK_DM_BenhNhan_CCCD` phải đổi thành **unique CÓ LỌC** (bỏ hai mã giả ra) — `Database/19_CHI_MUC_CCCD_CO_LOC.sql`.
  Chỉ mục cũ không lọc nên mỗi mã giả chỉ **một** hồ sơ giữ được trên toàn hệ thống, mà cả hai suất đã
  bị chiếm (ID 13 từ 25/08, ID 67 từ 09/09): người thứ ba không tạo nổi hồ sơ và còn đọc phải câu sai sự
  thật *"Số căn cước này đã thuộc về một người khác."*.
- Nhánh nới *Tầng 1* của `HIeu_10/09` **đã gỡ**. Phần Hiếu làm ở `HisDocService` (không gửi mã giả sang
  HIS) giữ nguyên — giờ là đường không ai đi tới, nhưng vô hại và đúng nếu sau này mở lại.
- Nhánh `@laCccdKhongCo` trong `DM_BenhNhan_Save` / `_SuaHoSo` **giữ nguyên**: nó lo việc gộp hồ sơ
  *phía cổng*, không liên quan tới nối bệnh án.

## Còn thiếu — đừng tưởng đã xong

- **Cửa tài liệu của ADR 0020 vẫn chưa được xây.** Đường *Tầng 2* (bệnh nhân tự chọn mã) hiện vẫn mở
  tài liệu ngay khi nối, và nó vẫn nhận danh tính công khai làm đầu vào. Siết chỗ này đòi hai thứ chưa
  có: HIS trả thêm cờ `dienThoaiKhop` (y khuôn `cccdKhop`), và một màn *"hồ sơ còn khoá — gõ Mã BN để
  mở"*. Không có đường mở đó mà siết trước là khoá chết người dùng thật.
- **`SPWA_TraCuuTheoMaBN` bên HIS đã sẵn sàng từ Đợt 1 và cổng chưa bao giờ gọi.** Đó là cửa đúng để
  sau này trả lại khả năng nối cho nhóm không căn cước: bằng chứng là **Mã BN in trên phiếu của chính
  họ**, không phải thứ tra ra được từ tên tuổi.
