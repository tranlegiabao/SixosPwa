# Báo Cáo & Tài Liệu Kỹ Thuật: Tùy Biến Tên và Logo PWA Theo Từng Cơ Sở Y Tế

Tài liệu này ghi lại chi tiết toàn bộ quá trình khảo sát, giải pháp kiến trúc và các bước triển khai đã thực hiện để chuyển đổi cơ chế cài đặt PWA từ cấu hình tĩnh (**HisSoft**) sang cấu hình động theo từng phòng khám (**PKĐK Thiên Nam**).

---

## 1. Hiện Trạng Ban Đầu & Vấn Đề

- **Yêu cầu của người dùng:** Khi bệnh nhân/người dùng vào cổng phòng khám (ví dụ: *PKĐK Thiên Nam*) và nhấn **"Cài đặt Ứng dụng vào máy"**, ứng dụng cài về màn hình điện thoại phải hiển thị đúng tên **"PKĐK Thiên Nam"** và **Logo của phòng khám**, thay vì hiển thị tên và logo mặc định của phần mềm gốc là *HisSoft*.
- **Nguyên nhân gốc rễ:**
  1. File `manifest.webmanifest` trong `wwwroot` được cấu hình dạng tĩnh (hardcoded) với `name: "HisSoft — Sixos"`, `short_name: "HisSoft"`, và trỏ tới bộ icon mặc định `/static/icon-192.png`, `/static/icon-512.png`.
  2. File partial `_PwaHead.cshtml` khai báo đường dẫn tĩnh `<link rel="manifest" href="~/manifest.webmanifest" />` và thẻ iOS tĩnh `<meta name="apple-mobile-web-app-title" content="HisSoft" />`.
  3. Khi trình duyệt (Chrome trên Android hoặc Safari trên iOS) kích hoạt tính năng cài đặt PWA, trình duyệt luôn đọc file manifest tĩnh này dẫn đến ứng dụng cài đặt luôn mang tên và logo HisSoft.

---

## 2. Giải Pháp Triển Khai (Dynamic PWA Architecture)

```mermaid
flowchart TD
    A["Người dùng truy cập /DangNhap/Login?coSo=pkdk-thien-nam"] --> B["_PwaHead.cshtml"]
    B --> C["Khai báo <link rel='manifest' href='/manifest.webmanifest?coSo=pkdk-thien-nam'>"]
    B --> D["Khai báo <meta name='apple-mobile-web-app-title' content='PKĐK Thiên Nam'>"]
    B --> E["Khai báo <link rel='apple-touch-icon' href='/pwa/icon/180.png?coSo=pkdk-thien-nam'>"]
    
    C --> F["PwaController: GET /manifest.webmanifest"]
    F --> G["Tra cứu CSDL DM_CSKCB theo slug / mã cơ sở"]
    G --> H["Sinh JSON Manifest động (name: PKĐK Thiên Nam, icons riêng)"]
    
    E --> I["PwaController: GET /pwa/icon/{size}.png"]
    I --> J["Tải logo từ FTP / static / URL"]
    J --> K["Xử lý chuẩn kích thước (180, 192, 512, Maskable 512) & Cache"]
    
    H & K --> L["Điện thoại cài đặt Web App đúng Tên & Logo Phòng Khám"]
```

---

## 3. Chi Tiết Các Bước Thực Hiện

### Bước 1: Khảo sát dữ liệu và chuẩn bị bộ icon chuẩn của phòng khám
- Truy vấn cơ sở dữ liệu `DM_CSKCB` với mã cơ sở `77121` (slug: `pkdk-thien-nam`):
  - **Tên cơ sở:** `PKĐK Thiên Nam`
  - **Logo đường dẫn FTP:** `/anh/77121/logo/images.png`
- Tải và xử lý bộ icon đạt chuẩn PWA đặt tại `wwwroot/static/`:
  - `icon-pkdk-thien-nam-192.png` (192x192 px - chuẩn Android)
  - `icon-pkdk-thien-nam-512.png` (512x512 px - chuẩn màn hình HD)
  - `icon-pkdk-thien-nam-maskable-512.png` (512x512 px kèm 20% safe zone margin chống bo viền)
  - `apple-touch-icon-pkdk-thien-nam-180.png` (180x180 px - chuẩn iOS Apple)

---

### Bước 2: Xây dựng Bộ Điều Khiển PWA Động ([`PwaController.cs`](file:///d:/web/SixosPwaTemplate/SixosPwaTemplate/SixosPwa/Controllers/PwaController.cs))

Tạo controller chuyên biệt đảm nhiệm việc phân giải cơ sở y tế và trả về tài nguyên PWA tương ứng:

1. **Endpoint `GET /manifest.webmanifest` & `GET /manifest.json`:**
   - Đọc tham số `?coSo=...`, cookie `pwa_co_so`, `ClaimMaCoSo` hoặc Referer header.
   - Tìm kiếm cơ sở trong CSDL `_dbContext.DMCSKCBs`.
   - Sinh payload Manifest JSON động:
     ```json
     {
       "id": "/?coSo=pkdk-thien-nam",
       "name": "PKĐK Thiên Nam",
       "short_name": "PKĐK Thiên Nam",
       "description": "Cổng thông tin và quản lý hồ sơ bệnh nhân PKĐK Thiên Nam",
       "lang": "vi",
       "dir": "ltr",
       "start_url": "/DangNhap/Login?coSo=pkdk-thien-nam",
       "scope": "/",
       "display": "standalone",
       "orientation": "any",
       "background_color": "#ffffff",
       "theme_color": "#3854A4",
       "icons": [
         {
           "src": "/pwa/icon/192.png?coSo=pkdk-thien-nam",
           "sizes": "192x192",
           "type": "image/png",
           "purpose": "any"
         },
         {
           "src": "/pwa/icon/512.png?coSo=pkdk-thien-nam",
           "sizes": "512x512",
           "type": "image/png",
           "purpose": "any"
         },
         {
           "src": "/pwa/icon/maskable-512.png?coSo=pkdk-thien-nam",
           "sizes": "512x512",
           "type": "image/png",
           "purpose": "maskable"
         }
       ]
     }
     ```
   - Trả về header `Cache-Control: no-cache, no-store, must-revalidate` để trình duyệt luôn cập nhật manifest mới.

2. **Endpoint `GET /pwa/icon/{tenIcon}`:**
   - Xử lý các kích thước `180.png`, `192.png`, `512.png`, `maskable-512.png`.
   - Ưu tiên đọc từ cache bộ nhớ `IMemoryCache` (24h) hoặc file tĩnh đã dựng sẵn.
   - Tự động download logo phòng khám từ kho FTP/URL và dùng thuật toán đồ họa căn giữa trên nền vuông đạt chuẩn.

---

### Bước 3: Cập nhật [`_PwaHead.cshtml`](file:///d:/web/SixosPwaTemplate/SixosPwaTemplate/SixosPwa/Views/Shared/_PwaHead.cshtml)

Cập nhật mã nhúng `<head>` để tự động gắn query cơ sở vào manifest và thẻ meta cho iOS:

```razor
@{
    string? slugCoSo = ViewBag.SlugCoSo as string ?? ViewData["Slug"] as string;
    string? maCoSo = ViewBag.MaCoSo as string ?? ViewData["MaCoSo"] as string;
    string? tenCoSo = ViewBag.TenCoSo as string ?? ViewData["TenCoSo"] as string;

    if (string.IsNullOrWhiteSpace(slugCoSo))
    {
        slugCoSo = Context.Request.Query["coSo"].FirstOrDefault();
    }
    if (string.IsNullOrWhiteSpace(slugCoSo))
    {
        slugCoSo = Context.Request.Cookies["pwa_co_so"];
    }
    if (string.IsNullOrWhiteSpace(slugCoSo))
    {
        slugCoSo = User.FindFirst(SixosPwa.Services.Partner.LuongCongBenhNhan.ClaimMaCoSo)?.Value;
    }

    var coSoQuery = !string.IsNullOrWhiteSpace(slugCoSo) ? $"?coSo={Uri.EscapeDataString(slugCoSo)}" : "";
    var manifestUrl = $"/manifest.webmanifest{coSoQuery}";
    var appTitle = !string.IsNullOrWhiteSpace(tenCoSo) ? tenCoSo : "HisSoft";
    var iconUrl = !string.IsNullOrWhiteSpace(slugCoSo) ? $"/pwa/icon/192.png{coSoQuery}" : "/static/icon-192.png";
    var appleIconUrl = !string.IsNullOrWhiteSpace(slugCoSo) ? $"/pwa/icon/180.png{coSoQuery}" : "/static/apple-touch-icon-180.png";
}

<link rel="manifest" href="@manifestUrl" />
<meta name="theme-color" content="#3854A4" />
<link rel="icon" type="image/png" href="@iconUrl" />

<link rel="apple-touch-icon" href="@appleIconUrl" />
<meta name="apple-mobile-web-app-capable" content="yes" />
<meta name="mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-status-bar-style" content="default" />
<meta name="apple-mobile-web-app-title" content="@appTitle" />
```

---

### Bước 4: Tinh chỉnh Giao diện Cài đặt và Tiêu đề
- **[`_PwaInstall.cshtml`](file:///d:/web/SixosPwaTemplate/SixosPwaTemplate/SixosPwa/Views/Shared/_PwaInstall.cshtml):** Tiêu đề modal hướng dẫn cài đặt được đổi thành `Thêm @(ViewBag.TenCoSo ?? "ứng dụng") vào màn hình chính`.
- **[`Login.cshtml`](file:///d:/web/SixosPwaTemplate/SixosPwaTemplate/SixosPwa/Views/DangNhap/Login.cshtml):** Thẻ `<title>` hiển thị `Đăng nhập — PKĐK Thiên Nam`.
- **Xóa file tĩnh cũ `wwwroot/manifest.webmanifest`:** Đảm bảo ASP.NET Core định tuyến toàn bộ request manifest qua `PwaController`.

---

## 4. Kết Quả Kiểm Tra & Xác Nhận

| Thành phần kiểm tra | Kết quả trả về | Trạng thái |
| :--- | :--- | :---: |
| `GET /manifest.webmanifest?coSo=pkdk-thien-nam` | HTTP 200, `name: "PKĐK Thiên Nam"`, `start_url: "/DangNhap/Login?coSo=pkdk-thien-nam"` | ✅ Đạt |
| `GET /pwa/icon/192.png?coSo=pkdk-thien-nam` | HTTP 200, image/png chuẩn logo Thiên Nam | ✅ Đạt |
| `GET /pwa/icon/180.png?coSo=pkdk-thien-nam` | HTTP 200, icon chuẩn Apple Touch Icon | ✅ Đạt |
| Giao diện HTML trang đăng nhập | Thẻ `<link rel="manifest">`, `<link rel="apple-touch-icon">` gắn đúng cơ sở | ✅ Đạt |
| Cloudflare Tunnel HTTPS | Hoạt động đầy đủ HTTPS và Service Worker | ✅ Đạt |
