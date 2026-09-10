# Nguồn stored procedure lấy từ CSDL đang chạy, và từ nay nằm trong git

Ngày 10/09/2026, khi nghiệm thu luật *một `MaBN` không xuất hiện hai lần*, phát hiện **5 thủ tục trong
`HIS_CSKH` mang chuỗi tiếng Việt hỏng ngay trong thân** (`sys.sql_modules`): `DM_BenhNhan_Save`,
`DM_BenhNhan_SuaHoSo`, `DM_BenhNhanCoSo_GoNoi`, `DM_BenhNhanCoSo_DoiMocXemLich`,
`DM_CSKCB_GioLamViec_Save`. Bệnh nhân đọc phải `Sá»‘ cÄƒn cÆ°á»›c nÃ y Ä‘Ã£…` thay vì
*"Số căn cước này đã được một tài khoản khác khai trước…"*.

Mọi file `.sql` trên đĩa đều **sạch**; hỏng xảy ra **lúc chạy** — file UTF-8 **không BOM** nên
SSMS/`sqlcmd` đoán là cp1252. Toàn kho có **21/29 file** cùng dạng, tức 21 quả mìn.

Khi đối soát để dựng bản vá thì lộ ra một chuyện lớn hơn: **thân hai thủ tục `DM_BenhNhan_Save` và
`DM_BenhNhan_SuaHoSo` trong CSDL MỚI HƠN mọi bản trên đĩa.** Chúng mang một tính năng không tồn tại ở
bất kỳ file nguồn nào — biến `@laCccdKhongCo`: khi căn cước là mã giả `11111111111` / `111111111111`
thì bỏ khớp theo căn cước, chuyển sang khớp *họ tên không dấu + ngày sinh + giới tính*. Cùng vết với
Đợt 2 (đồng nghiệp tự chạy script schema lên `HIS_CSKH@118`, ta chỉ biết nhờ catalog sync).

⚠️ **Đừng dùng `CREATE   PROCEDURE` (ba dấu cách) làm dấu vết triển khai tay** — bản đầu của ADR này
viết vậy và **sai**. Đo lại 10/09 sau khi chạy `18_`: script của ta dùng `CREATE OR ALTER PROCEDURE`
và SQL Server lưu vào `sys.sql_modules` thành `CREATE   PROCEDURE`, đúng ba dấu cách chỗ "OR ALTER";
còn `HT_TaiKhoan_Save` tạo bằng `CREATE PROCEDURE` trần thì lưu **một** dấu cách. Ba dấu cách chỉ nói
*"triển khai bằng CREATE OR ALTER"*, không nói gì về việc có file nguồn hay không. Bằng chứng dùng
được là **so thân thủ tục với file trên đĩa**, không phải nhìn dòng `CREATE`.

## Quyết định

**Thân đang chạy trong CSDL là bản gốc** cho hai thủ tục đó, không phải file trên đĩa. Chép từ nguồn
sạch sẽ âm thầm **xoá một tính năng đang phục vụ thật** — sửa chữ mà mất tính năng thì tệ hơn hẳn để
nguyên mojibake. Bản vá lấy thân từ CSDL (quay ngược cp1252 → UTF-8, đã kiểm: 0 ký tự nằm ngoài bảng
cp1252 nên phép quay không mất mát), đọc soát tay, rồi **gắn lại khối `ERROR_NUMBER() IN (2601, 2627)`
mà bản CSDL đã đánh rơi** — thiếu nó thì đua tranh trùng khoá rơi thẳng xuống `ResultCode 99` trống trơn
thay vì câu *"Số căn cước này đã thuộc về một người khác."*. Ba thủ tục còn lại khớp nguồn trên đĩa nên
lấy thẳng từ đó.

Kết quả đi vào **`Database/18_VA_MOJIBAKE_STORED.sql`, trong git**. Đây là chỗ **đảo một vế của
ADR 0008** — ADR đó ghi *"nguồn stored procedure nằm ngoài git… nghĩa là nghiệp vụ ghi của hệ thống
không có lịch sử thay đổi và không review được qua PR. Đây là cái giá đã biết trước."* Ngày hôm nay là
lần cái giá ấy được thu: không ai phát hiện được rằng thân thủ tục đã đổi, vì không có gì để so. Vế
*"đường ghi đi qua stored procedure"* của ADR 0008 **giữ nguyên**; chỉ vế *"nguồn nằm ngoài git"* bị
bỏ. Tiền lệ đã có sẵn: `13_DOT4_DONG_VONG_DOC.sql` vốn đang giữ 4 thủ tục trong git.

## Consequences

- Kho `.claude/prompts/2026-08-24_audit-redesign-his-cskh/sql/procedures/` **hết vai trò nguồn** cho 5
  thủ tục này. Không xoá — nó vẫn là nguồn của những thủ tục khác chưa được kéo vào git.
- **Mọi `.sql` có tiếng Việt phải có BOM UTF-8.** Đây là thứ duy nhất cứu được người mở file bằng SSMS;
  dặn nhau *"nhớ chạy `sqlcmd -f 65001`"* đã thất bại một lần rồi. 21 file được thêm BOM cùng đợt này
  (16 ở `Database/`, 5 ở kho ngoài git) — đổi đúng 3 byte đầu mỗi file.
- Mỗi script đụng thủ tục **kết thúc bằng một cổng kiểm**: câu `SELECT` dùng
  `COLLATE Latin1_General_BIN2` liệt kê thủ tục còn mojibake. 🔴 Phải là **collation nhị phân** —
  collation mặc định *bỏ dấu khi so sánh* nên `LIKE N'%Ã%'` khớp luôn cả chữ `A`, và chính cái bẫy đó
  làm lần đếm đầu tiên ra 14 thủ tục thay vì 5.
- Bất biến mới: **thân thủ tục trong CSDL phải khớp file trong git.** Lệch là có người triển khai tay.
  Đo bằng cách cắt khối `CREATE ... PROCEDURE` khỏi file rồi so với `OBJECT_DEFINITION(...)` sau khi
  chuẩn hoá khoảng trắng — **không** so bằng dòng `CREATE` (xem cảnh báo ở trên).
