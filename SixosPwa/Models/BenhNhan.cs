namespace SixosPwa.Models;

/// <summary>
/// Mot CON NGUOI, khoa dinh danh la CCCD.
/// Ho so tai tung co so nam o <see cref="BenhNhanCoSo"/> — truoc dot tai kien truc
/// hai khai niem nay bi tron chung trong mot bang DMBenhNhan.
/// </summary>
public class BenhNhan
{
    public long Id { get; set; }
    public string CCCD { get; set; } = "";
    public string TenBN { get; set; } = "";
    public string? SDT { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }

    /// <summary>
    /// Tai khoan dang quan ho so nay (ADR 0019). Quan he 1-N: mot tai khoan
    /// quan nhieu con nguoi — con dat kham cho me, me theo doi ket qua cho con.
    /// Truoc day chieu nguoc lai, <c>HT_TaiKhoan.IDBenhNhan</c>, la 1-1.
    /// Cot cu VAN CON vi khu Admin doc no; cot nay moi la nguon su that cua cong.
    /// </summary>
    public long? IdTaiKhoan { get; set; }

    /// <summary>Mot trong ba o cua luat gop ho so (ADR 0018).</summary>
    public DateTime? NgaySinh { get; set; }

    /// <summary>
    /// Ten da chuan hoa bo dau — o thu ba cua luat gop. Ben HIS cot cung ten nay
    /// RONG 100% (74.725/74.725) nen day la ket qua chuan hoa cua CONG, khong
    /// phai ban chep tu HIS.
    /// </summary>
    public string? HoTenKhongDau { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
