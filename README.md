# SixosPwa — cổng bệnh nhân của Sixos

Phần mềm web mà bệnh nhân mở bằng đường link, rồi **tự cài thành biểu tượng** trên điện thoại hoặc máy
tính. Bấm biểu tượng là vào thẳng phần mềm, không có thanh địa chỉ — nhìn như app, bản chất vẫn là web.

Đây là **sản phẩm đang chạy thật**, không phải khuôn mẫu để nhân bản: mọi cơ sở khám chữa bệnh dùng
chung một cổng này, phân biệt nhau bằng dữ liệu trong `HIS_CSKH`
([ADR 0042](docs/adr/0042-bo-mo-hinh-nhan-ban-mot-cong-dung-chung.md)). Bệnh nhân đăng nhập để xem hồ sơ
khám, tài liệu (toa thuốc, kết quả xét nghiệm, giấy ra viện) và lịch khám của mình.

Thuật ngữ dùng trong dự án xem [`CONTEXT.md`](CONTEXT.md); luật của repo xem [`AGENTS.md`](AGENTS.md);
các quyết định đã chốt xem [`docs/adr/`](docs/adr/).

## Chạy thử trên máy

```powershell
dotnet run --project SixosPwa --launch-profile https
```

Mở `https://localhost:7024` (hoặc `http://localhost:5015`). CSDL là `HIS_CSKH` trên server test, chuỗi
kết nối đã có sẵn trong `appsettings.json` ⇒ clone về là chạy được, xem
[`docs/dung-csdl.md`](docs/dung-csdl.md).

Chạy bằng `localhost` là đủ để thử cài đặt trên **máy tính** — trình duyệt coi `localhost` là môi trường
an toàn. Kiểm nhanh trong Chrome/Edge (F12 → tab **Application**):

| Xem ở đâu | Phải thấy |
|---|---|
| Manifest | Đủ tên, 3 icon, không có dòng đỏ |
| Service Workers | Trạng thái **activated** |
| Thanh địa chỉ | Có biểu tượng cài đặt |
| Network → tick **Offline** → F5 | Trang "Mất kết nối" có logo, không phải trang khủng long |

### Thử cài trên điện thoại thật

`localhost` chỉ tính là an toàn **trên chính máy đang chạy**. Cầm điện thoại gõ `http://192.168.x.x:5015`
thì service worker không đăng ký và nút cài không hiện — không phải code sai, mà do trình duyệt chặn.
Phải có HTTPS thật: mở một đường hầm tạm bằng `start-tunnel.ps1`, các bước đầy đủ ở
[`HUONG-DAN-TUNNEL.md`](HUONG-DAN-TUNNEL.md)
([ADR 0007](docs/adr/0007-moi-truong-thu-that-qua-cloudflare-tunnel.md)).

- **Android Chrome** — bấm nút "Cài HisSoft vào máy" là ra hộp thoại cài của hệ thống.
- **iPhone Safari** — bấm nút sẽ ra **bảng hướng dẫn** (Apple không cho website tự gọi cài đặt). Làm
  theo: Chia sẻ → Thêm vào MH chính.

## Bố cục

```
SixosPwa/                               (thư mục gốc repo)
├── AGENTS.md                           luật của repo — nguồn duy nhất
├── CONTEXT.md                          từ vựng dự án (~80 mục)
├── global.json                         ghim SDK 7.0.410 — xem ADR 0001
├── docs/
│   ├── adr/                            42 quyết định đã chốt, chỉ THÊM không SỬA
│   └── dung-csdl.md                    CSDL nào, 13 bảng, đường ghi nào
├── Database/                           script CSDL — CỐ Ý không lên git (ADR 0041)
└── SixosPwa/                           (project ASP.NET Core)
    ├── Program.cs                      kiểu nội dung .webmanifest + no-cache cho sw.js
    ├── Controllers/
    │   ├── DangNhapController.cs       đăng nhập OTP, quét QR, đăng ký, đổi mật khẩu
    │   ├── HomeController.cs           danh sách/chi tiết cơ sở, trang bệnh nhân, tài liệu
    │   ├── HoSoController.cs           hồ sơ bệnh nhân: thêm/sửa/nối/gỡ nối/xoá
    │   ├── LichKhamController.cs       "Lịch khám của tôi" (ADR 0025)
    │   ├── AnhController.cs            phục vụ ảnh lấy từ kho dùng chung (ADR 0012)
    │   └── Api/                        api/v1/{ho-so, tai-lieu, dot-kham} — HIS gọi vào
    ├── Areas/Admin/                    màn quản trị: cơ sở y tế, bệnh nhân, cấu hình, dashboard
    ├── Services/
    │   ├── HoSoBenhNhanService.cs      luật gộp/nối hồ sơ (ADR 0018, 0024, 0036)
    │   ├── TaiLieuService.cs           tài liệu hai kho (ADR 0030)
    │   ├── FtpService.cs · KhoAnh.cs   đọc/ghi kho ảnh dùng chung
    │   ├── AdminStoredProcedureService.cs   mọi đường ghi đi qua đây (ADR 0008)
    │   ├── His/ · Partner/             cửa cơ sở, cây quyết định luồng cổng bệnh nhân
    │   └── DbTaiKhoanService.cs        tài khoản + mật khẩu nội bộ (ADR 0009)
    ├── Data/ApplicationDbContext.cs    13 DbSet ánh xạ sang bảng HIS_CSKH
    ├── Security/                       khoá cơ sở, xác thực lại cho màn quản trị
    ├── Views/ · Areas/Admin/Views/     giao diện Razor
    └── wwwroot/
        ├── manifest.webmanifest        tên, màu, 3 icon
        ├── sw.js                       service worker — KHÔNG cache, xem ADR 0002
        ├── offline.html                trang mất kết nối, tự chứa hoàn toàn
        ├── js/<nhóm>/ · css/<nhóm>/    code nhà, tách theo màn
        └── dist/ · lib/                thư viện ngoài — đừng trộn với code nhà
```

## Ba luồng chính

### 1. Đăng nhập bằng OTP

`/DangKyOnline/{slug}` (cửa của cơ sở) → `/DangNhap/Login` → nhập số điện thoại → `GuiOtp` →
`XacNhanOtp` → đặt phiên → `/benh-nhan`.

- Đăng nhập được thì **phải** có tài khoản trong `HT_TaiKhoan`
  ([ADR 0027](docs/adr/0027-dang-nhap-duoc-thi-phai-co-tai-khoan.md)).
- Một tài khoản giữ nhiều hồ sơ, mỗi hồ sơ là **cặp người × cơ sở**
  ([ADR 0019](docs/adr/0019-mot-tai-khoan-nhieu-ho-so.md),
  [ADR 0036](docs/adr/0036-ho-so-la-cap-nguoi-x-co-so.md)).
- Đang có phiên ở cơ sở A mà mở cửa cơ sở B thì bị modal chặn, không đổi phiên ngầm
  ([ADR 0006](docs/adr/0006-chan-dang-nhap-cheo-co-so.md)).
- Cơ sở nào có cửa riêng thì chuyển hướng thẳng sang đó
  ([ADR 0038](docs/adr/0038-co-so-co-cua-rieng-thi-chuyen-huong-thang.md)).

### 2. Quét QR

`/qr` (≡ `/qr-kham`) đăng nhập **thẳng**, không qua OTP; `/qr-otp` vẫn bắt nhập OTP.

🔴 Cả hai đường đều bắt buộc có `mabn` trên URL. Mở `/qr` tay không thì **cố ý** đá về màn Login —
thiếu chốt này là ai gõ `/qr` cũng được cấp phiên của một bệnh nhân bất kỳ.

### 3. Tài liệu

`/benh-nhan/tai-lieu` (danh sách theo nhóm) → `/benh-nhan/tai-lieu/me` (trình xem) →
`/benh-nhan/tai-lieu/cung-loai` (đi tiếp trong cùng loại,
[ADR 0033](docs/adr/0033-trinh-xem-di-bang-danh-sach-cung-loai-rieng.md)).

- Tài liệu nằm ở **hai kho**: kho của cổng và FTP gốc của phòng khám; cổng giữ **đường dẫn**, không ôm
  bản sao ([ADR 0030](docs/adr/0030-tai-lieu-nam-o-hai-kho.md)).
- Tài liệu không gắn được vào hồ sơ nào thì **từ chối**, không hiện mồ côi
  ([ADR 0021](docs/adr/0021-tu-choi-tai-lieu-mo-coi.md)).
- Hồ sơ không có căn cước thì không nối sang bệnh án
  ([ADR 0028](docs/adr/0028-ho-so-khong-co-can-cuoc-thi-khong-noi-benh-an.md)).

## Những chỗ đừng đụng nếu chưa đọc ADR

- **`wwwroot/sw.js` không cache nội dung ứng dụng** — cố ý, xem
  [ADR 0002](docs/adr/0002-service-worker-khong-cache.md). Thêm cache vào đây là mở đường cho lỗi "khách
  vẫn thấy bản cũ" rất khó gỡ từ xa.
- **`global.json` ghim SDK 7.0.410** — xem [ADR 0001](docs/adr/0001-chon-net7-du-het-ho-tro.md). Máy dev
  có sẵn cả SDK 9; xoá hoặc nâng file này là project sinh ra ở phiên bản khác, và máy đích buộc phải có
  đúng SDK đã ghim mới build nổi. Bối cảnh:
  [`docs/ghi-chu-global-json-sdk.md`](docs/ghi-chu-global-json-sdk.md).
- **Khai báo trong `Views/Shared/_PwaHead.cshtml`** phải có mặt ở **mọi** trang, nhất là trang đăng nhập
  (đó là `start_url`). Nhóm thẻ `apple-*` là thứ duy nhất làm iPhone mở app không kèm thanh Safari.
- **`.webmanifest` phải được khai kiểu nội dung trong `Program.cs`.** Thiếu dòng đó thì trình duyệt bỏ
  qua manifest và không bao giờ cho cài — đây là lỗi phổ biến nhất khi làm PWA trên ASP.NET.
- **Mọi đường ghi đi qua stored procedure**
  ([ADR 0008](docs/adr/0008-moi-duong-ghi-qua-stored-procedure.md)). Đừng thêm chỗ ghi thẳng bảng.
- **`DM_CSKCB.Active` là cổng hiển thị duy nhất**
  ([ADR 0013](docs/adr/0013-active-la-cong-hien-thi-duy-nhat.md)). Tắt một cơ sở là đổi dữ liệu, không
  phải sửa code.
- **HIS gọi thẳng sang cổng qua linked server `SPWA_CONG`**
  ([ADR 0035](docs/adr/0035-hop-dong-linked-server-spwa-cong.md)). Tham số của các thủ tục phía cổng chỉ
  được **THÊM kèm giá trị mặc định** — bớt một tham số là chết màn bên HIS trong khi build vẫn xanh.

## Đi tiếp

| Đọc gì | Khi nào |
|---|---|
| [`AGENTS.md`](AGENTS.md) | Trước khi sửa bất cứ dòng nào — 8 luật của repo |
| [`CONTEXT.md`](CONTEXT.md) | Khi cần gọi đúng tên một khái niệm |
| [`docs/adr/`](docs/adr/) ([mục lục](docs/adr/README.md)) | Khi thấy code làm điều "kỳ lạ" — thường đã có ADR giải thích |
| [`docs/dung-csdl.md`](docs/dung-csdl.md) | Khi cần biết bảng nào, thủ tục nào |
| [`HUONG-DAN-TUNNEL.md`](HUONG-DAN-TUNNEL.md) | Khi cần link HTTPS để thử trên điện thoại |
