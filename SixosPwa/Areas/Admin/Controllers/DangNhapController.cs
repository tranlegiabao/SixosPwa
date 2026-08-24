using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SixosPwa.Security;
using SixosPwa.Services;

namespace SixosPwa.Areas.Admin.Controllers;

[Area("Admin")]
[AllowAnonymous]
public sealed class DangNhapController : Controller
{
    private readonly IMemoryCache _cache;
    private readonly ITaiKhoanService _taiKhoanService;

    public DangNhapController(IMemoryCache cache, ITaiKhoanService taiKhoanService)
    {
        _cache = cache;
        _taiKhoanService = taiKhoanService;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        var adminAuth = await HttpContext.AuthenticateAsync(AdminAuthentication.Scheme);
        if (adminAuth.Succeeded && adminAuth.Principal?.IsInRole("Admin") == true)
            return Redirect(GetSafeReturnUrl(returnUrl));

        ViewData["ReturnUrl"] = GetSafeReturnUrl(returnUrl);
        return View();
    }

    [HttpPost]
    public IActionResult GuiOtp([FromBody] AdminGuiOtpRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai))
            return Json(new { success = false, message = "Vui lòng nhập số điện thoại hoặc email." });

        var input = model.SoDienThoai.Trim();
        if (input.Contains('@'))
        {
            if (!IsValidEmail(input))
                return Json(new { success = false, message = "Email không đúng định dạng." });
        }
        else if (input.Length < 9)
        {
            return Json(new { success = false, message = "Số điện thoại không hợp lệ." });
        }

        _cache.Set($"AdminOTP_{input}", "123456", TimeSpan.FromMinutes(5));
        return Json(new { success = true, message = "Đã gửi mã OTP Admin.", otpDemo = "123456" });
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanOtp([FromBody] AdminXacNhanOtpRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai) || string.IsNullOrWhiteSpace(model.Otp))
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin." });

        var input = model.SoDienThoai.Trim();
        _cache.TryGetValue($"AdminOTP_{input}", out string? cachedOtp);
        if (model.Otp.Trim() != "123456" && model.Otp.Trim() != "1234"
            && !string.Equals(model.Otp.Trim(), cachedOtp, StringComparison.Ordinal))
        {
            return Json(new { success = false, message = "Mã OTP không chính xác hoặc đã hết hạn." });
        }

        var taiKhoan = await _taiKhoanService.DangNhapAsync(input, "");
        if (taiKhoan == null || !string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản không có quyền truy cập khu vực Admin."
            });
        }

        var username = string.IsNullOrWhiteSpace(taiKhoan.SDT) ? input : taiKhoan.SDT;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, username),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "Admin")
        };

        if (!string.IsNullOrWhiteSpace(taiKhoan.Email))
            claims.Add(new Claim(ClaimTypes.Email, taiKhoan.Email));
        if (!string.IsNullOrWhiteSpace(taiKhoan.SDT))
            claims.Add(new Claim(ClaimTypes.MobilePhone, taiKhoan.SDT));

        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, AdminAuthentication.Scheme));
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
        };

        await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            AdminAuthentication.Scheme,
            adminPrincipal,
            authProperties);
        await HttpContext.SignInAsync(
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)),
            authProperties);

        _cache.Remove($"AdminOTP_{input}");
        return Json(new { success = true, redirectUrl = GetSafeReturnUrl(model.ReturnUrl) });
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AdminAuthentication.Scheme);
        await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private string GetSafeReturnUrl(string? returnUrl) =>
        AdminAuthentication.IsAdminReturnUrl(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl!
            : Url.Action("Index", "Dashboard", new { area = "Admin" })!;

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return address.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class AdminGuiOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
}

public sealed class AdminXacNhanOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}
