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

    /// <summary>
    /// Cửa cơ sở đọc thẳng từ dữ liệu (DM_CSKCB.KetNoi_UrlChuyenHuong) sau khi tầng
    /// cửa đối tác bị gỡ. Chỉ dùng cho hàng rào mật khẩu ở màn Đăng ký.
    /// </summary>
    private readonly CuaCoSoService _cuaCoSo;
    private readonly IHoSoBenhNhanService _hoSo;

    public DangNhapController(
        IMemoryCache cache,
        ITaiKhoanService taiKhoanService,
        ApplicationDbContext dbContext,
        ILuongCongBenhNhan luong,
        AdminStoredProcedureService thuTuc,
        CuaCoSoService cuaCoSo,
        IHoSoBenhNhanService hoSo)
    {
        _cache = cache;
        _taiKhoanService = taiKhoanService;
        _dbContext = dbContext;
        _luong = luong;
        _thuTuc = thuTuc;
        _cuaCoSo = cuaCoSo;
        _hoSo = hoSo;
    }

    [HttpGet("/qr")]
    [HttpGet("/qr-kham")]
    public async Task<IActionResult> QrKham(string? mabn = null, string? coSo = null)
    {
        // 🔴 Đường này đăng nhập THẲNG, không qua OTP. Không có mã trên đường dẫn mà
        // vẫn đi tiếp thì mở '/qr' tay không là được cấp phiên của một bệnh nhân bất kỳ.
        if (string.IsNullOrWhiteSpace(mabn))
        {
            return Redirect("/DangNhap/Login?ReturnUrl=%2Fbenh-nhan");
        }

        var maBnTraCuu = mabn.Trim();
        var maCoSoTraCuu = string.IsNullOrWhiteSpace(coSo) ? "77121" : coSo.Trim();

        // Đợt 1B: một dòng ĐÃ LÀ "con người + hồ sơ tại cơ sở" nên không còn tự nối.
        var hoSo = await (from bn in _dbContext.BenhNhans.AsNoTracking()
                          join cs in _dbContext.DMCSKCBs.AsNoTracking() on bn.IdCoSo equals (long?)cs.Id
                          where bn.MaBN == maBnTraCuu && (cs.MaCoSo == maCoSoTraCuu || cs.Slug == maCoSoTraCuu)
                          select new { BenhNhan = bn, CoSo = cs }).FirstOrDefaultAsync();

        if (hoSo == null)
        {
            hoSo = await (from bn in _dbContext.BenhNhans.AsNoTracking()
                          join cs in _dbContext.DMCSKCBs.AsNoTracking() on bn.IdCoSo equals (long?)cs.Id
                          where bn.MaBN == maBnTraCuu
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
                new Claim(LuongCongBenhNhan.ClaimMaCoSo, maCoSo)
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

    [HttpGet("/qr-otp")]
    public async Task<IActionResult> QrOtp(string? mabn = null, string? coSo = null, string? sdt = null)
    {
        // 🔴 Không có mã trên đường dẫn thì KHÔNG phải luồng quét — đi tiếp là đăng
        // nhập hộ một bệnh nhân bất kỳ. Trả về màn Login trước khi SignOut, để mở
        // nhầm '/qr-otp' không đánh bay phiên đang có.
        if (string.IsNullOrWhiteSpace(mabn))
        {
            return Redirect("/DangNhap/Login?ReturnUrl=%2Fbenh-nhan");
        }

        // Luôn xóa phiên cookie cũ để đảm bảo hiển thị đúng màn hình OTP
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var maBnTraCuu = mabn.Trim();
        var maCoSoTraCuu = string.IsNullOrWhiteSpace(coSo) ? "77121" : coSo.Trim();

        // Đợt 1B: một dòng ĐÃ LÀ "con người + hồ sơ tại cơ sở" nên không còn tự nối.
        var hoSo = await (from bn in _dbContext.BenhNhans.AsNoTracking()
                          join cs in _dbContext.DMCSKCBs.AsNoTracking() on bn.IdCoSo equals (long?)cs.Id
                          where bn.MaBN == maBnTraCuu && (cs.MaCoSo == maCoSoTraCuu || cs.Slug == maCoSoTraCuu)
                          select new { BenhNhan = bn, CoSo = cs }).FirstOrDefaultAsync();

        if (hoSo == null)
        {
            hoSo = await (from bn in _dbContext.BenhNhans.AsNoTracking()
                          join cs in _dbContext.DMCSKCBs.AsNoTracking() on bn.IdCoSo equals (long?)cs.Id
                          where bn.MaBN == maBnTraCuu
                          select new { BenhNhan = bn, CoSo = cs }).FirstOrDefaultAsync();
        }

        var slug = hoSo != null && !string.IsNullOrWhiteSpace(hoSo.CoSo.Slug) ? hoSo.CoSo.Slug : "pkdk-thien-nam";

        // 🔴 MÃ QUÉT PHẢI ĐƯỢC SERVER GIỮ, không thể giao cho trang Login giữ hộ.
        // Đo thật trên máy thật: sau khi '/qr-otp' chuyển sang Login?...&mabn=...,
        // trang bị nạp lại rồi đi tiếp sang '/benh-nhan', bị đẩy về Login với mỗi
        // '?ReturnUrl=%2Fbenh-nhan' — mất sạch query. Cookie qr_data thì chính màn
        // Login xóa ở 'pagehide'/'click rời trang'. Kết quả: lúc bấm Đăng nhập không
        // còn gì để biết vừa quét ai, người dùng rơi vào hồ sơ ĐẦU TIÊN của tài khoản.
        //
        // Cookie này HttpOnly nên JS không xóa được, và sống 30 phút đủ để gõ OTP.
        Response.Cookies.Append("qr_mabn", maBnTraCuu, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Expires = DateTimeOffset.UtcNow.AddMinutes(30),
            SameSite = SameSiteMode.Lax
        });

        // Nhớ cơ sở ngay từ đây, không đợi màn Login ghi hộ: quét xong là cài app luôn
        // thì lần mở từ icon đầu tiên đã phải ra đúng logo/tên cơ sở.
        Response.Cookies.Append("pwa_co_so", slug, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            SameSite = SameSiteMode.Lax
        });

        // 🔴 Không đoán bừa khi tra không ra hồ sơ: số/CCCD cũ ở đây từng làm
        // người quét nhìn thấy tài khoản của người khác. Không có gì thật thì để rỗng,
        // màn Login sẽ hỏi số điện thoại như bình thường.
        var sdtBn = !string.IsNullOrWhiteSpace(sdt) ? sdt : (hoSo?.BenhNhan.SDT ?? "");
        var cccd = hoSo?.BenhNhan.CCCD ?? "";

        // Lưu trước OTP 123456 vào cache. Bỏ qua khóa rỗng: "OTP_" là khóa dùng chung
        // cho MỌI người không tra ra danh tính — đặt vào đó là mở cửa cho cả phiên khác.
        if (!string.IsNullOrWhiteSpace(sdtBn))
        {
            _cache.Set($"OTP_{sdtBn}", "123456", TimeSpan.FromMinutes(30));
        }
        if (!string.IsNullOrWhiteSpace(cccd))
        {
            _cache.Set($"OTP_{cccd}", "123456", TimeSpan.FromMinutes(30));
        }

        // 🔴 NHỚ THEO SỐ ĐIỆN THOẠI, không chỉ nhớ bằng cookie. Đo thật trên máy thật:
        // người bệnh quét QR ở trình duyệt nhưng bấm Đăng nhập trong APP ĐÃ CÀI — hai
        // ngữ cảnh giữ cookie RIÊNG, nên cookie đặt ở bên này bên kia không thấy. Lúc
        // đó cả qr_data, qr_mabn lẫn query string đều vô nghĩa.
        //
        // Cache theo SDT là đúng mô hình mã OTP đang dùng ("OTP_{sdt}"), và vòng đời
        // cùng 30 phút. Xóa ở HuyOtp để giữ nguyên nghiệp vụ "rời màn OTP thì không để
        // lại dấu vết".
        if (!string.IsNullOrWhiteSpace(sdtBn))
        {
            _cache.Set($"QR_MABN_{sdtBn}", maBnTraCuu, TimeSpan.FromMinutes(30));
        }

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
        // 🔴 KHÔNG Uri.EscapeDataString ở đây: Response.Cookies.Append ĐÃ tự URL-encode
        // giá trị. Bọc hai lớp thì JS chỉ gỡ được một (decodeURIComponent), JSON.parse
        // vỡ, qrCookieData = null => window.danhTinhQuet rỗng => POST XacNhanOtp không
        // mang DanhTinhQuet => không ai đặt claim HoSoDangChon => /benh-nhan rơi về hồ
        // sơ ĐẦU TIÊN. Đó đúng là triệu chứng "quét cả 3 QR đều vào một người".
        // Đường quét bằng camera (quet-qr.js) ghi cookie bằng encodeURIComponent MỘT
        // lớp — để một lớp ở đây là hai đường khớp nhau.
        Response.Cookies.Append("qr_data", qrJson, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = false,
            SameSite = SameSiteMode.Lax
        });

        // 🔴 mabn phải nằm trên URL, không được chỉ nằm trong cookie qr_data. Cookie đó
        // bị chính màn Login xóa ở 'pagehide'/'click rời trang' — trên Chrome Android
        // pagehide bắn cả khi chuyển app hay tắt màn hình, và tab có thể bị hệ điều hành
        // thu hồi rồi nạp lại. Lúc đó cookie mất, biến window.danhTinhQuet cũng mất theo
        // => POST XacNhanOtp không mang DanhTinhQuet => không ai đặt claim HoSoDangChon.
        // URL thì sống qua reload, nên đây là đường bền nhất để màn Login biết mã quét.
        return Redirect($"/DangNhap/Login?coSo={slug}&sdt={sdtBn}&cccd={cccd}&mabn={Uri.EscapeDataString(maBnTraCuu)}&hienOtp=1&tuQr=1&returnUrl=%2Fbenh-nhan");
    }

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

        if (!string.IsNullOrWhiteSpace(sdt))
        {
            _cache.Remove($"QR_MABN_{sdt.Trim()}");
        }

        Response.Cookies.Delete("qr_data", new CookieOptions { Path = "/" });

        // 🔴 Phải xóa CẢ qr_mabn. Nghiệp vụ: rời màn OTP mà chưa xác thực thì không
        // được để lại dấu vết của phiếu vừa quét. qr_mabn là HttpOnly nên JS không tự
        // xóa được — chỉ có cửa này dọn được nó.
        Response.Cookies.Delete("qr_mabn", new CookieOptions { Path = "/" });
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? coSo = null)
    {
        var adminReauth = AdminAuthentication.IsAdminReturnUrl(returnUrl) && Url.IsLocalUrl(returnUrl);

        // 🔴 App đã cài mở từ icon thì không ai gắn được ?coSo= vào đường dẫn: PWA vào
        // thẳng '/' rồi bị đẩy sang đây. Thiếu slug là màn đăng nhập tụt về logo/tên
        // HisSoft chung, người bệnh đang ở cửa Thiện Nam nhìn thấy thương hiệu khác.
        // Cookie này do chính màn đăng nhập ghi (Login.cshtml) nên chỉ là GỢI Ý khôi
        // phục — slug rác thì bỏ qua, KHÔNG về "/" như nhánh ?coSo= sai ở dưới.
        var laGoiYTuCookie = false;
        if (string.IsNullOrWhiteSpace(coSo))
        {
            var slugNho = Request.Cookies["pwa_co_so"];
            if (!string.IsNullOrWhiteSpace(slugNho))
            {
                coSo = slugNho.Trim();
                laGoiYTuCookie = true;
            }
        }

        // ?coSo=slug đến từ hai nút bên trang cơ sở. Đổ ra ViewBag để màn đăng
        // nhập hiện ô "Mã CSKCB" khóa cứng, và để JS gửi kèm khi gọi OTP.
        string? maCoSoTuUrl = null;
        long? idCoSoTuUrl = null;
        if (!string.IsNullOrWhiteSpace(coSo))
        {
            var thongTin = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == coSo);

            // Chặn ngay ở đây thay vì để bệnh nhân gõ hết OTP rồi mới bị từ chối
            // (và tốn một tin nhắn OTP vô ích). Entity đã nằm trong tay, không tốn
            // thêm truy vấn. Xem ADR 0013.
            if (thongTin is not null && !thongTin.HienThiCongKhai)
            {
                if (laGoiYTuCookie) { Response.Cookies.Delete("pwa_co_so", new CookieOptions { Path = "/" }); return Redirect("/DangNhap/Login"); }
                return Redirect("/");
            }

            // 🔴 ?coSo= chỉ nhận SLUG. Gõ vào một giá trị không tra ra cơ sở nào
            // (hay gặp nhất: gõ MÃ cơ sở, vd 77121) trước đây tụt lặng lẽ xuống chế
            // độ không-cơ-sở: vẫn cho gõ OTP, vẫn cấp cookie, rồi thả vào /benh-nhan
            // mà không tạo nổi tài khoản. Đã về "/" giống hệt nhánh HienThiCongKhai = 0 ngay
            // trên — sai cửa thì phải biết ngay, đừng sau khi gõ xong OTP. ADR 0027.
            if (thongTin is null)
            {
                // Slug từ cookie không còn tra ra cơ sở nào (đổi slug, cơ sở bị xóa):
                // dọn cookie rồi hiện màn đăng nhập chung, đừng ném người dùng về "/".
                if (laGoiYTuCookie) { Response.Cookies.Delete("pwa_co_so", new CookieOptions { Path = "/" }); return Redirect("/DangNhap/Login"); }
                return Redirect("/");
            }

            maCoSoTuUrl = thongTin?.MaCoSo;
            idCoSoTuUrl = thongTin?.Id;
            ViewBag.MaCoSo = thongTin?.MaCoSo;
            ViewBag.TenCoSo = thongTin?.TenCoSo;
            ViewBag.LogoCoSo = LayLogoCoSo(thongTin);
            ViewBag.SlugCoSo = coSo;
        }

        // Còn phiên thì hiện màn đăng nhập kèm một khối lựa chọn: đi tiếp bằng tài
        // khoản đang có, hoặc đăng nhập tài khoản khác.
        //
        // ⚠️ Khối này chỉ còn phục vụ CƠ SỞ NỘI BỘ. Với cơ sở dùng màn của đối tác,
        // ADR 0016 đã đổi luật: phiên còn sống và có dấu ấn thì ĐI THẲNG, không hỏi
        // — xem nhánh ở cuối hàm. Lý lẽ cũ ("không tự đẩy đi đâu cả vì SixosPwa
        // không biết bệnh nhân vừa đăng xuất bên đối tác hay chưa") vẫn đúng về
        // logic nhưng trả giá sai: nó bắt MỌI bệnh nhân gõ lại mật khẩu để phòng
        // một trường hợp hiếm, mà trường hợp hiếm đó đã có lối thoát riêng —
        // menu 3 gạch -> Đăng xuất.
        //
        // Đọc ra ngoài khối if: nhánh tự động ở cuối hàm cũng cần hai claim này.
        var cccdPhien = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        var cuaPhien = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        if (User.Identity?.IsAuthenticated == true && !adminReauth)
        {
            var maCoSoDich = maCoSoTuUrl ?? cuaPhien;

            if (!string.IsNullOrWhiteSpace(cccdPhien))
            {
                // Gõ thẳng URL/bookmark vào một cơ sở KHÁC cơ sở của phiên: không
                // tự dùng màn này nối hai thứ tự do lựa chọn nữa — đẩy về đúng
                // trang cơ sở đó để MỘT modal duy nhất bắt đăng nhập chéo cơ sở
                // hiện ra (ADR 0006), không lệch thông điệp giữa hai lối vào.
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

        // ── ĐIỂM RẼ: cơ sở dùng màn của khách ─────────────────────────────
        // 🔴 Cơ sở có KetNoi_UrlChuyenHuong (và KetNoi_Active) thì việc đăng nhập
        // / đăng ký do CHÍNH HỌ làm — SixosPwa không hỏi OTP, không dựng tài khoản.
        // Đưa thẳng sang trang của họ.
        //
        // Đặt ĐÚNG Ở ĐÂY, sau mọi guard, theo ADR 0014: không được phép vượt mặt
        // chặn cơ sở ẩn (ADR 0013) hay chặn đăng nhập chéo cơ sở (ADR 0006).
        //
        // 🔴 Vì sao nhánh này phải tồn tại: đợt A xoá bộ màn UB dựng-lại-trong-cổng
        // (UbLogin/UbDangKy/UbQuenMatKhau) VÀ xoá luôn điểm rẽ cũ
        // `if (cuaDoiTac?.DungManDoiTac == true) return View("UbLogin")`, định thay
        // bằng CuaCoSoService — nhưng nhánh thay thế KHÔNG được viết. Hệ quả: bệnh
        // nhân bấm Đăng nhập ở cơ sở UB lại thấy màn đăng nhập của SixosPwa, một
        // màn không dùng được cho họ. Phiên ĐÃ đăng nhập thì không dính, vì
        // ChonDichDenAsync vẫn trả URL đối tác — nên lỗi chỉ lộ ở người CHƯA đăng nhập.
        //
        // adminReauth đi đường riêng: quản trị viên xác thực lại thì phải ở lại cổng.
        if (!adminReauth && idCoSoTuUrl is > 0)
        {
            var cua = await _cuaCoSo.LayCuaAsync(idCoSoTuUrl.Value);
            if (cua?.UrlChuyenHuong is { Length: > 0 } urlKhach)
            {
                return Redirect(urlKhach);
            }
        }

        ViewData["AdminReauth"] = adminReauth;
        ViewData["ReturnUrl"] = adminReauth ? returnUrl : null;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GuiOtp([FromBody] GuiOtpRequest model)
    {
        var input = model.SoDienThoai?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input) && model.DanhTinhQuet != null)
        {
            // TÊN TÀI KHOẢN chỉ được lấy từ SỐ ĐIỆN THOẠI, hoặc từ MaBN khi quét QR
            // PHIẾU KHÁM HIS. KHÔNG lấy số CCCD: làm vậy là đẻ ra tài khoản mang số
            // căn cước, còn bệnh nhân thì mất đường nhập số điện thoại thật. Hàng rào
            // này phải đứng ở CẢ server, vì gọi thẳng API là vượt mặt trình duyệt.
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
                // 🔴 Đợt 1B: bệnh nhân KHÔNG CÒN tài khoản, nên không còn đường
                // nào đi từ CCCD sang HT_TaiKhoan. Bảng đó chỉ còn Admin.
                // "Có lối vào hay không" được hỏi ở hàng rào C7b bên dưới.
                taiKhoan = null;
            }
        }

        bool laQrHis = model.DanhTinhQuet != null && (model.DanhTinhQuet.LaNguonHis || !string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN));

        // 🔴 Cùng luật thứ tự với hai cửa kia: MaCoSo phải được chốt TRƯỚC khi hỏi
        // CoLoiVaoAsync. GuiOtp không có khối điền mặc định như XacNhanOtp, nên
        // phiên mở /DangNhap/Login không kèm ?coSo= sẽ có MaCoSo rỗng => bị chặn
        // ngay ở bước gửi OTP, trong khi XacNhanOtp thì lại cho qua. Hai cửa nói
        // hai kiểu chặn cùng một người là lỗi khó lần nhất.
        if (string.IsNullOrWhiteSpace(model.MaCoSo))
        {
            if (laQrHis && !string.IsNullOrWhiteSpace(model.DanhTinhQuet?.MaBN))
            {
                var coSoMa = await (from cs in _dbContext.BenhNhans.AsNoTracking()
                                    join kcb in _dbContext.DMCSKCBs.AsNoTracking() on cs.IdCoSo equals (long?)kcb.Id
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
                    .Where(x => x.HienThiCongKhai)
                    .OrderBy(x => x.Id)
                    .Select(x => x.MaCoSo)
                    .FirstOrDefaultAsync();
            }
        }

        // 🔴 C7b — CỬA 3, ở bước GỬI OTP. PLAN §7.1 KHÔNG LIỆT KÊ CỬA NÀY (nó chỉ
        // nêu hai cửa :476 và :708). Bỏ sót thì bệnh nhân chết ngay từ bước gửi
        // OTP, trước khi chạm tới hai cửa kia: sau đợt 1B `taiKhoan` LUÔN null
        // với bệnh nhân (HT_TaiKhoan chỉ còn Admin) nên điều kiện cũ chặn sạch.
        // Bắt được lúc chạy nghiệm thu thật 19-09 — không phép nào khác thay.
        //
        // Admin vẫn phải đi đường `taiKhoan != null` ngay dưới (nhánh mật khẩu),
        // nên ở đây chỉ mở cho ai CÓ LỐI VÀO, và vẫn giữ ngoại lệ quét QR HIS.
        var coLoiVao = taiKhoan != null
                       || await _hoSo.CoLoiVaoAsync(input, model.MaCoSo);

        if (!coLoiVao && !laQrHis)
        {
            return Json(new {
                success = false,
                message = "Số điện thoại chưa có hồ sơ tại cơ sở y tế. Vui lòng liên hệ phòng khám/bệnh viện để được đăng ký."
            });
        }

        if (taiKhoan != null && (string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return Json(new {
                success = true,
                isPassword = true,
                message = "Vui lòng nhập mật khẩu của bạn để đăng nhập."
            });
        }

        var otpCode = "123456";

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
            // TÊN TÀI KHOẢN chỉ được lấy từ SỐ ĐIỆN THOẠI, hoặc từ MaBN khi quét QR
            // PHIẾU KHÁM HIS. KHÔNG lấy số CCCD: làm vậy là đẻ ra tài khoản mang số
            // căn cước, còn bệnh nhân thì mất đường nhập số điện thoại thật. Hàng rào
            // này phải đứng ở CẢ server, vì gọi thẳng API là vượt mặt trình duyệt.
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

        // Cơ sở đang ẩn (DM_CSKCB.HienThiCongKhai = 0) thì không sinh phiên MỚI tại cơ sở đó.
        // Vế !adminReauth là BẮT BUỘC: action này phục vụ cả re-auth Admin/DoiTac,
        // thiếu nó là khóa luôn đường đăng nhập quản trị. Xem ADR 0013.
        if (!adminReauth && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && !await _luong.CoSoDangHienThiAsync(model.MaCoSo))
        {
            return Json(new { success = false, message = "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến." });
        }

        var taiKhoan = await _taiKhoanService.DangNhapAsync(input, "");

        if (taiKhoan != null && (string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase)))
        {
            // Cột này là MatKhauNoiBo (ADR 0009). Sau migration nó đang NULL vì phần
            // BĂM chưa thi hành — xem mục Đính chính của ADR 0009 — nên hai tài khoản
            // Admin/Đối tác tạm thời rơi vào nhánh "mật khẩu không chính xác".
            if (string.IsNullOrEmpty(taiKhoan.MatKhauNoiBo)
                || !string.Equals(taiKhoan.MatKhauNoiBo, otpInput, StringComparison.Ordinal))
            {
                return Json(new { success = false, message = "Mật khẩu không chính xác!" });
            }
        }
        else
        {
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

        // 🔴 Bất biến (ADR 0027): đăng nhập được thì phải có tài khoản. HT_TaiKhoan
        // chỉ được tạo trong BaoDamHoSoNoiBoAsync, mà hàm đó thoát ngay khi không có
        // cơ sở => cấp cookie ở đây là đẻ ra một phiên KHÔNG CÓ TÀI KHOẢN, không lối
        // thoát, mọi màn phía sau chỉ biết nói "Không tìm thấy tài khoản.".
        // Ranh giới là VÀ, không phải HOẶC: người ĐÃ CÓ tài khoản vẫn vào được từ
        // /DangNhap/Login trần (LoginPath của Program.cs đẩy về đây), không bị chặn.
        // (Họ vẫn hạ cánh ở /benh-nhan chứ không đứng đầu trang, vì ChonDichDenAsync
        //  bỏ returnUrl khi thiếu mã cơ sở — hành vi CÓ SẴN, không phải do cổng chặn
        //  này. Đo thật 10/09.) Re-auth Admin được miễn trừ.
        bool laQrHis = model.DanhTinhQuet != null && (model.DanhTinhQuet.LaNguonHis || !string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN));

        // Khóa luồng tự đăng ký tài khoản: chỉ cho phép tài khoản đã có sẵn từ HIS
        // NGOẠI LỆ: Bệnh nhân đã khám quét QR phiếu khám HIS -> tự động tạo tài khoản và hồ sơ
        // 🔴 THỨ TỰ BẮT BUỘC: khối điền MaCoSo phải chạy TRƯỚC chốt C7b.
        // Chốt cũ ("taiKhoan is null") không dùng tới MaCoSo nên đặt ở đâu cũng
        // được; chốt C7b thì CÓ — để nguyên thứ tự cũ là phiên không mang MaCoSo
        // bị chặn sạch, kể cả người CÓ hồ sơ. Đã đạp thật lúc chạy nghiệm thu 19-09.
        if (string.IsNullOrWhiteSpace(model.MaCoSo) && !string.IsNullOrWhiteSpace(model.DanhTinhQuet?.MaBN))
        {
            var coSoMa = await (from cs in _dbContext.BenhNhans.AsNoTracking()
                                join kcb in _dbContext.DMCSKCBs.AsNoTracking() on cs.IdCoSo equals (long?)kcb.Id
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
                .Where(x => x.HienThiCongKhai)
                .OrderBy(x => x.Id)
                .Select(x => x.MaCoSo)
                .FirstOrDefaultAsync();
        }

        // 🔴 C7b — CỬA 1 trong HAI cửa. Cửa kia ở nhánh Firebase (tìm
        // "C7b - CỬA 2"). Sót một cửa là mở đường lách (ADR 0027 đã cảnh báo
        // đúng chuyện này). Từ đợt 1B hàng rào không còn treo vào HT_TaiKhoan
        // mà hỏi thẳng: SỐ NÀY CÓ HỒ SƠ TẠI CƠ SỞ NÀY KHÔNG (ADR 0040).
        // Lỗi báo hiện tại nói "chưa có hồ sơ tại cơ sở y tế" — trước 1B lỗi đó
        // CHẶT HƠN code, nay mới đúng nghĩa đen, nên giữ nguyên chữ.
        if (!adminReauth && !await _hoSo.CoLoiVaoAsync(input, model.MaCoSo))
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


        if (string.IsNullOrWhiteSpace(model.Cccd))
        {
            if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.Cccd))
            {
                model.Cccd = model.DanhTinhQuet.Cccd.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.MaBN))
            {
                // 🔴 MaBN chỉ duy nhất THEO TỪNG CƠ SỞ (ApplicationDbContext: unique
                // (IdCoSo, MaBN)), nên tra không lọc cơ sở là đọc nhầm hồ sơ của cơ
                // sở khác — xem TimHoSoTheoMaBnTaiCoSoAsync.
                var idHoSoTheoMa = await TimHoSoTheoMaBnTaiCoSoAsync(model.DanhTinhQuet.MaBN, model.MaCoSo);
                model.Cccd = idHoSoTheoMa is null
                    ? null
                    : await _dbContext.BenhNhans.AsNoTracking()
                        .Where(bn => bn.Id == idHoSoTheoMa.Value)
                        .Select(bn => bn.CCCD)
                        .FirstOrDefaultAsync();
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

            // Hai claim này là cách các màn phía sau (Đăng ký / Liên kết / Bàn
            // giao / trang bệnh nhân) biết bệnh nhân là ai và đang ở cơ sở nào.
            if (!string.IsNullOrWhiteSpace(model.Cccd))
            {
                claims.Add(new Claim(LuongCongBenhNhan.ClaimCccd, model.Cccd.Trim()));
            }
            if (!string.IsNullOrWhiteSpace(model.MaCoSo))
            {
                claims.Add(new Claim(LuongCongBenhNhan.ClaimMaCoSo, model.MaCoSo.Trim()));
            }

            // Tự động gắn claim HoSoDangChon khi người dùng quét mã QR phiếu khám HIS
            // 🔴 Chỉ mở sẵn hồ sơ khi tài khoản có ĐÚNG MỘT hồ sơ. Tài khoản nhiều hồ sơ
        // (mẹ + 2 con) mà lấy bừa một cái là bug thầm lặng — để null thì người dùng
        // tự chọn ở màn Hồ sơ. Nhánh QR dưới đây vẫn ghi đè bằng hồ sơ quét được.
        long? idHoSoQuet = await HoSoDuyNhatCuaTaiKhoanAsync(input, model.MaCoSo);
            if (model.DanhTinhQuet != null)
            {
                var maBnQuet = model.DanhTinhQuet.MaBN?.Trim();
                var cccdQuet = model.DanhTinhQuet.Cccd?.Trim();
                if (!string.IsNullOrWhiteSpace(maBnQuet))
                {
                    var idBn = await TimHoSoTheoMaBnTaiCoSoAsync(maBnQuet, model.MaCoSo);
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

                // CHỈ luồng QUÉT PHIẾU: MỘT_HỒ_SƠ bắt thì mở sẵn đúng hồ sơ mà màn
                // *Hồ sơ của tôi* sẽ hiện, rồi vào thẳng /benh-nhan — người vừa quét
                // phiếu không phải chọn lại một danh sách chỉ có một dòng.
                idHoSoQuet ??= await _hoSo.LayIdHoSoMoSanKhiQuetAsync(input, model.MaCoSo, cccdQuet);
            }

            idHoSoQuet ??= await HoSoTheoMaQuetDaNhoAsync(model.MaCoSo, input);

            if (idHoSoQuet != null && idHoSoQuet > 0)
            {
                // 🔴 Cửa 3 "nhận chủ sở hữu" đã chết ở đợt 1B: cột
                // DM_BenhNhan.IDTaiKhoan không còn, chủ sở hữu này là cặp
                // (SDT x cơ sở) của chính dòng đó. Xem ADR 0040.

                var csList = await _dbContext.BenhNhans
                    .Where(x => x.Id == idHoSoQuet.Value && !x.DaMoTaiLieu)
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

            // Cây quyết định sau OTP: chỉ một chỗ duy nhất, nằm trong
            // ILuongCongBenhNhan. Màn hình không được tự kiểm tra MaCoSo.
            var redirectUrl = adminReauth
                ? model.ReturnUrl
                : await ChonDichDenAsync(model.MaCoSo, model.Cccd, input, model.ReturnUrl, model.DanhTinhQuet);

            Response.Cookies.Delete("qr_data", new CookieOptions { Path = "/" });
            Response.Cookies.Delete("qr_mabn", new CookieOptions { Path = "/" });
            if (!string.IsNullOrWhiteSpace(input)) _cache.Remove($"QR_MABN_{input.Trim()}");

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

        // Cùng lý lẽ với XacNhanOtp: đây là cửa thứ hai (và cuối cùng) sinh phiên mới.
        if (!adminReauth && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && !await _luong.CoSoDangHienThiAsync(model.MaCoSo))
        {
            return Json(new { success = false, message = "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến." });
        }

        bool laQrHis = model.DanhTinhQuet != null && (model.DanhTinhQuet.LaNguonHis || !string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN));

        // 🔴 THỨ TỰ BẮT BUỘC: khối điền MaCoSo phải chạy TRƯỚC chốt C7b.
        // Chốt cũ ("taiKhoan is null") không dùng tới MaCoSo nên đặt ở đâu cũng
        // được; chốt C7b thì CÓ — để nguyên thứ tự cũ là phiên không mang MaCoSo
        // bị chặn sạch, kể cả người CÓ hồ sơ. Đã đạp thật lúc chạy nghiệm thu 19-09.
        if (string.IsNullOrWhiteSpace(model.MaCoSo) && !string.IsNullOrWhiteSpace(model.DanhTinhQuet?.MaBN))
        {
            var coSoMa = await (from cs in _dbContext.BenhNhans.AsNoTracking()
                                join kcb in _dbContext.DMCSKCBs.AsNoTracking() on cs.IdCoSo equals (long?)kcb.Id
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
                .Where(x => x.HienThiCongKhai)
                .OrderBy(x => x.Id)
                .Select(x => x.MaCoSo)
                .FirstOrDefaultAsync();
        }

        // 🔴 C7b — CỬA 2 trong HAI cửa (cửa kia ở nhánh OTP phía trên).
        // Sót một cửa là mở đường lách. Cùng luật, cùng lối báo.
        if (!adminReauth && !await _hoSo.CoLoiVaoAsync(sdt, model.MaCoSo))
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


        if (string.IsNullOrWhiteSpace(model.Cccd))
        {
            if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.Cccd))
            {
                model.Cccd = model.DanhTinhQuet.Cccd.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(model.DanhTinhQuet?.MaBN))
            {
                // 🔴 MaBN chỉ duy nhất THEO TỪNG CƠ SỞ (ApplicationDbContext: unique
                // (IdCoSo, MaBN)), nên tra không lọc cơ sở là đọc nhầm hồ sơ của cơ
                // sở khác — xem TimHoSoTheoMaBnTaiCoSoAsync.
                var idHoSoTheoMa = await TimHoSoTheoMaBnTaiCoSoAsync(model.DanhTinhQuet.MaBN, model.MaCoSo);
                model.Cccd = idHoSoTheoMa is null
                    ? null
                    : await _dbContext.BenhNhans.AsNoTracking()
                        .Where(bn => bn.Id == idHoSoTheoMa.Value)
                        .Select(bn => bn.CCCD)
                        .FirstOrDefaultAsync();
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
        // 🔴 Chỉ mở sẵn hồ sơ khi tài khoản có ĐÚNG MỘT hồ sơ. Tài khoản nhiều hồ sơ
        // (mẹ + 2 con) mà lấy bừa một cái là bug thầm lặng — để null thì người dùng
        // tự chọn ở màn Hồ sơ. Nhánh QR dưới đây vẫn ghi đè bằng hồ sơ quét được.
        long? idHoSoQuet = await HoSoDuyNhatCuaTaiKhoanAsync(sdt, model.MaCoSo);
        if (model.DanhTinhQuet != null)
        {
            var maBnQuet = model.DanhTinhQuet.MaBN?.Trim();
            var cccdQuet = model.DanhTinhQuet.Cccd?.Trim();
            if (!string.IsNullOrWhiteSpace(maBnQuet))
            {
                var idBn = await TimHoSoTheoMaBnTaiCoSoAsync(maBnQuet, model.MaCoSo);
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

            // CHỈ luồng QUÉT PHIẾU: MỘT_HỒ_SƠ bắt thì mở sẵn đúng hồ sơ mà màn
            // *Hồ sơ của tôi* sẽ hiện, rồi vào thẳng /benh-nhan — người vừa quét
            // phiếu không phải chọn lại một danh sách chỉ có một dòng.
            idHoSoQuet ??= await _hoSo.LayIdHoSoMoSanKhiQuetAsync(sdt, model.MaCoSo, cccdQuet);
        }

        idHoSoQuet ??= await HoSoTheoMaQuetDaNhoAsync(model.MaCoSo, sdt);

        if (idHoSoQuet != null && idHoSoQuet > 0)
        {
            if (taiKhoan != null && taiKhoan.Id > 0)
            {
                // Cửa 3 đã thành no-op từ đợt 1B (ADR 0040).
            }

            var csList = await _dbContext.BenhNhans
                .Where(x => x.Id == idHoSoQuet.Value && !x.DaMoTaiLieu)
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

        var dichDen = adminReauth
            ? model.ReturnUrl
            : await ChonDichDenAsync(model.MaCoSo, model.Cccd, sdt, model.ReturnUrl, model.DanhTinhQuet);

        Response.Cookies.Delete("qr_data", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("qr_mabn", new CookieOptions { Path = "/" });
        if (!string.IsNullOrWhiteSpace(sdt)) _cache.Remove($"QR_MABN_{sdt.Trim()}");

        return Json(new { success = true, redirectUrl = dichDen });
    }

    // ==================================================================
    //  Cổng bệnh nhân: Đăng ký / Liên kết / Đổi mật khẩu / Bàn giao
    //  Bốn màn này chỉ đến từ cây quyết định trong ILuongCongBenhNhan.
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

        // Co so co URL chuyen huong sang he doi tac thi mat khau do BEN DO dat,
        // man Dang ky khong hoi nua. Co so KHONG co URL chuyen huong la co so noi bo:
        // benh nhan tu dat mat khau nen VAN phai kiem du dai toi thieu 6 ky tu.
        var idCoSo = await _dbContext.DMCSKCBs.AsNoTracking()
            .Where(x => x.MaCoSo == maCoSo).Select(x => (long?)x.Id).FirstOrDefaultAsync();
        var coChuyenHuong = idCoSo is not null && await _cuaCoSo.CoChuyenHuongAsync(idCoSo.Value);
        if (!coChuyenHuong && (string.IsNullOrWhiteSpace(model.MatKhau) || model.MatKhau.Length < 6))
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
    /// Người dùng chủ động bấm "Tiếp tục" ở màn đăng nhập khi phiên vẫn còn, hoặc
    /// bấm "Hồ sơ bệnh nhân" ở menu 3 gạch khi đã đăng nhập. Đây mới là chỗ chạy
    /// cây quyết định — KHÔNG tự chạy khi chỉ mở trang. Chỉ thao tác trên MaCoSo
    /// CỦA PHIÊN — đổi sang cơ sở khác không còn đi qua đây nữa, modal chặn đăng
    /// nhập chéo cơ sở (ADR 0006) đã lo từ lúc vào, nên không còn nhánh "đổi claim
    /// âm thầm" ở đây.
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

        var dichDen = await _luong.ChonDichDenAsync(maCoSo, cccd, dinhDanh, returnUrl);
        return Redirect(dichDen);
    }

    /// <summary>
    /// Danh tính bệnh nhân LẤY TỪ PHIÊN. Mọi thao tác chạm tới hệ đối tác đều
    /// phải đi qua đây — không bao giờ tin cccd/maCoSo gửi lên từ trình duyệt.
    /// </summary>
    private (string? MaCoSo, string? Cccd, string? DinhDanh) LayDanhTinhPhien()
        => (User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value,
            User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value,
            User.FindFirst(ClaimTypes.Name)?.Value);

    /// <summary>
    /// Mã cơ sở của phiên. Tham số trên URL chỉ được dùng khi phiên chưa có —
    /// và phải là mã cơ sở hợp lệ, để không ai đổi URL để nhảy sang cơ sở khác.
    /// </summary>
    private string? LayMaCoSoPhien(string? coSoTrenUrl)
    {
        var cuaPhien = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        if (!string.IsNullOrWhiteSpace(cuaPhien)) return cuaPhien;

        return string.IsNullOrWhiteSpace(coSoTrenUrl) ? null : coSoTrenUrl;
    }

    /// <summary>
    /// Logo của cơ sở để bộ màn đối tác hiện đúng nhận diện của họ, thay vì logo
    /// SixosPwa. Cột DM_CSKCB.Logo giữ hoặc URL tuyệt đối, hoặc đường dẫn nội bộ
    /// "/anh/logo_cs/x.jpg" (KhoAnh sinh ra, AnhController phục vụ) — cả hai dạng
    /// đều gắn thẳng vào src được.
    ///
    /// UnescapeDataString bám theo Views/Home/ChiTietCoSo.cshtml:7: URL trong cột
    /// này có thể đã bị mã hóa một lần trước khi lưu.
    ///
    /// Trả null khi cơ sở chưa có logo — view tự rơi về ảnh mặc định.
    /// </summary>
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
    }

    private async Task<TaiKhoan> TaoTaiKhoanVaHoSoTuQrAsync(string sdt, string? maCoSo, string? cccdInput, DanhTinhQuet quet)
    {
        var cccd = !string.IsNullOrWhiteSpace(quet.Cccd) ? quet.Cccd.Trim() : (cccdInput?.Trim() ?? string.Empty);
        var hoTen = !string.IsNullOrWhiteSpace(quet.HoTen) ? quet.HoTen.Trim() : sdt;
        var ngaySinh = quet.DoiNgay();
        var gioiTinh = quet.DoiGioiTinh();
        var diaChi = string.IsNullOrWhiteSpace(quet.DiaChi) ? null : quet.DiaChi.Trim();
        var slug = Services.ChuanHoaTen.BoDau(hoTen);

        long? idCoSo = null;
        long? existingBnId = null;

        var idCoSoQuet = string.IsNullOrWhiteSpace(maCoSo)
            ? (long?)null
            : await _dbContext.DMCSKCBs.AsNoTracking()
                .Where(x => x.MaCoSo == maCoSo || x.Slug == maCoSo)
                .Select(x => (long?)x.Id).FirstOrDefaultAsync();

        if (idCoSoQuet != null)
        {
            idCoSo = idCoSoQuet;

            if (!string.IsNullOrWhiteSpace(quet.MaBN))
            {
                var coSoInfo = await (from cs in _dbContext.BenhNhans
                                      where cs.MaBN == quet.MaBN && cs.IdCoSo == idCoSoQuet.Value
                                      select (long?)cs.Id).FirstOrDefaultAsync();
                if (coSoInfo != null)
                {
                    existingBnId = coSoInfo;
                }
            }

            if (existingBnId == null && !string.IsNullOrWhiteSpace(cccd))
            {
                existingBnId = await _dbContext.BenhNhans.AsNoTracking()
                    .Where(cs => cs.IdCoSo == idCoSoQuet.Value && cs.CCCD == cccd)
                    .Select(cs => (long?)cs.Id).FirstOrDefaultAsync();
            }

        }

        if (idCoSo == null)
        {
            idCoSo = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .Where(x => x.HienThiCongKhai)
                .OrderBy(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync();
        }

        long idBenhNhanTarget = existingBnId ?? 0;
        if (idBenhNhanTarget <= 0)
        {
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
                var (_, idMoi) = await _thuTuc.TaoHoSoTuKhaiAsync(idBenhNhanTarget, idCoSo.Value);
                if (idMoi > 0)
                {
                    idBenhNhanTarget = idMoi;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(quet.MaBN) && idCoSo != null && idBenhNhanTarget > 0)
        {
            var maTrim = quet.MaBN.Trim();
            var daCoMa = await _dbContext.BenhNhans
                .AsNoTracking()
                .AnyAsync(b => b.IdCoSo == idCoSo.Value && b.MaBN == maTrim && b.Id != idBenhNhanTarget);
            if (!daCoMa)
            {
                var bnRecord = await _dbContext.BenhNhans.FirstOrDefaultAsync(b => b.Id == idBenhNhanTarget);
                if (bnRecord != null && string.IsNullOrWhiteSpace(bnRecord.MaBN))
                {
                    bnRecord.MaBN = maTrim;
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        var luuTaiKhoan = await _thuTuc.SaveTaiKhoanAsync(
            0,
            sdt,
            null,
            "BenhNhan",
            null);

        var idTaiKhoan = luuTaiKhoan.Id;

        if (idBenhNhanTarget > 0 && idTaiKhoan > 0)
        {
            // Cửa 3 đã thành no-op từ đợt 1B (ADR 0040).
        }

        if (idBenhNhanTarget > 0)
        {
            var csList = await _dbContext.BenhNhans
                .Where(x => x.Id == idBenhNhanTarget && !x.DaMoTaiLieu)
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
                       ?? new TaiKhoan { Id = idTaiKhoan, SDT = sdt, Role = "BenhNhan" };

        return taiKhoan;
    }

    /// <summary>
    /// Chỗ hạ cánh sau khi xác thực. Vào thẳng /DangNhap/Login (không qua trang
    /// cơ sở) thì không biết bệnh nhân ở cơ sở nào, nên chỉ về được trang bệnh
    /// nhân dạng tối giản — muốn đi tiếp phải vào lại qua /DangKyOnline/{slug}.
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

    /// <summary>
    /// denCoSo (slug): dùng khi bấm "Đăng xuất và đăng nhập lại" trong modal chặn
    /// đăng nhập chéo cơ sở (ADR 0006) — sau khi thoát phiên cũ, đưa thẳng tới màn
    /// đăng nhập của cơ sở MỚI kèm returnUrl là ý định của nút bệnh nhân đã bấm,
    /// thay vì về lại cơ sở cũ như đăng xuất bình thường.
    /// </summary>
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> DangXuat(string? denCoSo = null, string? returnUrl = null)
    {
        // Đọc mã cơ sở TRƯỚC khi đăng xuất, vì sau SignOut là mất sạch claim.
        // Đăng xuất khỏi cổng bệnh nhân của một cơ sở thì phải quay về đúng
        // trang cơ sở đó — về /DangNhap/Login trần thì lần đăng nhập sau không
        // còn mang theo mã cơ sở, và bệnh nhân rơi thẳng vào nhánh nội bộ.
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        var slug = string.IsNullOrWhiteSpace(maCoSo)
            ? null
            : await _dbContext.DMCSKCBs
                .AsNoTracking()
                .Where(x => x.MaCoSo == maCoSo)
                .Select(x => x.Slug)
                .FirstOrDefaultAsync();

        // 🔴 Dọn dấu vết phiếu vừa quét TRƯỚC khi mất claim. Thiếu bước này thì đăng
        // xuất rồi đăng nhập lại bằng số điện thoại (không quét gì cả) vẫn bị mở sẵn
        // hồ sơ của lần quét trước — vừa sai ý người dùng, vừa là dấu vết còn sót lại
        // của một phiên đã kết thúc.
        var sdtDangXuat = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value
                          ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(sdtDangXuat))
        {
            _cache.Remove($"QR_MABN_{sdtDangXuat.Trim()}");
        }
        Response.Cookies.Delete("qr_mabn", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("qr_data", new CookieOptions { Path = "/" });

        await HttpContext.SignOutAsync(AdminAuthentication.Scheme);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // 🔴 'vuaDangXuat=1' là tín hiệu cho TRÌNH DUYỆT tự dọn localStorage
        // 'pwa_patient_cache'. Số điện thoại + CCCD của người vừa dùng nằm ở đó, server
        // không với tới được; không dọn thì đăng xuất xong màn đăng nhập vẫn nhận ra số
        // cũ và nhảy thẳng vào ô OTP của chính người đó — đăng xuất như không.
        if (!string.IsNullOrWhiteSpace(denCoSo))
        {
            var yDinh = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
            return Redirect($"/DangNhap/Login?coSo={denCoSo}&vuaDangXuat=1&returnUrl={Uri.EscapeDataString(yDinh)}");
        }

        if (!string.IsNullOrWhiteSpace(slug))
        {
            return Redirect($"/DangKyOnline/{slug}?vuaDangXuat=1");
        }

        return Redirect("/DangNhap/Login?vuaDangXuat=1");
    }

    /// <summary>
    /// Tra hồ sơ (<c>DM_BenhNhan.ID</c>) theo mã bệnh nhân quét được trên phiếu khám,
    /// <b>giới hạn trong đúng cơ sở đang đăng nhập</b>.
    ///
    /// 🔴 <c>MaBN</c> chỉ duy nhất THEO TỪNG CƠ SỞ — <c>ApplicationDbContext</c> đặt
    /// <c>HasIndex(IdCoSo, MaBN).IsUnique()</c>. Tra mà không lọc cơ sở thì bệnh nhân
    /// đang ở cơ sở B quét phiếu mang mã do cơ sở A cấp sẽ lấy ra hồ sơ của NGƯỜI LẠ,
    /// và đường <c>NhanChuSoHuuAsync</c> phía sau sẽ gán hồ sơ đó cho tài khoản đang quét.
    ///
    /// Không biết cơ sở nào (thiếu <paramref name="maCoSo"/>) thì trả <c>null</c> —
    /// hỏng theo hướng an toàn, thua hơn là đoán.
    /// </summary>
    private async Task<long?> TimHoSoTheoMaBnTaiCoSoAsync(string? maBn, string? maCoSo)
    {
        if (string.IsNullOrWhiteSpace(maBn) || string.IsNullOrWhiteSpace(maCoSo)) return null;

        var ma = maBn.Trim();
        var maCs = maCoSo.Trim();

        return await (from cs in _dbContext.BenhNhans.AsNoTracking()
                      join co in _dbContext.DMCSKCBs.AsNoTracking() on cs.IdCoSo equals (long?)co.Id
                      where cs.MaBN == ma && (co.MaCoSo == maCs || co.Slug == maCs)
                      select (long?)cs.Id).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Mã bệnh nhân của phiếu vừa quét, khi màn Login không mang được danh tính quét
    /// tới cửa xác thực.
    ///
    /// <para>
    /// 🔴 Vì sao phải có: đo thật trên máy thật cho thấy query string rụng sau vài lần
    /// chuyển trang (Login?...&mabn=... -> Login?ReturnUrl=%2Fbenh-nhan), còn cookie thì
    /// không sống qua được ranh giới TRÌNH DUYỆT <-> APP ĐÃ CÀI (hai kho cookie riêng:
    /// quét QR ở Chrome rồi bấm Đăng nhập trong app là mất sạch).
    /// </para>
    /// <para>
    /// Nên bám nhớ theo SỐ ĐIỆN THOẠI (cache server) đi trước, cookie chỉ là đường lùi.
    /// Vòng đời 30 phút, giống mã OTP. Bị dọn ở: HuyOtp (rời màn OTP), đăng nhập xong,
    /// và DangXuat — để không còn dấu vết của phiên đã kết thúc.
    /// </para>
    /// </summary>
    private async Task<long?> HoSoTheoMaQuetDaNhoAsync(string? maCoSo, string? sdt)
    {
        // Ưu tiên bám nhớ theo SDT: nó sống được cả khi người bệnh quét ở trình duyệt
        // rồi bấm Đăng nhập trong app đã cài (hai ngữ cảnh cookie khác nhau).
        if (!string.IsNullOrWhiteSpace(sdt)
            && _cache.TryGetValue($"QR_MABN_{sdt.Trim()}", out string? maTuCache)
            && !string.IsNullOrWhiteSpace(maTuCache))
        {
            var id = await TimHoSoTheoMaBnTaiCoSoAsync(maTuCache.Trim(), maCoSo);
            if (id != null && id > 0) return id;
        }

        var maBn = Request.Cookies["qr_mabn"];
        if (string.IsNullOrWhiteSpace(maBn)) return null;

        return await TimHoSoTheoMaBnTaiCoSoAsync(maBn.Trim(), maCoSo);
    }

    /// <summary>
    /// Hồ sơ DUY NHẤT của một số điện thoại TẠI MỘT CƠ SỞ, hoặc <c>null</c> khi
    /// không có hồ sơ nào HOẶC có từ hai hồ sơ trở lên.
    ///
    /// 🔴 Từ đợt 1B quan hệ là (SDT x cơ sở) -> N hồ sơ: một số giữ được nhiều hồ
    /// sơ (mẹ + các con) tại cùng cơ sở, nên "hồ sơ đang chọn" phải do người dùng
    /// chọn và nằm ở claim/phiên — KHÔNG được lấy <c>FirstOrDefault</c> bất kỳ.
    /// Xem ADR 0040 và mục *Lối vào* trong CONTEXT.md.
    /// </summary>
    private async Task<long?> HoSoDuyNhatCuaTaiKhoanAsync(string? sdt, string? maCoSo)
    {
        if (string.IsNullOrWhiteSpace(sdt) || string.IsNullOrWhiteSpace(maCoSo)) return null;

        var idCoSo = await _dbContext.DMCSKCBs.AsNoTracking()
            .Where(x => x.MaCoSo == maCoSo)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync();

        if (idCoSo is null) return null;

        var ids = await _dbContext.BenhNhans.AsNoTracking()
            .Where(b => b.SDT == sdt && b.IdCoSo == idCoSo.Value)
            .Select(b => b.Id)
            .Take(2)
            .ToListAsync();

        return ids.Count == 1 ? ids[0] : null;
    }
}

public class GuiOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;

    /// <summary>Mã cơ sở bệnh nhân đang dùng (từ ?coSo=slug bên trang cơ sở).</summary>
    public string? MaCoSo { get; set; }

    /// <summary>Khóa nối bệnh nhân sang hệ đối tác (V6).</summary>
    public string? Cccd { get; set; }

    public string? ReturnUrl { get; set; }
    /// <summary>
    /// Danh tính đọc được từ mã QR ở màn Đăng nhập, nếu bệnh nhân đi đường đó.
    /// 🔴 Dữ liệu thô từ máy khách — xem cảnh báo trong <see cref="DanhTinhQuet"/>.
    /// </summary>
    public DanhTinhQuet? DanhTinhQuet { get; set; }

}

public class XacNhanOtpRequest
{
    public string SoDienThoai { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;

    /// <summary>Mã cơ sở bệnh nhân đang dùng (từ ?coSo=slug bên trang cơ sở).</summary>
    public string? MaCoSo { get; set; }

    /// <summary>Khóa nối bệnh nhân sang hệ đối tác (V6).</summary>
    public string? Cccd { get; set; }

    public string? ReturnUrl { get; set; }
    /// <summary>
    /// Danh tính đọc được từ mã QR ở màn Đăng nhập, nếu bệnh nhân đi đường đó.
    /// 🔴 Dữ liệu thô từ máy khách — xem cảnh báo trong <see cref="DanhTinhQuet"/>.
    /// </summary>
    public DanhTinhQuet? DanhTinhQuet { get; set; }

}

public class TaoTaiKhoanRequest
{
    // Không nhận MaCoSo / Cccd / DinhDanh: chúng được lấy từ claim của phiên.
    public string HoTen { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;

    /// <summary>Ý định của nút bệnh nhân đã bấm lúc đầu ("/dat-goi-kham"...).</summary>
    public string? ReturnUrl { get; set; }
}

public class HuyOtpRequest
{
    public string? SoDienThoai { get; set; }
    public string? Cccd { get; set; }
}

public class DoiMatKhauRequest
{
    public string MatKhauMoi { get; set; } = string.Empty;
}
