# Ghi chú — `global.json` đã từng trôi khỏi ADR 0001 một lần (đã vá)

- **Tác giả:** Nam · **Ngày:** 2026-09-22 · **Trạng thái:** Đã xử lý

## Hiện trạng

`global.json` ghim `7.0.410`, khớp [ADR 0001](adr/0001-chon-net7-du-het-ho-tro.md) và mục "Đừng đụng"
trong `README.md`. Không có việc gì phải làm. File này để lại chỉ vì **nó từng trôi một lần mà không
ai biết** — đọc để lần sau nhận ra sớm.

## Chuyện đã xảy ra

File được tạo ở commit `4857c37` (*Add project files.*) với `"version": "7.0.410"`, đúng ADR 0001.
Giá trị bị đổi thành `9.0.100` ở đúng **một** commit:

```
commit 40c1f71bd38efe81d33bcabb173fc98b5f9a8b37
Author: tranlegiabao.com <tbao2446@gmail.com>
Date:   Thu Aug 6 10:25:48 2026 +0700
    .
```

Commit đó **không phải một đợt nâng .NET**: nó sửa `DangNhapController.cs`, `HomeController.cs`,
`Program.cs`, `Login.cshtml`, `Home/Index.cshtml`, `_Layout.cshtml`, `launchSettings.json` (733 dòng
thêm), và `global.json` chỉ đổi **một dòng** lẫn trong đó, message commit là dấu `.`.
`SixosPwa.csproj` thì **chưa từng rời `net7.0`**.

Đảo lại ngày 22/09/2026 trong đợt bàn giao, sau khi đo: SDK `7.0.410` build **0 lỗi / 19 warning**,
SDK `9.0.307` build **0 lỗi / 21 warning** (2 dòng dư là `NETSDK1138` — net7.0 hết hỗ trợ). Trả về SDK 7
không mất gì.

## Vì sao đáng nhớ

Ba thứ phải khớp nhau, và chúng **không tự kiểm tra lẫn nhau**:

| Nơi | Giá trị đúng |
|---|---|
| `global.json` → `sdk.version` | `7.0.410` |
| `SixosPwa.csproj` → `TargetFramework` | `net7.0` |
| `README.md` mục "Đừng đụng" | nhắc đúng `7.0.410` |

Bẫy: máy chỉ có SDK 9 vẫn build ra nhị phân **.NET 7** (vì `TargetFramework` mới là thứ quyết định),
nên nhìn `global.json` mà không nhìn `.csproj` sẽ tưởng "đã nâng cấp rồi". Ngược lại, ghim một major
mà máy đích không có SDK thì `dotnet` **báo lỗi thiếu SDK ngay từ lệnh đầu** — lỗi không dính gì tới
code nên rất khó đoán, đây là rủi ro thật khi repo sang tay máy khác.

Muốn nâng nền thật thì làm đủ ba chỗ cùng lúc **và viết ADR mới thay thế ADR 0001** — đừng sửa lẻ một
dòng `global.json`.
