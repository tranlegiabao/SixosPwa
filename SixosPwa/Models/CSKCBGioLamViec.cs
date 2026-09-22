namespace SixosPwa.Models;

/// <summary>
/// Giờ làm việc của một cơ sở theo TỪNG THỨ. Thay cho cặp cột TGLamViec/NgayLamViec
/// cũ — hai cột đó nội dung ngược với tên cột.
/// </summary>
public class CSKCBGioLamViec
{
    public long Id { get; set; }
    public long IdCoSo { get; set; }

    /// <summary>Thứ trong tuần: 0 = Chủ nhật, 1..6 = Thứ 2..Thứ 7.</summary>
    public byte Thu { get; set; }

    public TimeSpan GioMoCua { get; set; }
    public TimeSpan GioDongCua { get; set; }
}
