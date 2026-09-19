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

            // Chi con MOT chieu noi: DM_BenhNhan.IDTaiKhoan (1 tai khoan - N ho so).
            // Nhanh cu noi nguoc qua HT_TaiKhoan.IDBenhNhan da bi go (ADR 0019).
            return await (from p in _context.BenhNhans
                          join t in _context.TaiKhoans on p.IdTaiKhoan equals t.Id
                          where p.CCCD == term
                          select t).FirstOrDefaultAsync();
        }
    }

    public async Task<TaiKhoan?> LayTaiKhoanTheoSoDienThoaiAsync(string soDienThoai)
    {
        return await _context.TaiKhoans
            .FirstOrDefaultAsync(tk => tk.SDT == soDienThoai);
    }
}