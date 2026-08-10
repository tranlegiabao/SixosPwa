namespace SixosPwa.Models;

/// <summary>
/// Model bệnh nhân
/// </summary>
public class BenhNhan
{
    public int Id { get; set; }
    public string MaBenhNhan { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public DateTime? NgaySinh { get; set; }
    public string GioiTinh { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string Email { get; set; } = "";
    public bool ChoPhepNhanTinNhan { get; set; } = true;
    public DateTime NgayTao { get; set; } = DateTime.Now;
    public DateTime? NgayCapNhat { get; set; }
}

/// <summary>
/// Model lịch sử tin nhắn
/// </summary>
public class LichSuTinNhan
{
    public int Id { get; set; }
    public int BenhNhanId { get; set; }
    public string? DoiTac { get; set; }
    public string NoiDung { get; set; } = "";
    public string LoaiTinNhan { get; set; } = ""; // NhacLichKham, KetQuaXetNghiem, NhacUongThuoc, TuyChon
    public DateTime ThoiGianGui { get; set; } = DateTime.Now;
    public string TrangThai { get; set; } = "DaGui"; // DaGui, ThatBai, DaDoc
    public bool DaDoc { get; set; } = false; // Đánh dấu bệnh nhân đã đọc hay chưa
    public string? GhiChu { get; set; }
    
    // Navigation
    public BenhNhan? BenhNhan { get; set; }
}

/// <summary>
/// Model mẫu tin nhắn
/// </summary>
public class MauTinNhan
{
    public int Id { get; set; }
    public string TenMau { get; set; } = "";
    public string LoaiTinNhan { get; set; } = "";
    public string NoiDung { get; set; } = "";
    public string MoTa { get; set; } = "";
    public bool KichHoat { get; set; } = true;
    public DateTime NgayTao { get; set; } = DateTime.Now;
}

/// <summary>
/// Model hóa đơn
/// </summary>
public class HoaDon
{
    public int Id { get; set; }
    public int BenhNhanId { get; set; }
    public string MaHoaDon { get; set; } = "";
    public DateTime NgayKham { get; set; }
    public string DichVu { get; set; } = "";
    public decimal TongTien { get; set; }
    public decimal DaThanhToan { get; set; }
    public decimal ConLai { get; set; }
    public string TrangThai { get; set; } = "ChuaThanhToan"; // DaThanhToan, ChuaThanhToan, ThanhToanMotPhan
    public DateTime? NgayThanhToan { get; set; }
    
    // Navigation
    public BenhNhan? BenhNhan { get; set; }
}

/// <summary>
/// DTO Import bệnh nhân
/// </summary>
public class ImportBenhNhanDto
{
    public string MaBenhNhan { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public string? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? DiaChi { get; set; }
    public string? Email { get; set; }
}
