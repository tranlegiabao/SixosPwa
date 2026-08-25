namespace SixosPwa.Models;

public sealed class QCKCB
{
    public long Id { get; set; }
    public long IdCoSo { get; set; }
    public string? NoiDung { get; set; }
    public string? Img { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.Now;
}
