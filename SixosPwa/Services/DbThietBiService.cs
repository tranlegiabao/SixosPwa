using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services;

public class DbThietBiService : IThietBiService
{
    private readonly ApplicationDbContext _context;

    public DbThietBiService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ThietBi?> LayTheoSdtVaIdAsync(string sdt, string idThietBi)
    {
        return await _context.ThietBis
            .FirstOrDefaultAsync(x => x.SDT == sdt && x.IdThietBi == idThietBi);
    }

    public async Task<bool> LuuHoacCapNhatAsync(string sdt, string idThietBi, string tenThietBi)
    {
        if (string.IsNullOrWhiteSpace(sdt) || string.IsNullOrWhiteSpace(idThietBi))
        {
            return false;
        }

        try
        {
            var existing = await _context.ThietBis
                .FirstOrDefaultAsync(x => x.SDT == sdt && x.IdThietBi == idThietBi);

            // R?t ng?n MaBN th?nh 20 k? t? t?i ?a (b?ng y?u c?u MaBN NOT NULL maxlength 20)
            var maBNTruncated = sdt.Length <= 20 ? sdt : sdt.Substring(0, 20);

            if (existing != null)
            {
                existing.TenThietBi = tenThietBi;
                existing.TrangThai = true;
                existing.MaBN = maBNTruncated;
                await _context.SaveChangesAsync();
                return true;
            }

            var thietBi = new ThietBi
            {
                SDT = sdt,
                MaBN = maBNTruncated,
                IdThietBi = idThietBi,
                TenThietBi = tenThietBi,
                TrangThai = true
            };

            _context.ThietBis.Add(thietBi);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Log l?i n?u c?n
            System.Diagnostics.Debug.WriteLine($"L?i l?u thi?t b?: {ex.Message}");
            return false;
        }
    }
}