using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services;

/// <summary>
/// Triển khai dịch vụ tra cứu cấu hình từ bảng HT_Config có sử dụng IMemoryCache.
/// </summary>
public class HTConfigService : IHTConfigService
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HTConfigService> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    public HTConfigService(ApplicationDbContext db, IMemoryCache cache, ILogger<HTConfigService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> KiemTraHieuLucAsync(string maChucNang, bool macDinh = false)
    {
        if (string.IsNullOrWhiteSpace(maChucNang)) return macDinh;

        var config = await LayConfigAsync(maChucNang);
        return config?.HieuLuc ?? macDinh;
    }

    public async Task<HTConfig?> LayConfigAsync(string maChucNang)
    {
        if (string.IsNullOrWhiteSpace(maChucNang)) return null;

        var trimmed = maChucNang.Trim();
        var cacheKey = $"HT_Config:{trimmed.ToUpperInvariant()}";
        if (_cache.TryGetValue(cacheKey, out HTConfig? cached))
        {
            return cached;
        }

        // 🔴 SingleOrDefault chứ không FirstOrDefault: sau bước A8 thì HT_Config có
        // UNIQUE(MaChucNang). Trùng mã phải NỔ ra chứ không im lặng lấy dòng đầu —
        // im lặng thì một mã bị nhân đôi sẽ bật/tắt tính năng theo dòng nào tuỳ plan.
        var config = await _db.HTConfigs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.MaChucNang == trimmed);

        _cache.Set(cacheKey, config, CacheTtl);
        return config;
    }

    /// <summary>
    /// 🔴 Cột <c>HT_Config.SoLuong</c> đã được gộp vào <c>GiaTri</c> (dữ liệu thật: cả hai
    /// đang NULL, chỉ <c>HieuLuc</c> được dùng ⇒ gộp an toàn). Giữ tên phương thức để
    /// không vỡ nơi gọi, nhưng nay đọc <c>GiaTri</c>.
    /// </summary>
    public async Task<int?> LaySoLuongAsync(string maChucNang)
    {
        var config = await LayConfigAsync(maChucNang);
        return config?.GiaTri;
    }

    public async Task<int?> LayGiaTriAsync(string maChucNang)
    {
        var config = await LayConfigAsync(maChucNang);
        return config?.GiaTri;
    }

    public void XoaCache(string maChucNang)
    {
        if (!string.IsNullOrWhiteSpace(maChucNang))
        {
            var cacheKey = $"HT_Config:{maChucNang.Trim().ToUpperInvariant()}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("Da xoa cache HT_Config cho ma: {MaChucNang}", maChucNang);
        }
    }

    public async Task<List<HTConfig>> LayDanhSachAsync(string? q = null, string? nhom = null)
    {
        var query = _db.HTConfigs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim().ToLower();
            query = query.Where(x => (x.MaChucNang != null && x.MaChucNang.ToLower().Contains(keyword))
                                  || (x.Ghichu != null && x.Ghichu.ToLower().Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(nhom))
        {
            var nhomTrim = nhom.Trim();
            query = query.Where(x => x.Nhom == nhomTrim);
        }

        return await query.OrderBy(x => x.Nhom).ThenBy(x => x.MaChucNang).ToListAsync();
    }

    public async Task<List<string>> LayDanhSachNhomAsync()
    {
        return await _db.HTConfigs.AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Nhom))
            .Select(x => x.Nhom!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    public async Task<bool> CapNhatHieuLucAsync(long id, bool hieuLuc)
    {
        var item = await _db.HTConfigs.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return false;

        item.HieuLuc = hieuLuc;
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(item.MaChucNang))
        {
            XoaCache(item.MaChucNang);
        }
        _logger.LogInformation("Cap nhat HieuLuc={HieuLuc} cho HT_Config [ID={Id}, MaChucNang={Ma}]", hieuLuc, id, item.MaChucNang);
        return true;
    }
}
