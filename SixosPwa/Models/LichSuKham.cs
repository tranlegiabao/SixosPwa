namespace SixosPwa.Models;

/// <summary>Lịch sử khám bệnh - Liên kết giữa Bệnh nhân và Phòng khám</summary>
public class LichSuKham
{
    public long Id { get; set; }

    /// <summary>Mã bệnh nhân</summary>
    public string MaBN { get; set; } = "";

    /// <summary>ID phòng khám</summary>
    public long PhongKhamId { get; set; }

    /// <summary>Navigation property</summary>
    public PhongKham? PhongKham { get; set; }

    /// <summary>Ngày khám lần đầu</summary>
    public DateTime NgayKhamDau { get; set; } = DateTime.Now;

    /// <summary>Ngày khám gần nhất</summary>
    public DateTime NgayKhamGanNhat { get; set; } = DateTime.Now;

    /// <summary>Số lần đã khám</summary>
    public int SoLanKham { get; set; } = 1;

    /// <summary>Trạng thái (Đang điều trị, Đã khỏi, ...)</summary>
    public string? TrangThai { get; set; }
}
