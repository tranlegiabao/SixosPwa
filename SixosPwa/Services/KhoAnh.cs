namespace SixosPwa.Services;

/// <summary>
/// Quy ước đường dẫn ảnh giữa kho FTP dùng chung và URL công khai.
///
///     Mới (theo cơ sở):
///     FTP   sixospwa/{MaCoSo}/{thuMuc}/{ten} (vd: sixospwa/CS1/logo/x.jpg, sixospwa/_chung/noi_dung/y.jpg)
///     URL   /anh/{MaCoSo}/{thuMuc}/{ten} (vd: /anh/CS1/logo/x.jpg, /anh/_chung/noi_dung/y.jpg)
///
///     Cũ (backward-compatible):
///     FTP   sixospwa/{thuMuc}/{ten} (vd: sixospwa/logo_cs/x.jpg)
///     URL   /anh/{thuMuc}/{ten} (vd: /anh/logo_cs/x.jpg)
///
/// Kho FTP này dùng CHUNG với HisSoft. Mọi thứ của SixosPwa nằm gọn dưới thư mục gốc
/// 'sixospwa' để không thể xóa nhầm đồ của HisSoft: xem HangRao trong DonAnhService.
/// </summary>
public static class KhoAnh
{
    /// <summary>Thư mục gốc của riêng SixosPwa trên FTP dùng chung.</summary>
    public const string GocFtp = "sixospwa";

    /// <summary>Tiền tố URL của route đọc ảnh (AnhController).</summary>
    public const string TienToUrl = "/anh";

    public const string CoSoChung = "_chung";

    // Tên thư mục con mới (chuẩn hóa theo từng cơ sở)
    public const string ThuMucLogo = "logo";
    public const string ThuMucQuangCao = "quang_cao";
    public const string ThuMucHinhAnh = "hinh_anh";
    public const string ThuMucCoSo = "hinh_anh"; // Alias giữ tương thích code cũ
    public const string ThuMucNoiDung = "noi_dung";
    public const string ThuMucTaiLieu = "tailieu";

    /// <summary>Danh sách thư mục con mới hợp lệ dưới cơ sở.</summary>
    public static readonly string[] ThuMucMoiHopLe =
    {
        ThuMucLogo, ThuMucQuangCao, ThuMucHinhAnh, ThuMucNoiDung
    };

    /// <summary>Bốn thư mục cũ trước đợt tái cấu trúc (backward compatibility).</summary>
    public static readonly string[] ThuMucCuHopLe =
    {
        "logo_cs", "img_qc_kcb", "img_cs", "img_nd"
    };

    /// <summary>Tất cả thư mục ảnh hợp lệ (cả mới và cũ).</summary>
    public static readonly string[] ThuMucHopLe =
        ThuMucMoiHopLe.Concat(ThuMucCuHopLe).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static bool LaThuMucHopLe(string? thuMuc) =>
        !string.IsNullOrWhiteSpace(thuMuc)
        && ThuMucHopLe.Contains(thuMuc, StringComparer.OrdinalIgnoreCase);

    /// <summary>Đường dẫn thư mục trên FTP theo cơ sở, vd "sixospwa/CS1/logo".</summary>
    public static string ThuMucFtp(string? maCoSo, string thuMuc)
    {
        var safeMa = string.IsNullOrWhiteSpace(maCoSo) ? CoSoChung : maCoSo.Trim();
        return $"{GocFtp}/{safeMa}/{thuMuc}";
    }

    /// <summary>Đường dẫn thư mục cũ trên FTP, vd "sixospwa/logo_cs" hoặc fallback.</summary>
    public static string ThuMucFtp(string thuMuc) => $"{GocFtp}/{thuMuc}";

    /// <summary>
    /// Đổi kết quả của IFtpService.UploadFileAsync ("sixospwa/CS1/logo/x.jpg" hoặc "sixospwa/logo_cs/x.jpg")
    /// thành URL cất vào cột DB ("/anh/CS1/logo/x.jpg" hoặc "/anh/logo_cs/x.jpg").
    /// </summary>
    public static string? UrlTuDuongDanFtp(string? duongDanFtp)
    {
        if (string.IsNullOrWhiteSpace(duongDanFtp)) return null;

        var duongDan = duongDanFtp.Replace('\\', '/').TrimStart('/');
        if (!duongDan.StartsWith(GocFtp + "/", StringComparison.OrdinalIgnoreCase)) return null;

        return $"{TienToUrl}/{duongDan[(GocFtp.Length + 1)..]}";
    }

    /// <summary>
    /// Chiều ngược lại. Trả null khi URL KHÔNG thuộc kho ảnh của mình — đó là
    /// hàng rào chính: link http(s) admin dán vào, hay đường dẫn cũ /static/...,
    /// đều không quy ra được đường dẫn FTP nên không thể bị xóa.
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
    /// Tách URL "/anh/..." thành mảng các phần:
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

        // Chặn traversal ra ngoài kho
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
