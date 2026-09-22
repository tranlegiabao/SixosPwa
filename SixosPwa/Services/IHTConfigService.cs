using SixosPwa.Models;

namespace SixosPwa.Services;

/// <summary>
/// Dịch vụ tra cứu và quản lý cấu hình hệ thống từ bảng HT_Config.
/// Tích hợp IMemoryCache (TTL 1 phút) để tối ưu hiệu năng.
/// </summary>
public interface IHTConfigService
{
    Task<bool> KiemTraHieuLucAsync(string maChucNang, bool macDinh = false);

    Task<HTConfig?> LayConfigAsync(string maChucNang);

    Task<int?> LaySoLuongAsync(string maChucNang);

    Task<int?> LayGiaTriAsync(string maChucNang);

    void XoaCache(string maChucNang);

    Task<List<HTConfig>> LayDanhSachAsync(string? q = null, string? nhom = null);

    Task<List<string>> LayDanhSachNhomAsync();

    Task<bool> CapNhatHieuLucAsync(long id, bool hieuLuc);
}
