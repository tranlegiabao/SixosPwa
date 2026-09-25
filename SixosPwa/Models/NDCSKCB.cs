namespace SixosPwa.Models;

public sealed class NDCSKCB
{
    /// <summary>Mã chủ đề — nay là DM_ChuDe.MaChuDe, trước đây là chuỗi LoaiND.</summary>
    public const string GioiThieu = "gioithieu";
    public const string DichVu = "dichvu";
    public const string DoiNgu = "doingu";
    public const string TrangThietBi = "trangthietbi";
    public const string LienHe = "lienhe";

    public static readonly IReadOnlyList<string> AllowedLoaiND = new[]
    {
        GioiThieu,
        DichVu,
        DoiNgu,
        TrangThietBi,
        LienHe
    };

    public long Id { get; set; }
    public long IdCoSo { get; set; }
    public long IdChuDe { get; set; }
    public string? NoiDung { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.Now;
    public DateTime? NgayCapNhat { get; set; }
}
