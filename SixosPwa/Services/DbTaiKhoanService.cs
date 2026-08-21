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
            return await _context.TaiKhoans
                .FirstOrDefaultAsync(tk => tk.SDT == term);
        }
    }

    public async Task<TaiKhoan?> LayTaiKhoanTheoSoDienThoaiAsync(string soDienThoai)
    {
        return await _context.TaiKhoans
            .FirstOrDefaultAsync(tk => tk.SDT == soDienThoai);
    }
}