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
    /// Cua co so doc thang tu du lieu (DM_CSKCB.KetNoi_UrlChuyenHuong) sau khi tang
    /// cua doi tac bi go. Chi dung cho hang rao mat khau o man Dang ky.
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

    /// <summary>
    /// Cổng tiếp nhận quét mã QR: Tự động nhận diện hồ sơ bệnh nhân và chuyển thẳng vào Cổng bệnh nhân
    /// </summary>
    [HttpGet("/qr")]
    [HttpGet("/qr-kham")]
    public async Task<IActionResult> QrKham(string? mabn = null, string? coSo = null)
    {
        // 🔴 Duong nay dang nhap THANG, khong qua OTP. Khong co ma tren duong dan ma
        // van di tiep thi mo '/qr' tay khong la duoc cap phien cua mot benh nhan bat ky.
        if (string.IsNullOrWhiteSpace(mabn))
        {
            return Redirect("/DangNhap/Login?ReturnUrl=%2Fbenh-nhan");
        }

        var maBnTraCuu = mabn.Trim();
        var maCoSoTraCuu = string.IsNullOrWhiteSpace(coSo) ? "77121" : coSo.Trim();

        // Dot 1B: mot dong DA LA "con nguoi + ho so tai co so" nen khong con tu noi.
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

    /// <summary>
    /// Cổng tiếp nhận quét mã QR: Mở màn hình OTP, sau khi xác thực OTP thành công sẽ vào Cổng bệnh nhân
    /// </summary>
    [HttpGet("/qr-otp")]
    public async Task<IActionResult> QrOtp(string? mabn = null, string? coSo = null, string? sdt = null)
    {
        // 🔴 Khong co ma tren duong dan thi KHONG phai luong quet — di tiep la dang
        // nhap ho mot benh nhan bat ky. Tra ve man Login truoc khi SignOut, de mo
        // nham '/qr-otp' khong danh bay phien dang co.
        if (string.IsNullOrWhiteSpace(mabn))
        {
            return Redirect("/DangNhap/Login?ReturnUrl=%2Fbenh-nhan");
        }

        // Luôn xóa phiên cookie cũ để đảm bảo hiển thị đúng màn hình OTP
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var maBnTraCuu = mabn.Trim();
        var maCoSoTraCuu = string.IsNullOrWhiteSpace(coSo) ? "77121" : coSo.Trim();

        // Dot 1B: mot dong DA LA "con nguoi + ho so tai co so" nen khong con tu noi.
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

        // 🔴 MA QUET PHAI DUOC SERVER GIU, khong the giao cho trang Login giu ho.
        // Do that tren may that: sau khi '/qr-otp' chuyen sang Login?...&mabn=...,
        // trang bi nap lai roi di tiep sang '/benh-nhan', bi day ve Login voi moi
        // '?ReturnUrl=%2Fbenh-nhan' — mat sach query. Cookie qr_data thi chinh man
        // Login xoa o 'pagehide'/'click roi trang'. Ket qua: luc bam Dang nhap khong
        // con gi de biet vua quet ai, nguoi dung roi vao ho so DAU TIEN cua tai khoan.
        //
        // Cookie nay HttpOnly nen JS khong xoa duoc, va song 30 phut du de go OTP.
        Response.Cookies.Append("qr_mabn", maBnTraCuu, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Expires = DateTimeOffset.UtcNow.AddMinutes(30),
            SameSite = SameSiteMode.Lax
        });

        // Nho co so ngay tu day, khong doi man Login ghi ho: quet xong la cai app luon
        // thi lan mo tu icon dau tien da phai ra dung logo/ten co so.
        Response.Cookies.Append("pwa_co_so", slug, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            SameSite = SameSiteMode.Lax
        });

        // 🔴 Khong doan bua khi tra khong ra ho so: so/CCCD cung cu o day tung lam
        // nguoi quet nhin thay tai khoan cua nguoi khac. Khong co gi that thi de rong,
        // man Login se hoi so dien thoai nhu binh thuong.
        var sdtBn = !string.IsNullOrWhiteSpace(sdt) ? sdt : (hoSo?.BenhNhan.SDT ?? "");
        var cccd = hoSo?.BenhNhan.CCCD ?? "";

        // Lưu trước OTP 123456 vào cache. Bo qua khoa rong: "OTP_" la khoa dung chung
        // cho MOI nguoi khong tra ra danh tinh — dat vao do la mo cua cho ca phien khac.
        if (!string.IsNullOrWhiteSpace(sdtBn))
        {
            _cache.Set($"OTP_{sdtBn}", "123456", TimeSpan.FromMinutes(30));
        }
        if (!string.IsNullOrWhiteSpace(cccd))
        {
            _cache.Set($"OTP_{cccd}", "123456", TimeSpan.FromMinutes(30));
        }

        // 🔴 NHO THEO SO DIEN THOAI, khong chi nho bang cookie. Do that tren may that:
        // nguoi benh quet QR o trinh duyet nhung bam Dang nhap trong APP DA CAI — hai
        // ngu canh giu cookie RIENG, nen cookie dat o ben nay ben kia khong thay. Luc
        // do ca qr_data, qr_mabn lan query string deu vo nghia.
        //
        // Cache theo SDT la dung mo hinh ma OTP dang dung ("OTP_{sdt}"), va vong doi
        // cung 30 phut. Xoa o HuyOtp de giu nguyen nghiep vu "roi man OTP thi khong de
        // lai dau vet".
        if (!string.IsNullOrWhiteSpace(sdtBn))
        {
            _cache.Set($"QR_MABN_{sdtBn}", maBnTraCuu, TimeSpan.FromMinutes(30));
        }

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
        // 🔴 KHONG Uri.EscapeDataString o day: Response.Cookies.Append DA tu URL-encode
        // gia tri. Boc hai lop thi JS chi go duoc mot (decodeURIComponent), JSON.parse
        // vo, qrCookieData = null => window.danhTinhQuet rong => POST XacNhanOtp khong
        // mang DanhTinhQuet => khong ai dat claim HoSoDangChon => /benh-nhan roi ve ho
        // so DAU TIEN. Do dung la trieu chung "quet ca 3 QR deu vao mot nguoi".
        // Duong quet bang camera (quet-qr.js) ghi cookie bang encodeURIComponent MOT
        // lop — de mot lop o day la hai duong khop nhau.
        Response.Cookies.Append("qr_data", qrJson, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = false,
            SameSite = SameSiteMode.Lax
        });

        // 🔴 mabn phai nam tren URL, khong duoc chi nam trong cookie qr_data. Cookie do
        // bi chinh man Login xoa o 'pagehide'/'click roi trang' — tren Chrome Android
        // pagehide ban ca khi chuyen app hay tat man hinh, va tab co the bi he dieu hanh
        // thu hoi roi nap lai. Luc do cookie mat, bien window.danhTinhQuet cung mat theo
        // => POST XacNhanOtp khong mang DanhTinhQuet => khong ai dat claim HoSoDangChon.
        // URL thi song qua reload, nen day la duong ben nhat de man Login biet ma quet.
        return Redirect($"/DangNhap/Login?coSo={slug}&sdt={sdtBn}&cccd={cccd}&mabn={Uri.EscapeDataString(maBnTraCuu)}&hienOtp=1&tuQr=1&returnUrl=%2Fbenh-nhan");
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

        if (!string.IsNullOrWhiteSpace(sdt))
        {
            _cache.Remove($"QR_MABN_{sdt.Trim()}");
        }

        Response.Cookies.Delete("qr_data", new CookieOptions { Path = "/" });

        // 🔴 Phai xoa CA qr_mabn. Nghiep vu: roi man OTP ma chua xac thuc thi khong
        // duoc de lai dau vet cua phieu vua quet. qr_mabn la HttpOnly nen JS khong tu
        // xoa duoc — chi co cua nay don duoc no.
        Response.Cookies.Delete("qr_mabn", new CookieOptions { Path = "/" });
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? coSo = null)
    {
        var adminReauth = AdminAuthentication.IsAdminReturnUrl(returnUrl) && Url.IsLocalUrl(returnUrl);

        // 🔴 App da cai mo tu icon thi khong ai gan duoc ?coSo= vao duong dan: PWA vao
        // thang '/' roi bi day sang day. Thieu slug la man dang nhap tut ve logo/ten
        // HisSoft chung, nguoi benh dang o cua Thien Nam nhin thay thuong hieu khac.
        // Cookie nay do chinh man dang nhap ghi (Login.cshtml) nen chi la GOI Y khoi
        // phuc — slug rac thi bo qua, KHONG ve "/" nhu nhanh ?coSo= sai o duoi.
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

        // ?coSo=slug den tu hai nut ben trang co so. Do ra ViewBag de man dang
        // nhap hien o "Ma CSKCB" khoa cung, va de JS gui kem khi goi OTP.
        string? maCoSoTuUrl = null;
        long? idCoSoTuUrl = null;
        if (!string.IsNullOrWhiteSpace(coSo))
        {
            var thongTin = await _dbContext.DMCSKCBs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == coSo);

            // Chan ngay o day thay vi de benh nhan go het OTP roi moi bi tu choi
            // (va ton mot tin nhan OTP vo ich). Entity da nam trong tay, khong ton
            // them truy van. Xem ADR 0013.
            if (thongTin is not null && !thongTin.HienThiCongKhai)
            {
                if (laGoiYTuCookie) { Response.Cookies.Delete("pwa_co_so", new CookieOptions { Path = "/" }); return Redirect("/DangNhap/Login"); }
                return Redirect("/");
            }

            // 🔴 ?coSo= chi nhan SLUG. Go vao mot gia tri khong tra ra co so nao
            // (hay gap nhat: go MA co so, vd 77121) truoc day tut lang le xuong che
            // do khong-co-so: van cho go OTP, van cap cookie, roi tha vao /benh-nhan
            // ma khong tao noi tai khoan. Da ve "/" giong het nhanh HienThiCongKhai = 0 ngay
            // tren — sai cua thi phai biet ngay, dung sau khi go xong OTP. ADR 0027.
            if (thongTin is null)
            {
                // Slug tu cookie khong con tra ra co so nao (doi slug, co so bi xoa):
                // don cookie roi hien man dang nhap chung, dung nem nguoi dung ve "/".
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
                // 🔴 Dot 1B: benh nhan KHONG CON tai khoan, nen khong con duong
                // nao di tu CCCD sang HT_TaiKhoan. Bang do chi con Admin.
                // "Co loi vao hay khong" duoc hoi o hang rao C7b ben duoi.
                taiKhoan = null;
            }
        }

        bool laQrHis = model.DanhTinhQuet != null && (model.DanhTinhQuet.LaNguonHis || !string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN));

        // 🔴 Cung luat thu tu voi hai cua kia: MaCoSo phai duoc chot TRUOC khi hoi
        // CoLoiVaoAsync. GuiOtp khong co khoi dien mac dinh nhu XacNhanOtp, nen
        // phien mo /DangNhap/Login khong kem ?coSo= se co MaCoSo rong => bi chan
        // ngay o buoc gui OTP, trong khi XacNhanOtp thi lai cho qua. Hai cua noi
        // hai kieu cho cung mot nguoi la loi kho lan nhat.
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

        // 🔴 C7b — CUA 3, o buoc GUI OTP. PLAN §7.1 KHONG LIET KE CUA NAY (no chi
        // neu hai cua :476 va :708). Bo sot thi benh nhan chet ngay tu buoc gui
        // OTP, truoc khi cham toi hai cua kia: sau dot 1B `taiKhoan` LUON null
        // voi benh nhan (HT_TaiKhoan chi con Admin) nen dieu kien cu chan sach.
        // Bat duoc luc chay nghiem thu that 19-09 — khong phep nao khac thay.
        //
        // Admin van phai di duong `taiKhoan != null` ngay duoi (nhanh mat khau),
        // nen o day chi mo cho ai CO LOI VAO, va van giu ngoai le quet QR HIS.
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

        // Co so dang an (DM_CSKCB.HienThiCongKhai = 0) thi khong sinh phien MOI tai co so do.
        // Ve !adminReauth la BAT BUOC: action nay phuc vu ca re-auth Admin/DoiTac,
        // thieu no la khoa luon duong dang nhap quan tri. Xem ADR 0013.
        if (!adminReauth && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && !await _luong.CoSoDangHienThiAsync(model.MaCoSo))
        {
            return Json(new { success = false, message = "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến." });
        }

        // Tìm tài khoản từ database theo SĐT hoặc Email trước để kiểm tra role
        var taiKhoan = await _taiKhoanService.DangNhapAsync(input, "");

        if (taiKhoan != null && (string.Equals(taiKhoan.Role, "Admin", StringComparison.OrdinalIgnoreCase)))
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
        // 🔴 THU TU BAT BUOC: khoi dien MaCoSo phai chay TRUOC chot C7b.
        // Chot cu ("taiKhoan is null") khong dung toi MaCoSo nen dat o dau cung
        // duoc; chot C7b thi CO — de nguyen thu tu cu la phien khong mang MaCoSo
        // bi chan sach, ke ca nguoi CO ho so. Da dap that luc chay nghiem thu 19-09.
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

        // 🔴 C7b — CUA 1 trong HAI cua. Cua kia o nhanh Firebase (tim
        // "C7b - CUA 2"). Sot mot cua la mo duong lach (ADR 0027 da canh bao
        // dung chuyen nay). Tu dot 1B hang rao khong con treo vao HT_TaiKhoan
        // ma hoi thang: SO NAY CO HO SO TAI CO SO NAY KHONG (ADR 0034).
        // Loi bao hien tai noi "chua co ho so tai co so y te" — truoc 1B loi do
        // CHAT HON code, nay moi dung nghia den, nen giu nguyen chu.
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
                // 🔴 MaBN chi duy nhat THEO TUNG CO SO (ApplicationDbContext: unique
                // (IdCoSo, MaBN)), nen tra khong loc co so la doc nham ho so cua co
                // so khac — xem TimHoSoTheoMaBnTaiCoSoAsync.
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
            // 🔴 Chi mo san ho so khi tai khoan co DUNG MOT ho so. Tai khoan nhieu ho so
        // (me + 2 con) ma lay bua mot cai la bug tham lang — de null thi nguoi dung
        // tu chon o man Ho so. Nhanh QR duoi day van ghi de bang ho so quet duoc.
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

                // CHI luong QUET PHIEU: MOT_HO_SO bat thi mo san dung ho so ma man
                // *Ho so cua toi* se hien, roi vao thang /benh-nhan — nguoi vua quet
                // phieu khong phai chon lai mot danh sach chi co mot dong.
                idHoSoQuet ??= await _hoSo.LayIdHoSoMoSanKhiQuetAsync(input, model.MaCoSo, cccdQuet);
            }

            idHoSoQuet ??= await HoSoTheoMaQuetDaNhoAsync(model.MaCoSo, input);

            if (idHoSoQuet != null && idHoSoQuet > 0)
            {
                // 🔴 Cua 3 "nhan chu so huu" da chet o dot 1B: cot
                // DM_BenhNhan.IDTaiKhoan khong con, chu so huu nay la cap
                // (SDT x co so) cua chinh dong do. Xem ADR 0034.

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

            // Cay quyet dinh sau OTP: chi mot cho duy nhat, nam trong
            // ILuongCongBenhNhan. Man hinh khong duoc tu kiem tra MaCoSo.
            var redirectUrl = adminReauth
                ? model.ReturnUrl
                : await ChonDichDenAsync(model.MaCoSo, model.Cccd, input, model.ReturnUrl, model.DanhTinhQuet);

            // Xóa cookie QR đã quét khi đăng nhập thành công
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

        // Cung ly le voi XacNhanOtp: day la cua thu hai (va cuoi cung) sinh phien moi.
        if (!adminReauth && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && !await _luong.CoSoDangHienThiAsync(model.MaCoSo))
        {
            return Json(new { success = false, message = "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến." });
        }

        bool laQrHis = model.DanhTinhQuet != null && (model.DanhTinhQuet.LaNguonHis || !string.IsNullOrWhiteSpace(model.DanhTinhQuet.MaBN));

        // 🔴 THU TU BAT BUOC: khoi dien MaCoSo phai chay TRUOC chot C7b.
        // Chot cu ("taiKhoan is null") khong dung toi MaCoSo nen dat o dau cung
        // duoc; chot C7b thi CO — de nguyen thu tu cu la phien khong mang MaCoSo
        // bi chan sach, ke ca nguoi CO ho so. Da dap that luc chay nghiem thu 19-09.
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

        // 🔴 C7b — CUA 2 trong HAI cua (cua kia o nhanh OTP phia tren).
        // Sot mot cua la mo duong lach. Cung luat, cung loi bao.
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
                // 🔴 MaBN chi duy nhat THEO TUNG CO SO (ApplicationDbContext: unique
                // (IdCoSo, MaBN)), nen tra khong loc co so la doc nham ho so cua co
                // so khac — xem TimHoSoTheoMaBnTaiCoSoAsync.
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
        // 🔴 Chi mo san ho so khi tai khoan co DUNG MOT ho so. Tai khoan nhieu ho so
        // (me + 2 con) ma lay bua mot cai la bug tham lang — de null thi nguoi dung
        // tu chon o man Ho so. Nhanh QR duoi day van ghi de bang ho so quet duoc.
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

            // CHI luong QUET PHIEU: MOT_HO_SO bat thi mo san dung ho so ma man
            // *Ho so cua toi* se hien, roi vao thang /benh-nhan — nguoi vua quet
            // phieu khong phai chon lai mot danh sach chi co mot dong.
            idHoSoQuet ??= await _hoSo.LayIdHoSoMoSanKhiQuetAsync(sdt, model.MaCoSo, cccdQuet);
        }

        idHoSoQuet ??= await HoSoTheoMaQuetDaNhoAsync(model.MaCoSo, sdt);

        if (idHoSoQuet != null && idHoSoQuet > 0)
        {
            if (taiKhoan != null && taiKhoan.Id > 0)
            {
                // Cua 3 da thanh no-op tu dot 1B (ADR 0034).
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

        // Xóa cookie QR đã quét khi đăng nhập thành công
        Response.Cookies.Delete("qr_data", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("qr_mabn", new CookieOptions { Path = "/" });
        if (!string.IsNullOrWhiteSpace(sdt)) _cache.Remove($"QR_MABN_{sdt.Trim()}");

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

        var dichDen = await _luong.ChonDichDenAsync(maCoSo, cccd, dinhDanh, returnUrl);
        return Redirect(dichDen);
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

        // 1. Xác định cơ sở y tế và hồ sơ hiện có (nếu bệnh nhân đã có trên HIS)
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

            // 1a. Thử tìm hồ sơ theo MaBN tại đúng cơ sở này
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

            // 1b. Nếu chưa có theo MaBN, thử tìm hồ sơ theo CCCD tại đúng cơ sở này
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
                var (_, idMoi) = await _thuTuc.TaoHoSoTuKhaiAsync(idBenhNhanTarget, idCoSo.Value);
                if (idMoi > 0)
                {
                    idBenhNhanTarget = idMoi;
                }
            }
        }

        // 2b. Gán Mã BN từ QR nếu cơ sở này chưa ai dùng mã này
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

        // 3. Tạo tài khoản HT_TaiKhoan
        var luuTaiKhoan = await _thuTuc.SaveTaiKhoanAsync(
            0,
            sdt,
            null,
            "BenhNhan",
            null);

        var idTaiKhoan = luuTaiKhoan.Id;

        // 4. Gán quyền sở hữu hồ sơ cho tài khoản
        if (idBenhNhanTarget > 0 && idTaiKhoan > 0)
        {
            // Cua 3 da thanh no-op tu dot 1B (ADR 0034).
        }

        // 5. Đảm bảo mở tài liệu
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

        // 🔴 Don dau vet phieu vua quet TRUOC khi mat claim. Thieu buoc nay thi dang
        // xuat roi dang nhap lai bang so dien thoai (khong quet gi ca) van bi mo san
        // ho so cua lan quet truoc — vua sai y nguoi dung, vua la dau vet con sot lai
        // cua mot phien da ket thuc.
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

        // 🔴 'vuaDangXuat=1' la tin hieu cho TRINH DUYET tu don localStorage
        // 'pwa_patient_cache'. So dien thoai + CCCD cua nguoi vua dung nam o do, server
        // khong voi toi duoc; khong don thi dang xuat xong man dang nhap van nhan ra so
        // cu va nhay thang vao o OTP cua chinh nguoi do — dang xuat nhu khong.
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
    /// Tra ho so (<c>DM_BenhNhan.ID</c>) theo ma benh nhan quet duoc tren phieu kham,
    /// <b>gioi han trong dung co so dang dang nhap</b>.
    ///
    /// 🔴 <c>MaBN</c> chi duy nhat THEO TUNG CO SO — <c>ApplicationDbContext</c> dat
    /// <c>HasIndex(IdCoSo, MaBN).IsUnique()</c>. Tra ma khong loc co so thi benh nhan
    /// dang o co so B quet phieu mang ma do co so A cap se lay ra ho so cua NGUOI LA,
    /// va duong <c>NhanChuSoHuuAsync</c> phia sau se gan ho so do cho tai khoan dang quet.
    ///
    /// Khong biet co so nao (thieu <paramref name="maCoSo"/>) thi tra <c>null</c> —
    /// hong theo huong an toan, thua hon la doan.
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
    /// Ma benh nhan cua phieu vua quet, khi man Login khong mang duoc danh tinh quet
    /// toi cua xac thuc.
    ///
    /// <para>
    /// 🔴 Vi sao phai co: do that tren may that cho thay query string rung sau vai lan
    /// chuyen trang (Login?...&mabn=... -> Login?ReturnUrl=%2Fbenh-nhan), con cookie thi
    /// khong song qua duoc ranh gioi TRINH DUYET <-> APP DA CAI (hai kho cookie rieng:
    /// quet QR o Chrome roi bam Dang nhap trong app la mat sach).
    /// </para>
    /// <para>
    /// Nen ban nho theo SO DIEN THOAI (cache server) di truoc, cookie chi la duong lui.
    /// Vong doi 30 phut, giong ma OTP. Bi don o: HuyOtp (roi man OTP), dang nhap xong,
    /// va DangXuat — de khong con dau vet cua phien da ket thuc.
    /// </para>
    /// </summary>
    private async Task<long?> HoSoTheoMaQuetDaNhoAsync(string? maCoSo, string? sdt)
    {
        // Uu tien ban nho theo SDT: no song duoc ca khi nguoi benh quet o trinh duyet
        // roi bam Dang nhap trong app da cai (hai ngu canh cookie khac nhau).
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
    /// Ho so DUY NHAT cua mot so dien thoai TAI MOT CO SO, hoac <c>null</c> khi
    /// khong co ho so nao HOAC co tu hai ho so tro len.
    ///
    /// 🔴 Tu dot 1B quan he la (SDT x co so) -> N ho so: mot so giu duoc nhieu ho
    /// so (me + cac con) tai cung co so, nen "ho so dang chon" phai do nguoi dung
    /// chon va nam o claim/phien — KHONG duoc lay <c>FirstOrDefault</c> bat ky.
    /// Xem ADR 0034 va muc *Loi vao* trong CONTEXT.md.
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

    /// <summary>Ma co so benh nhan dang dung (tu ?coSo=slug ben trang co so).</summary>
    public string? MaCoSo { get; set; }

    /// <summary>Khoa noi benh nhan sang he doi tac (V6).</summary>
    public string? Cccd { get; set; }

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

public class DoiMatKhauRequest
{
    public string MatKhauMoi { get; set; } = string.Empty;
}
