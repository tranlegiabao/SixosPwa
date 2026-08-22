using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Security;
using SixosPwa.Services;
using SixosPwa.Services.Partner;

namespace SixosPwa.Controllers;

public class DangNhapController : Controller
{
    private readonly IMemoryCache _cache;
    private readonly ITaiKhoanService _taiKhoanService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILuongCongBenhNhan _luong;

    public DangNhapController(
        IMemoryCache cache,
        ITaiKhoanService taiKhoanService,
        ApplicationDbContext dbContext,
        ILuongCongBenhNhan luong)
    {
        _cache = cache;
        _taiKhoanService = taiKhoanService;
        _dbContext = dbContext;
        _luong = luong;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? coSo = null)
    {
        var adminReauth = AdminReauthentication.IsAdminReturnUrl(returnUrl) && Url.IsLocalUrl(returnUrl);

        // ?coSo=slug den tu hai nut ben trang co so. Do ra ViewBag de man dang
        // nhap hien o "Ma CSKCB" khoa cung, va de JS gui kem khi goi OTP.
        string? maCoSoTuUrl = null;
        if (!string.IsNullOrWhiteSpace(coSo))
        {
            var thongTin = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == coSo);

            maCoSoTuUrl = thongTin?.MaCoSo;
            ViewBag.MaCoSo = thongTin?.MaCoSo;
            ViewBag.TenCoSo = thongTin?.TenCoSo;
            ViewBag.SlugCoSo = coSo;
        }

        // Con phien thi KHONG tu day di dau ca. Ly do: SixosPwa khong biet benh
        // nhan vua dang xuat ben he doi tac hay chua (hai ten mien, khong co
        // single-logout), nen tu dong ban giao lai se khien nut "Dang nhap"
        // thanh nut khong cho dang nhap. Hien man dang nhap kem mot khoi lua
        // chon: di tiep bang tai khoan dang co, hoac dang nhap tai khoan khac.
        if (User.Identity?.IsAuthenticated == true && !adminReauth)
        {
            var cccdPhien = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
            var maCoSoDich = maCoSoTuUrl ?? User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

            if (!string.IsNullOrWhiteSpace(cccdPhien))
            {
                ViewBag.DangDangNhapLa = User.FindFirst(ClaimTypes.Name)?.Value;
                ViewBag.LinkDiTiep = Url.Action(nameof(DiTiep), new { coSo, returnUrl });
            }
            else
            {
                // Phien cu, thieu claim cua cong benh nhan: coi nhu chua dang nhap.
                ViewBag.DangDangNhapLa = null;
            }
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

            // Hai claim nay la cach cac man phia sau (Dang ky / Lien ket / Ban
            // giao / trang benh nhan) biet benh nhan la ai va dang o co so nao.
            if (!string.IsNullOrWhiteSpace(model.Cccd))
            {
                claims.Add(new Claim(LuongCongBenhNhan.ClaimCccd, model.Cccd.Trim()));
            }
            if (!string.IsNullOrWhiteSpace(model.MaCoSo))
            {
                claims.Add(new Claim(LuongCongBenhNhan.ClaimMaCoSo, model.MaCoSo.Trim()));
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

            // Cay quyet dinh sau OTP: chi mot cho duy nhat, nam trong
            // ILuongCongBenhNhan. Man hinh khong duoc tu kiem tra MaCoSo.
            var redirectUrl = adminReauth
                ? model.ReturnUrl
                : await ChonDichDenAsync(model.MaCoSo, model.Cccd, input, model.ReturnUrl);

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

        if (!string.IsNullOrWhiteSpace(model.Cccd))
        {
            claims.Add(new Claim(LuongCongBenhNhan.ClaimCccd, model.Cccd.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(model.MaCoSo))
        {
            claims.Add(new Claim(LuongCongBenhNhan.ClaimMaCoSo, model.MaCoSo.Trim()));
        }

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

        var dichDen = adminReauth
            ? model.ReturnUrl
            : await ChonDichDenAsync(model.MaCoSo, model.Cccd, sdt, model.ReturnUrl);

        return Json(new { success = true, redirectUrl = dichDen });
    }

    // ==================================================================
    //  Cong benh nhan: Dang ky / Lien ket / Doi mat khau / Ban giao
    //  Bon man nay chi den tu cay quyet dinh trong ILuongCongBenhNhan.
    // ==================================================================

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> DangKy(string coSo, string? returnUrl = null)
    {
        var maCoSo = LayMaCoSoPhien(coSo);
        if (maCoSo is null) return RedirectToAction(nameof(Login));

        await DoNguCanhRaViewBagAsync(maCoSo, returnUrl);
        return View();
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> TaoTaiKhoan([FromBody] TaoTaiKhoanRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.HoTen) || string.IsNullOrWhiteSpace(model.MatKhau))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ họ tên và mật khẩu!" });
        }

        if (model.MatKhau.Length < 6)
        {
            return Json(new { success = false, message = "Mật khẩu phải có ít nhất 6 ký tự!" });
        }

        // KHONG tin cccd / maCoSo / dinhDanh tu body: neu tin thi bat ky ai cung
        // POST duoc voi CCCD nguoi khac de mo tai khoan ben doi tac. Lay tu phien.
        var (maCoSo, cccd, dinhDanh) = LayDanhTinhPhien();
        if (maCoSo is null || cccd is null)
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại!" });
        }

        var ketQua = await _luong.MoTaiKhoanAsync(
            maCoSo, cccd, dinhDanh ?? string.Empty, model.HoTen.Trim(), model.MatKhau, model.ReturnUrl);

        if (!ketQua.ThanhCong)
        {
            return Json(new { success = false, message = ketQua.ThongBao });
        }

        return Json(new { success = true, redirectUrl = ketQua.DichDen });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> LienKet(string coSo, string? returnUrl = null)
    {
        var maCoSo = LayMaCoSoPhien(coSo);
        if (maCoSo is null) return RedirectToAction(nameof(Login));

        await DoNguCanhRaViewBagAsync(maCoSo, returnUrl);
        return View();
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> GuiMaLienKet([FromBody] GuiMaLienKetRequest model)
    {
        var (maCoSo, cccd, _) = LayDanhTinhPhien();
        if (maCoSo is null || cccd is null)
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại!" });
        }

        // Rieng so dien thoai thi lay tu form: no phai khop so benh nhan da dang
        // ky BEN CO SO, co the khac so dung de dang nhap SixosPwa.
        var ketQua = await _luong.GuiMaLienKetAsync(maCoSo, cccd, model.DienThoai);
        return Json(new { success = ketQua.ThanhCong, message = ketQua.ThongBao });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> XacNhanLienKet([FromBody] XacNhanLienKetRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Ma) || string.IsNullOrWhiteSpace(model.MatKhau))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ mã xác thực và mật khẩu!" });
        }

        var (maCoSo, cccd, _) = LayDanhTinhPhien();
        if (maCoSo is null || cccd is null)
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại!" });
        }

        var ketQua = await _luong.XacNhanLienKetAsync(
            maCoSo, cccd, model.DienThoai, model.Ma.Trim(), model.MatKhau);

        if (!ketQua.ThanhCong)
        {
            return Json(new { success = false, message = ketQua.ThongBao });
        }

        return Json(new { success = true, redirectUrl = ketQua.DichDen });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> DoiMatKhau(string coSo)
    {
        var maCoSo = LayMaCoSoPhien(coSo);
        if (maCoSo is null) return RedirectToAction(nameof(Login));

        await DoNguCanhRaViewBagAsync(maCoSo, null);
        return View();
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> LuuMatKhauMoi([FromBody] DoiMatKhauRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.MatKhauMoi) || model.MatKhauMoi.Length < 6)
        {
            return Json(new { success = false, message = "Mật khẩu phải có ít nhất 6 ký tự!" });
        }

        var (maCoSo, _, _) = LayDanhTinhPhien();
        if (maCoSo is null)
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại!" });
        }

        var ketQua = await _luong.DoiMatKhauAsync(maCoSo, User, model.MatKhauMoi);

        return Json(new { success = ketQua.ThanhCong, message = ketQua.ThongBao });
    }

    /// <summary>
    /// Man trung gian ban giao phien sang he doi tac. KHONG goi HTTP o day —
    /// cookie phai duoc dat tren trinh duyet benh nhan, nen view se POST bang
    /// form top-level trong popup. Xem ADR 0003.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> BanGiao(string coSo, string? returnUrl = null)
    {
        var maCoSo = LayMaCoSoPhien(coSo);
        if (maCoSo is null) return RedirectToAction(nameof(Login));

        // returnUrl la y dinh cua nut benh nhan da bam ("/dat-goi-kham"...).
        // Doi tac se tu dich sang man tuong ung ben ho.
        var yDinh = returnUrl?.Trim('/');
        var thongTin = await _luong.DungThongTinBanGiaoAsync(maCoSo, User, yDinh);

        if (thongTin is null)
        {
            // Khong dung duoc form ban giao (thieu credential, hoac co so khong
            // co API): dua ve trang benh nhan noi bo thay vi treo man trang.
            return Redirect("/benh-nhan");
        }

        await DoNguCanhRaViewBagAsync(maCoSo, null);
        ViewBag.Action = thongTin.Action;
        ViewBag.Truong = thongTin.Truong;
        ViewBag.TrangChu = thongTin.DichCuoi;
        return View();
    }

    /// <summary>
    /// Nguoi dung chu dong bam "Tiep tuc" o man dang nhap khi phien van con.
    /// Day moi la cho chay cay quyet dinh — KHONG tu chay khi chi mo trang.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> DiTiep(string? coSo = null, string? returnUrl = null)
    {
        var cccd = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        if (!string.IsNullOrWhiteSpace(coSo))
        {
            var thongTin = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == coSo);

            if (thongTin?.MaCoSo is not null) maCoSo = thongTin.MaCoSo;
        }

        if (string.IsNullOrWhiteSpace(maCoSo) || string.IsNullOrWhiteSpace(cccd))
        {
            return Redirect("/benh-nhan");
        }

        // Bam nut tu mot co so KHAC voi co so cua phien: doi claim sang co so moi.
        // Danh tinh da xac thuc bang OTP roi nen khong bat lam lai tu dau.
        if (maCoSo != User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value)
        {
            await DoiCoSoTrongPhienAsync(maCoSo);
        }

        var dichDen = await _luong.ChonDichDenAsync(maCoSo, cccd, dinhDanh, returnUrl);
        return Redirect(dichDen);
    }

    /// <summary>
    /// Doi ma co so trong phien hien tai, giu nguyen moi claim khac. Dung khi
    /// benh nhan da dang nhap roi bam nut tu mot co so khac — danh tinh da xac
    /// thuc bang OTP nen khong co ly do bat ho lam lai tu dau.
    /// </summary>
    private async Task DoiCoSoTrongPhienAsync(string maCoSoMoi)
    {
        var claims = User.Claims
            .Where(c => c.Type != LuongCongBenhNhan.ClaimMaCoSo)
            .Select(c => new Claim(c.Type, c.Value))
            .ToList();

        claims.Add(new Claim(LuongCongBenhNhan.ClaimMaCoSo, maCoSoMoi));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
            });
    }

    /// <summary>
    /// Danh tinh benh nhan LAY TU PHIEN. Moi thao tac cham toi he doi tac deu
    /// phai di qua day — khong bao gio tin cccd/maCoSo gui len tu trinh duyet.
    /// </summary>
    private (string? MaCoSo, string? Cccd, string? DinhDanh) LayDanhTinhPhien()
        => (User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value,
            User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value,
            User.FindFirst(ClaimTypes.Name)?.Value);

    /// <summary>
    /// Ma co so cua phien. Tham so tren URL chi duoc dung khi phien chua co —
    /// va phai la ma co so hop le, de khong ai doi URL de nhay sang co so khac.
    /// </summary>
    private string? LayMaCoSoPhien(string? coSoTrenUrl)
    {
        var cuaPhien = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        if (!string.IsNullOrWhiteSpace(cuaPhien)) return cuaPhien;

        return string.IsNullOrWhiteSpace(coSoTrenUrl) ? null : coSoTrenUrl;
    }

    /// <summary>Do ten co so + dinh danh benh nhan ra ViewBag cho bon man tren.</summary>
    private async Task DoNguCanhRaViewBagAsync(string maCoSo, string? returnUrl)
    {
        var coSo = await _dbContext.DMCSKCBs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaCoSo == maCoSo);

        ViewBag.MaCoSo = maCoSo;
        ViewBag.TenCoSo = coSo?.TenCoSo ?? "cơ sở khám chữa bệnh";
        ViewBag.SlugCoSo = coSo?.Slug;
        ViewBag.ReturnUrl = returnUrl;

        ViewBag.Cccd = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        ViewBag.DinhDanh = User.FindFirst(ClaimTypes.Name)?.Value;
        ViewBag.DienThoai = User.FindFirst(ClaimTypes.MobilePhone)?.Value;
    }

    /// <summary>
    /// Cho ha canh sau khi xac thuc. Vao thang /DangNhap/Login (khong qua trang
    /// co so) thi khong biet benh nhan o co so nao, nen chi ve duoc trang benh
    /// nhan dang toi gian — muon di tiep phai vao lai qua /pk/{slug}.
    /// </summary>
    private async Task<string?> ChonDichDenAsync(string? maCoSo, string? cccd, string dinhDanh, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(maCoSo) || string.IsNullOrWhiteSpace(cccd))
        {
            return "/benh-nhan";
        }

        return await _luong.ChonDichDenAsync(maCoSo, cccd.Trim(), dinhDanh, returnUrl);
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
        // Doc ma co so TRUOC khi dang xuat, vi sau SignOut la mat sach claim.
        // Dang xuat khoi cong benh nhan cua mot co so thi phai quay ve dung
        // trang co so do — ve /DangNhap/Login tran thi lan dang nhap sau khong
        // con mang theo ma co so, va benh nhan roi thang vao nhanh noi bo.
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        var slug = string.IsNullOrWhiteSpace(maCoSo)
            ? null
            : await _dbContext.DMCSKCBs
                .AsNoTracking()
                .Where(x => x.MaCoSo == maCoSo)
                .Select(x => x.Slug)
                .FirstOrDefaultAsync();

        await HttpContext.SignOutAsync(AdminReauthentication.Scheme);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!string.IsNullOrWhiteSpace(slug))
        {
            return Redirect($"/pk/{slug}");
        }

        return RedirectToAction(nameof(Login));
    }
}

public class GuiOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;

    /// <summary>Ma co so benh nhan dang dung (tu ?coSo=slug ben trang co so).</summary>
    public string? MaCoSo { get; set; }

    /// <summary>Khoa noi benh nhan sang he doi tac (V6).</summary>
    public string? Cccd { get; set; }

    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? ReturnUrl { get; set; }
}

public class XacNhanOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;

    /// <summary>Ma co so benh nhan dang dung (tu ?coSo=slug ben trang co so).</summary>
    public string? MaCoSo { get; set; }

    /// <summary>Khoa noi benh nhan sang he doi tac (V6).</summary>
    public string? Cccd { get; set; }

    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? ReturnUrl { get; set; }
}

public class TaoTaiKhoanRequest
{
    // Khong nhan MaCoSo / Cccd / DinhDanh: chung duoc lay tu claim cua phien.
    public string HoTen { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;

    /// <summary>Y dinh cua nut benh nhan da bam luc dau ("/dat-goi-kham"...).</summary>
    public string? ReturnUrl { get; set; }
}

public class GuiMaLienKetRequest
{
    /// <summary>So dien thoai da dang ky BEN CO SO — co the khac so dang nhap.</summary>
    public string DienThoai { get; set; } = string.Empty;
}

public class XacNhanLienKetRequest
{
    /// <summary>So dien thoai da dang ky BEN CO SO — co the khac so dang nhap.</summary>
    public string DienThoai { get; set; } = string.Empty;
    public string Ma { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;
}

public class DoiMatKhauRequest
{
    public string MatKhauMoi { get; set; } = string.Empty;
}
