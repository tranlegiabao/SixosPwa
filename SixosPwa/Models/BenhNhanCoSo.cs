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

    /// <summary>
    /// *Cua tai lieu* (chot 9 dot 1, ADR 0020): ho so nay da duoc phep mo ket
    /// qua can lam sang / don thuoc chua.
    ///
    /// Doc no la "co so da ghi ban la dau moi lien lac cua nguoi nay", KHONG
    /// phai "ban chinh la nguoi nay". Can cua rieng vi CCCD go luc dang nhap
    /// KHONG duoc xac thuc — OTP chi xac thuc so dien thoai.
    /// </summary>
    public bool DaMoTaiLieu { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.Now;
}
