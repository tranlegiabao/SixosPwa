namespace SixosPwa.Services;

/// <summary>
/// Quy uoc duong dan anh giua kho FTP dung chung va URL cong khai.
///
///     FTP   sixospwa/logo_cs/x.jpg
///     URL   /anh/logo_cs/x.jpg
///
/// Kho FTP nay dung CHUNG voi HisSoft (goc FTP dang co ttpt_images, HinhAnh,
/// 79423... cua master_3). Moi thu cua SixosPwa nam gon duoi mot thu muc goc
/// de khong the xoa nham do cua HisSoft: xem HangRao trong DonAnhService.
/// </summary>
public static class KhoAnh
{
    /// <summary>Thu muc goc cua rieng SixosPwa tren FTP dung chung.</summary>
    public const string GocFtp = "sixospwa";

    /// <summary>Tien to URL cua route doc anh (AnhController).</summary>
    public const string TienToUrl = "/anh";

    public const string ThuMucLogo = "logo_cs";
    public const string ThuMucQuangCao = "img_qc_kcb";
    public const string ThuMucCoSo = "img_cs";
    public const string ThuMucNoiDung = "img_nd";

    /// <summary>
    /// Bon thu muc nay giu dung ten cu trong wwwroot/static de con doi chieu
    /// duoc voi du lieu truoc khi chuyen kho.
    /// </summary>
    public static readonly string[] ThuMucHopLe =
    {
        ThuMucLogo, ThuMucQuangCao, ThuMucCoSo, ThuMucNoiDung
    };

    public static bool LaThuMucHopLe(string? thuMuc) =>
        !string.IsNullOrWhiteSpace(thuMuc)
        && ThuMucHopLe.Contains(thuMuc, StringComparer.OrdinalIgnoreCase);

    /// <summary>Duong dan thu muc tren FTP, vd "sixospwa/logo_cs".</summary>
    public static string ThuMucFtp(string thuMuc) => $"{GocFtp}/{thuMuc}";

    /// <summary>
    /// Doi ket qua cua IFtpService.UploadFileAsync ("sixospwa/logo_cs/x.jpg")
    /// thanh URL cat vao cot DB ("/anh/logo_cs/x.jpg").
    /// </summary>
    public static string? UrlTuDuongDanFtp(string? duongDanFtp)
    {
        if (string.IsNullOrWhiteSpace(duongDanFtp)) return null;

        var duongDan = duongDanFtp.Replace('\\', '/').TrimStart('/');
        if (!duongDan.StartsWith(GocFtp + "/", StringComparison.OrdinalIgnoreCase)) return null;

        return $"{TienToUrl}/{duongDan[(GocFtp.Length + 1)..]}";
    }

    /// <summary>
    /// Chieu nguoc lai. Tra null khi URL KHONG thuoc kho anh cua minh — do la
    /// hang rao chinh: link http(s) admin dan vao, hay duong dan cu /static/...,
    /// deu khong quy ra duoc duong dan FTP nen khong the bi xoa.
    /// </summary>
    public static string? DuongDanFtpTuUrl(string? url)
    {
        var thuMuc = ThuMucTuUrl(url);
        var ten = TenTepTuUrl(url);
        if (thuMuc == null || ten == null) return null;

        return $"{ThuMucFtp(thuMuc)}/{ten}";
    }

    public static string? ThuMucTuUrl(string? url)
    {
        var doan = TachDoan(url);
        return doan != null && LaThuMucHopLe(doan[0]) ? doan[0] : null;
    }

    public static string? TenTepTuUrl(string? url)
    {
        var doan = TachDoan(url);
        return doan != null && LaThuMucHopLe(doan[0]) ? doan[1] : null;
    }

    /// <summary>
    /// Tach "/anh/logo_cs/x.jpg" thanh ["logo_cs", "x.jpg"]. Bo qua chuoi rong,
    /// URL tuyet doi, va moi thu khong bat dau bang tien to cua kho.
    /// </summary>
    private static string[]? TachDoan(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var duongDan = url.Trim();
        if (!duongDan.StartsWith(TienToUrl + "/", StringComparison.OrdinalIgnoreCase)) return null;

        var phanConLai = duongDan[(TienToUrl.Length + 1)..];
        var viTri = phanConLai.IndexOf('/');
        if (viTri <= 0 || viTri == phanConLai.Length - 1) return null;

        var thuMuc = phanConLai[..viTri];
        var ten = phanConLai[(viTri + 1)..];

        // Ten tep khong duoc chua them dau gach cheo hay ".." — chan di ra ngoai kho.
        if (ten.Contains('/') || ten.Contains('\\') || ten.Contains("..")) return null;

        return new[] { thuMuc, ten };
    }

    /// <summary>
    /// Suy kieu noi dung theo duoi tep. Ban master_3 (HomeController.cs:133) tra
    /// cung "image/png" cho moi anh; o day kho co du bon loai nen phai suy that.
    /// </summary>
    public static string KieuNoiDung(string tenTep) =>
        Path.GetExtension(tenTep).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" or ".jfif" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
}
