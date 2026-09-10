using SixosPwa.Models;

namespace SixosPwa.Services;

/// <summary>
/// Dịch vụ tra cứu và quản lý cấu hình hệ thống từ bảng HT_Config.
/// Tích hợp IMemoryCache (TTL 1 phút) để tối ưu hiệu năng.
/// </summary>
public interface IHTConfigService
{
    /// <summary>
    /// Kiểm tra cấu hình có đang bật hiệu lực hay không (HieuLuc == true).
    /// </summary>
    Task<bool> KiemTraHieuLucAsync(string maChucNang, bool macDinh = false);

    /// <summary>
    /// Lấy toàn bộ thông tin cấu hình theo MaChucNang.
    /// </summary>
    Task<HTConfig?> LayConfigAsync(string maChucNang);

    /// <summary>
    /// Lấy giá trị SoLuong của cấu hình.
    /// </summary>
    Task<int?> LaySoLuongAsync(string maChucNang);

    /// <summary>
    /// Lấy giá trị GiaTri của cấu hình.
    /// </summary>
    Task<int?> LayGiaTriAsync(string maChucNang);

    /// <summary>
    /// Xóa cache bộ nhớ của một mã cấu hình.
    /// </summary>
    void XoaCache(string maChucNang);

    /// <summary>
    /// Lấy danh sách tất cả cấu hình với bộ lọc tìm kiếm và nhóm.
    /// </summary>
    Task<List<HTConfig>> LayDanhSachAsync(string? q = null, string? nhom = null);

    /// <summary>
    /// Lấy danh sách các nhóm cấu hình phân biệt (phục vụ bộ lọc).
    /// </summary>
    Task<List<string>> LayDanhSachNhomAsync();

    /// <summary>
    /// Bật hoặc tắt hiệu lực của cấu hình theo ID và xóa cache tức thì.
    /// </summary>
    Task<bool> CapNhatHieuLucAsync(long id, bool hieuLuc);
}
