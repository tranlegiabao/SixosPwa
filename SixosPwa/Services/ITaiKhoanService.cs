using SixosPwa.Models;

namespace SixosPwa.Services;

public interface ITaiKhoanService
{
    Task<TaiKhoan?> DangNhapAsync(string soDienThoaiOrEmail, string matKhau);
    Task<TaiKhoan?> LayTaiKhoanTheoSoDienThoaiAsync(string soDienThoai);
}