using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SixosPwa.Models;
using SixosPwa.Security;
using SixosPwa.Services;

namespace SixosPwa.Areas.Admin.Controllers;

[Area("Admin")]
[AllowAnonymous]
public sealed class DangNhapController : Controller
{
    private readonly IMemoryCache _cache;
    private readonly ITaiKhoanService _taiKhoanService;
    private readonly SixosPwa.Data.ApplicationDbContext _db;

    public DangNhapController(
        IMemoryCache cache,
        ITaiKhoanService taiKhoanService,
        SixosPwa.Data.ApplicationDbContext db)
    {
        _cache = cache;
        _taiKhoanService = taiKhoanService;
        _db = db;
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
    public async Task<IActionResult> GuiOtp([FromBody] AdminGuiOtpRequest model)
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

        // Kiểm tra xem tài khoản có tồn tại và là Admin hay không
        var taiKhoan = await _taiKhoanService.DangNhapAsync(input, "");

        if (taiKhoan == null || (!string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return Json(new { success = false, message = "Tài khoản không có quyền truy cập khu vực Admin." });
        }

        return Json(new {
            success = true,
            isPassword = true,
            message = "Vui lòng nhập mật khẩu Admin để đăng nhập."
        });
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanOtp([FromBody] AdminXacNhanOtpRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai) || string.IsNullOrWhiteSpace(model.Otp))
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin." });

        var input = model.SoDienThoai.Trim();
        var otpInput = model.Otp.Trim();

        var taiKhoan = await _taiKhoanService.DangNhapAsync(input, "");
        if (taiKhoan == null || (!string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản không có quyền truy cập khu vực Admin."
            });
        }

        // Cot nay la MatKhauNoiBo (ADR 0009). Sau migration no dang NULL vi phan
        // BAM chua duoc thi hanh — xem muc Dinh chinh cua ADR 0009. Tai khoan
        // Admin/DoiTac vi vay tam thoi khong dang nhap duoc, va roi vao nhanh duoi.
        if (string.IsNullOrEmpty(taiKhoan.MatKhauNoiBo)
            || !string.Equals(taiKhoan.MatKhauNoiBo, otpInput, StringComparison.Ordinal))
        {
            return Json(new { success = false, message = "Mật khẩu không chính xác." });
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

    [HttpPost]
    [Authorize(Roles = "Admin", AuthenticationSchemes = AdminAuthentication.Scheme)]
    public async Task<IActionResult> DoiMatKhau([FromBody] AdminDoiMatKhauRequest model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.MatKhauCu) || string.IsNullOrWhiteSpace(model.MatKhauMoi))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ mật khẩu cũ và mật khẩu mới." });
        }

        var currentUsername = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(currentUsername))
        {
            return Json(new { success = false, message = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại." });
        }

        var taiKhoan = await _taiKhoanService.DangNhapAsync(currentUsername, "");
        if (taiKhoan == null || !string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { success = false, message = "Không tìm thấy tài khoản quản trị viên." });
        }

        if (string.IsNullOrEmpty(taiKhoan.MatKhauNoiBo)
            || !string.Equals(taiKhoan.MatKhauNoiBo, model.MatKhauCu.Trim(), StringComparison.Ordinal))
        {
            return Json(new { success = false, message = "Mật khẩu hiện tại không chính xác." });
        }

        var matKhauMoi = model.MatKhauMoi.Trim();
        if (matKhauMoi.Length < 4)
        {
            return Json(new { success = false, message = "Mật khẩu mới phải có ít nhất 4 ký tự." });
        }

        taiKhoan.MatKhauNoiBo = matKhauMoi;
        _db.TaiKhoans.Update(taiKhoan);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Đổi mật khẩu quản trị thành công." });
    }
}

public sealed class AdminDoiMatKhauRequest
{
    public string MatKhauCu { get; set; } = string.Empty;
    public string MatKhauMoi { get; set; } = string.Empty;
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
