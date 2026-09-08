using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixosPwa.Services;
using SixosPwa.Services.Partner;

namespace SixosPwa.Controllers;

/// <summary>
/// Man *Ho so cua toi* — mot tai khoan quan nhieu con nguoi (ADR 0019).
///
/// <para>
/// Doi ho so = PHAT LAI cookie voi claim <c>HoSoDangChon</c>, dung khuon
/// <c>DangKyOnlineUB</c> (<c>ThemIdXemThongTinBenhNhan</c> goi
/// <c>AddClaimsAsync</c>). KHONG luu lua chon vao CSDL: no la trang thai cua
/// PHIEN, khong phai cua con nguoi — hai thiet bi cua cung mot tai khoan phai
/// xem duoc hai ho so khac nhau cung luc.
/// </para>
/// </summary>
[Authorize]
public class HoSoController : Controller
{
    private readonly IHoSoBenhNhanService _hoSo;
    private readonly ILogger<HoSoController> _logger;

    public HoSoController(IHoSoBenhNhanService hoSo, ILogger<HoSoController> logger)
    {
        _hoSo = hoSo;
        _logger = logger;
    }

    [HttpGet("/benh-nhan/ho-so")]
    public async Task<IActionResult> Index(string? loi = null, string? xong = null)
    {
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(dinhDanh);

        ViewBag.MaCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        ViewBag.Loi = loi;
        ViewBag.Xong = xong;

        if (idTaiKhoan is null)
        {
            return View(new List<HoSoCuaToi>());
        }

        var idDangChon = LayIdDangChon();
        return View(await _hoSo.LayDanhSachAsync(idTaiKhoan.Value, idDangChon));
    }

    /// <summary>Doi ho so dang xem.</summary>
    [HttpPost("/benh-nhan/ho-so/chon")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Chon(long id)
    {
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(dinhDanh);

        if (idTaiKhoan is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        // 🔴 Cong chan. Thieu phep kiem nay thi go ID ho so nguoi khac vao la xem
        // duoc benh an cua ho — dung loai lo hong ma duong doc tai lieu tung mac.
        if (!await _hoSo.HoSoThuocTaiKhoanAsync(id, idTaiKhoan.Value))
        {
            _logger.LogWarning(
                "Tai khoan {IdTaiKhoan} thu chon ho so {IdHoSo} khong thuoc ve minh.",
                idTaiKhoan.Value, id);

            return RedirectToAction(nameof(Index), new { loi = "Hồ sơ này không thuộc tài khoản của bạn." });
        }

        await PhatLaiClaimAsync(id);

        // Chon xong la vao thang trang benh nhan — do la ly do nguoi ta bam.
        // Muon doi tiep thi quay lai bang o *Ho so cua toi* tren do.
        return Redirect("/benh-nhan");
    }

    [HttpGet("/benh-nhan/ho-so/them")]
    public IActionResult Them(string? loi = null)
    {
        ViewBag.Loi = loi;
        return View();
    }

    [HttpPost("/benh-nhan/ho-so/them")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Them(string cccd, string hoTen, DateTime? ngaySinh, string? sdt)
    {
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(dinhDanh);

        if (idTaiKhoan is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        var (thanhCong, thongBao, idBenhNhan) =
            await _hoSo.TaoAsync(idTaiKhoan.Value, maCoSo, cccd, hoTen, ngaySinh, sdt);

        if (!thanhCong)
        {
            // Loi "ai khai truoc giu CCCD" phai hien NGUYEN VAN — no co chi duong
            // ra. Hien o chinh man Them de nguoi dung khong mat nhung gi vua go.
            return RedirectToAction(nameof(Them), new { loi = thongBao });
        }

        // Tao xong thi chuyen sang xem luon ho so vua tao — dung buoc nguoi dung
        // phai bam them mot lan nua.
        await PhatLaiClaimAsync(idBenhNhan);
        return RedirectToAction(nameof(Index), new { xong = "1" });
    }

    [HttpPost("/benh-nhan/ho-so/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Xoa(long id)
    {
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(dinhDanh);

        if (idTaiKhoan is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var (thanhCong, thongBao) = await _hoSo.XoaAsync(id, idTaiKhoan.Value);

        if (!thanhCong)
        {
            return RedirectToAction(nameof(Index), new { loi = thongBao });
        }

        // Vua xoa dung ho so dang chon => bo claim di, de LayHoSoDangDungAsync
        // roi ve ho so dau tien thay vi tro toi mot ID khong con ton tai.
        if (LayIdDangChon() == id)
        {
            await PhatLaiClaimAsync(null);
        }

        return RedirectToAction(nameof(Index), new { xong = "1" });
    }

    private long? LayIdDangChon() =>
        long.TryParse(User.FindFirst(LuongCongBenhNhan.ClaimHoSoDangChon)?.Value, out var id)
            ? id
            : null;

    /// <summary>
    /// Phat lai cookie: giu nguyen moi claim cu, chi thay claim *ho so dang chon*.
    ///
    /// Phai chep lai ca bo chu khong tao identity moi — mat claim <c>MaCoSo</c>
    /// hay dau an doi tac la phien tut xuong trang thai khac han (ADR 0016).
    /// </summary>
    private async Task PhatLaiClaimAsync(long? idHoSo)
    {
        var claims = User.Claims
            .Where(c => c.Type != LuongCongBenhNhan.ClaimHoSoDangChon)
            .ToList();

        if (idHoSo is not null)
        {
            claims.Add(new Claim(LuongCongBenhNhan.ClaimHoSoDangChon, idHoSo.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // Giu dung thoi han cua luong dang nhap (DangNhapController): phat lai ma
        // quen IsPersistent thi phien tut ve cookie phien, dong trinh duyet la mat.
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
            });
    }
}
