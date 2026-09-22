# Ghi chú — `global.json` ghi SDK 9.0.100 nhưng ADR 0001 vẫn còn hiệu lực

- **Tác giả:** Nam · **Ngày:** 2026-09-22

## Phát hiện

`global.json` ở gốc repo hiện ghi:

```json
{ "sdk": { "version": "9.0.100", "rollForward": "latestFeature" } }
```

Trong khi [ADR 0001](adr/0001-chon-net7-du-het-ho-tro.md) ghim quyết định dùng **SDK 7.0.410**, và
`README.md` mục "Đừng đụng" vẫn nhắc lại đúng chuỗi đó.

Truy bằng `git log -p -- global.json`: file được tạo ở commit `4857c37` (*Add project files.*) với
`"version": "7.0.410"`, đúng như ADR 0001. Giá trị đổi thành `9.0.100` ở đúng **một** commit sau đó:

```
commit 40c1f71bd38efe81d33bcabb173fc98b5f9a8b37
Author: tranlegiabao.com <tbao2446@gmail.com>
Date:   Thu Aug 6 10:25:48 2026 +0700
    .
```

Commit này **không phải một đợt nâng cấp .NET** — nó sửa `DangNhapController.cs`, `HomeController.cs`,
`Program.cs`, `Login.cshtml`, `Home/Index.cshtml`, `_Layout.cshtml`, `launchSettings.json` (tổng 733
dòng thêm), và `global.json` chỉ đổi **một dòng** trong đó, không kèm giải thích (message commit là
dấu `.`).

Đối chiếu `SixosPwa/SixosPwa.csproj`:

```
<TargetFramework>net7.0</TargetFramework>
```

**Vẫn là net7.0**, không đổi từ commit đó tới nay.

## Kết luận

**Đây là "sửa lén", không phải một đợt nâng nền thật.** `TargetFramework` chưa từng đổi; chỉ
`global.json` bị đổi giá trị SDK như tác dụng phụ của một commit không liên quan (message "."). Không
có bằng chứng nào cho thấy đội đã chủ ý chuyển sang .NET 9.

⇒ **Không viết ADR thay thế ADR 0001.** ADR 0001 (ghim .NET 7) vẫn đúng và vẫn có hiệu lực.

## Cần orchestrator xử lý

`global.json` đang lệch khỏi quyết định đã chốt — máy nào chỉ có SDK 9 (không có 7.0.410) sẽ build
được mà không biết mình đã trôi khỏi ADR 0001, còn máy có SDK 7.0.410 build vẫn ra .NET 7 nhị phân do
`TargetFramework` chưa đổi (dễ gây ảo giác "đã nâng cấp" khi so `global.json` mà không so `.csproj`).
Việc sửa `global.json` về lại `7.0.410` nằm ngoài vùng agent tài liệu (A4) được phép đụng — cần
orchestrator hoặc agent giữ code (`SixosPwa/**`) xác nhận rồi tự sửa.
