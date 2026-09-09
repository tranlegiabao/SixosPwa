using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixosPwa.Services;

namespace SixosPwa.Controllers;

/// <summary>
/// Duong doc anh tu kho FTP dung chung. Phuc vu ca:
///  - URL moi theo co so: /anh/{maCoSo}/{thuMuc}/{ten} (vd: /anh/CS1/logo/x.jpg, /anh/_chung/noi_dung/y.jpg)
///  - URL cu (backward-compatible): /anh/{thuMuc}/{ten} (vd: /anh/logo_cs/x.jpg)
///
/// Moi lan tai anh la mot phien FTP moi: khong dem, khong giu ket noi.
/// </summary>
[AllowAnonymous]
public sealed class AnhController : Controller
{
    private readonly IFtpService _ftp;
    private readonly ILogger<AnhController> _logger;

    public AnhController(IFtpService ftp, ILogger<AnhController> logger)
    {
        _ftp = ftp;
        _logger = logger;
    }

    [HttpGet("/anh/{*duongDan}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Xem(string? duongDan)
    {
        if (string.IsNullOrWhiteSpace(duongDan))
            return NotFound();

        var url = $"{KhoAnh.TienToUrl}/{duongDan.TrimStart('/')}";
        var duongDanFtp = KhoAnh.DuongDanFtpTuUrl(url);
        var tenTep = KhoAnh.TenTepTuUrl(url);

        if (duongDanFtp == null || tenTep == null)
        {
            _logger.LogWarning("Duong dan anh khong hop le hoac nam ngoai kho: {Url}", url);
            return NotFound();
        }

        try
        {
            var luong = await _ftp.DownloadAsync(duongDanFtp);
            return File(luong, KhoAnh.KieuNoiDung(tenTep));
        }
        catch (Exception ex)
        {
            // FTP chet hay khong co tep deu ra 404 — the <img> vo, khong do trang.
            _logger.LogWarning(ex, "Khong doc duoc anh {DuongDan} tu FTP.", duongDanFtp);
            return NotFound();
        }
    }
}
