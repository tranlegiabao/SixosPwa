# 0029 — Dựng tải bằng bản sao bệnh nhân thật, trộn thẳng vào cơ sở thật

- **Tác giả:** Nam · **Ngày:** 2026-09-17 · **Trạng thái:** Đã chấp nhận
- **Bối cảnh liên quan:** [0018](0018-luat-gop-ho-so-cccd-ten-ngaysinh.md) — *Luật gộp hồ sơ* và các
  tỉ lệ bẩn đo được. [0019](0019-mot-tai-khoan-nhieu-ho-so.md) — một tài khoản nhiều hồ sơ.
  [0021](0021-tu-choi-tai-lieu-mo-coi.md) — cổng từ chối tài liệu mồ côi.
  [0008](0008-moi-duong-ghi-qua-stored-procedure.md) — mọi đường ghi qua thủ tục.

## Bối cảnh

Đợt 2 (2026-08-25) khép lại với một kết luận rất cụ thể và rất khó chịu: **không đo được gì về hiệu
năng trên `HIS_CSKH`**. Bảng lớn nhất khi ấy 114 dòng, truy vấn chậm nhất 0,88 ms, `missing-indexes.sql`
trả **0 dòng**. Mọi công cụ DMV, mọi phép đo index đều vô nghĩa ở quy mô đó. Hôm nay bảng lớn nhất là
`QL_TaiLieuBenhNhan` với **245 dòng** — tình hình không khác gì.

Trong khi đó cổng đang đi tới chỗ phải chịu tải thật: gộp tất cả phòng khám, bệnh viện và nha khoa lại
thì lượng người dùng tiềm năng ở mức hàng trăm nghìn tới hàng triệu, mỗi người 20–50 tệp PDF tại mỗi
cơ sở. Câu hỏi "thiết kế CSDL hiện tại chịu được không" **không thể trả lời bằng cách đọc DDL** — phải
có dữ liệu.

Rà đường đọc thật thì thấy ba chỗ đáng ngờ, và cả ba đều **chỉ lộ ra khi có dữ liệu**:

| Chỗ | Bằng chứng đo được hôm nay |
|---|---|
| `DM_BenhNhan.IDTaiKhoan`, `DM_BenhNhanCoSo.IDBenhNhan`, `QL_DotKham.IDBenhNhanCoSo` | **không có index nào**, trong khi cả ba là cột lọc của màn mở mỗi lần vào app |
| `HomeController.DanhSachTaiLieu` | `ORDER BY ISNULL(NgayKham, NgayTao)` là **biểu thức** ⇒ không index nào sắp sẵn được, buộc sort toàn tập trước khi lấy 50 dòng; mẻ sau dùng `Skip()` (OFFSET) |
| `QL_TaiLieuBenhNhan_Save` | `SELECT MAX(PhienBan) ... WITH (UPDLOCK, HOLDLOCK)` giữ khoá phạm vi tới lúc commit |

Nguồn dữ liệu có sẵn ngay: **9 DB khách nằm cùng instance** với `HIS_CSKH` — `Nhakhoa_DHYD`
(242.913 bệnh nhân), `Dev_Master3` (54.685), `NhaKhoa_NoVa` (43.194), `nhakhoatamduc712htp` (30.355).
Cùng instance nghĩa là `INSERT ... SELECT` gọi tên ba phần, không qua tệp trung gian, không dính bẫy
méo tiếng Việt qua console mà hồ sơ này đã trả giá nhiều lần.

## Quyết định

**Bốn vế, đi cùng nhau.**

1. **Chép người thật, không sinh tổng hợp.** 20.000 con người lấy thẳng từ các DB khách nói trên, giữ
   nguyên tên, CCCD, ngày sinh, số điện thoại. Lý do không phải tiện: ADR 0018 đã đo và ghi lại rằng
   dữ liệu thật **bẩn có hệ thống** — 17,3% người có ≥2 `MaBN` tại cùng cơ sở, `000000000000` xuất hiện
   2.847 lần làm CCCD, 345 nhóm cùng CCCD khác tên, một số điện thoại gắn 876 người. Toàn bộ *Luật gộp
   hồ sơ* và cơ chế *nối hồ sơ hai tầng* (ADR 0024) sinh ra để chống đỡ đúng mấy phân phối đó. Dữ liệu
   sạch tự sinh sẽ **không bắn một viên nào** vào chúng, và sẽ cho ra kết luận "ổn" không có giá trị.

2. **Giữ ba hình dạng lệch, không trải đều.** (a) *Lệch theo cơ sở* — một cơ sở ~12k người, hai cơ sở
   ~3k, chín cơ sở vài trăm, khớp hình dạng thật hiện nay (cơ sở 35 đang giữ 139/168 hồ sơ và 238/245
   tài liệu). (b) *Lệch theo tài khoản* — đa số một tài khoản một hồ sơ, ~10% giữ 2–4 hồ sơ, vài tài
   khoản giữ hàng trăm, khớp số đo của ADR 0019. (c) *Lệch theo số tài liệu* — đa số 5–20 tệp, ~500
   người ở mức 200–500, vài ca 1.000+. Trải đều ở bất kỳ trục nào cũng làm biến mất đúng thứ cần đo:
   bệnh ngửi tham số chỉ xuất hiện khi kế hoạch truy vấn được "ngửi" bằng một cơ sở nhỏ rồi đem áp cho
   cơ sở lớn, và màn *Danh sách tài liệu* chỉ chậm khi gặp *đuôi nặng*.

3. **Tôn trọng luật từ chối tài liệu mồ côi.** Tài liệu chỉ gắn vào hồ sơ đã nối mã; ~70% hồ sơ nối và
   mang tài liệu, ~30% là *Hồ sơ tự khai* không có tài liệu nào — khớp tỉ lệ 117/168 hiện tại. Quần thể
   chưa nối không phải phần thừa: nó là thứ duy nhất đo được cửa bù
   `POST /api/v1/ho-so/kiem-tra-nhan`, thứ chưa từng chạy ở quy mô nào.

4. **Trộn thẳng vào 12 cơ sở thật, không dựng cơ sở riêng.** Dữ liệu dựng tải nằm chung với dữ liệu
   thật đang chạy trong `HIS_CSKH`.

Kèm theo, **không lệ thuộc `.bak` làm đường dọn**: trước khi seed, chụp danh sách ID sẽ sinh vào bảng
phụ `bak.SeedIds_20260917` (schema `bak` đã có từ Đợt 2). Bảng thật không thêm cột nào. `.bak` chụp
trước khi chạy chỉ còn vai trò lưới cứu hộ.

## Hệ quả

- **20.000 người giả sẽ hiện trên cổng và khu Admin.** Màn *Danh sách bệnh nhân*, các bộ lọc và mọi con
  số đếm đều thấy họ. Người trong nhóm mở lên sẽ gặp, và phải biết trước để không tưởng là dữ liệu hỏng.
- **Hồ sơ bệnh án của bệnh nhân thật nằm trong một DB dùng để thử nghiệm.** Chấp nhận được vì cả nguồn
  lẫn đích đều là DB do chính công ty vận hành trên cùng một máy chủ, và cổng chưa triển khai cho khách.
  Nếu về sau `HIS_CSKH` được giao ra ngoài, dữ liệu dựng tải **phải bị dọn trước** — đó là lý do vế
  `bak.SeedIds` là bắt buộc chứ không phải tuỳ chọn.
- **Trần quy mô bị khoá ở mức nhỏ.** Tệp `HIS_CSKH.mdf` nằm ở `C:\Program Files\...\DATA\` trên ổ chỉ
  còn **3,8 GB trống**, trong khi mọi DB khác của máy chủ nằm ở `G:\SixOsTest\Data\` (1.595 GB trống).
  Mốc 20k chiếm ~0,9 GB lúc đỉnh nên vừa; mốc 100k đã **không lọt**. Muốn đi xa hơn thì phải dời tệp
  trước — đó là việc riêng, chưa làm trong đợt này.
- Recovery model hạ **`SIMPLE`** trong lúc nạp để log không phình trên ổ C:. Chuỗi sao lưu hiện đã
  hỏng sẵn (bản đầy đủ gần nhất 25/08, không có sao lưu log) nên không mất gì.
- Không cần tải một tệp nào lên FTP: **141 đường dẫn FTP khác nhau đã có sẵn** trong 245 dòng hiện tại,
  đủ cho yêu cầu "vài trăm tệp dùng chung". Kho FTP 118 không bị đụng tới.

## Vì sao không chọn cách khác

**Dựng cơ sở `SEED*` riêng với `Active=0`** là phương án được cân nhắc kỹ nhất và có hai lợi thế thật:
dữ liệu dựng tải không hiện trên cổng, và dọn chỉ còn là `DELETE` theo `IDCoSo` — cột dẫn đầu mọi index
nóng nên xoá rất nhanh. Bỏ qua vì trộn vào cơ sở thật cho số đo sát production hơn: cùng phân phối
`IDCoSo`, cùng thống kê, cùng kế hoạch truy vấn được dùng lại giữa cơ sở to và cơ sở nhỏ — mà việc dùng
lại kế hoạch giữa hai kích cỡ chính là hiện tượng cần bắt. Cái giá (dữ liệu giả hiện ra) được trả bằng
`bak.SeedIds`.

**Phục hồi `.bak` thành một DB riêng trên ổ G: rồi dựng tải ở đó** không tốn thêm bước nào (đằng nào
cũng chụp `.bak`), giữ `HIS_CSKH` thật sạch tuyệt đối, và gỡ luôn trần dung lượng. Bỏ qua vì phải đổi
chuỗi kết nối mỗi lần muốn bấm thử trên app thật, và vì cổng chưa có khách nên rủi ro của việc dựng tải
trên chính DB thật đang ở mức thấp nhất mà nó sẽ từng có.

**Sinh dữ liệu tổng hợp hoàn toàn** sạch, lặp lại được, không dính dữ liệu khách. Bỏ qua vì phải tự tay
tái tạo các tỉ lệ bẩn của ADR 0018, mà tái tạo bằng tay thì chỉ tái tạo được những tỉ lệ **đã biết** —
trong khi giá trị lớn nhất của việc dựng tải nằm ở chỗ nó bày ra thứ chưa ai nghĩ tới.
