using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;

namespace SixosPwa.Services;

public interface IDonAnhService
{
    /// <summary>Ảnh cũ bị thay bằng ảnh mới (logo, ảnh quảng cáo).</summary>
    Task DonAsync(string? urlCu, string? urlMoi);

    /// <summary>Ảnh biến mất khỏi một bản HTML sau khi admin sửa nội dung.</summary>
    Task DonTheoHtmlAsync(string? htmlCu, string? htmlMoi);
}

/// <summary>
/// Xóa ảnh không còn ai dùng trên kho FTP.
///
/// BA HÀNG RÀO, không có ngoại lệ nào:
///  1. Chỉ xóa đường dẫn quy được ra kho của mình (KhoAnh.DuongDanFtpTuUrl trả
///     null cho link http(s) admin dán vào, cho đường dẫn /static/... cũ, và cho
///     mọi thứ của HisSoft trên FTP dùng chung).
///  2. Chỉ xóa khi không còn dòng nào trong ba bảng trỏ tới tệp đó.
///  3. Chỉ được gọi SAU KHI thủ tục lưu đã thành công — xem ghi chú ConAiDungAsync.
///
/// FTP hỏng thì chỉ ghi log: DB đã đúng rồi, cái còn lại chỉ là một tệp mồ côi.
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
        // Hàng rào 1.
        var duongDanFtp = KhoAnh.DuongDanFtpTuUrl(url);
        var tenTep = KhoAnh.TenTepTuUrl(url);
        if (duongDanFtp == null || tenTep == null) return;

        // Hàng rào 2.
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
    /// Dò chéo mọi bảng có thể trỏ tới một tệp ảnh.
    ///
    /// KHÔNG cần trừ dòng vừa lưu: dịch vụ này chỉ chạy SAU KHI thủ tục lưu đã
    /// thành công, nên dòng đó trong DB đã mang giá trị MỚI rồi — ảnh cũ tự nó
    /// không còn khớp nữa.
    ///
    /// Dò theo TÊN TỆP chứ không theo cả URL, và dò cả dạng đã mã hóa: nếu
    /// TinyMCE có mã hóa khoảng trắng thành %20 khi ghi lại HTML thì so theo URL
    /// thô sẽ trượt, mà trượt ở đây nghĩa là XÓA NHẦM tệp còn người khác dùng.
    /// Dò theo tên thì cùng lắm là giữ lại một tệp thừa — hướng sai an toàn.
    /// </summary>
    private async Task<bool> ConAiDungAsync(string tenTep)
    {
        var maHoa = Uri.EscapeDataString(tenTep);
        var dang = maHoa.Equals(tenTep, StringComparison.Ordinal)
            ? new[] { tenTep }
            : new[] { tenTep, maHoa };

        foreach (var ten in dang)
        {
            // Sau đợt A chỉ còn HAI bảng: ảnh bìa + logo + ảnh quảng cáo đều nằm
            // thẳng trên DM_CSKCB (bảng DM_CSKCB_QuangCao đã bị xóa, cột Img đổi
            // tên thành AnhBia).
            if (await _db.DMCSKCBs.AsNoTracking()
                    .AnyAsync(x => (x.Logo != null && x.Logo.Contains(ten))
                                || (x.AnhBia != null && x.AnhBia.Contains(ten))
                                || (x.QcAnh != null && x.QcAnh.Contains(ten))))
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

            // Gỡ mã hóa URL: TinyMCE có thể ghi lại src thành /anh/img_nd/a%20b.jpg.
            // Không gỡ thì đường dẫn FTP dùng sai tên và lệnh xóa trượt im lặng.
            if (nguon.Contains('%')) nguon = Uri.UnescapeDataString(nguon);

            // Chỉ giữ thứ quy được ra kho của mình; còn lại bỏ qua từ đây cho rẻ.
            if (KhoAnh.DuongDanFtpTuUrl(nguon) != null)
                ket.Add(nguon);
        }

        return ket;
    }
}
