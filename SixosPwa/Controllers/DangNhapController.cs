using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SixosPwa.Services;

namespace SixosPwa.Controllers;

public class DangNhapController : Controller
{
    private readonly IMemoryCache _cache;
    private readonly ITaiKhoanService _taiKhoanService;
    private readonly IThietBiService _thietBiService;

    public DangNhapController(IMemoryCache cache, ITaiKhoanService taiKhoanService, IThietBiService thietBiService)
    {
        _cache = cache;
        _taiKhoanService = taiKhoanService;
        _thietBiService = thietBiService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role == "Admin" || role == "DoiTac")
            {
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("ThongTinBenhNhan", "Home");
        }

        return View();
    }

    [HttpPost]
    public IActionResult GuiOtp([FromBody] GuiOtpRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai) || model.SoDienThoai.Trim().Length < 9)
        {
            return Json(new { success = false, message = "Số điện thoại không hợp lệ!" });
        }

        var sdt = model.SoDienThoai.Trim();
        var otpCode = "123456";

        _cache.Set($"OTP_{sdt}", otpCode, TimeSpan.FromMinutes(5));

        return Json(new
        {
            success = true,
            message = $"Mã OTP đã gửi thành công tới số {sdt}!",
            otpDemo = otpCode
        });
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanOtp([FromBody] XacNhanOtpRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai) || string.IsNullOrWhiteSpace(model.Otp))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ số điện thoại và mã OTP!" });
        }

        var sdt = model.SoDienThoai.Trim();
        var otpInput = model.Otp.Trim();

        _cache.TryGetValue($"OTP_{sdt}", out string? cachedOtp);

        if (otpInput == "123456" || (cachedOtp != null && cachedOtp == otpInput))
        {
            var taiKhoan = await _taiKhoanService.DangNhapAsync(sdt, "");

            string role = "User";

            if (taiKhoan != null)
            {
                role = taiKhoan.Role;
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, sdt),
                new Claim(ClaimTypes.Name, sdt),
                new Claim(ClaimTypes.MobilePhone, sdt),
                new Claim(ClaimTypes.Role, role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _cache.Remove($"OTP_{sdt}");

            var redirectUrl = (role == "Admin" || role == "DoiTac")
                ? Url.Action("Index", "Home")
                : Url.Action("ThongTinBenhNhan", "Home");

            return Json(new { success = true, redirectUrl });
        }

        return Json(new { success = false, message = "Mã OTP không chính xác hoặc đã hết hạn!" });
    }

    [HttpPost]
    public async Task<IActionResult> LuuThietBi([FromBody] LuuThietBiRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai) || string.IsNullOrWhiteSpace(model.IdThietBi))
        {
            return Json(new { success = false, message = "Thiếu thông tin thiết bị." });
        }

        var ok = await _thietBiService.LuuHoacCapNhatAsync(
            model.SoDienThoai.Trim(),
            model.IdThietBi.Trim(),
            string.IsNullOrWhiteSpace(model.TenThietBi) ? "Unknown Device" : model.TenThietBi.Trim());

        return Json(new
        {
            success = ok,
            message = ok ? "Lưu thiết bị thành công." : "Lưu thiết bị thất bại."
        });
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanFirebaseToken([FromBody] GuiOtpRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai))
        {
            return Json(new { success = false, message = "Số điện thoại không hợp lệ!" });
        }

        var sdt = model.SoDienThoai.Trim();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, sdt),
            new Claim(ClaimTypes.Name, sdt),
            new Claim(ClaimTypes.MobilePhone, sdt),
            new Claim(ClaimTypes.Role, "User")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return Json(new { success = true, redirectUrl = Url.Action("ThongTinBenhNhan", "Home") });
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> DangXuat()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }



}

public class GuiOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
}

public class XacNhanOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}

public class LuuThietBiRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
    public string IdThietBi { get; set; } = string.Empty;
    public string TenThietBi { get; set; } = string.Empty;
};



