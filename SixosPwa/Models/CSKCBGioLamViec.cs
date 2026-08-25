namespace SixosPwa.Models;

/// <summary>
/// Gio lam viec cua mot co so theo TUNG THU. Thay cho cap cot TGLamViec/NgayLamViec
/// cu — hai cot do noi dung nguoc voi ten cot.
/// </summary>
public class CSKCBGioLamViec
{
    public long Id { get; set; }
    public long IdCoSo { get; set; }

    /// <summary>Thu trong tuan: 2..8 (8 = Chu nhat), theo cach doc quen thuoc o VN.</summary>
    public byte Thu { get; set; }

    public TimeSpan GioMoCua { get; set; }
    public TimeSpan GioDongCua { get; set; }
}
