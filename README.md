# SixosPwa — khuôn mẫu web cài đặt được

Phần mềm web mà khách mở bằng link, rồi **tự cài thành biểu tượng** trên điện thoại và máy tính. Bấm
biểu tượng là vào thẳng phần mềm, không có thanh địa chỉ — nhìn như app, bản chất vẫn là web.

Đây là **khuôn mẫu**, không phải sản phẩm. Bản thân nó chỉ có màn đăng nhập và một trang chào. Thuật ngữ
dùng trong dự án xem [`CONTEXT.md`](CONTEXT.md); các quyết định đã chốt xem [`docs/adr/`](docs/adr/).

## Chạy thử trên máy

```powershell
dotnet run --project SixosPwa --launch-profile https
```

Mở `https://localhost:7024`. Chạy bằng `localhost` là đủ để thử cài đặt trên **máy tính** — trình duyệt
coi `localhost` là môi trường an toàn.

Kiểm nhanh trong Chrome/Edge (F12 → tab **Application**):

| Xem ở đâu | Phải thấy |
|---|---|
| Manifest | Đủ tên, 3 icon, không có dòng đỏ |
| Service Workers | Trạng thái **activated** |
| Thanh địa chỉ | Có biểu tượng cài đặt |
| Network → tick **Offline** → F5 | Trang "Mất kết nối" có logo, không phải trang khủng long |

## Thử cài trên điện thoại thật

`localhost` chỉ tính là an toàn **trên chính máy đang chạy**. Cầm điện thoại gõ `http://192.168.x.x:5012`
thì service worker không đăng ký và nút cài không hiện — không phải code sai, mà do trình duyệt chặn.
Phải có HTTPS thật. Cách nhanh nhất là mở một đường hầm tạm:

```powershell
devtunnel user login          # chỉ cần một lần
devtunnel host -p 7024 --protocol https --allow-anonymous
```

Lấy địa chỉ HTTPS nó in ra, mở trên điện thoại:

- **Android Chrome** — bấm nút "Cài HisSoft vào máy" là ra hộp thoại cài của hệ thống.
- **iPhone Safari** — bấm nút sẽ ra **bảng hướng dẫn** (Apple không cho website tự gọi cài đặt). Làm
  theo: Chia sẻ → Thêm vào MH chính.

## Nhân bản thành phần mềm thật

1. Chép cả thư mục `SixosPwaTemplate` sang tên mới, đổi tên project và namespace `SixosPwa`.
2. Thay logo: đặt ảnh vuông (từ 512px trở lên) rồi chạy lại bộ sinh icon —

   ```powershell
   python tools/generate-icons.py duong-dan-logo-moi.png
   ```

   Ảnh nguồn mặc định là logo Sixos của HisSoft. Script tự cắt sát viền và canh giữa cho từng cỡ.
   ⚠️ Máy công ty **không có ImageMagick**; lệnh `convert` trên Windows là công cụ chuyển đổi *ổ đĩa*,
   tuyệt đối đừng gọi. Script này dùng Pillow.
3. Sửa `wwwroot/manifest.webmanifest`: `name`, `short_name`, `description`, `theme_color`.
4. Sửa `theme_color` cho khớp ở hai chỗ còn lại: `--sx-blue` trong `wwwroot/css/site.css` và thẻ
   `<meta name="theme-color">` trong `Views/Shared/_PwaHead.cshtml`.
5. Thay xác thực thật vào `Controllers/DangNhapController.cs` — hiện tại hàm `Login` POST **không kiểm
   tra gì**, bấm là vào.

## Những chỗ đừng đụng nếu chưa đọc ADR

- **`wwwroot/sw.js` không cache nội dung ứng dụng** — cố ý, xem
  [ADR 0002](docs/adr/0002-service-worker-khong-cache.md). Thêm cache vào đây là mở đường cho lỗi "khách
  vẫn thấy bản cũ" rất khó gỡ từ xa.
- **`global.json` ghim SDK 7.0.410** — xem [ADR 0001](docs/adr/0001-chon-net7-du-het-ho-tro.md). Máy dev
  có sẵn cả SDK 9; xoá file này là project mới sẽ sinh ra ở phiên bản khác.
- **Khai báo trong `Views/Shared/_PwaHead.cshtml`** phải có mặt ở **mọi** trang, nhất là trang đăng nhập
  (đó là `start_url`). Nhóm thẻ `apple-*` là thứ duy nhất làm iPhone mở app không kèm thanh Safari.
- **`.webmanifest` phải được khai kiểu nội dung trong `Program.cs`.** Thiếu dòng đó thì trình duyệt bỏ
  qua manifest và không bao giờ cho cài — đây là lỗi phổ biến nhất khi làm PWA trên ASP.NET.

## Bố cục

```
SixosPwa/
├── Program.cs                      kiểu nội dung .webmanifest + no-cache cho sw.js
├── Controllers/DangNhapController.cs   đăng nhập giả, chỗ để cắm xác thực thật
├── Views/Shared/_PwaHead.cshtml    khai báo manifest + nhóm thẻ riêng của iOS
├── Views/Shared/_PwaInstall.cshtml nút cài + bảng hướng dẫn (toàn bộ chữ tiếng Việt ở đây)
└── wwwroot/
    ├── manifest.webmanifest        tên, màu, 3 icon
    ├── sw.js                       service worker — KHÔNG cache, xem ADR 0002
    ├── offline.html                trang mất kết nối, tự chứa hoàn toàn
    ├── js/pwa-install.js           nhận diện nền tảng, bật/tắt nút (ASCII-only)
    └── static/                     4 icon sinh từ tools/generate-icons.py
```
