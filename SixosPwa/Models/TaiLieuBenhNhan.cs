namespace SixosPwa.Models;

/// <summary>
/// Tài liệu y tế của bệnh nhân (đơn thuốc, kết quả xét nghiệm, CDHA, giấy ra viện,...)
/// do HIS của cơ sở y tế đẩy vào qua API tiếp nhận tài liệu.
/// </summary>
public class TaiLieuBenhNhan
{
    public long Id { get; set; }

    /// <summary>Khóa ngoại sang <see cref="DMCSKCB"/>.</summary>
    public long IdCoSo { get; set; }

    /// <summary>Khóa ngoại sang <see cref="BenhNhanCoSo"/> (null nếu bệnh nhân chưa có hồ sơ tại cơ sở).</summary>
    public long? IdBenhNhanCoSo { get; set; }

    /// <summary>Mã bệnh nhân do chính cơ sở cấp (MaBN trong HIS).</summary>
    public string MaBN { get; set; } = "";

    /// <summary>Loại tài liệu (DON_THUOC, KET_QUA_XN, CDHA, GIAY_RA_VIEN,...).</summary>
    public string LoaiTaiLieu { get; set; } = "";

    /// <summary>Tên hiển thị / tiêu đề của tài liệu.</summary>
    public string TenTaiLieu { get; set; } = "";

    /// <summary>Đường dẫn lưu file trên FTP.</summary>
    public string DuongDanFtp { get; set; } = "";

    /// <summary>Dung lượng file (byte).</summary>
    public long DungLuongByte { get; set; }

    /// <summary>Ngày khám / ngày phát hành tài liệu.</summary>
    public DateTime? NgayKham { get; set; }

    /// <summary>Ghi chú bổ sung.</summary>
    public string? GhiChu { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
