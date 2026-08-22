namespace SixosPwa.Models;

/// <summary>
/// Dang ky API cua tung co so — cho re nhanh "co API rieng" / "khong co API".
/// Khoa la MaCoSo chu KHONG phai MaDT: DMCSKCB va DMDoiTac hien khong co cot nao
/// noi voi nhau, va MaCoSo moi la thu trang co so mang theo.
/// Them mot doi tac o giai doan 2 = them mot dong o day + mot ban cai IPartnerGateway.
/// </summary>
public class DoiTacApi
{
    public long Id { get; set; }

    /// <summary>Khoa noi voi <see cref="DMCSKCB.MaCoSo"/>.</summary>
    public string MaCoSo { get; set; } = "";

    /// <summary>
    /// Chon ban cai cua IPartnerGateway. Xem <see cref="KieuApiDoiTac"/>.
    /// </summary>
    public string KieuApi { get; set; } = KieuApiDoiTac.KhongCo;

    /// <summary>Goc dia chi API cua doi tac (vd SixOSDatKhamAPI). Rong voi kieu NONE.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Trang chu web cua doi tac — dich den sau khi ban giao phien.</summary>
    public string? TrangChu { get; set; }

    public int Active { get; set; } = 1;

    public DateTime NgayTao { get; set; } = DateTime.Now;
}

/// <summary>Cac kieu API doi tac da biet. Giai doan 3 se them vao day.</summary>
public static class KieuApiDoiTac
{
    /// <summary>Co so khong co API rieng — o lai trang benh nhan noi bo.</summary>
    public const string KhongCo = "NONE";

    /// <summary>Benh vien Ung Buou — SixOSDatKhamAPI + trang MVC kcg.bvungbuou.vn.</summary>
    public const string UngBuou = "UB";
}
