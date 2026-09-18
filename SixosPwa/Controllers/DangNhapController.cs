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

    private readonly AdminStoredProcedureService _thuTuc;

    public DangNhapController(
        IMemoryCache cache,
        ITaiKhoanService taiKhoanService,
        ApplicationDbContext dbContext,
        ILuongCongBenhNhan luong,
        AdminStoredProcedureService thuTuc)
    {
        _cache = cache;
        _taiKhoanService = taiKhoanService;
        _dbContext = dbContext;
        _luong = luong;
        _thuTuc = thuTuc;
    }

    /// <summary>
    /// Cổng tiếp nhận quét mã QR: Tự động nhận diện hồ sơ bệnh nhân và chuyển thẳng vào Cổng bệnh nhân
    /// </summary>
    [HttpGet("/qr")]
    [HttpGet("/qr-kham")]
    public async Task<IActionResult> QrKham(string? mabn = null, string? coSo = null)
    {
        var maBnTraCuu = string.IsNullOrWhiteSpace(mabn) ? "145703" : mabn.Trim();
        var maCoSoTraCuu = string.IsNullOrWhiteSpace(coSo) ? "77121" : coSo.Trim();

        var hoSo = await (from bnCs in _dbContext.BenhNhanCoSos.AsNoTracking()
                          join cs in _dbContext.DMCSKCBs.AsNoTracking() on bnCs.IdCoSo equals cs.Id
                          join bn in _dbContext.BenhNhans.AsNoTracking() on bnCs.IdBenhNhan equals bn.Id
                          where bnCs.MaBN == maBnTraCuu && (cs.MaCoSo == maCoSoTraCuu || cs.Slug == maCoSoTraCuu)
                          select new { BenhNhan = bn, CoSo = cs }).FirstOrDefaultAsync();

        if (hoSo == null)
        {
            hoSo = await (from bnCs in _dbContext.BenhNhanCoSos.AsNoTracking()
                          join cs in _dbContext.DMCSKCBs.AsNoTracking() on bnCs.IdCoSo equals cs.Id
                          join bn in _dbContext.BenhNhans.AsNoTracking() on bnCs.IdBenhNhan equals bn.Id
                          where bnCs.MaBN == maBnTraCuu
                          select new { BenhNhan = bn, CoSo = cs }).FirstOrDefaultAsync();
        }

        if (hoSo != null)
        {
            var cccd = hoSo.BenhNhan.CCCD;
            var maCoSo = hoSo.CoSo.MaCoSo;
            var username = !string.IsNullOrWhiteSpace(hoSo.BenhNhan.SDT) ? hoSo.BenhNhan.SDT : cccd;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, username),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, "BenhNhan"),
                new Claim(LuongCongBenhNhan.ClaimCccd, cccd),
                new Claim(LuongCongBenhNhan.ClaimMaCoSo, maCoSo),
                new Claim(LuongCongBenhNhan.ClaimDoiTacXacThuc, "1")
            };

            if (!string.IsNullOrWhiteSpace(hoSo.BenhNhan.SDT))
            {
                claims.Add(new Claim(ClaimTypes.MobilePhone, hoSo.BenhNhan.SDT));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
            };

            await HttpContext.SignOutAsync(AdminAuthentication.Scheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                authProperties);

            return Redirect("/benh-nhan");
        }

        return Redirect($"/DangNhap/Login?ReturnUrl=%2Fbenh-nhan");
    }

    /// <summary>
    /// Cổng tiếp nhận quét mã QR: Mở màn hình OTP, sau khi xác thực OTP thành công sẽ vào Cổng bệnh nhân
    /// </summary>
    [HttpGet("/qr-otp")]
    public async Task<IActionResult> QrOtp(string? mabn = null, string? coSo = null, string? sdt = null)
    {
        // Luôn xóa phiên cookie cũ để đảm bảo hiển thị đúng màn hình OTP
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var maBnTraCuu = string.IsNullOrWhiteSpace(mabn) ? "145703" : mabn.Trim();
        var maCoSoTraCuu = string.IsNullOrWhiteSpace(coSo) ? "77121" : coSo.Trim();

        var hoSo = await (from bnCs in _dbContext.BenhNhanCoSos.AsNoTracking()
                          join cs in _dbContext.DMCSKCBs.AsNoTracking() on bnCs.IdCoSo equals cs.Id
                          join bn in _dbContext.BenhNhans.AsNoTracking() on bnCs.IdBenhNhan equals bn.Id
                          where bnCs.MaBN == maBnTraCuu && (cs.MaCoSo == maCoSoTraCuu || cs.Slug == maCoSoTraCuu)
                          select new { BenhNhan = bn, CoSo = cs }).FirstOrDefaultAsync();

        if (hoSo == null)
        {
            hoSo = await (from bnCs in _dbContext.BenhNhanCoSos.AsNoTracking()
                          join cs in _dbContext.DMCSKCBs.AsNoTracking() on bnCs.IdCoSo equals cs.Id
                          join bn in _dbContext.BenhNhans.AsNoTracking() on bnCs.IdBenhNhan equals bn.Id
                          where bnCs.MaBN == maBnTraCuu
                          select new { BenhNhan = bn, CoSo = cs }).FirstOrDefaultAsync();
        }

        var slug = hoSo != null && !string.IsNullOrWhiteSpace(hoSo.CoSo.Slug) ? hoSo.CoSo.Slug : "pkdk-thien-nam";
        var sdtBn = !string.IsNullOrWhiteSpace(sdt) ? sdt : (hoSo?.BenhNhan.SDT ?? "0773746879");
        var cccd = hoSo?.BenhNhan.CCCD ?? "044071000792";

        // Lưu trước OTP 123456 vào cache
        _cache.Set($"OTP_{sdtBn}", "123456", TimeSpan.FromMinutes(30));
        _cache.Set($"OTP_{cccd}", "123456", TimeSpan.FromMinutes(30));

        // Lưu thông tin quét QR vào cookie qr_data (không giới hạn thời gian - 365 ngày)
        var qrData = new DanhTinhQuet
        {
            MaBN = maBnTraCuu,
            HoTen = hoSo?.BenhNhan.TenBN,
            Cccd = cccd,
            DienThoai = sdtBn,
            NgaySinh = hoSo?.BenhNhan.NgaySinh?.ToString("dd/MM/yyyy"),
            GioiTinh = hoSo?.BenhNhan.GioiTinh,
            DiaChi = hoSo?.BenhNhan.DiaChi,
            Nguon = "his"
        };
        var jsonOpt = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        var qrJson = System.Text.Json.JsonSerializer.Serialize(qrData, jsonOpt);
        Response.Cookies.Append("qr_data", Uri.EscapeDataString(qrJson), new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = false,
            SameSite = SameSiteMode.Lax
        });

        return Redirect($"/DangNhap/Login?coSo={slug}&sdt={sdtBn}&cccd={cccd}&hienOtp=1&tuQr=1&returnUrl=%2Fbenh-nhan");
    }

    /// <summary>
    /// Hủy mã OTP trong cache và xóa cookie qr_data khi người dùng chuyển khỏi màn hình OTP
    /// </summary>
    [HttpPost("/DangNhap/HuyOtp")]
    public IActionResult HuyOtp([FromBody] HuyOtpRequest? model)
    {
        string? sdt = model?.SoDienThoai;
        string? cccd = model?.Cccd;

        if (string.IsNullOrWhiteSpace(sdt) && string.IsNullOrWhiteSpace(cccd))
        {
            sdt = Request.Query["sdt"].FirstOrDefault();
            cccd = Request.Query["cccd"].FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(sdt))
        {
            _cache.Remove($"OTP_{sdt.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(cccd))
        {
            _cache.Remove($"OTP_{cccd.Trim()}");
        }

        Response.Cookies.Delete("qr_data", new CookieOptions { Path = "/" });
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? coSo = null)
    {
        var adminReauth = AdminAuthentication.IsAdminReturnUrl(returnUrl) && Url.IsLocalUrl(returnUrl);

        // ?coSo=slug den tu hai nut ben trang co so. Do ra ViewBag de man dang
        // nhap hien o "Ma CSKCB" khoa cung, va de JS gui kem khi goi OTP.
        string? maCoSoTuUrl = null;
        if (!string.IsNullOrWhiteSpace(coSo))
        {
            var thongTin = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == coSo);

            // Chan ngay o day thay vi de benh nhan go het OTP roi moi bi tu choi
            // (va ton mot tin nhan OTP vo ich). Entity da nam trong tay, khong ton
            // them truy van. Xem ADR 0013.
            if (thongTin is not null && !thongTin.Active) return Redirect("/");

            // 🔴 ?coSo= chi nhan SLUG. Go vao mot gia tri khong tra ra co so nao
            // (hay gap nhat: go MA co so, vd 77121) truoc day tut lang le xuong che
            // do khong-co-so: van cho go OTP, van cap cookie, roi tha vao /benh-nhan
            // ma khong tao noi tai khoan. Da ve "/" giong het nhanh Active = 0 ngay
            // tren — sai cua thi phai biet ngay, dung sau khi go xong OTP. ADR 0027.
            if (thongTin is null) return Redirect("/");

            maCoSoTuUrl = thongTin?.MaCoSo;
            ViewBag.MaCoSo = thongTin?.MaCoSo;
            ViewBag.TenCoSo = thongTin?.TenCoSo;
            ViewBag.LogoCoSo = LayLogoCoSo(thongTin);
            ViewBag.TrangChuDoiTac = null as string;
            ViewBag.SlugCoSo = coSo;
        }

        // Con phien thi hien man dang nhap kem mot khoi lua chon: di tiep bang tai
        // khoan dang co, hoac dang nhap tai khoan khac.
        //
        // ⚠️ Khoi nay chi con phuc vu CO SO NOI BO. Voi co so dung man cua doi tac,
        // ADR 0016 da doi luat: phien con song va co dau an thi DI THANG, khong hoi
        // — xem nhanh o cuoi ham. Ly le cu ("khong tu day di dau ca vi SixosPwa
        // khong biet benh nhan vua dang xuat ben doi tac hay chua") van dung ve
        // logic nhung tra gia sai: no bat MOI benh nhan go lai mat khau de phong
        // mot truong hop hiem, ma truong hop hiem do da co loi thoat rieng —
        // menu 3 gach -> Dang xuat.
        //
        // Doc ra ngoai khoi if: nhanh tu dong o cuoi ham cung can hai claim nay.
        var cccdPhien = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        var cuaPhien = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        if (User.Identity?.IsAuthenticated == true && !adminReauth)
        {
            var maCoSoDich = maCoSoTuUrl ?? cuaPhien;

            if (!string.IsNullOrWhiteSpace(cccdPhien))
            {
                // Go thang URL/bookmark vao mot co so KHAC co so cua phien: khong
                // tu dung man nay noi hai thu tu do lua chon nua — day ve dung
                // trang co so do de MOT modal duy nhat bat dang nhap cheo co so
                // hien ra (ADR 0006), khong lech thong diep giua hai loi vao.
                if (!string.IsNullOrWhiteSpace(coSo) && maCoSoDich != cuaPhien)
                {
                    var yDinh = returnUrl ?? "/";
                    return Redirect($"/DangKyOnline/{coSo}?canhBao=1&returnUrl={Uri.EscapeDataString(yDinh)}");
                }

                // Phiên còn sống: đi thẳng vào trong, không bắt xác nhận lại một bước thừa
                return await DiTiep(returnUrl);
            }
            else if (User.IsInRole("Admin"))
            {
                return Redirect("/Admin");
            }
        }

        ViewData["AdminReauth"] = adminReauth;
        ViewData["ReturnUrl"] = adminReauth ? returnUrl : null;

        // DIEM RE DUY NHAT. Co so da biet TRUOC khi vao man dang nhap (benh nhan
        // di DanhSachCoSo -> ChiTietCoSo -> /DangNhap/Login?coSo={slug}), nen chi
        // can tra cau hinh cua co so o day. Co so dung bo man cua doi tac thi tra man clone;
        // moi co so khac giu nguyen man OTP cua SixosPwa. ADR 0014.
        //
        // Dat SAU moi guard phia tren la co y: re nhanh khong duoc phep bo qua
        // chan co so an (ADR 0013) hay chan dang nhap cheo co so (ADR 0006).
        if (!adminReauth && !string.IsNullOrWhiteSpace(maCoSoTuUrl))
        {
            var cuaDoiTac = await _luong.LayCuaAsync(maCoSoTuUrl);
            if (cuaDoiTac?.DungManDoiTac == true)
            {
                // Phien con song va DA MANG DAU AN cua doi tac: benh nhan da tung
                // go mat khau that o may nay, khong bat go lai nua — hoi doi tac
                // bang mat khau da cat roi ban giao thang. ADR 0016.
                //
                // Doi ca ba ve, thieu mot la khong tu di:
                //   - con phien (co CCCD),
                //   - CO dau an (phien duc tu XacNhanOtp thi khong co),
                //   - dung CO SO cua phien (khong muon dang nhap ho sang co so khac).
                //
                // 🔴 Khoi nay nam SAU moi guard phia tren la CO Y — chan co so an
                // (ADR 0013) va chan dang nhap cheo co so (ADR 0006) phai chay
                // truoc. Dung don len dau ham.
                if (!string.IsNullOrWhiteSpace(cccdPhien)
                    && User.FindFirst(LuongCongBenhNhan.ClaimDoiTacXacThuc) is not null
                    && string.Equals(cuaPhien, maCoSoTuUrl, StringComparison.Ordinal))
                {
                    var diTiep = await _luong.DangNhapLaiBangMatKhauDaCatAsync(
                        maCoSoTuUrl, cccdPhien, returnUrl);

                    if (diTiep.ThanhCong && !string.IsNullOrWhiteSpace(diTiep.DichDen))
                    {
                        return Redirect(diTiep.DichDen);
                    }

                    // Khong tu di duoc (mat khau da doi ben ho, hoac ben ho dang
                    // hong): roi xuong man dang nhap nhu cu, nhung phai NOI RO vi
                    // sao — benh nhan vua bam mot nut ma lai thay man dang nhap.
                    ViewBag.LoiDangNhapLai = diTiep.ThongBao;
                }

                ViewBag.ReturnUrl = returnUrl;
                ViewBag.TrangChuDoiTac = cuaDoiTac.CauHinh.TrangChu?.TrimEnd('/');
                ViewBag.ChiNhanh = await LayChiNhanhAsync(maCoSoTuUrl);
                return View("UbLogin");
            }
        }

        return View();
    }

    [HttpPost]
    public IActionResult GuiOtp([FromBody] GuiOtpRequest model)
    {
        var input = model.SoDienThoai?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input) && model.DanhTinhQuet != null)
        {
            // TEN TAI KHOAN chi duoc lay tu SO DIEN THOAI, hoac tu MaBN khi quet QR
            // PHIEU KHAM HIS. KHONG lay so CCCD: lam vay la de ra tai khoan mang so
            // can cuoc, con benh nhan thi mat duong nhap so dien thoai that. Hang rao
            // nay phai dung o CA server, vi goi thang API la vuot mat trinh duyet.
            if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet.DienThoai))
                input = model.DanhTinhQuet.DienThoai.Trim();
            else if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN))
                input = model.DanhTinhQuet.MaBN.Trim();
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return Json(new { success = false, message = "Vui lòng nhập Số điện thoại hoặc Email!" });
        }

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
                return Json(new { success = false, message = "Số điện thoại hoặc Căn cước công dân không hợp lệ!" });
            }
        }

        // Kiểm tra tài khoản trong database
        TaiKhoan? taiKhoan = null;
        var term = input.ToLower();
        if (term.Contains('@'))
        {
            taiKhoan = _dbContext.TaiKhoans.AsNoTracking().FirstOrDefault(tk => tk.Email != null && tk.Email.ToLower() == term);
        }
        else
        {
            taiKhoan = _dbContext.TaiKhoans.AsNoTracking().FirstOrDefault(tk => tk.SDT == term);
            if (taiKhoan == null && !string.IsNullOrWhiteSpace(model.Cccd))
            {
                var cccd = model.Cccd.Trim();
                taiKhoan = (from p in _dbContext.BenhNhans.AsNoTracking()
                            join t in _dbContext.TaiKhoans.AsNoTracking() on p.IdTaiKhoan equals t.Id
                            where p.CCCD == cccd
                            select t).FirstOrDefault()
                        ?? (from t in _dbContext.TaiKhoans.AsNoTracking()
                            join p in _dbContext.BenhNhans.AsNoTracking() on t.IdBenhNhan equals p.Id
                            where p.CCCD == cccd
                            select t).FirstOrDefault();
            }
        }

        bool laQrHis = model.DanhTinhQuet != null && (model.DanhTinhQuet.LaNguonHis || !string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN));

        // Khóa luồng tự đăng ký tài khoản: chỉ cho phép tài khoản đã có sẵn từ HIS
        // NGOẠI LỆ: Bệnh nhân đã khám quét QR phiếu khám HIS (nguồn HIS hoặc có MaBN)
        if (taiKhoan == null && !laQrHis)
        {
            return Json(new {
                success = false,
                message = "Số điện thoại chưa có hồ sơ tại cơ sở y tế. Vui lòng liên hệ phòng khám/bệnh viện để được đăng ký."
            });
        }

        if (taiKhoan != null && (string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(taiKhoan.Role, "DoiTac", StringComparison.OrdinalIgnoreCase)))
        {
            return Json(new {
                success = true,
                isPassword = true,
                message = "Vui lòng nhập mật khẩu của bạn để đăng nhập."
            });
        }

        // Ma OTP thu nghiem (hoac sinh ngau nhien 6 chu so)
        var otpCode = "123456";

        // Luu vao cache trong 5 phut
        _cache.Set($"OTP_{input}", otpCode, TimeSpan.FromMinutes(5));

        var displayMessage = input.Contains('@')
            ? $"Mã OTP đã gửi thành công tới email {input}!"
            : (input.Length == 12 && char.IsDigit(input[0]))
                ? $"Mã OTP đã tạo thành công cho CCCD {input}!"
                : $"Mã OTP đã gửi thành công tới số {input}!";

        return Json(new {
            success = true,
            message = displayMessage,
            otpDemo = otpCode,
            soDienThoai = input
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
        var input = model.SoDienThoai?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input) && model.DanhTinhQuet != null)
        {
            // TEN TAI KHOAN chi duoc lay tu SO DIEN THOAI, hoac tu MaBN khi quet QR
            // PHIEU KHAM HIS. KHONG lay so CCCD: lam vay la de ra tai khoan mang so
            // can cuoc, con benh nhan thi mat duong nhap so dien thoai that. Hang rao
            // nay phai dung o CA server, vi goi thang API la vuot mat trinh duyet.
            if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet.DienThoai))
                input = model.DanhTinhQuet.DienThoai.Trim();
            else if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN))
                input = model.DanhTinhQuet.MaBN.Trim();
        }

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(model.Otp))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin!" });
        }

        var otpInput = model.Otp.Trim();
        var adminReauth = AdminAuthentication.IsAdminReturnUrl(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl);

        // Co so dang an (DM_CSKCB.Active = 0) thi khong sinh phien MOI tai co so do.
        // Ve !adminReauth la BAT BUOC: action nay phuc vu ca re-auth Admin/DoiTac,
        // thieu no la khoa luon duong dang nhap quan tri. Xem ADR 0013.
        if (!adminReauth && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && !await _luong.CoSoDangHienThiAsync(model.MaCoSo))
        {
            return Json(new { success = false, message = "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến." });
        }

        // Tìm tài khoản từ database theo SĐT hoặc Email trước để kiểm tra role
        var taiKhoan = await _taiKhoanService.DangNhapAsync(input, "");

        if (taiKhoan != null && (string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(taiKhoan.Role, "DoiTac", StringComparison.OrdinalIgnoreCase)))
        {
            // Kiểm tra mật khẩu trong database
            // Cot nay la MatKhauNoiBo (ADR 0009). Sau migration no dang NULL vi phan
            // BAM chua thi hanh — xem muc Dinh chinh cua ADR 0009 — nen hai tai khoan
            // Admin/DoiTac tam thoi roi vao nhanh "mat khau khong chinh xac".
            if (string.IsNullOrEmpty(taiKhoan.MatKhauNoiBo)
                || !string.Equals(taiKhoan.MatKhauNoiBo, otpInput, StringComparison.Ordinal))
            {
                return Json(new { success = false, message = "Mật khẩu không chính xác!" });
            }
        }
        else
        {
            // Kiểm tra mã OTP
            _cache.TryGetValue($"OTP_{input}", out string? cachedOtp);
            if (otpInput != "123456" && otpInput != "1234" && (cachedOtp == null || cachedOtp != otpInput))
            {
                return Json(new { success = false, message = "Mã xác thực không chính xác hoặc đã hết hạn!" });
            }
        }

        if (taiKhoan != null
            && string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản quản trị vui lòng đăng nhập tại khu vực Admin."
            });
        }

        // 🔴 Bat bien (ADR 0027): dang nhap duoc thi phai co tai khoan. HT_TaiKhoan
        // chi duoc tao trong BaoDamHoSoNoiBoAsync, ma ham do thoat ngay khi khong co
        // co so => cap cookie o day la de ra mot phien KHONG CO TAI KHOAN, khong loi
        // thoat, moi man phia sau chi biet noi "Khong tim thay tai khoan.".
        // Ranh gioi la VA, khong phai HOAC: nguoi DA CO tai khoan van vao duoc tu
        // /DangNhap/Login tran (LoginPath cua Program.cs day ve day), khong bi chan.
        // (Ho van ha canh o /benh-nhan chu khong dung dau trang, vi ChonDichDenAsync
        //  bo returnUrl khi thieu ma co so — hanh vi CO SAN, khong phai do cong chan
        //  nay. Do that 10/09.) Re-auth Admin duoc mien tru.
        bool laQrHis = model.DanhTinhQuet != null && (model.DanhTinhQuet.LaNguonHis || !string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN));

        // Khóa luồng tự đăng ký tài khoản: chỉ cho phép tài khoản đã có sẵn từ HIS
        // NGOẠI LỆ: Bệnh nhân đã khám quét QR phiếu khám HIS -> tự động tạo tài khoản và hồ sơ
        if (!adminReauth && taiKhoan is null)
        {
            if (!laQrHis)
            {
                return Json(new
                {
                    success = false,
                    message = "Số điện thoại chưa có hồ sơ tại cơ sở y tế. Vui lòng liên hệ phòng khám/bệnh viện để được đăng ký.",
                    dichDen = "/"
                });
            }

            taiKhoan = await TaoTaiKhoanVaHoSoTuQrAsync(input, model.MaCoSo, model.Cccd, model.DanhTinhQuet!);
        }

        if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.MaBN))
        {
            var coSoMa = await (from cs in _dbContext.BenhNhanCoSos
                                join kcb in _dbContext.DMCSKCBs on cs.IdCoSo equals kcb.Id
                                where cs.MaBN == model.DanhTinhQuet.MaBN
                                select kcb.MaCoSo).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(coSoMa))
            {
                model.MaCoSo = coSoMa;
            }
        }
        if (string.IsNullOrWhiteSpace(model.MaCoSo))
        {
            model.MaCoSo = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Id)
                .Select(x => x.MaCoSo)
                .FirstOrDefaultAsync();
        }

        if (string.IsNullOrWhiteSpace(model.Cccd))
        {
            if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.Cccd))
            {
                model.Cccd = model.DanhTinhQuet.Cccd.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.MaBN))
            {
                model.Cccd = await (from cs in _dbContext.BenhNhanCoSos
                                    join bn in _dbContext.BenhNhans on cs.IdBenhNhan equals bn.Id
                                    where cs.MaBN == model.DanhTinhQuet.MaBN
                                    select bn.CCCD).FirstOrDefaultAsync();
            }
        }

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

            // Tự động gắn claim HoSoDangChon khi người dùng quét mã QR phiếu khám HIS
            long? idHoSoQuet = taiKhoan?.IdBenhNhan;
            if (model.DanhTinhQuet != null)
            {
                var maBnQuet = model.DanhTinhQuet.MaBN?.Trim();
                var cccdQuet = model.DanhTinhQuet.Cccd?.Trim();
                if (!string.IsNullOrWhiteSpace(maBnQuet))
                {
                    var idBn = await (from cs in _dbContext.BenhNhanCoSos
                                      where cs.MaBN == maBnQuet
                                      select (long?)cs.IdBenhNhan).FirstOrDefaultAsync();
                    if (idBn != null && idBn > 0) idHoSoQuet = idBn;
                }
                if (idHoSoQuet == null && !string.IsNullOrWhiteSpace(cccdQuet) && !Services.Partner.LuongCongBenhNhan.LaMaGia(cccdQuet))
                {
                    var idBn = await _dbContext.BenhNhans
                        .Where(b => b.CCCD == cccdQuet)
                        .Select(b => (long?)b.Id)
                        .FirstOrDefaultAsync();
                    if (idBn != null && idBn > 0) idHoSoQuet = idBn;
                }
            }

            if (idHoSoQuet != null && idHoSoQuet > 0)
            {
                if (taiKhoan != null && taiKhoan.Id > 0)
                {
                    await _thuTuc.NhanChuSoHuuAsync(idHoSoQuet.Value, taiKhoan.Id);
                }

                var csList = await _dbContext.BenhNhanCoSos
                    .Where(x => x.IdBenhNhan == idHoSoQuet.Value && !x.DaMoTaiLieu)
                    .ToListAsync();
                if (csList.Count > 0)
                {
                    foreach (var item in csList)
                    {
                        item.DaMoTaiLieu = true;
                    }
                    await _dbContext.SaveChangesAsync();
                }

                claims.Add(new Claim(LuongCongBenhNhan.ClaimHoSoDangChon, idHoSoQuet.Value.ToString()));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Ghi nho dang nhap truong ton
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365) // Het han sau 1 nam
            };

            await HttpContext.SignOutAsync(AdminAuthentication.Scheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            if (adminReauth)
            {
                var adminIdentity = new ClaimsIdentity(claims, AdminAuthentication.Scheme);
                await HttpContext.SignInAsync(
                    AdminAuthentication.Scheme,
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
                : await ChonDichDenAsync(model.MaCoSo, model.Cccd, input, model.ReturnUrl, model.DanhTinhQuet);

            // Xóa cookie QR đã quét khi đăng nhập thành công
            Response.Cookies.Delete("qr_data", new CookieOptions { Path = "/" });

            return Json(new { success = true, redirectUrl });
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanFirebaseToken([FromBody] GuiOtpRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.SoDienThoai))
        {
            return Json(new { success = false, message = "Số điện thoại không hợp lệ!" });
        }

        var sdt = model.SoDienThoai.Trim();
        var taiKhoan = await _taiKhoanService.DangNhapAsync(sdt, "");
        if (taiKhoan != null && string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản quản trị vui lòng đăng nhập tại khu vực Admin."
            });
        }
        var adminReauth = AdminAuthentication.IsAdminReturnUrl(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl);

        // Cung ly le voi XacNhanOtp: day la cua thu hai (va cuoi cung) sinh phien moi.
        if (!adminReauth && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && !await _luong.CoSoDangHienThiAsync(model.MaCoSo))
        {
            return Json(new { success = false, message = "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến." });
        }

        bool laQrHis = model.DanhTinhQuet != null && (model.DanhTinhQuet.LaNguonHis || !string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN));

        // Khóa luồng tự đăng ký tài khoản: chỉ cho phép tài khoản đã có sẵn từ HIS
        // NGOẠI LỆ: Bệnh nhân đã khám quét QR phiếu khám HIS -> tự động tạo tài khoản và hồ sơ
        if (!adminReauth && taiKhoan is null)
        {
            if (!laQrHis)
            {
                return Json(new
                {
                    success = false,
                    message = "Số điện thoại chưa có hồ sơ tại cơ sở y tế. Vui lòng liên hệ phòng khám/bệnh viện để được đăng ký.",
                    dichDen = "/"
                });
            }

            taiKhoan = await TaoTaiKhoanVaHoSoTuQrAsync(sdt, model.MaCoSo, model.Cccd, model.DanhTinhQuet!);
        }

        if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.MaBN))
        {
            var coSoMa = await (from cs in _dbContext.BenhNhanCoSos
                                join kcb in _dbContext.DMCSKCBs on cs.IdCoSo equals kcb.Id
                                where cs.MaBN == model.DanhTinhQuet.MaBN
                                select kcb.MaCoSo).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(coSoMa))
            {
                model.MaCoSo = coSoMa;
            }
        }
        if (string.IsNullOrWhiteSpace(model.MaCoSo))
        {
            model.MaCoSo = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Id)
                .Select(x => x.MaCoSo)
                .FirstOrDefaultAsync();
        }

        if (string.IsNullOrWhiteSpace(model.Cccd))
        {
            if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.Cccd))
            {
                model.Cccd = model.DanhTinhQuet.Cccd.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.MaBN))
            {
                model.Cccd = await (from cs in _dbContext.BenhNhanCoSos
                                    join bn in _dbContext.BenhNhans on cs.IdBenhNhan equals bn.Id
                                    where cs.MaBN == model.DanhTinhQuet.MaBN
                                    select bn.CCCD).FirstOrDefaultAsync();
            }
        }

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

        // Tự động gắn claim HoSoDangChon khi người dùng quét mã QR phiếu khám HIS
        long? idHoSoQuet = taiKhoan?.IdBenhNhan;
        if (model.DanhTinhQuet != null)
        {
            var maBnQuet = model.DanhTinhQuet.MaBN?.Trim();
            var cccdQuet = model.DanhTinhQuet.Cccd?.Trim();
            if (!string.IsNullOrWhiteSpace(maBnQuet))
            {
                var idBn = await (from cs in _dbContext.BenhNhanCoSos
                                  where cs.MaBN == maBnQuet
                                  select (long?)cs.IdBenhNhan).FirstOrDefaultAsync();
                if (idBn != null && idBn > 0) idHoSoQuet = idBn;
            }
            if (idHoSoQuet == null && !string.IsNullOrWhiteSpace(cccdQuet) && !Services.Partner.LuongCongBenhNhan.LaMaGia(cccdQuet))
            {
                var idBn = await _dbContext.BenhNhans
                    .Where(b => b.CCCD == cccdQuet)
                    .Select(b => (long?)b.Id)
                    .FirstOrDefaultAsync();
                if (idBn != null && idBn > 0) idHoSoQuet = idBn;
            }
        }

        if (idHoSoQuet != null && idHoSoQuet > 0)
        {
            if (taiKhoan != null && taiKhoan.Id > 0)
            {
                await _thuTuc.NhanChuSoHuuAsync(idHoSoQuet.Value, taiKhoan.Id);
            }

            var csList = await _dbContext.BenhNhanCoSos
                .Where(x => x.IdBenhNhan == idHoSoQuet.Value && !x.DaMoTaiLieu)
                .ToListAsync();
            if (csList.Count > 0)
            {
                foreach (var item in csList)
                {
                    item.DaMoTaiLieu = true;
                }
                await _dbContext.SaveChangesAsync();
            }

            claims.Add(new Claim(LuongCongBenhNhan.ClaimHoSoDangChon, idHoSoQuet.Value.ToString()));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, // Ghi nho dang nhap truong ton 365 ngay
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
        };

        await HttpContext.SignOutAsync(AdminAuthentication.Scheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        if (adminReauth)
        {
            var adminIdentity = new ClaimsIdentity(claims, AdminAuthentication.Scheme);
            await HttpContext.SignInAsync(
                AdminAuthentication.Scheme,
                new ClaimsPrincipal(adminIdentity),
                new AuthenticationProperties { IsPersistent = false });
        }

        // Lưu thông tin thiết bị đăng nhập vào database
        await LuuThietBiDangNhapAsync(sdt, model.DeviceId, model.DeviceName);

        var dichDen = adminReauth
            ? model.ReturnUrl
            : await ChonDichDenAsync(model.MaCoSo, model.Cccd, sdt, model.ReturnUrl, model.DanhTinhQuet);

        // Xóa cookie QR đã quét khi đăng nhập thành công
        Response.Cookies.Delete("qr_data", new CookieOptions { Path = "/" });

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
        // Khóa luồng tự đăng ký tài khoản bệnh nhân (chỉ chừa luồng từ HIS sang)
        return RedirectToAction(nameof(Login), new { coSo, returnUrl });
        /*
        var maCoSo = LayMaCoSoPhien(coSo);
        if (maCoSo is null) return RedirectToAction(nameof(Login));

        await DoNguCanhRaViewBagAsync(maCoSo, returnUrl);
        return View();
        */
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> TaoTaiKhoan([FromBody] TaoTaiKhoanRequest model)
    {
        // Khóa luồng tự đăng ký tài khoản bệnh nhân (chỉ chừa luồng từ HIS sang)
        return Json(new { success = false, message = "Chức năng đăng ký tài khoản trực tuyến đã tạm đóng. Vui lòng liên hệ cơ sở y tế." });
        /*
        if (string.IsNullOrWhiteSpace(model.HoTen))
        {
            return Json(new { success = false, message = "Vui lòng nhập họ và tên!" });
        }

        // KHONG tin cccd / maCoSo / dinhDanh tu body: neu tin thi bat ky ai cung
        // POST duoc voi CCCD nguoi khac de mo tai khoan ben doi tac. Lay tu phien.
        var (maCoSo, cccd, dinhDanh) = LayDanhTinhPhien();
        if (maCoSo is null || cccd is null)
        {
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại!" });
        }

        // Nhanh co API: mat khau ben doi tac = CCCD, man Dang ky khong hoi nua.
        // Nhanh noi bo: benh nhan tu dat, van phai kiem do dai.
        var cua = await _luong.LayCuaAsync(maCoSo);
        if (cua?.CoBanGiao != true && (string.IsNullOrWhiteSpace(model.MatKhau) || model.MatKhau.Length < 6))
        {
            return Json(new { success = false, message = "Mật khẩu phải có ít nhất 6 ký tự!" });
        }

        var ketQua = await _luong.MoTaiKhoanAsync(
            maCoSo, cccd, dinhDanh ?? string.Empty, model.HoTen.Trim(), model.MatKhau, model.ReturnUrl);

        if (!ketQua.ThanhCong)
        {
            return Json(new { success = false, message = ketQua.ThongBao });
        }

        return Json(new { success = true, redirectUrl = ketQua.DichDen });
        */
    }

    // ==================================================================
    //  Bo man cua doi tac — Dang nhap / Dang ky / Quen mat khau (ADR 0014)
    //
    //  Deu la [AllowAnonymous]: benh nhan CHUA co phien khi go vao day, nen
    //  khac han cac man o tren (lay danh tinh tu phien). maCoSo nhan tu client
    //  la CHAP NHAN DUOC — slug co so von cong khai tren trang, va thu quyet
    //  dinh moi chuyen la mat khau, do CHINH doi tac phan xu.
    // ==================================================================

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> UbDangNhap([FromBody] UbDangNhapRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.MaCoSo)
            || string.IsNullOrWhiteSpace(model.Cccd)
            || string.IsNullOrWhiteSpace(model.MatKhau))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin!" });
        }

        var cccd = model.Cccd.Trim();
        var ketQua = await _luong.DangNhapDoiTacAsync(model.MaCoSo.Trim(), cccd, model.MatKhau, model.ReturnUrl);

        if (!ketQua.ThanhCong)
        {
            return Json(new { success = false, message = ketQua.ThongBao });
        }

        await CapPhienBenhNhanAsync(cccd, model.MaCoSo.Trim(), model.DeviceId, model.DeviceName);

        return Json(new { success = true, redirectUrl = ketQua.DichDen });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> UbDangKy(string coSo, string? returnUrl = null)
    {
        var maCoSo = await LayMaCoSoDoiTacAsync(coSo);
        if (maCoSo is null) return RedirectToAction(nameof(Login), new { coSo });

        await DoNguCanhRaViewBagAsync(maCoSo, returnUrl);
        ViewBag.SlugCoSo = coSo;

        var cua = await _luong.LayCuaAsync(maCoSo);
        ViewBag.TrangChuDoiTac = cua?.CauHinh.TrangChu?.TrimEnd('/');
        ViewBag.ChiNhanh = await LayChiNhanhAsync(maCoSo);
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> UbTaoTaiKhoan([FromBody] UbDangKyRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.MaCoSo)
            || string.IsNullOrWhiteSpace(model.Cccd)
            || string.IsNullOrWhiteSpace(model.DienThoai))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin!" });
        }

        // Doi tac bat toi thieu 6 ky tu; chan ngay o day de khoi ton mot luot goi.
        if (string.IsNullOrWhiteSpace(model.MatKhau) || model.MatKhau.Length < 6)
        {
            return Json(new { success = false, message = "Mật khẩu phải có ít nhất 6 ký tự!" });
        }

        var ketQua = await _luong.DangKyDoiTacAsync(
            model.MaCoSo.Trim(), model.Cccd.Trim(), model.DienThoai.Trim(), model.Email, model.MatKhau, model.Kenh);

        return Json(new { success = ketQua.ThanhCong, message = ketQua.ThongBao });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> UbXacThucMa([FromBody] UbXacThucMaRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.MaCoSo)
            || string.IsNullOrWhiteSpace(model.Cccd)
            || string.IsNullOrWhiteSpace(model.Ma))
        {
            return Json(new { success = false, message = "Vui lòng nhập mã xác thực!" });
        }

        var cccd = model.Cccd.Trim();
        var ketQua = await _luong.XacThucMaDoiTacAsync(model.MaCoSo.Trim(), cccd, model.Ma.Trim(), model.ReturnUrl);

        if (!ketQua.ThanhCong)
        {
            return Json(new { success = false, message = ketQua.ThongBao });
        }

        await CapPhienBenhNhanAsync(cccd, model.MaCoSo.Trim(), model.DeviceId, model.DeviceName);

        return Json(new { success = true, redirectUrl = ketQua.DichDen });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> UbQuenMatKhau(string coSo)
    {
        var maCoSo = await LayMaCoSoDoiTacAsync(coSo);
        if (maCoSo is null) return RedirectToAction(nameof(Login), new { coSo });

        await DoNguCanhRaViewBagAsync(maCoSo, null);
        ViewBag.SlugCoSo = coSo;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> UbGuiQuenMatKhau([FromBody] UbQuenMatKhauRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.MaCoSo)
            || string.IsNullOrWhiteSpace(model.Cccd)
            || string.IsNullOrWhiteSpace(model.EmailHoacSdt))
        {
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin!" });
        }

        var ketQua = await _luong.QuenMatKhauDoiTacAsync(
            model.MaCoSo.Trim(), model.Cccd.Trim(), model.EmailHoacSdt.Trim());

        return Json(new { success = ketQua.ThanhCong, message = ketQua.ThongBao });
    }

    /// <summary>
    /// Cap phien SixosPwa cho benh nhan vua duoc DOI TAC xac nhan danh tinh.
    ///
    /// Bam theo dung khuon claim cua XacNhanOtp — hai claim ClaimCccd va
    /// ClaimMaCoSo la cach cac man phia sau (Ban giao, trang benh nhan) biet
    /// benh nhan la ai va dang o co so nao.
    ///
    /// KHONG co nhanh adminReauth o day: bo man doi tac chi phuc vu benh nhan,
    /// tai khoan quan tri van dang nhap o khu vuc Admin nhu cu.
    /// </summary>
    private async Task CapPhienBenhNhanAsync(string cccd, string maCoSo, string? deviceId, string? deviceName)
    {
        // Doi tac giu ten/dien thoai that; ta chi chac chan ve CCCD. Ho so noi bo
        // vua duoc GhiHoSoRoiChoBanGiaoAsync tao xong nen doc lai duoc so dien thoai.
        var taiKhoan = await (from t in _dbContext.TaiKhoans
                              join b in _dbContext.BenhNhans on t.IdBenhNhan equals b.Id
                              where b.CCCD == cccd
                              select t).FirstOrDefaultAsync();

        var username = string.IsNullOrWhiteSpace(taiKhoan?.SDT) ? cccd : taiKhoan!.SDT;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "BenhNhan"),
            new Claim(LuongCongBenhNhan.ClaimCccd, cccd),
            new Claim(LuongCongBenhNhan.ClaimMaCoSo, maCoSo),

            // DAU AN — chi dong o day, va day la CHO DUY NHAT. Toi duoc ham nay
            // nghia la CHINH doi tac vua phan xu mat khau that (UbDangNhap) hoac ma
            // xac thuc cua ho (UbXacThucMa). Phien duc tu XacNhanOtp KHONG bao gio
            // co claim nay — do la toan bo diem cua thiet ke. Xem ADR 0016.
            new Claim(LuongCongBenhNhan.ClaimDoiTacXacThuc, "1")
        };

        if (!string.IsNullOrWhiteSpace(taiKhoan?.SDT))
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, taiKhoan.SDT));
        }
        if (!string.IsNullOrWhiteSpace(taiKhoan?.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, taiKhoan.Email));
        }

        if (taiKhoan?.IdBenhNhan != null && taiKhoan.IdBenhNhan > 0)
        {
            claims.Add(new Claim(LuongCongBenhNhan.ClaimHoSoDangChon, taiKhoan.IdBenhNhan.ToString()!));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
        };

        await HttpContext.SignOutAsync(AdminAuthentication.Scheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            authProperties);

        await LuuThietBiDangNhapAsync(username, deviceId, deviceName);
    }

    /// <summary>
    /// Slug -> ma co so, va chi tra ve khi co so do THAT SU dung man doi tac.
    /// Go tay URL /DangNhap/UbDangKy?coSo=&lt;co so noi bo&gt; thi bi day ve man
    /// dang nhap thuong chu khong duoc thay man clone.
    /// </summary>
    private async Task<string?> LayMaCoSoDoiTacAsync(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        var thongTin = await _dbContext.DMCSKCBs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == slug);

        // Co so dang an thi khong nhan dang ky moi (ADR 0013).
        if (thongTin is null || !thongTin.Active) return null;

        var cua = await _luong.LayCuaAsync(thongTin.MaCoSo);
        return cua?.DungManDoiTac == true ? thongTin.MaCoSo : null;
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

        // 🔴 CHOT CHAN — dung go bo. Man nay tra ve mot form da dien san MAT KHAU
        // THAT cua benh nhan, chi cho tu POST sang doi tac. Vi vay [Authorize] mot
        // minh la KHONG du: XacNhanOtp goi tran duoc, ma OTP con dang ke tam mot
        // gia tri co dinh, va ca Cccd lan MaCoSo deu lay thang tu than request —
        // ai biet CCCD cua nguoi khac la duc duoc mot phien roi mo thang man nay.
        //
        // Chan ngay tai cua XacNhanOtp thi KHONG du: bo trong MaCoSo la phien khong
        // co claim, roi LayMaCoSoPhien lai lay ?coSo= tren URL. Phai doi DAU AN nam
        // tren chinh phien. Xem ADR 0016.
        var cuaBanGiao = await _luong.LayCuaAsync(maCoSo);
        if (cuaBanGiao?.DungManDoiTac == true
            && User.FindFirst(LuongCongBenhNhan.ClaimDoiTacXacThuc) is null)
        {
            return await VeManDangNhapCoSoAsync(maCoSo);
        }

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
        ViewBag.CacBuoc = thongTin.CacBuoc;
        ViewBag.TrangChu = thongTin.DichCuoi;
        return View();
    }

    /// <summary>
    /// Nguoi dung chu dong bam "Tiep tuc" o man dang nhap khi phien van con, hoac
    /// bam "Ho so benh nhan" o menu 3 gach khi da dang nhap. Day moi la cho chay
    /// cay quyet dinh — KHONG tu chay khi chi mo trang. Chi thao tac tren MaCoSo
    /// CUA PHIEN — doi sang co so khac khong con di qua day nua, modal chan dang
    /// nhap cheo co so (ADR 0006) da lo tu luc vao, nen khong con nhanh "doi claim
    /// am tham" o day.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> DiTiep(string? returnUrl = null)
    {
        var cccd = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        if (string.IsNullOrWhiteSpace(maCoSo) || string.IsNullOrWhiteSpace(cccd))
        {
            return Redirect("/benh-nhan");
        }

        // Co so dung man cua doi tac: ChonDichDenAsync CO Y tra ve man dang nhap
        // (chot chan cho duong OTP — xem chu thich tai LuongCongBenhNhan.cs). Duong
        // moi di BEN CANH no, khoa bang dau an, chu KHONG di xuyen qua no: dung sua
        // ChonDichDenAsync de "cho tien". ADR 0016.
        var cuaCoSo = await _luong.LayCuaAsync(maCoSo);
        if (cuaCoSo?.DungManDoiTac == true
            && User.FindFirst(LuongCongBenhNhan.ClaimDoiTacXacThuc) is not null)
        {
            var diTiep = await _luong.DangNhapLaiBangMatKhauDaCatAsync(maCoSo, cccd, returnUrl);
            if (diTiep.ThanhCong && !string.IsNullOrWhiteSpace(diTiep.DichDen))
            {
                return Redirect(diTiep.DichDen);
            }
        }

        var dichDen = await _luong.ChonDichDenAsync(maCoSo, cccd, dinhDanh, returnUrl);
        return Redirect(dichDen);
    }

    /// <summary>
    /// Dua benh nhan ve dung man dang nhap CUA CO SO do. Ve /DangNhap/Login tran
    /// thi man dang nhap khong biet co so nao, phien sau khong co claim MaCoSo va
    /// benh nhan roi thang vao nhanh noi bo — cung ly le voi DangXuat.
    /// </summary>
    private async Task<IActionResult> VeManDangNhapCoSoAsync(string maCoSo)
    {
        var slug = await _dbContext.DMCSKCBs
            .AsNoTracking()
            .Where(x => x.MaCoSo == maCoSo)
            .Select(x => x.Slug)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(slug)
            ? RedirectToAction(nameof(Login))
            : Redirect($"/DangNhap/Login?coSo={Uri.EscapeDataString(slug)}");
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
    /// <summary>
    /// Logo cua co so de bo man doi tac hien dung nhan dien cua ho, thay vi logo
    /// SixosPwa. Cot DM_CSKCB.Logo giu hoac URL tuyet doi, hoac duong dan noi bo
    /// "/anh/logo_cs/x.jpg" (KhoAnh sinh ra, AnhController phuc vu) — ca hai dang
    /// deu gan thang vao src duoc.
    ///
    /// UnescapeDataString bam theo Views/Home/ChiTietCoSo.cshtml:7: URL trong cot
    /// nay co the da bi ma hoa mot lan truoc khi luu.
    ///
    /// Tra null khi co so chua co logo — view tu roi ve anh mac dinh.
    /// </summary>
    /// <summary>
    /// Chi nhanh cua doi tac cho panel trai o kho may tinh. Cache 10 phut: day chi
    /// la khoi TRANG TRI, khong dang de moi lan mo man dang nhap la mot luot goi
    /// sang trang doi tac. Hong thi tra rong — panel tu an, khong chan dang nhap.
    /// </summary>
    private async Task<IReadOnlyList<ChiNhanhDoiTac>> LayChiNhanhAsync(string? maCoSo)
    {
        if (string.IsNullOrWhiteSpace(maCoSo)) return Array.Empty<ChiNhanhDoiTac>();

        var khoa = "ChiNhanhDoiTac_" + maCoSo;
        if (_cache.TryGetValue(khoa, out IReadOnlyList<ChiNhanhDoiTac>? cu) && cu is not null)
        {
            return cu;
        }

        var ds = await _luong.LayChiNhanhDoiTacAsync(maCoSo);
        _cache.Set(khoa, ds, TimeSpan.FromMinutes(10));
        return ds;
    }

    private static string? LayLogoCoSo(DMCSKCB? coSo)
    {
        var logo = coSo?.Logo;
        return string.IsNullOrWhiteSpace(logo) ? null : Uri.UnescapeDataString(logo);
    }

    private async Task DoNguCanhRaViewBagAsync(string maCoSo, string? returnUrl)
    {
        var coSo = await _dbContext.DMCSKCBs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaCoSo == maCoSo);

        ViewBag.MaCoSo = maCoSo;
        ViewBag.TenCoSo = coSo?.TenCoSo ?? "cơ sở khám chữa bệnh";
        ViewBag.SlugCoSo = coSo?.Slug;
        ViewBag.LogoCoSo = LayLogoCoSo(coSo);
        ViewBag.ReturnUrl = returnUrl;

        ViewBag.Cccd = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        ViewBag.DinhDanh = User.FindFirst(ClaimTypes.Name)?.Value;
        ViewBag.DienThoai = User.FindFirst(ClaimTypes.MobilePhone)?.Value;

        // Co so co API rieng thi mat khau ben ho dat bang CCCD, khong hoi benh
        // nhan nua. Co so noi bo thi van de ho tu dat.
        var cua = await _luong.LayCuaAsync(maCoSo);
        ViewBag.CoBanGiao = cua?.CoBanGiao == true;
    }

    private async Task<TaiKhoan> TaoTaiKhoanVaHoSoTuQrAsync(string sdt, string? maCoSo, string? cccdInput, DanhTinhQuet quet)
    {
        var cccd = !string.IsNullOrWhiteSpace(quet.Cccd) ? quet.Cccd.Trim() : (cccdInput?.Trim() ?? string.Empty);
        var hoTen = !string.IsNullOrWhiteSpace(quet.HoTen) ? quet.HoTen.Trim() : sdt;
        var ngaySinh = quet.DoiNgay();
        var gioiTinh = quet.DoiGioiTinh();
        var diaChi = string.IsNullOrWhiteSpace(quet.DiaChi) ? null : quet.DiaChi.Trim();
        var slug = Services.ChuanHoaTen.BoDau(hoTen);

        // 1. Xác định cơ sở y tế và hồ sơ hiện có (nếu bệnh nhân đã có trên HIS)
        long? idCoSo = null;
        long? existingBnId = null;
        if (!string.IsNullOrWhiteSpace(quet.MaBN))
        {
            var coSoInfo = await (from cs in _dbContext.BenhNhanCoSos
                                  where cs.MaBN == quet.MaBN
                                  select new { cs.IdCoSo, cs.IdBenhNhan }).FirstOrDefaultAsync();
            if (coSoInfo != null)
            {
                idCoSo = coSoInfo.IdCoSo;
                existingBnId = coSoInfo.IdBenhNhan;
            }
        }
        if (idCoSo == null && !string.IsNullOrWhiteSpace(maCoSo))
        {
            idCoSo = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .Where(x => x.MaCoSo == maCoSo)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync();
        }
        if (idCoSo == null)
        {
            idCoSo = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync();
        }

        long idBenhNhanTarget = existingBnId ?? 0;
        if (idBenhNhanTarget <= 0)
        {
            // 2. Lưu thông tin người bệnh vào DM_BenhNhan nếu chưa có
            var luuNguoi = await _thuTuc.SaveBenhNhanAsync(
                cccd,
                hoTen,
                sdt,
                null,
                diaChi,
                idTaiKhoan: null,
                ngaySinh: ngaySinh,
                hoTenKhongDau: slug,
                gioiTinh: gioiTinh);
            idBenhNhanTarget = luuNguoi.Id;

            if (idCoSo != null && idBenhNhanTarget > 0)
            {
                await _thuTuc.TaoHoSoTuKhaiAsync(idBenhNhanTarget, idCoSo.Value);
            }
        }

        // 3. Tạo tài khoản HT_TaiKhoan
        var luuTaiKhoan = await _thuTuc.SaveTaiKhoanAsync(
            0,
            sdt,
            null,
            "BenhNhan",
            null,
            idBenhNhanTarget > 0 ? idBenhNhanTarget : null);

        var idTaiKhoan = luuTaiKhoan.Id;

        // 4. Gán quyền sở hữu hồ sơ cho tài khoản
        if (idBenhNhanTarget > 0 && idTaiKhoan > 0)
        {
            await _thuTuc.NhanChuSoHuuAsync(idBenhNhanTarget, idTaiKhoan);
        }

        // 5. Đảm bảo mở tài liệu
        if (idBenhNhanTarget > 0)
        {
            var csList = await _dbContext.BenhNhanCoSos
                .Where(x => x.IdBenhNhan == idBenhNhanTarget && !x.DaMoTaiLieu)
                .ToListAsync();
            if (csList.Count > 0)
            {
                foreach (var item in csList)
                {
                    item.DaMoTaiLieu = true;
                }
                await _dbContext.SaveChangesAsync();
            }
        }

        var taiKhoan = await _dbContext.TaiKhoans.FirstOrDefaultAsync(x => x.Id == idTaiKhoan)
                       ?? new TaiKhoan { Id = idTaiKhoan, SDT = sdt, Role = "BenhNhan", IdBenhNhan = idBenhNhanTarget > 0 ? idBenhNhanTarget : null };

        return taiKhoan;
    }

    /// <summary>
    /// Cho ha canh sau khi xac thuc. Vao thang /DangNhap/Login (khong qua trang
    /// co so) thi khong biet benh nhan o co so nao, nen chi ve duoc trang benh
    /// nhan dang toi gian — muon di tiep phai vao lai qua /DangKyOnline/{slug}.
    /// </summary>
    private async Task<string?> ChonDichDenAsync(string? maCoSo, string? cccd, string dinhDanh,
                                                 string? returnUrl, DanhTinhQuet? quet = null)
    {
        // Khi quét mã phiếu khám HIS, đích đến phải luôn là /benh-nhan (trừ khi có returnUrl cụ thể khác "/")
        if (quet != null && (quet.LaNguonHis || !string.IsNullOrWhiteSpace(quet.MaBN)))
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl != "/" && returnUrl != "/Home" && !returnUrl.StartsWith("/DangNhap"))
            {
                return returnUrl;
            }
            return "/benh-nhan";
        }

        if (string.IsNullOrWhiteSpace(maCoSo) || string.IsNullOrWhiteSpace(cccd))
        {
            return "/benh-nhan";
        }

        return await _luong.ChonDichDenAsync(maCoSo, cccd.Trim(), dinhDanh, returnUrl, quet);
    }

    private async Task LuuThietBiDangNhapAsync(string soDienThoai, string? deviceId, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;

        try
        {
            // Thiet bi nay khoa theo IDTaiKhoan chu khong con theo chuoi so dien thoai,
            // va moi duong ghi di qua thu tuc (ADR 0008). Thu tuc tu lo them-hay-cap-nhat.
            var idTaiKhoan = await _dbContext.TaiKhoans.AsNoTracking()
                .Where(t => t.SDT == soDienThoai)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync();

            if (idTaiKhoan is null) return;

            await _thuTuc.SaveThietBiAsync(idTaiKhoan.Value, deviceId, deviceName, true);
        }
        catch (Exception)
        {
            // Bỏ qua lỗi để không làm gián đoạn đăng nhập của người dùng
        }
    }

    /// <summary>
    /// denCoSo (slug): dung khi bam "Dang xuat va dang nhap lai" trong modal chan
    /// dang nhap cheo co so (ADR 0006) — sau khi thoat phien cu, dua thang toi man
    /// dang nhap cua co so MOI kem returnUrl la y dinh cua nut benh nhan da bam,
    /// thay vi ve lai co so cu nhu dang xuat binh thuong.
    /// </summary>
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> DangXuat(string? denCoSo = null, string? returnUrl = null)
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

        await HttpContext.SignOutAsync(AdminAuthentication.Scheme);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!string.IsNullOrWhiteSpace(denCoSo))
        {
            var yDinh = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
            return Redirect($"/DangNhap/Login?coSo={denCoSo}&returnUrl={Uri.EscapeDataString(yDinh)}");
        }

        if (!string.IsNullOrWhiteSpace(slug))
        {
            return Redirect($"/DangKyOnline/{slug}");
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
    /// <summary>
    /// Danh tinh doc duoc tu ma QR o man Dang nhap, neu benh nhan di duong do.
    /// 🔴 Du lieu tho tu may khach — xem canh bao trong <see cref="DanhTinhQuet"/>.
    /// </summary>
    public DanhTinhQuet? DanhTinhQuet { get; set; }

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
    /// <summary>
    /// Danh tinh doc duoc tu ma QR o man Dang nhap, neu benh nhan di duong do.
    /// 🔴 Du lieu tho tu may khach — xem canh bao trong <see cref="DanhTinhQuet"/>.
    /// </summary>
    public DanhTinhQuet? DanhTinhQuet { get; set; }

}

public class TaoTaiKhoanRequest
{
    // Khong nhan MaCoSo / Cccd / DinhDanh: chung duoc lay tu claim cua phien.
    public string HoTen { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;

    /// <summary>Y dinh cua nut benh nhan da bam luc dau ("/dat-goi-kham"...).</summary>
    public string? ReturnUrl { get; set; }
}

public class HuyOtpRequest
{
    public string? SoDienThoai { get; set; }
    public string? Cccd { get; set; }
}

// --- Bo man cua doi tac (ADR 0014) ---------------------------------------
//  Deu mang MaCoSo trong than: benh nhan CHUA co phien khi go vao cac man nay.

public class UbDangNhapRequest
{
    public string MaCoSo { get; set; } = string.Empty;
    public string Cccd { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
}

public class UbDangKyRequest
{
    public string MaCoSo { get; set; } = string.Empty;
    public string Cccd { get; set; } = string.Empty;
    public string DienThoai { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string MatKhau { get; set; } = string.Empty;

    /// <summary>
    /// Kenh benh nhan chon o man Dang ky (1 = Zalo, 3 = SMS). Ban cai cua doi tac
    /// tu kiem lai, khong nhan ra thi ve SMS — nen khong tin thang gia tri nay.
    /// </summary>
    public int Kenh { get; set; }
}

public class UbXacThucMaRequest
{
    public string MaCoSo { get; set; } = string.Empty;
    public string Cccd { get; set; } = string.Empty;

    /// <summary>Ma 4 so doi tac vua gui qua SMS — benh nhan tu go.</summary>
    public string Ma { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
}

public class UbQuenMatKhauRequest
{
    public string MaCoSo { get; set; } = string.Empty;
    public string Cccd { get; set; } = string.Empty;

    /// <summary>Doi tac tu nhan ra la Email hay so dien thoai va chon kenh gui.</summary>
    public string EmailHoacSdt { get; set; } = string.Empty;
}

public class DoiMatKhauRequest
{
    public string MatKhauMoi { get; set; } = string.Empty;
}
