using SixosPwa.Models;

namespace SixosPwa.Services;

/// <summary>
/// Service quản lý tài khoản
/// </summary>
public interface ITaiKhoanService
{
    Task<TaiKhoan?> DangNhapAsync(string tenDangNhap, string matKhau);
    Task<TaiKhoan?> LayTaiKhoanTheoIdAsync(int id);
    Task<bool> ThemTaiKhoanAsync(TaiKhoan taiKhoan);
}

/// <summary>
/// Service in-memory cho tài khoản
/// </summary>
public class InMemoryTaiKhoanService : ITaiKhoanService
{
    // Danh sách tài khoản mẫu
    private static readonly List<TaiKhoan> _danhSachTaiKhoan = new()
    {
        // Admin - Đăng nhập bằng SĐT: 0999999999, OTP: 123456
        new TaiKhoan 
        { 
            Id = 1, 
            TenDangNhap = "0999999999", // SĐT admin
            MatKhau = "admin123",
            HoTen = "Quản trị viên",
            LoaiTaiKhoan = "Admin",
            KichHoat = true
        },
        
        // Đối tác - SĐT: 0988888888
        new TaiKhoan 
        { 
            Id = 2, 
            TenDangNhap = "0988888888",
            MatKhau = "doitac123",
            HoTen = "Phòng khám ABC",
            LoaiTaiKhoan = "DoiTac",
            KichHoat = true
        },
        
        // Bệnh nhân 1 - SĐT: 0901234567 (link với BenhNhanId = 1)
        new TaiKhoan 
        { 
            Id = 3, 
            TenDangNhap = "0901234567",
            MatKhau = "bn123",
            HoTen = "Nguyễn Văn An",
            LoaiTaiKhoan = "BenhNhan",
            BenhNhanId = 1,
            KichHoat = true
        },
        
    // Bệnh nhân 2 - SĐT: 0912345678 (link với BenhNhanId = 2)
        new TaiKhoan 
        { 
            Id = 4, 
            TenDangNhap = "0912345678",
            MatKhau = "bn123",
            HoTen = "Trần Thị Bình",
            LoaiTaiKhoan = "BenhNhan",
            BenhNhanId = 2,
            KichHoat = true
        },

        // Bệnh nhân 4 - SĐT: 0926007363 (link với BenhNhanId = 4)
        new TaiKhoan 
        { 
            Id = 5, 
            TenDangNhap = "0926007363",
            MatKhau = "bn123",
            HoTen = "Phạm Thị Dung",
            LoaiTaiKhoan = "BenhNhan",
            BenhNhanId = 4,
            KichHoat = true
        }
    };

    public Task<TaiKhoan?> DangNhapAsync(string tenDangNhap, string matKhau)
    {
        // Tìm theo tên đăng nhập hoặc số điện thoại
        var taiKhoan = _danhSachTaiKhoan.FirstOrDefault(tk => 
            (tk.TenDangNhap == tenDangNhap || tenDangNhap.Contains(tk.TenDangNhap)) &&
            (string.IsNullOrEmpty(matKhau) || tk.MatKhau == matKhau) && 
            tk.KichHoat);

        return Task.FromResult(taiKhoan);
    }

    public Task<TaiKhoan?> LayTaiKhoanTheoIdAsync(int id)
    {
        return Task.FromResult(_danhSachTaiKhoan.FirstOrDefault(tk => tk.Id == id));
    }

    public Task<bool> ThemTaiKhoanAsync(TaiKhoan taiKhoan)
    {
        taiKhoan.Id = _danhSachTaiKhoan.Any() ? _danhSachTaiKhoan.Max(tk => tk.Id) + 1 : 1;
        _danhSachTaiKhoan.Add(taiKhoan);
        return Task.FromResult(true);
    }
}
