namespace SixosPwa.Models;

/// <summary>
/// Dang ky API cua tung co so — cho re nhanh "co API rieng" / "khong co API".
/// Khoa nay la IdCoSo (khoa ngoai sang DM_CSKCB); truoc dot tai kien truc no la
/// chuoi MaCoSo, khong co rang buoc nao bao dam ma do co that.
/// Them mot doi tac = them mot dong o day + mot ban cai IPartnerGateway.
/// </summary>
public class DoiTacApi
{
    public long Id { get; set; }

    /// <summary>Khoa ngoai sang <see cref="DMCSKCB"/>. Moi co so nhieu nhat mot dong.</summary>
    public long IdCoSo { get; set; }

    /// <summary>Chon ban cai cua IPartnerGateway. Xem <see cref="KieuApiDoiTac"/>.</summary>
    public string KieuApi { get; set; } = KieuApiDoiTac.KhongCo;

    /// <summary>
    /// Goc dia chi API rieng cua doi tac (vd SixOSDatKhamAPI). Rong voi kieu NONE.
    ///
    /// HIEN KHONG CON AI DOC — dung tim cach goi qua day. Voi Ung Buou, dia chi
    /// nay la IP NOI BO benh vien (10.85.9.34) nen SixosPwa chay tren internet
    /// khong bao gio goi toi duoc; ca ba cua tung nam sau no da chuyen sang
    /// TrangChu cong khai (ADR 0014).
    ///
    /// Giu cot lai vi IPartnerGateway phuc vu NHIEU doi tac: ban cai sau co the
    /// co Web API rieng that su goi duoc. Xoa di la bat nguoi sau them lai cot
    /// tren DB that cua khach, va mat luon dia chi API noi bo cua Ung Buou.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>Trang chu web cua doi tac — dich den sau khi ban giao phien.</summary>
    public string? TrangChu { get; set; }

    /// <summary>
    /// Chi nhanh ben he doi tac ma co so nay ung voi (HT_DoiTac.Id ben Ung Buou).
    ///
    /// HIEN CHUA DUNG TOI. Da thu POST select-branch giup benh nhan nhung khong
    /// chay duoc: cookie cua ho la SameSite=Lax nen POST lien site khong mang
    /// cookie (xem ADR 0003). Giu lai vi neu sau nay ben ho mo mot cua GET thi
    /// day chinh la tham so can gui.
    /// </summary>
    public int? MaChiNhanh { get; set; }

    public bool Active { get; set; } = true;

    public DateTime NgayTao { get; set; } = DateTime.Now;
}

/// <summary>Cac kieu API doi tac da biet.</summary>
public static class KieuApiDoiTac
{
    /// <summary>Co so khong co API rieng — o lai trang benh nhan noi bo.</summary>
    public const string KhongCo = "NONE";

    /// <summary>Benh vien Ung Buou — SixOSDatKhamAPI + trang MVC kcg.bvungbuou.vn.</summary>
    public const string UngBuou = "UB";
}
