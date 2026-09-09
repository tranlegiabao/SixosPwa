namespace SixosPwa.Services;

/// <summary>
/// Quy uoc duong dan anh giua kho FTP dung chung va URL cong khai.
///
///     Moi (theo co so):
///     FTP   sixospwa/{MaCoSo}/{thuMuc}/{ten} (vd: sixospwa/CS1/logo/x.jpg, sixospwa/_chung/noi_dung/y.jpg)
///     URL   /anh/{MaCoSo}/{thuMuc}/{ten} (vd: /anh/CS1/logo/x.jpg, /anh/_chung/noi_dung/y.jpg)
///
///     Cu (backward-compatible):
///     FTP   sixospwa/{thuMuc}/{ten} (vd: sixospwa/logo_cs/x.jpg)
///     URL   /anh/{thuMuc}/{ten} (vd: /anh/logo_cs/x.jpg)
///
/// Kho FTP nay dung CHUNG voi HisSoft. Moi thu cua SixosPwa nam gon duoi thu muc goc
/// 'sixospwa' de khong the xoa nham do cua HisSoft: xem HangRao trong DonAnhService.
/// </summary>
public static class KhoAnh
{
    /// <summary>Thu muc goc cua rieng SixosPwa tren FTP dung chung.</summary>
    public const string GocFtp = "sixospwa";

    /// <summary>Tien to URL cua route doc anh (AnhController).</summary>
    public const string TienToUrl = "/anh";

    public const string CoSoChung = "_chung";

    // Ten thu muc con moi (chuẩn hóa theo từng cơ sở)
    public const string ThuMucLogo = "logo";
    public const string ThuMucQuangCao = "quang_cao";
    public const string ThuMucHinhAnh = "hinh_anh";
    public const string ThuMucCoSo = "hinh_anh"; // Alias giu tuong thich code cu
    public const string ThuMucNoiDung = "noi_dung";
    public const string ThuMucTaiLieu = "tailieu";

    /// <summary>Danh sach thu muc con moi hop le duoi co so.</summary>
    public static readonly string[] ThuMucMoiHopLe =
    {
        ThuMucLogo, ThuMucQuangCao, ThuMucHinhAnh, ThuMucNoiDung
    };

    /// <summary>Bon thu muc cu truoc dot tai cau truc (backward compatibility).</summary>
    public static readonly string[] ThuMucCuHopLe =
    {
        "logo_cs", "img_qc_kcb", "img_cs", "img_nd"
    };

    /// <summary>Tat ca thu muc anh hop le (ca moi va cu).</summary>
    public static readonly string[] ThuMucHopLe =
        ThuMucMoiHopLe.Concat(ThuMucCuHopLe).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static bool LaThuMucHopLe(string? thuMuc) =>
        !string.IsNullOrWhiteSpace(thuMuc)
        && ThuMucHopLe.Contains(thuMuc, StringComparer.OrdinalIgnoreCase);

    /// <summary>Duong dan thu muc tren FTP theo co so, vd "sixospwa/CS1/logo".</summary>
    public static string ThuMucFtp(string? maCoSo, string thuMuc)
    {
        var safeMa = string.IsNullOrWhiteSpace(maCoSo) ? CoSoChung : maCoSo.Trim();
        return $"{GocFtp}/{safeMa}/{thuMuc}";
    }

    /// <summary>Duong dan thu muc cu tren FTP, vd "sixospwa/logo_cs" hoac fallback.</summary>
    public static string ThuMucFtp(string thuMuc) => $"{GocFtp}/{thuMuc}";

    /// <summary>
    /// Doi ket qua cua IFtpService.UploadFileAsync ("sixospwa/CS1/logo/x.jpg" hoac "sixospwa/logo_cs/x.jpg")
    /// thanh URL cat vao cot DB ("/anh/CS1/logo/x.jpg" hoac "/anh/logo_cs/x.jpg").
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
        var doan = TachDoan(url);
        if (doan == null) return null;

        if (doan.Length == 3) // [maCoSo, thuMuc, ten]
        {
            var maCoSo = doan[0];
            var thuMuc = doan[1];
            var ten = doan[2];
            return $"{GocFtp}/{maCoSo}/{thuMuc}/{ten}";
        }

        if (doan.Length == 2) // [thuMuc, ten]
        {
            var thuMuc = doan[0];
            var ten = doan[1];
            return $"{GocFtp}/{thuMuc}/{ten}";
        }

        return null;
    }

    public static string? ThuMucTuUrl(string? url)
    {
        var doan = TachDoan(url);
        if (doan == null) return null;
        return doan.Length == 3 ? doan[1] : doan[0];
    }

    public static string? TenTepTuUrl(string? url)
    {
        var doan = TachDoan(url);
        if (doan == null) return null;
        return doan.Length == 3 ? doan[2] : doan[1];
    }

    /// <summary>
    /// Tach URL "/anh/..." thanh mang cac phan:
    /// - "/anh/CS1/logo/x.jpg" -> ["CS1", "logo", "x.jpg"]
    /// - "/anh/logo_cs/x.jpg" -> ["logo_cs", "x.jpg"]
    /// </summary>
    private static string[]? TachDoan(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var duongDan = url.Trim();
        if (!duongDan.StartsWith(TienToUrl + "/", StringComparison.OrdinalIgnoreCase)) return null;

        var phanConLai = duongDan[(TienToUrl.Length + 1)..].Trim('/');
        var parts = phanConLai.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Chan traversal ra ngoai kho
        if (parts.Any(p => p.Contains("..") || p.Contains('\\'))) return null;

        if (parts.Length == 3)
        {
            var maCoSo = parts[0];
            var thuMuc = parts[1];
            var ten = parts[2];
            if (!LaThuMucHopLe(thuMuc)) return null;
            return new[] { maCoSo, thuMuc, ten };
        }

        if (parts.Length == 2)
        {
            var thuMuc = parts[0];
            var ten = parts[1];
            if (!LaThuMucHopLe(thuMuc)) return null;
            return new[] { thuMuc, ten };
        }

        return null;
    }

    /// <summary>
    /// Suy kieu noi dung theo duoi tep.
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
