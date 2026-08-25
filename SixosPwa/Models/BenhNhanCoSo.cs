namespace SixosPwa.Models;

/// <summary>
/// Ho so cua mot benh nhan TAI MOT CO SO. MaBN do chinh co so cap nen chi duy nhat
/// trong pham vi co so do, khong duy nhat toan he thong.
/// </summary>
public class BenhNhanCoSo
{
    public long Id { get; set; }
    public long IdBenhNhan { get; set; }
    public long IdCoSo { get; set; }
    public string MaBN { get; set; } = "";
    public DateTime NgayTao { get; set; } = DateTime.Now;
}
