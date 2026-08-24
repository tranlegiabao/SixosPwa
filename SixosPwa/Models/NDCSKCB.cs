namespace SixosPwa.Models;

public sealed class NDCSKCB
{
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
    public string? MaCoSo { get; set; }
    public string? TenCoSo { get; set; }
    public string? NoiDung { get; set; }
    public string? LoaiND { get; set; }
}
