using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;

namespace SixosPwa.Services;

public interface IDonAnhService
{
    /// <summary>Anh cu bi thay bang anh moi (logo, anh quang cao).</summary>
    Task DonAsync(string? urlCu, string? urlMoi);

    /// <summary>Anh bien mat khoi mot ban HTML sau khi admin sua noi dung.</summary>
    Task DonTheoHtmlAsync(string? htmlCu, string? htmlMoi);
}

/// <summary>
/// Xoa anh khong con ai dung tren kho FTP.
///
/// BA HANG RAO, khong co ngoai le nao:
///  1. Chi xoa duong dan quy duoc ra kho cua minh (KhoAnh.DuongDanFtpTuUrl tra
///     null cho link http(s) admin dan vao, cho duong dan /static/... cu, va cho
///     moi thu cua HisSoft tren FTP dung chung).
///  2. Chi xoa khi khong con dong nao trong ba bang tro toi tep do.
///  3. Chi duoc goi SAU KHI thu tuc luu da thanh cong — xem ghi chu ConAiDungAsync.
///
/// FTP hong thi chi ghi log: DB da dung roi, cai con lai chi la mot tep mo coi.
/// </summary>
public sealed class DonAnhService : IDonAnhService
{
    private static readonly Regex NguonAnh = new(
        "<img[^>]*?\\ssrc\\s*=\\s*[\"']([^\"']+)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    private readonly IFtpService _ftp;
    private readonly ILogger<DonAnhService> _logger;

    public DonAnhService(ApplicationDbContext db, IFtpService ftp, ILogger<DonAnhService> logger)
    {
        _db = db;
        _ftp = ftp;
        _logger = logger;
    }

    public Task DonAsync(string? urlCu, string? urlMoi)
    {
        if (string.IsNullOrWhiteSpace(urlCu)) return Task.CompletedTask;
        if (string.Equals(urlCu.Trim(), urlMoi?.Trim(), StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        return XoaNeuKhongAiDungAsync(urlCu);
    }

    public async Task DonTheoHtmlAsync(string? htmlCu, string? htmlMoi)
    {
        if (string.IsNullOrWhiteSpace(htmlCu)) return;

        var truoc = LayNguonAnh(htmlCu);
        if (truoc.Count == 0) return;

        var sau = LayNguonAnh(htmlMoi);
        truoc.ExceptWith(sau);

        foreach (var url in truoc)
            await XoaNeuKhongAiDungAsync(url);
    }

    private async Task XoaNeuKhongAiDungAsync(string url)
    {
        // Hang rao 1.
        var duongDanFtp = KhoAnh.DuongDanFtpTuUrl(url);
        var tenTep = KhoAnh.TenTepTuUrl(url);
        if (duongDanFtp == null || tenTep == null) return;

        // Hang rao 2.
        if (await ConAiDungAsync(tenTep))
        {
            _logger.LogInformation("Giu lai {Url}: van con dong khac tro toi tep nay.", url);
            return;
        }

        var xong = await _ftp.DeleteFileAsync(duongDanFtp);
        if (xong)
            _logger.LogInformation("Da xoa {DuongDan} tren FTP.", duongDanFtp);
        else
            _logger.LogWarning("Khong xoa duoc {DuongDan} tren FTP; tep nay thanh mo coi.", duongDanFtp);
    }

    /// <summary>
    /// Do cheo ca ba bang co the tro toi mot tep anh.
    ///
    /// KHONG can tru dong vua luu: dich vu nay chi chay SAU KHI thu tuc luu da
    /// thanh cong, nen dong do trong DB da mang gia tri MOI roi — anh cu tu no
    /// khong con khop nua.
    ///
    /// Do theo TEN TEP chu khong theo ca URL, va do ca dang da ma hoa: neu
    /// TinyMCE co ma hoa khoang trang thanh %20 khi ghi lai HTML thi so theo URL
    /// tho se truot, ma truot o day nghia la XOA NHAM tep con nguoi khac dung.
    /// Do theo ten thi cung lam nhieu nhat la giu lai mot tep thua — huong sai
    /// an toan.
    /// </summary>
    private async Task<bool> ConAiDungAsync(string tenTep)
    {
        var maHoa = Uri.EscapeDataString(tenTep);
        var dang = maHoa.Equals(tenTep, StringComparison.Ordinal)
            ? new[] { tenTep }
            : new[] { tenTep, maHoa };

        foreach (var ten in dang)
        {
            if (await _db.DMCSKCBs.AsNoTracking()
                    .AnyAsync(x => (x.Logo != null && x.Logo.Contains(ten))
                                || (x.Img != null && x.Img.Contains(ten))))
                return true;

            if (await _db.QCKCBs.AsNoTracking()
                    .AnyAsync(x => x.Img != null && x.Img.Contains(ten)))
                return true;

            if (await _db.NDCSKCBs.AsNoTracking()
                    .AnyAsync(x => x.NoiDung != null && x.NoiDung.Contains(ten)))
                return true;
        }

        return false;
    }

    private static HashSet<string> LayNguonAnh(string? html)
    {
        var ket = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(html)) return ket;

        foreach (Match khop in NguonAnh.Matches(html))
        {
            var nguon = System.Net.WebUtility.HtmlDecode(khop.Groups[1].Value).Trim();

            // Go ma hoa URL: TinyMCE co the ghi lai src thanh /anh/img_nd/a%20b.jpg.
            // Khong go thi duong dan FTP dung sai ten va lenh xoa truot im lang.
            if (nguon.Contains('%')) nguon = Uri.UnescapeDataString(nguon);

            // Chi giu thu quy duoc ra kho cua minh; con lai bo qua tu day cho re.
            if (KhoAnh.DuongDanFtpTuUrl(nguon) != null)
                ket.Add(nguon);
        }

        return ket;
    }
}
