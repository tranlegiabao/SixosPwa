# SixosPwaTemplate

Khuôn mẫu web cài đặt được của Sixos: một phần mềm ASP.NET Core tối giản mà khách hàng mở bằng đường
link, rồi tự cài thành biểu tượng trên điện thoại hoặc máy tính. Không chứa nghiệp vụ nào — tồn tại để
được nhân bản làm điểm khởi đầu cho các phần mềm sau.

## Language

**Khuôn mẫu**:
Chính project `SixosPwa` trong thư mục này. Nó không phải sản phẩm giao khách; nó là bản gốc để sao ra.
_Tránh_: bản demo, bản mẫu, POC

**Bản nhân**:
Một phần mềm thật được sao ra từ khuôn mẫu rồi phát triển tiếp. Mỗi bản nhân có tên, tên miền và vòng
đời riêng, không đồng bộ ngược về khuôn mẫu.
_Tránh_: bản sao, fork, clone

**Cài đặt**:
Việc khách bấm nút để đưa phần mềm thành biểu tượng ngoài màn hình chính. Không có gì được tải về máy
theo nghĩa tệp cài đặt — đây vẫn là web.
_Tránh_: tải app, tải phần mềm, download

**Chế độ ứng dụng**:
Trạng thái phần mềm mở ra từ biểu tượng đã cài: chiếm trọn cửa sổ, không có thanh địa chỉ. Trạng thái
còn lại là *chế độ trình duyệt*.
_Tránh_: standalone, fullscreen, toàn màn hình

**Nút cài thông minh**:
Nút "Cài HisSoft vào máy" dưới form đăng nhập. Gọi là thông minh vì trên Android và máy tính nó cài trực
tiếp, còn trên iPhone nó mở bảng hướng dẫn — do Apple không cho website tự gọi cài đặt.
_Tránh_: nút tải, nút download

**Trang mất kết nối**:
Trang tĩnh duy nhất được giữ sẵn trong máy khách, hiện lên thay cho trang lỗi của trình duyệt khi rớt
mạng. Đây là ngoại lệ duy nhất của nguyên tắc không cache.
_Tránh_: trang offline, trang lỗi

**Đường hầm tạm**:
Địa chỉ HTTPS công khai tạm thời (Dev Tunnels, ngrok) trỏ về máy đang chạy, dùng để thử cài đặt trên
điện thoại thật. Địa chỉ đổi mỗi lần chạy nên không giao cho khách.
_Tránh_: tunnel, ngrok link

## Quyết định

Xem [`docs/adr/`](docs/adr/). Hai quyết định định hình khuôn mẫu này:

- [0001](docs/adr/0001-chon-net7-du-het-ho-tro.md) — vì sao nền là .NET 7 dù đã hết hỗ trợ.
- [0002](docs/adr/0002-service-worker-khong-cache.md) — vì sao service worker cố ý không cache gì.
