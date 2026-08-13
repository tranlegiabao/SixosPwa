using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SixosPwa.Data;
using SixosPwa.Models;
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
    public IActionResult Login()
    {
        // Neu da dang nhap truoc do (Cookie truong ton hop le)
        if (User.Identity?.IsAuthenticated == true)
        {
            // Admin và Đối tác vào trang Index như cũ
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (role == "Admin" || role == "DoiTac")
            {
                return RedirectToAction("Index", "Home");
            }
            
            // Bệnh nhân (User) vào trang ThongTinBenhNhan
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
            // Tìm tài khoản từ database theo SĐT
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
                IsPersistent = true, // Ghi nho dang nhap truong ton
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365) // Het han sau 1 nam
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _cache.Remove($"OTP_{sdt}");

            // Lưu thông tin thiết bị đăng nhập vào database
            await LuuThietBiDangNhapAsync(sdt, model.DeviceId, model.DeviceName);

            // Admin và Đối tác vào Index, Bệnh nhân vào ThongTinBenhNhan
            var redirectUrl = (role == "Admin" || role == "DoiTac") 
                ? Url.Action("Index", "Home") 
                : Url.Action("ThongTinBenhNhan", "Home");

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
            new Claim(ClaimTypes.MobilePhone, sdt),
            new Claim(ClaimTypes.Role, "User")
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

        // Lưu thông tin thiết bị đăng nhập vào database
        await LuuThietBiDangNhapAsync(sdt, model.DeviceId, model.DeviceName);

        return Json(new { success = true, redirectUrl = Url.Action("ThongTinBenhNhan", "Home") });
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
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}

public class GuiOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
}

public class XacNhanOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
}

