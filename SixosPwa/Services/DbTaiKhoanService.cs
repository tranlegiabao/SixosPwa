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

            // 🔴 Dot 1B: benh nhan KHONG CON tai khoan (HT_TaiKhoan chi con Admin,
            // CK_HT_TaiKhoan_Role CHECK Role='Admin'), nen khong con duong nao di
            // tu CCCD sang tai khoan. Xem ADR 0027 (da dao) va ADR 0034.
            return null;
        }
    }

    public async Task<TaiKhoan?> LayTaiKhoanTheoSoDienThoaiAsync(string soDienThoai)
    {
        return await _context.TaiKhoans
            .FirstOrDefaultAsync(tk => tk.SDT == soDienThoai);
    }
}