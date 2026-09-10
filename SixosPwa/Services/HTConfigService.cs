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

        var config = await _db.HTConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaChucNang == trimmed);

        _cache.Set(cacheKey, config, CacheTtl);
        return config;
    }

    public async Task<int?> LaySoLuongAsync(string maChucNang)
    {
        var config = await LayConfigAsync(maChucNang);
        return config?.SoLuong;
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
}
