using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixosPwa.Services;

namespace SixosPwa.Controllers;

/// <summary>
/// Đường đọc ảnh từ kho FTP dùng chung. Phục vụ cả:
///  - URL mới theo cơ sở: /anh/{maCoSo}/{thuMuc}/{ten} (vd: /anh/CS1/logo/x.jpg, /anh/_chung/noi_dung/y.jpg)
///  - URL cũ (backward-compatible): /anh/{thuMuc}/{ten} (vd: /anh/logo_cs/x.jpg)
///
/// Mỗi lần tải ảnh là một phiên FTP mới: không đệm, không giữ kết nối.
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
            // FTP chết hay không có tệp đều ra 404 — thẻ <img> vỡ, không đổ trang.
            _logger.LogWarning(ex, "Khong doc duoc anh {DuongDan} tu FTP.", duongDanFtp);
            return NotFound();
        }
    }
}
