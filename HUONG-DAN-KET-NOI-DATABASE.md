# 🗄️ Hướng dẫn Kết nối Database SQL Server

## ✅ Đã hoàn thành:

### 1. Cài đặt packages:
- ✅ Microsoft.EntityFrameworkCore.SqlServer (v7.0.20)
- ✅ Microsoft.EntityFrameworkCore.Design (v7.0.20)

### 2. Tạo DbContext:
- ✅ File: `Data/AppDbContext.cs`
- ✅ Đã cấu hình tất cả models: BenhNhan, TaiKhoan, LichSuTinNhan, MauTinNhan, HoaDon
- ✅ Đã thêm seed data cho TaiKhoan và MauTinNhan

### 3. Cập nhật Connection String:
- ✅ File: `appsettings.json`
- ✅ Connection String:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=118.69.34.247,8392;Database=HIS_CSKH;User Id=sixostest;Password=sixostest;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

### 4. Đăng ký DbContext trong Program.cs:
- ✅ Đã thêm `using Microsoft.EntityFrameworkCore`
- ✅ Đã thêm `using SixosPwa.Data`
- ✅ Đã đăng ký: `builder.Services.AddDbContext<AppDbContext>`

---

## ⏳ Cần làm tiếp:

### Bước 1: DỪNG APP ĐANG CHẠY
**QUAN TRỌNG:** App đang chạy nên không build được!

```powershell
# Trong terminal đang chạy app, bấm:
Ctrl + C
```

### Bước 2: Tạo Migration
```powershell
cd d:\web\SixosPwaTemplate\SixosPwaTemplate\SixosPwa
dotnet ef migrations add InitialCreate
```

### Bước 3: Tạo bảng trong Database
```powershell
dotnet ef database update
```

Lệnh này sẽ:
- Kết nối tới SQL Server: `118.69.34.247,8392`
- Tạo database `HIS_CSKH` (nếu chưa có)
- Tạo các bảng: BenhNhan, TaiKhoan, LichSuTinNhan, MauTinNhan, HoaDon
- Insert data mẫu cho TaiKhoan và MauTinNhan

### Bước 4: Tạo Database Services (thay thế InMemory)

Tạo các service mới để dùng database thay vì InMemory:

#### File: `Services/DbBenhNhanService.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services;

public class DbBenhNhanService : IBenhNhanService
{
    private readonly AppDbContext _context;

    public DbBenhNhanService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<BenhNhan>> LayDanhSachBenhNhanAsync()
    {
        return await _context.BenhNhans.ToListAsync();
    }

    public async Task<BenhNhan?> LayBenhNhanTheoIdAsync(int id)
    {
        return await _context.BenhNhans.FindAsync(id);
    }

    public async Task<BenhNhan?> LayBenhNhanTheoSoDienThoaiAsync(string soDienThoai)
    {
        return await _context.BenhNhans.FirstOrDefaultAsync(b => b.SoDienThoai == soDienThoai);
    }

    public async Task<bool> ThemBenhNhanAsync(BenhNhan benhNhan)
    {
        _context.BenhNhans.Add(benhNhan);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> CapNhatBenhNhanAsync(BenhNhan benhNhan)
    {
        _context.BenhNhans.Update(benhNhan);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<int> ImportDanhSachBenhNhanAsync(List<ImportBenhNhanDto> danhSach)
    {
        int count = 0;
        foreach (var dto in danhSach)
        {
            if (string.IsNullOrEmpty(dto.SoDienThoai)) continue;

            var existing = await _context.BenhNhans.FirstOrDefaultAsync(b => b.SoDienThoai == dto.SoDienThoai);
            if (existing != null) continue;

            var benhNhan = new BenhNhan
            {
                MaBenhNhan = dto.MaBenhNhan,
                HoTen = dto.HoTen,
                SoDienThoai = dto.SoDienThoai,
                GioiTinh = dto.GioiTinh ?? "",
                DiaChi = dto.DiaChi ?? "",
                Email = dto.Email ?? "",
                ChoPhepNhanTinNhan = true
            };

            if (DateTime.TryParse(dto.NgaySinh, out var ngaySinh))
            {
                benhNhan.NgaySinh = ngaySinh;
            }

            _context.BenhNhans.Add(benhNhan);
            count++;
        }

        await _context.SaveChangesAsync();
        return count;
    }

    public async Task<bool> CapNhatTrangThaiNhanTinNhanAsync(int benhNhanId, bool choPhep)
    {
        var benhNhan = await _context.BenhNhans.FindAsync(benhNhanId);
        if (benhNhan == null) return false;

        benhNhan.ChoPhepNhanTinNhan = choPhep;
        benhNhan.NgayCapNhat = DateTime.Now;

        return await _context.SaveChangesAsync() > 0;
    }
}
```

#### Tương tự cho các service khác:
- `DbLichSuTinNhanService.cs`
- `DbMauTinNhanService.cs`
- `DbHoaDonService.cs`
- `DbTaiKhoanService.cs`

### Bước 5: Cập nhật Program.cs

Thay đổi từ InMemory sang Database:

```csharp
// BEFORE (InMemory):
builder.Services.AddSingleton<IBenhNhanService, InMemoryBenhNhanService>();
builder.Services.AddSingleton<ILichSuTinNhanService, InMemoryLichSuTinNhanService>();
builder.Services.AddSingleton<IMauTinNhanService, InMemoryMauTinNhanService>();
builder.Services.AddSingleton<IHoaDonService, InMemoryHoaDonService>();
builder.Services.AddSingleton<ITaiKhoanService, InMemoryTaiKhoanService>();

// AFTER (Database):
builder.Services.AddScoped<IBenhNhanService, DbBenhNhanService>();
builder.Services.AddScoped<ILichSuTinNhanService, DbLichSuTinNhanService>();
builder.Services.AddScoped<IMauTinNhanService, DbMauTinNhanService>();
builder.Services.AddScoped<IHoaDonService, DbHoaDonService>();
builder.Services.AddScoped<ITaiKhoanService, DbTaiKhoanService>();
```

### Bước 6: Chạy lại app
```powershell
dotnet run --project SixosPwa\SixosPwa.csproj --urls "https://localhost:7024"
```

---

## 🔍 Kiểm tra kết nối

### Test kết nối database:

```powershell
dotnet ef dbcontext info
```

Nếu thành công sẽ thấy:
```
Provider name: Microsoft.EntityFrameworkCore.SqlServer
Database name: HIS_CSKH
Data source: 118.69.34.247,8392
```

### Xem migrations đã tạo:
```powershell
dotnet ef migrations list
```

### Xem SQL sẽ được execute:
```powershell
dotnet ef migrations script
```

---

## ❓ Troubleshooting

### Lỗi: "Cannot connect to SQL Server"
**Nguyên nhân:** 
- Server không accessible từ máy bạn
- Port 8392 bị firewall chặn
- Thông tin đăng nhập sai

**Giải pháp:**
1. Test kết nối bằng SQL Server Management Studio (SSMS)
2. Kiểm tra firewall/network
3. Xác nhận lại username/password

### Lỗi: "Login failed for user 'sixostest'"
**Giải pháp:**
- Kiểm tra lại password
- Kiểm tra user có quyền trên database `HIS_CSKH` không

### Lỗi: "Database 'HIS_CSKH' does not exist"
**Giải pháp:**
- Tạo database trước bằng SSMS hoặc
- Đảm bảo user có quyền CREATE DATABASE

### Lỗi: "Build failed - file is locked"
**Giải pháp:**
- **Dừng app bằng Ctrl+C** trước khi build
- Hoặc kill process:
  ```powershell
  taskkill /F /IM SixosPwa.exe
  ```

---

## 📊 Cấu trúc Database sau khi tạo

### Bảng BenhNhan
- Id (PK)
- MaBenhNhan (unique)
- HoTen
- SoDienThoai (unique index)
- NgaySinh
- GioiTinh
- DiaChi
- Email
- ChoPhepNhanTinNhan
- NgayTao
- NgayCapNhat

### Bảng TaiKhoan
- Id (PK)
- TenDangNhap (unique)
- MatKhau
- HoTen
- LoaiTaiKhoan (Admin, DoiTac, BenhNhan)
- BenhNhanId (FK → BenhNhan)
- KichHoat

### Bảng LichSuTinNhan
- Id (PK)
- BenhNhanId (FK → BenhNhan)
- DoiTac
- NoiDung
- LoaiTinNhan
- ThoiGianGui
- TrangThai
- DaDoc
- GhiChu

### Bảng MauTinNhan
- Id (PK)
- TenMau
- LoaiTinNhan
- NoiDung
- MoTa
- KichHoat
- NgayTao

### Bảng HoaDon
- Id (PK)
- BenhNhanId (FK → BenhNhan)
- MaHoaDon
- NgayKham
- DichVu
- TongTien (decimal 18,2)
- DaThanhToan (decimal 18,2)
- ConLai (decimal 18,2)
- TrangThai
- NgayThanhToan

---

## 🎯 Lợi ích của Database

### So với InMemory:
- ✅ Data **persistent** - không mất khi restart app
- ✅ Chia sẻ data giữa nhiều instance/server
- ✅ Query phức tạp (JOIN, aggregate, filter...)
- ✅ Transaction support - đảm bảo data integrity
- ✅ Concurrent access - nhiều user cùng lúc
- ✅ Backup & restore
- ✅ Performance với index, caching...

### Production-ready features:
- Connection pooling
- Retry logic
- Migration versioning
- Rollback support

---

## 📚 Tài liệu tham khảo

- Entity Framework Core: https://learn.microsoft.com/en-us/ef/core/
- Migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- DbContext: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/

---

**Bước tiếp theo:** Dừng app (Ctrl+C), rồi chạy `dotnet ef migrations add InitialCreate`
