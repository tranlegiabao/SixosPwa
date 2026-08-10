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

    public DangNhapController(IMemoryCache cache, ITaiKhoanService taiKhoanService)
    {
        _cache = cache;
        _taiKhoanService = taiKhoanService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        // Neu da dang nhap truoc do (Cookie truong ton hop le), vao thang trang chu
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(HomeController.Index), "Home");
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
        // Ma OTP thu nghiem (hoac sinh ngau nhien 6 chu so)
        var otpCode = "123456";

        // Luu vao cache trong 5 phut
        _cache.Set($"OTP_{sdt}", otpCode, TimeSpan.FromMinutes(5));

        return Json(new { 
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

        // Chap nhan neu dung ma trong cache hoac dung ma mac dinh "123456" cho tien test
        if (otpInput == "123456" || (cachedOtp != null && cachedOtp == otpInput))
        {
            // Kiểm tra loại tài khoản
            var taiKhoan = await _taiKhoanService.DangNhapAsync(sdt, ""); // Tìm theo SĐT
            
            string loaiTaiKhoan = "BenhNhan"; // Mặc định
            string hoTen = sdt;
            string? benhNhanId = null;
            
            if (taiKhoan != null)
            {
                loaiTaiKhoan = taiKhoan.LoaiTaiKhoan;
                hoTen = taiKhoan.HoTen;
                benhNhanId = taiKhoan.BenhNhanId?.ToString();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, sdt),
                new Claim(ClaimTypes.Name, sdt),
                new Claim(ClaimTypes.MobilePhone, sdt),
                new Claim(ClaimTypes.Role, loaiTaiKhoan),
                new Claim("HoTen", hoTen)
            };

            if (benhNhanId != null)
            {
                claims.Add(new Claim("BenhNhanId", benhNhanId));
            }

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

            // Chuyển hướng theo loại tài khoản
            var redirectUrl = loaiTaiKhoan switch
            {
                "Admin" => Url.Action("Index", "Sms"),
                "DoiTac" => Url.Action("Index", "Sms"),
                _ => Url.Action("Index", "BenhNhan")
            };

            return Json(new { success = true, redirectUrl });
        }

        return Json(new { success = false, message = "Mã OTP không chính xác hoặc đã hết hạn!" });
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
            new Claim(ClaimTypes.MobilePhone, sdt)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, // Ghi nho dang nhap truong ton 365 ngay
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });
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

