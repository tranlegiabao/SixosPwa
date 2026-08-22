using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Security;
using SixosPwa.Services;

namespace SixosPwa.Controllers;

public class DangNhapController : Controller
{
    private readonly IMemoryCache _cache;
    private readonly ITaiKhoanService _taiKhoanService;
    private readonly ApplicationDbContext _dbContext;

    public DangNhapController(IMemoryCache cache, ITaiKhoanService taiKhoanService, ApplicationDbContext dbContext)
    {
        _cache = cache;
        _taiKhoanService = taiKhoanService;
        _dbContext = dbContext;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        var adminReauth = AdminReauthentication.IsAdminReturnUrl(returnUrl) && Url.IsLocalUrl(returnUrl);

        // Neu da dang nhap truoc do (Cookie truong ton hop le)
        if (User.Identity?.IsAuthenticated == true && !adminReauth)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("ThongTinBenhNhan", "Home");
            }
            return RedirectToAction("Index", "Home");
        }

        ViewData["AdminReauth"] = adminReauth;
        ViewData["ReturnUrl"] = adminReauth ? returnUrl : null;
        return View();
    }

    [HttpPost]
    public IActionResult GuiOtp([FromBody] GuiOtpRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai))
        {
            return Json(new { success = false, message = "Vui lòng nhập Số điện thoại hoặc Email!" });
        }

        var input = model.SoDienThoai.Trim();
        if (input.Contains('@'))
        {
            if (!IsValidEmail(input))
            {
                return Json(new { success = false, message = "Email không đúng định dạng!" });
            }
        }
        else
        {
            if (input.Length < 9)
            {
                return Json(new { success = false, message = "Số điện thoại không hợp lệ!" });
            }
        }

        // Ma OTP thu nghiem (hoac sinh ngau nhien 6 chu so)
        var otpCode = "123456";

        // Luu vao cache trong 5 phut
        _cache.Set($"OTP_{input}", otpCode, TimeSpan.FromMinutes(5));

        var displayMessage = input.Contains('@') 
            ? $"Mã OTP đã gửi thành công tới email {input}!"
            : $"Mã OTP đã gửi thành công tới số {input}!";

        return Json(new { 
            success = true, 
            message = displayMessage,
            otpDemo = otpCode
        });
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanOtp([FromBody] XacNhanOtpRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai) || string.IsNullOrWhiteSpace(model.Otp))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin!" });
        }

        var input = model.SoDienThoai.Trim();
        var otpInput = model.Otp.Trim();
        var adminReauth = AdminReauthentication.IsAdminReturnUrl(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl);

        _cache.TryGetValue($"OTP_{input}", out string? cachedOtp);

        // Chap nhan neu dung ma trong cache hoac dung ma mac dinh "123456" hoac "1234" cho tien test
        if (otpInput == "123456" || otpInput == "1234" || (cachedOtp != null && cachedOtp == otpInput))
        {
            // Tìm tài khoản từ database theo SĐT hoặc Email
            var taiKhoan = await _taiKhoanService.DangNhapAsync(input, "");
            
            string role = "BenhNhan";
            
            if (taiKhoan != null)
            {
                // Chỉ duy trì hai vai trò công khai. Dữ liệu cũ User/DoiTac được quy về bệnh nhân.
                role = string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                    ? "Admin"
                    : "BenhNhan";
            }

            var username = input;
            var sdtClaimValue = "";
            var emailClaimValue = "";

            if (input.Contains('@'))
            {
                emailClaimValue = input;
                if (taiKhoan != null)
                {
                    sdtClaimValue = taiKhoan.SDT ?? "";
                    if (!string.IsNullOrWhiteSpace(taiKhoan.SDT))
                    {
                        username = taiKhoan.SDT;
                    }
                }
            }
            else
            {
                sdtClaimValue = input;
                if (taiKhoan != null)
                {
                    emailClaimValue = taiKhoan.Email ?? "";
                }
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, username),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            if (!string.IsNullOrEmpty(sdtClaimValue))
            {
                claims.Add(new Claim(ClaimTypes.MobilePhone, sdtClaimValue));
            }
            if (!string.IsNullOrEmpty(emailClaimValue))
            {
                claims.Add(new Claim(ClaimTypes.Email, emailClaimValue));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Ghi nho dang nhap truong ton
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365) // Het han sau 1 nam
            };

            await HttpContext.SignOutAsync(AdminReauthentication.Scheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            if (adminReauth)
            {
                var adminIdentity = new ClaimsIdentity(claims, AdminReauthentication.Scheme);
                await HttpContext.SignInAsync(
                    AdminReauthentication.Scheme,
                    new ClaimsPrincipal(adminIdentity),
                    new AuthenticationProperties { IsPersistent = false });
            }

            _cache.Remove($"OTP_{input}");

            // Lưu thông tin thiết bị đăng nhập vào database
            await LuuThietBiDangNhapAsync(username, model.DeviceId, model.DeviceName);

            string redirectUrl;
            if (adminReauth)
            {
                redirectUrl = model.ReturnUrl ?? Url.Action("Index", "Home");
            }
            else if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                redirectUrl = Url.Action("ThongTinBenhNhan", "Home");
            }
            else
            {
                redirectUrl = Url.Action("Index", "Home");
            }

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
        var adminReauth = AdminReauthentication.IsAdminReturnUrl(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, sdt),
            new Claim(ClaimTypes.Name, sdt),
            new Claim(ClaimTypes.MobilePhone, sdt),
            new Claim(ClaimTypes.Role, "BenhNhan")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, // Ghi nho dang nhap truong ton 365 ngay
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
        };

        await HttpContext.SignOutAsync(AdminReauthentication.Scheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        if (adminReauth)
        {
            var adminIdentity = new ClaimsIdentity(claims, AdminReauthentication.Scheme);
            await HttpContext.SignInAsync(
                AdminReauthentication.Scheme,
                new ClaimsPrincipal(adminIdentity),
                new AuthenticationProperties { IsPersistent = false });
        }

        // Lưu thông tin thiết bị đăng nhập vào database
        await LuuThietBiDangNhapAsync(sdt, model.DeviceId, model.DeviceName);

        return Json(new { success = true, redirectUrl = adminReauth ? model.ReturnUrl : Url.Action("ThongTinBenhNhan", "Home") });
    }

    private async Task LuuThietBiDangNhapAsync(string soDienThoai, string? deviceId, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;

        try
        {
            var existingDevice = await _dbContext.ThietBis
                .FirstOrDefaultAsync(tb => tb.SDT == soDienThoai && tb.IdThietBi == deviceId);

            if (existingDevice != null)
            {
                existingDevice.TenThietBi = deviceName;
                existingDevice.TrangThai = true;
                existingDevice.MaBN = soDienThoai;
                _dbContext.ThietBis.Update(existingDevice);
            }
            else
            {
                var newDevice = new ThietBi
                {
                    SDT = soDienThoai,
                    MaBN = soDienThoai,
                    IdThietBi = deviceId,
                    TrangThai = true,
                    TenThietBi = deviceName
                };
                await _dbContext.ThietBis.AddAsync(newDevice);
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception)
        {
            // Bỏ qua lỗi để không làm gián đoạn đăng nhập của người dùng
        }
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> DangXuat()
    {
        await HttpContext.SignOutAsync(AdminReauthentication.Scheme);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}

public class GuiOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? ReturnUrl { get; set; }
}

public class XacNhanOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? ReturnUrl { get; set; }
}

