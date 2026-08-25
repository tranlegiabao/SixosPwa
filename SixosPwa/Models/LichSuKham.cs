namespace SixosPwa.Models;

/// <summary>
/// Lich su kham, treo vao HO SO TAI MOT CO SO chu khong treo vao PhongKham —
/// bang PhongKham da bi xoa o dot tai kien truc (W-05).
/// </summary>
public class LichSuKham
{
    public long Id { get; set; }

    public long IdBenhNhanCoSo { get; set; }

    public DateTime NgayKhamDau { get; set; } = DateTime.Now;

    public DateTime NgayKhamGanNhat { get; set; } = DateTime.Now;

    public int SoLanKham { get; set; } = 1;

    public string? TrangThai { get; set; }
}
