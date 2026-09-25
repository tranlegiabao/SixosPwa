# Dựng CSDL cho SixosPwa

- **Tác giả:** Nam · **Ngày:** 2026-09-22

`Database/` không còn nằm trong git ([ADR 0041](adr/0041-database-khong-len-git.md)). Tài liệu này thay
chỗ đó: mô tả CSDL mà `SixosPwa` cần để chạy, lấy thẳng từ mã nguồn hiện tại — không bịa thêm.

## CSDL

- **Tên:** `HIS_CSKH`
- **Server:** `118.69.34.247,8392`
- **Connection string** (`appsettings.json`, khoá `DaotaoHIS`):
  `Server=118.69.34.247,8392;Database=HIS_CSKH;User Id=sixostest;Password=sixostest;TrustServerCertificate=True;MultipleActiveResultSets=True`

## 13 bảng (EF Core `DbSet`, `SixosPwa/Data/ApplicationDbContext.cs`)

| `DbSet` (code) | Bảng thật (`ToTable` / `[Table]`) |
|---|---|
| `BenhNhans` | `DM_BenhNhan` |
| `TaiKhoans` | `HT_TaiKhoan` |
| `ThongBaos` | `HT_ThongBao` |
| `PushDangKys` | `HT_PushDangKy` |
| `DotKhams` | `QL_DotKham` |
| `DMCSKCBs` | `DM_CSKCB` |
| `CSKCBGioLamViecs` | `DM_CSKCB_GioLamViec` |
| `CSKCBCapQuangCaos` | `DM_CSKCB_CapQuangCao` |
| `NDCSKCBs` | `DM_CSKCB_NoiDung` |
| `DMNhomCSs` | `DM_NhomCS` |
| `DMChuDes` | `DM_ChuDe` |
| `TaiLieuBenhNhans` | `QL_TaiLieuBenhNhan` |
| `HTConfigs` | `HT_Config` |

EF Core ở đây **chỉ đọc** (`AsNoTracking`/truy vấn). EF không tự sinh migration lên các bảng này —
schema đã tồn tại sẵn trên `HIS_CSKH`.

## Đường ghi duy nhất — stored procedure

Mọi ghi dữ liệu đi qua `AdminStoredProcedureService` (`SixosPwa/Services/AdminStoredProcedureService.cs`),
gọi đúng các thủ tục sau bằng `CommandType.StoredProcedure` (không `INSERT`/`UPDATE`/`DELETE` trực
tiếp từ EF):

| Thủ tục | Dùng khi |
|---|---|
| `dbo.HT_TaiKhoan_Loc` | Lọc/tìm tài khoản (đọc, có phân trang) |
| `dbo.HT_TaiKhoan_Save` | Lưu tài khoản |
| `dbo.DM_CSKCB_GhiNhanThuDatFtp` | Ghi nhận thử đạt FTP của một cơ sở |
| `dbo.DM_BenhNhan_Save` | Lưu hồ sơ bệnh nhân |
| `dbo.DM_BenhNhan_SuaHoSo` | Sửa hồ sơ bệnh nhân |
| `dbo.DM_BenhNhan_GoNoi` | Gỡ nối mã bệnh nhân |
| `dbo.DM_BenhNhan_DoiMocXemLich` | Đổi mốc xem lịch của bệnh nhân |
| `dbo.DM_BenhNhan_TaoTuKhai` | Tạo hồ sơ tự khai |
| `dbo.DM_BenhNhan_XoaHoSo` | Xoá hồ sơ bệnh nhân |
| `dbo.DM_BenhNhanCoSo_Save` | Lưu quan hệ bệnh nhân × cơ sở |
| `dbo.DM_BenhNhan_DoiMa` | Đổi mã bệnh nhân tại cơ sở |
| `dbo.QL_TaiLieuBenhNhan_Save` | Lưu (thêm phiên bản) tài liệu bệnh nhân |
| `dbo.QL_DotKham_Save` | Lưu đợt khám |
| `dbo.DM_CSKCB_Save` | Lưu cơ sở khám chữa bệnh |
| `dbo.DM_CSKCB_Delete` | Xoá cơ sở khám chữa bệnh |
| `dbo.DM_CSKCB_NoiDung_Save` | Lưu nội dung cơ sở (giới thiệu, …) |
| `dbo.DM_CSKCB_GioLamViec_Save` | Lưu giờ làm việc cơ sở |
| `dbo.DM_CSKCB_GioLamViec_Xoa` | Xoá giờ làm việc cơ sở |
| `dbo.HT_ThongBao_Save` | Lưu thông báo |
| `dbo.HT_PushDangKy_Save` | Đăng ký push notification |

Không có đường ghi nào khác vào 13 bảng ở trên ngoài danh sách thủ tục này.

## Dựng CSDL ở môi trường mới

`Database/` (63 file `.sql` triển khai các thủ tục trên) không còn theo git — xem lý do và cách thay
thế ở [ADR 0041](adr/0041-database-khong-len-git.md). Để dựng CSDL ở một môi trường khác, cần: 13
bảng theo đúng tên/định nghĩa cột mà EF Core map ở trên, và toàn bộ 19 stored procedure trong bảng
trên với đúng tên/tham số mà `AdminStoredProcedureService` đang gọi.
