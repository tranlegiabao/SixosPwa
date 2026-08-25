using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixosPwa.Services;

namespace SixosPwa.Controllers;

/// <summary>
/// Duong doc anh tu kho FTP dung chung. Khuon lay tu master_3
/// (HomeController.cs:123 "/HinhCLS/..."), lech co y hai cho:
///
///  1. DE RIENG MOT CONTROLLER, khong nhet vao HomeController — lop do co
///     [Authorize] o cap lop, ma logo co so phai xem duoc khi CHUA dang nhap
///     (trang DanhSachCoSo, trang chi tiet co so deu la trang cong).
///  2. Suy kieu noi dung theo duoi tep thay vi tra cung "image/png" — kho co
///     ca jpg/png/webp/gif.
///
/// Moi lan tai anh la mot phien FTP moi (quyet dinh 1 cua ADR 0012): khong dem,
/// khong giu ket noi.
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

    [HttpGet("/anh/{thuMuc}/{*ten}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Xem(string thuMuc, string ten)
    {
        // Ten tep tren FTP do nguoi dung dat (quyet dinh 7: giu nguyen ten goc),
        // nen phai chan duong di ra ngoai kho truoc khi ghep duong dan.
        var url = $"{KhoAnh.TienToUrl}/{thuMuc}/{ten}";
        var duongDanFtp = KhoAnh.DuongDanFtpTuUrl(url);
        if (duongDanFtp == null)
        {
            _logger.LogWarning("Duong dan anh khong hop le: {Url}", url);
            return NotFound();
        }

        try
        {
            var luong = await _ftp.DownloadAsync(duongDanFtp);
            return File(luong, KhoAnh.KieuNoiDung(ten));
        }
        catch (Exception ex)
        {
            // FTP chet hay khong co tep deu ra 404 — the <img> vo, khong do trang.
            _logger.LogWarning(ex, "Khong doc duoc anh {DuongDan} tu FTP.", duongDanFtp);
            return NotFound();
        }
    }
}
