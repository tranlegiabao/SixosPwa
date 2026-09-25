using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services;

public class DbTaiKhoanService : ITaiKhoanService
{
    private readonly ApplicationDbContext _context;

    public DbTaiKhoanService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TaiKhoan?> DangNhapAsync(string soDienThoaiOrEmail, string matKhau)
    {
        if (string.IsNullOrWhiteSpace(soDienThoaiOrEmail)) return null;

        var term = soDienThoaiOrEmail.Trim().ToLower();
        if (term.Contains('@'))
        {
            return await _context.TaiKhoans
                .FirstOrDefaultAsync(tk => tk.Email != null && tk.Email.ToLower() == term);
        }
        else
        {
            var tk = await _context.TaiKhoans
                .FirstOrDefaultAsync(tk => tk.SDT == term);
            if (tk != null) return tk;

            // 🔴 Đợt 1B: bệnh nhân KHÔNG CÒN tài khoản (HT_TaiKhoan chỉ còn Admin,
            // CK_HT_TaiKhoan_Role CHECK Role='Admin'), nên không còn đường nào đi
            // từ CCCD sang tài khoản. Xem ADR 0027 (đã đảo) và ADR 0040.
            return null;
        }
    }

    public async Task<TaiKhoan?> LayTaiKhoanTheoSoDienThoaiAsync(string soDienThoai)
    {
        return await _context.TaiKhoans
            .FirstOrDefaultAsync(tk => tk.SDT == soDienThoai);
    }
}