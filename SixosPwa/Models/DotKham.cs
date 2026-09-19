namespace SixosPwa.Models;

/// <summary>
/// Mot dong = MOT LAN DEN kham. Thay cho <c>QL_LichSuKham</c> cu (bang 4 cot
/// dem, da khai tu) — ba so dem do suy thang tu bang nay bang MIN/MAX/COUNT.
///
/// <para>
/// Khoa tu nhien la <c>(IdCoSo, MaVaoVien)</c>: HIS day lai ca lo cung khong de
/// dong trung. Do that ben Thien Nam: 142.895 dot kham nam 2026 tren 142.542
/// cap (benh nhan, ngay) — mot dot xap xi mot ngay, chi 353 ca trung.
/// </para>
/// <para>
/// Khoa va bac si luu bang TEN chu khong bang ID: ID cua HIS khong co nghia gi
/// ben cong, va moi co so danh so mot kieu.
/// </para>
/// </summary>
public class DotKham
{
    public long Id { get; set; }

    public long IdCoSo { get; set; }

    public long IdBenhNhanCoSo { get; set; }

    /// <summary>Dinh danh dot kham ben HIS — nua con lai cua khoa tu nhien.</summary>
    public string MaVaoVien { get; set; } = "";

    // 🔴 Ba cot MaBN / NgayGioRa / ChanDoan da BI BO khoi QL_DotKham (dot A, §3).
    //    MaBN suy duoc qua IDBenhNhanCoSo -> DM_BenhNhanCoSo.MaBN, khong can chep lai.
    //    DTO `DotKhamDtos` VAN GIU ba truong nay vi HIS dang gui len — nhan roi BO.

    public DateTime NgayGioVao { get; set; }

    public string? TenKhoa { get; set; }

    public string? TenBacSi { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;

    public DateTime? NgayCapNhat { get; set; }
}
