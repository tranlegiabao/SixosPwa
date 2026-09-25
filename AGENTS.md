# Luật của repo SixosPwa

- `Database/` KHÔNG lên git. Cách dựng CSDL: `docs/dung-csdl.md`
- ADR trong `docs/adr/` đã chốt thì CHỈ THÊM, KHÔNG SỬA.
  Đổi ý ⇒ viết ADR mới ghi "Thay thế ADR xxxx"
- Đụng code ⇒ chạy `graphify update .` trước khi trả lời (5,8 giây, 0 token)
- Comment: ngắn, tiếng Việt CÓ DẤU, chỉ giải thích "vì sao",
  không nói lại điều code đã nói
- Toast: dùng `showToast(msg, statusCode)` — KHÔNG dùng `toastr`
- Code nhà ở `wwwroot/js|css/<nhóm>/`; thư viện ngoài ở `wwwroot/dist/` — đừng trộn
- Mọi đường ghi đi qua stored procedure (ADR 0008)
- Thuật ngữ dùng đúng `CONTEXT.md`
