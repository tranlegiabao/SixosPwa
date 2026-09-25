# ADR — luật bất biến

**ADR đã chốt CHỈ THÊM, KHÔNG SỬA.** Đổi ý ⇒ viết ADR mới, ghi rõ `Thay thế ADR xxxx` ở đầu ADR mới,
rồi sửa ADR cũ thành `Trạng thái: Đã bị thay thế bởi xxxx` — đó là **thay đổi DUY NHẤT** được phép làm
lên một ADR đã chốt. Không xoá file cũ, không viết đè nội dung cũ, không đổi lại quyết định đã ghi.

Đính chính một sai sót nhỏ (số liệu đo sai, link chết) có thể sửa tại chỗ; **đổi quyết định** thì luôn
phải qua ADR mới theo đúng luật trên.

## Đánh số ADR tiếp theo

1. Nhìn số lớn nhất đang có trong `docs/adr/` (đọc tên file, 4 chữ số đầu).
2. Số tiếp theo = số đó **+ 1** — trừ khi có số đang trống (xem bên dưới), thì ưu tiên dùng số trống
   trước để không để lại lỗ.
3. Đặt tên file dạng `00xx-slug-tieng-viet-khong-dau.md`, tiêu đề trong file bắt đầu bằng `00xx — `.

**Số đang trống:** không còn — mọi số từ `0001` đến số ADR mới nhất đều đã được dùng (kiểm bằng
`ls docs/adr/*.md`). Nếu về sau có số bị bỏ trống (ADR bị huỷ trước khi viết, hai ADR trùng số được
đánh lại), ghi danh sách số trống vào đây để người viết ADR tiếp theo không phải dò lại từ đầu.
