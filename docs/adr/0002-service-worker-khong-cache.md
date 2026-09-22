# Service worker cố ý KHÔNG cache nội dung ứng dụng

- **Tác giả:** Nam · **Ngày:** 2026-08-06 · **Trạng thái:** Đã chốt

Người đọc sau sẽ thắc mắc: PWA mà sao không cache gì cả? Đây là chủ ý. Khuôn mẫu này nhắm tới các phần
mềm y tế, nơi **hiện dữ liệu cũ là sai nghiệp vụ**, chứ không chỉ là bất tiện. Service worker trong
`wwwroot/sw.js` vì thế để mọi request đi thẳng ra server, và chỉ giữ trong máy đúng hai file: trang
`offline.html` cùng logo của nó.

## Vì sao vẫn phải có service worker

Không phải để chạy nhanh, mà vì **trình duyệt bắt buộc**: Chrome/Edge chỉ cho cài đặt khi trang có
service worker đăng ký kèm trình xử lý `fetch`, và còn đòi service worker đó **trả lời được lúc mất
mạng**. Nhánh `catch` trả về `offline.html` chính là thứ thoả điều kiện thứ hai — nó bắt buộc chứ không
phải trang trí. Bỏ nhánh đó đi là mất luôn nút cài.

## Đánh đổi

Mở app không nhanh hơn mở web, và mất mạng thì không dùng được gì ngoài trang báo lỗi. Chấp nhận, đổi
lấy hai điều quan trọng hơn: không bao giờ hiện dữ liệu cũ, và deploy bản mới là máy khách ăn ngay —
tránh được cái bẫy kinh điển "sửa bug xong khách vẫn dính bug, F5 không ăn, phải xoá cache trình duyệt".

## Hệ quả với bản nhân

Nếu một bản nhân sau này **thật sự cần** chạy offline (ví dụ app cho nhân viên đi hiện trường), đừng sửa
lén `sw.js`. Hãy viết một ADR mới ghi rõ dữ liệu nào được phép cache và hết hạn thế nào, rồi mới sửa —
vì đây là nơi lỗi sẽ âm thầm và rất khó gỡ từ xa.

`Program.cs` cũng gắn `Cache-Control: no-cache` cho chính `sw.js` và `manifest.webmanifest`, để lần sau
sửa service worker thì máy khách nhận được bản mới.
