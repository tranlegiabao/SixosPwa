# 0041 — `Database/` không lên git nữa, repo chỉ mang mã nguồn

- **Tác giả:** Nam · **Ngày:** 2026-09-22 · **Trạng thái:** Đã chấp nhận
- **Thay thế:** [0026](0026-nguon-stored-procedure-lay-tu-csdl-va-dua-vao-git.md)
- **Bối cảnh liên quan:** [0008](0008-moi-duong-ghi-qua-stored-procedure.md) — mọi đường ghi qua
  stored procedure, quyết định này không đụng vế đó.

## Bối cảnh

Repo `SixosPwa` đang mang thư mục `Database/` với **63 file `.sql`** (+ `30_DOTA_00_DOC_TRUOC.md`)
ngay trong git — mỗi file là một thủ tục/script triển khai lên `HIS_CSKH`. ADR 0026 (10/09/2026) từng
cố ý đảo vế *"nguồn stored procedure nằm ngoài git"* của ADR 0008, đưa thân thủ tục vào git để có lịch
sử thay đổi và review qua PR.

Đợt bàn giao repo này cho công ty (2026-09) đặt lại câu hỏi: hai base kia — `master_2`, `master_3` —
đều **không** mang schema/DDL CSDL trong git; catalog của chúng nằm riêng ở `Projects/Databases`
(ROOT_STACK), tách khỏi repo mã nguồn. `SixosPwa` bàn giao ra ngoài (không còn là workspace nội bộ) nên
áp cùng luật: **repo bàn giao chỉ nên mang mã nguồn**, không mang các script vận hành CSDL của một môi
trường thử cụ thể.

## Quyết định

**`Database/` không còn lên git.** 63 file `.sql` bị bỏ khỏi lần theo dõi của git (đã có dòng
`Database/` trong `.gitignore`); repo từ nay chỉ chứa mã nguồn C#/JS/cshtml của ứng dụng.

Cách dựng CSDL cho môi trường mới chuyển sang tài liệu vận hành: **`docs/dung-csdl.md`**.

## Vì sao đảo ADR 0026

ADR 0026 lập luận: đưa thân thủ tục vào git để **review qua PR** và giữ **lịch sử thay đổi**. Đúng và
vẫn đúng cho một workspace nội bộ đang phát triển tính năng cổng bệnh nhân. Nhưng bối cảnh đã đổi:
repo này giờ **bàn giao ra ngoài** cho công ty vận hành, và ở vị trí đó, việc trộn script CSDL của một
môi trường thử (`HIS_CSKH` @ 118.69.34.247,8392, tài khoản `sixostest`) vào cùng git với mã nguồn ứng
dụng gây nhiễu hơn là giúp: người nhận bàn giao không cần 63 script triển khai thủ tục của một DB thử
nghiệm cụ thể, họ cần mã nguồn ứng dụng và tài liệu để tự dựng CSDL ở môi trường của họ.

## Cái giá đã biết trước — không giấu

- **Nghiệp vụ ghi nằm trong thân stored procedure sẽ không còn được review qua PR.** Đây chính là vế
  mà ADR 0026 đưa vào git để tránh; bỏ `Database/` khỏi git là **quay lại** đúng cái giá đó. Từ nay,
  đổi một thủ tục là đổi trực tiếp trên CSDL, không đi qua diff/PR nào của repo này.
- **Bất biến "thân thủ tục trong CSDL phải khớp file trong git" của ADR 0026 hết hiệu lực.** Không còn
  file trong git để so sánh — nghĩa là không còn cách nào (từ repo này) phát hiện một người đã sửa tay
  một thủ tục trên CSDL production mà không ai biết. Rủi ro mojibake / lệch thân thủ tục mà ADR 0026
  từng bắt được (5 thủ tục hỏng chuỗi tiếng Việt, 2 thủ tục có tính năng mới không ai commit) **sẽ
  không còn cơ chế phát hiện tự động** — chỉ phát hiện được bằng cách so `sys.sql_modules` trực tiếp
  trên CSDL, thủ công, khi có nghi ngờ.
- Vế **"mọi đường ghi đi qua stored procedure"** của ADR 0008 **giữ nguyên** — quyết định này chỉ đụng
  tới chỗ *nguồn* của thủ tục nằm ở đâu, không đụng tới *kiến trúc* ghi dữ liệu.

## Hệ quả

- `Database/` vẫn tồn tại trên đĩa của người đang phát triển (không xoá file), chỉ không còn được git
  theo dõi từ commit này trở đi.
- Người dựng CSDL mới đọc `docs/dung-csdl.md` thay vì tìm trong `Database/` của repo.
- Nếu về sau cần lại khả năng review qua PR cho thân thủ tục (ví dụ quay lại làm workspace nội bộ),
  đó là lý do để viết một ADR mới thay thế ADR này — không tự ý gỡ dòng `Database/` khỏi `.gitignore`
  mà không ghi lại quyết định.
