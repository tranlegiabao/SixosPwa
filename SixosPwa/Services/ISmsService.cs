namespace SixosPwa.Services;

/// <summary>
/// Dịch vụ gửi SMS cho bệnh nhân
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Gửi SMS nhắc lịch khám
    /// </summary>
    Task<bool> GuiNhacLichKhamAsync(string soDienThoai, string tenBenhNhan, DateTime thoiGianKham, string tenBacSi, string phongKham);

    /// <summary>
    /// Gửi SMS xác nhận đặt lịch
    /// </summary>
    Task<bool> GuiXacNhanDatLichAsync(string soDienThoai, string tenBenhNhan, DateTime thoiGianKham, string maDatLich);

    /// <summary>
    /// Gửi SMS kết quả xét nghiệm
    /// </summary>
    Task<bool> GuiKetQuaXetNghiemAsync(string soDienThoai, string tenBenhNhan, string loaiXetNghiem, string linkXemKetQua);

    /// <summary>
    /// Gửi SMS nhắc uống thuốc
    /// </summary>
    Task<bool> GuiNhacUongThuocAsync(string soDienThoai, string tenBenhNhan, string tenThuoc, string lieuDung);

    /// <summary>
    /// Gửi SMS tùy chỉnh
    /// </summary>
    Task<bool> GuiSmsAsync(string soDienThoai, string noiDung);
}
