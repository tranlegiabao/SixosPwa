# 0043 — Manifest và icon PWA sinh động theo cơ sở, không còn file tĩnh

- **Tác giả:** Nam · **Ngày:** 2026-09-22 · **Trạng thái:** Đã chấp nhận
- **Bối cảnh liên quan:** [0042](0042-bo-mo-hinh-nhan-ban-mot-cong-dung-chung.md) — một cổng dùng
  chung cho mọi cơ sở, đây là hệ quả trực tiếp ·
  [0012](0012-anh-luu-tren-ftp-dung-chung.md) — logo lấy từ kho ảnh dùng chung ·
  [0013](0013-active-la-cong-hien-thi-duy-nhat.md) · [0002](0002-service-worker-khong-cache.md)

## Bối cảnh

ADR 0042 chốt: bỏ mô hình "mỗi khách một bản nhân", mọi cơ sở dùng **chung một cổng**, phân biệt nhau
bằng dữ liệu trong `DM_CSKCB`. Nhưng phần **cài đặt PWA** vẫn còn nguyên nếp của mô hình cũ:

- `wwwroot/manifest.webmanifest` là file tĩnh, ghi cứng `name: "HisSoft — Sixos"`,
  `short_name: "HisSoft"` và trỏ `/static/icon-192.png`, `/static/icon-512.png`.
- `_PwaHead.cshtml` khai tĩnh `<link rel="manifest" href="~/manifest.webmanifest">` và
  `<meta name="apple-mobile-web-app-title" content="HisSoft">`.

Hệ quả: bệnh nhân vào cửa **PKĐK Thiên Nam**, bấm "Cài HisSoft vào máy", thì biểu tượng hiện trên màn
hình điện thoại mang tên **HisSoft** với logo Sixos. Đúng cái tên mà bệnh nhân không biết là gì. Cả 12
cơ sở cài xong đều ra một biểu tượng giống hệt nhau, không phân biệt được.

## Quyết định

**Manifest và icon do `PwaController` sinh động theo cơ sở; xoá file `wwwroot/manifest.webmanifest`.**

| Đường | Trả về |
|---|---|
| `GET /manifest.webmanifest?coSo=<slug>` (và `/manifest.json`) | JSON manifest với `name`/`short_name` = `DM_CSKCB.TenCoSo`, `start_url` = `/DangNhap/Login?coSo=<slug>`, `icons` trỏ `/pwa/icon/...` |
| `GET /pwa/icon/{192\|512\|180\|maskable-512}.png?coSo=<slug>` | PNG vuông đúng cỡ, dựng từ `DM_CSKCB.Logo` |

`_PwaHead.cshtml` gắn `?coSo=` vào cả `<link rel="manifest">`, `<link rel="icon">` và
`<link rel="apple-touch-icon">`, đồng thời đặt `apple-mobile-web-app-title` = tên cơ sở.

Mã cơ sở lấy theo bốn nguồn, tin cậy giảm dần: **ViewBag của controller → `?coSo` trên URL → cookie
`pwa_co_so` → claim của phiên**. `PwaController` có thêm nguồn thứ năm là header `Referer`. Phải đủ
ngần đó vì **trình duyệt nạp manifest bằng một request riêng**, không mang ViewBag, và trên màn công
khai thì chưa có phiên nào để đọc claim.

Khi không phân giải được cơ sở, hoặc cơ sở không có logo dùng được: **rơi về manifest và icon mặc định
của HisSoft**. Cài vẫn được, chỉ mất nhận diện riêng.

## Vì sao không dựng sẵn file tĩnh cho từng cơ sở

Đã cân nhắc: sinh sẵn 4 file PNG + 1 manifest cho mỗi cơ sở rồi đặt vào `wwwroot/static/`. Bỏ, vì
đúng bằng mô hình "mỗi khách một bản nhân" mà ADR 0042 vừa dẹp: thêm một cơ sở là phải nhớ chạy lại bộ
sinh, đổi logo trong màn quản trị là biểu tượng đã cài **không đổi theo** cho tới khi có người chạy tay.

Thoả hiệp đang dùng: `PwaController` **vẫn ưu tiên file tĩnh nếu có** (`icon-<slug>-192.png`,
`apple-touch-icon-<slug>-180.png`, `icon-<slug>-maskable-512.png`) — đó chỉ là chỗ để ghi đè thủ công
khi logo trong CSDL cho ra icon xấu, không phải đường chính. Hiện chỉ Thiên Nam có bộ tĩnh này.

## Cái giá đã biết trước

- **Dựng ảnh bằng `SkiaSharp` 3.119.0, KHÔNG dùng `System.Drawing`.** Bản đầu của mạch này dùng
  `System.Drawing.Common` và trả giá ngay: (a) chỉ chạy Windows kể từ .NET 6, (b) **không đọc nổi
  WebP**. Đo 22/09 trên `HIS_CSKH`: **3/11 cơ sở** (`benh-vien-ung-buou-cs1`, `benh-vien-mat`,
  `benh-vien-hung-vuong`) mất logo riêng chỉ vì nguồn trả `image/webp` — `Image.FromStream` ném
  `ArgumentException`, `catch` nuốt, rơi về icon HisSoft, **không ai biết**. Repo này vốn đã tự lưu
  `.webp` (`wwwroot/static/img_cs/*.webp`) nên đây không phải ca hiếm.
  SkiaSharp đọc WebP sẵn, chạy mọi nền tảng (hết luôn cảnh báo CA1416), giấy phép **MIT** — quan
  trọng với một repo sắp bàn giao: ImageSharp từ 3.x đổi sang Six Labors Split License, **buộc mua
  giấy phép thương mại** trên ngưỡng doanh thu. `master_3` cũng đã ghim đúng SkiaSharp 3.119.0.
- **Vẫn còn định dạng không dựng được: SVG.** SkiaSharp đọc raster, không raster hoá SVG (cần
  `Svg.Skia` hoặc Magick.NET). Cơ sở nào để `DM_CSKCB.Logo` trỏ tệp `.svg` sẽ rơi về icon mặc định.
  `SKBitmap.Decode` trả `null` trong ca đó và `TaoIconVuong` trả `null` — im lặng theo đúng thiết kế
  fallback, không ném lỗi.
- **Icon dựng từ logo được giữ trong `IMemoryCache` 24 giờ.** Đổi logo của cơ sở trong màn quản trị
  thì icon mới chỉ ra sau khi cache hết hạn hoặc ứng dụng khởi động lại. Chấp nhận: việc đổi logo hiếm,
  còn dựng lại ảnh mỗi lượt là tốn.
- **Biểu tượng đã cài trên máy khách không tự đổi tên/đổi logo.** Manifest trả về kèm
  `Cache-Control: no-cache` nên trình duyệt luôn đọc bản mới, nhưng tên và icon của shortcut đã nằm
  trên màn hình chính thì phải **gỡ ra cài lại** mới đổi. Đây là giới hạn của hệ điều hành, không phải
  của cổng.
- **Logo hỏng/thiếu trên FTP làm mất nhận diện, im lặng phía khách.** Đo 22/09: `nha-khoa-tam-duc`
  (`/anh/87989/logo/a5287cac564e43dc8f0c10c143855ae5.png`) trả FTP 550 — file không còn trên kho, nên
  cơ sở này rơi về icon HisSoft. Sau khi đổi sang SkiaSharp thì đây là **ca duy nhất còn lại** trong
  11 cơ sở, và là ca **dữ liệu** thật sự. Đây là **lỗi dữ liệu có sẵn** (đường `/anh/...` cũ cũng trả 404), cổng
  chỉ ghi `LogWarning` rồi đi tiếp. Muốn người vận hành thấy thì cần đưa vào mạch cảnh báo của
  [ADR 0039](0039-loi-cua-phai-ve-toi-nguoi-van-hanh.md) — chưa làm.

## Hệ quả

- Xoá `wwwroot/manifest.webmanifest`. Đừng tạo lại: file tĩnh nằm đó sẽ **chặn mất** route của
  `PwaController` (static file middleware chạy trước MVC).
- Khai báo kiểu nội dung `.webmanifest` trong `Program.cs` vẫn giữ — nó phục vụ cả đường tĩnh lẫn
  chuỗi `Content-Type` trả về.
- Thêm phụ thuộc `SkiaSharp` 3.119.0 vào `SixosPwa.csproj` (trùng bản `master_3` đang dùng).
- Chi tiết triển khai và kết quả đo: [`docs/pwa-ten-va-logo-theo-co-so.md`](../pwa-ten-va-logo-theo-co-so.md).
