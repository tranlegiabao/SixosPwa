namespace SixosPwa.Models;

/// <summary>
/// Model tài khoản đăng nhập
/// </summary>
public class TaiKhoan
{
    public int Id { get; set; }
    public string TenDangNhap { get; set; } = "";
    public string MatKhau { get; set; } = ""; // Thực tế nên hash
    public string HoTen { get; set; } = "";
    public string LoaiTaiKhoan { get; set; } = ""; // Admin, BenhNhan, DoiTac
    public int? BenhNhanId { get; set; } // Link đến bệnh nhân nếu là tài khoản bệnh nhân
    public bool KichHoat { get; set; } = true;
    public DateTime NgayTao { get; set; } = DateTime.Now;
}

/// <summary>
/// DTO đăng nhập
/// </summary>
public class DangNhapDto
{
    public string TenDangNhap { get; set; } = "";
    public string MatKhau { get; set; } = "";
}
