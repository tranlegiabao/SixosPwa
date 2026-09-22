# Đổi tên lan tới tận JavaScript, chấp nhận phá hợp đồng JSON

- **Tác giả:** Nam · **Ngày:** 2026-08-25 · **Trạng thái:** Đã chốt

Đợt tái kiến trúc `HIS_CSKH` (2026-08-24) đổi tên gần như toàn bộ bảng và cột sang khuôn HisSoft
(`DM_`/`QL_`/`HT_`, cột khoá chính `ID`, khoá ngoại `ID<ThựcThể>`). Làn sóng đổi tên **không dừng ở tầng
EF hay ở ranh giới JSON** — nó đi tiếp vào tên thuộc tính C#, vào key của JSON trả về, và vào cả biến
JavaScript đang đọc những key đó.

Đây là deviation cần ghi lại vì **bản yêu cầu gốc của chính đợt này cấm điều đó**: prompt
`audit-redesign-sql-server-schema.md` viết *"Ràng buộc quan trọng: KHÔNG được đổi frontend… nếu bắt buộc
phải đổi API contract thì phải nêu rõ và hỏi xác nhận trước."*

Đã nêu, và người dùng chọn đổi. Lý do chọn: nửa vời thì DB sạch nhưng C# vẫn mang tên cũ lộn xộn, và
lớp `[JsonPropertyName]` để giữ key cũ tự nó là một lớp nói dối thứ hai phải nuôi mãi.

## Consequences

- **Compiler không đỡ được tầng ngoài.** 45 điểm `fetch`/`$.ajax` và 67 điểm `return Json(new { ... })`
  chỉ vỡ **lúc chạy**, không vỡ lúc build. Vì vậy việc rà chúng được tách hẳn thành một đợt riêng
  (Đợt 3) với nghiệm thu Playwright, thay vì làm lẫn trong đợt sửa C#.
- Bất kỳ bên thứ ba nào đang gọi API của SixosPwa sẽ gãy. Tại thời điểm quyết định, không tìm thấy bên
  nào như vậy trong workspace — chỉ `SixosPwa/appsettings.json` trỏ tới `HIS_CSKH`.
- Nếu về sau cần giữ key JSON cũ cho một khách cụ thể, cách đúng là thêm một endpoint phiên bản mới,
  **không** phải bỏ `[JsonPropertyName]` vào model để bọc lại tên cũ.
