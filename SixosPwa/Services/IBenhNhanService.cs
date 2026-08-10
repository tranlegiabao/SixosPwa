using SixosPwa.Models;

namespace SixosPwa.Services;

/// <summary>
/// Service quản lý bệnh nhân
/// </summary>
public interface IBenhNhanService
{
    Task<List<BenhNhan>> LayDanhSachBenhNhanAsync();
    Task<BenhNhan?> LayBenhNhanTheoIdAsync(int id);
    Task<BenhNhan?> LayBenhNhanTheoSoDienThoaiAsync(string soDienThoai);
    Task<bool> ThemBenhNhanAsync(BenhNhan benhNhan);
    Task<bool> CapNhatBenhNhanAsync(BenhNhan benhNhan);
    Task<int> ImportDanhSachBenhNhanAsync(List<ImportBenhNhanDto> danhSach);
    Task<bool> CapNhatTrangThaiNhanTinNhanAsync(int benhNhanId, bool choPhep);
}

/// <summary>
/// Service quản lý lịch sử tin nhắn
/// </summary>
public interface ILichSuTinNhanService
{
    Task<bool> LuuLichSuAsync(LichSuTinNhan lichSu);
    Task<List<LichSuTinNhan>> LayLichSuTheoBenhNhanAsync(int benhNhanId);
    Task<List<LichSuTinNhan>> LayTatCaLichSuAsync(DateTime? tuNgay = null, DateTime? denNgay = null);
    Task<bool> CapNhatTrangThaiAsync(int id, string trangThai);
}

/// <summary>
/// Service quản lý mẫu tin nhắn
/// </summary>
public interface IMauTinNhanService
{
    Task<List<MauTinNhan>> LayDanhSachMauAsync();
    Task<MauTinNhan?> LayMauTheoIdAsync(int id);
    Task<bool> ThemMauAsync(MauTinNhan mau);
    Task<bool> CapNhatMauAsync(MauTinNhan mau);
    Task<bool> XoaMauAsync(int id);
}

/// <summary>
/// Service quản lý hóa đơn
/// </summary>
public interface IHoaDonService
{
    Task<List<HoaDon>> LayDanhSachHoaDonTheoBenhNhanAsync(int benhNhanId);
    Task<HoaDon?> LayHoaDonTheoIdAsync(int id);
    Task<bool> ThemHoaDonAsync(HoaDon hoaDon);
    Task<bool> CapNhatHoaDonAsync(HoaDon hoaDon);
}
